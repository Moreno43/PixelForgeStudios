using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Rellena la biblioteca de audio del juego a partir de Assets/Audio cada vez que se añade, renombra o borra un archivo allí.
    /// Los sonidos se asocian por nombre de archivo: Menu, Music1..3, Golpe, Patada, GolpeFuerte, Bloqueo, Dash, Salto.
    /// Los pasos y el aterrizaje reutilizan los sonidos de Starter Assets.
    /// </summary>
    public sealed class ForgeAudioSetup : AssetPostprocessor
    {
        const string AssetPath="Assets/Resources/ForgeAudio.asset";
        const string Folder="Assets/Audio";
        const string Starter="Assets/Character/Sfx";

        static bool IsAudio(string path) => path.Replace('\\','/').StartsWith(Folder+"/",StringComparison.OrdinalIgnoreCase);

        // Ajustes de importación de cada audio (solo la primera vez, para respetar cambios manuales).
        void OnPreprocessAudio()
        {
            if(!IsAudio(assetPath))return;
            var importer=(AudioImporter)assetImporter;if(!importer.importSettingsMissing)return;
            var settings=importer.defaultSampleSettings;
            // Las pistas largas se leen del disco mientras suenan (Streaming); los efectos cortos se descomprimen
            // al cargar para que suenen al instante. forceToMono: los efectos no necesitan estéreo.
            if(assetPath.Replace('\\','/').Contains("/Music/")){settings.loadType=AudioClipLoadType.Streaming;importer.loadInBackground=true;}
            else{settings.loadType=AudioClipLoadType.DecompressOnLoad;importer.forceToMono=true;}
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.7f;
            importer.defaultSampleSettings=settings;
        }

        // Si cambió algún archivo de Assets/Audio, programa la reconstrucción de la biblioteca.
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            foreach(var group in new[]{imported,deleted,moved,movedFrom})foreach(var path in group)
                if(IsAudio(path)){EditorApplication.delayCall-=Refresh;EditorApplication.delayCall+=Refresh;return;}
        }

        [InitializeOnLoadMethod]
        static void Initialize(){EditorApplication.delayCall+=Refresh;}

        // PixelForge > Rebuild Audio Library: rehace la biblioteca a mano.
        [MenuItem("PixelForge/Rebuild Audio Library")]
        public static void Refresh()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall-=Refresh;EditorApplication.delayCall+=Refresh;return;}
            if(!AssetDatabase.IsValidFolder(Folder))return;
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            var library=AssetDatabase.LoadAssetAtPath<ForgeAudioLibrary>(AssetPath);
            bool created=library==null;if(created)library=ScriptableObject.CreateInstance<ForgeAudioLibrary>();
            var music=Clips(Folder+"/Music");var sfx=Clips(Folder+"/SFX");var starter=Clips(Starter);
            // La música que contiene "menu" es la del menú; todas las demás son de combate.
            library.menuMusic=music.FirstOrDefault(c=>Name(c).Contains("menu"));
            library.battleMusic=music.Where(c=>!Name(c).Contains("menu")).ToArray();
            // "golpefuerte" también contiene "golpe", así que los golpes fuertes se buscan primero y se excluyen de los puños.
            library.heavy=Match(sfx,"golpefuerte","fuerte","heavy");
            library.punch=Match(sfx,"golpe","punch").Except(library.heavy).ToArray();
            library.kick=Match(sfx,"patada","kick");
            library.block=Match(sfx,"bloqueo","block");
            library.dash=Match(sfx,"dash","esquiva");
            library.jump=Match(sfx,"salto","jump");
            // Si no hay sonidos propios de aterrizaje o pasos, se usan los de Starter Assets.
            library.land=Match(sfx,"aterriz","land");if(library.land.Length==0)library.land=Match(starter,"land");
            library.footsteps=Match(sfx,"paso","footstep");if(library.footsteps.Length==0)library.footsteps=Match(starter,"footstep");
            if(created)AssetDatabase.CreateAsset(library,AssetPath);
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            Debug.Log("FORGE_AUDIO_OK: menu="+(library.menuMusic!=null?library.menuMusic.name:"none")+", battle tracks="+library.battleMusic.Length
                +", punch="+library.punch.Length+", kick="+library.kick.Length+", heavy="+library.heavy.Length+", block="+library.block.Length
                +", dash="+library.dash.Length+", jump="+library.jump.Length+", land="+library.land.Length+", footsteps="+library.footsteps.Length);
        }

        static string Name(AudioClip clip)=>clip.name.ToLowerInvariant();

        // Todos los AudioClip de una carpeta, en orden alfabético.
        static List<AudioClip> Clips(string folder)
        {
            if(!AssetDatabase.IsValidFolder(folder))return new List<AudioClip>();
            return AssetDatabase.FindAssets("t:AudioClip",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c=>c!=null).ToList();
        }

        // Los sonidos cuyo nombre contiene alguna de las palabras clave.
        static AudioClip[] Match(List<AudioClip> clips,params string[] keywords)=>clips.Where(c=>keywords.Any(k=>Name(c).Contains(k))).ToArray();
    }
}
