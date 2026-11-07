using System;
using System.IO;
using UnityEditor;

namespace PixelForge
{
    /// <summary>
    /// Importa los archivos de Mixamo colocados en la carpeta de combate como animaciones Humanoid que se reproducen en el sitio.
    /// Los ajustes solo se aplican en la primera importación, así se conservan los cambios hechos después en el Inspector.
    /// Un AssetPostprocessor es una clase que Unity llama automáticamente al importar assets.
    /// </summary>
    public sealed class ForgeCombatImport : AssetPostprocessor
    {
        // ¿El archivo es un .fbx dentro de la carpeta de combate?
        static bool IsCombat(string path) => path.Replace('\\','/').StartsWith(ForgeCombatClips.Folder+"/",StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase);
        // Se llama antes de importar el modelo.
        void OnPreprocessModel()
        {
            if(!IsCombat(assetPath))return;
            var importer=(ModelImporter)assetImporter;if(!importer.importSettingsMissing)return;
            // Rig Humanoid con su propio avatar, para poder reproducir la animación en cualquier personaje humanoide.
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            // Los archivos descargados "With Skin" traen la malla y materiales de Mixamo; solo se necesita el movimiento.
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importCameras=importer.importLights=false;importer.importAnimation=true;
        }
        // Se llama antes de importar las animaciones del archivo.
        void OnPreprocessAnimation()
        {
            if(!IsCombat(assetPath))return;
            var importer=(ModelImporter)assetImporter;if(!importer.importSettingsMissing)return;
            // Las descargas con malla también traen un "Take 001" vacío; solo se conserva la toma de Mixamo.
            var clips=importer.defaultClipAnimations;var mixamo=Array.FindAll(clips,clip=>clip.takeName=="mixamo.com");
            if(mixamo.Length>0)clips=mixamo;
            // Las poses y ciclos de movimiento se repiten en bucle; los ataques, reacciones y esquivas se reproducen una vez.
            var name=Path.GetFileNameWithoutExtension(assetPath);var lower=name.ToLowerInvariant();
            bool loop=lower.Contains("idle")||lower.Contains("walk")||lower.Contains("run")||lower.Contains("strafe");
            foreach(var clip in clips)
            {
                clip.name=name;clip.loopTime=clip.loopPose=loop;
                // La rotación y la altura se "hornean" en la pose, alineadas con la dirección inicial del cuerpo.
                // El desplazamiento horizontal queda como root motion, que los luchadores ignoran: los mueve el código.
                clip.lockRootRotation=true;clip.keepOriginalOrientation=false;
                clip.lockRootHeightY=true;clip.keepOriginalPositionY=true;
                clip.lockRootPositionXZ=false;
            }
            importer.clipAnimations=clips;
        }
        // Se llama después de importar, borrar o mover cualquier asset.
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            // Reconstruye el Animator de los luchadores cuando se añade, sustituye o quita una animación de combate.
            foreach(var group in new[]{imported,deleted,moved,movedFrom})foreach(var path in group)
                if(IsCombat(path)){EditorApplication.delayCall-=ForgeCombatAnimatorSetup.Refresh;EditorApplication.delayCall+=ForgeCombatAnimatorSetup.Refresh;return;}
        }
    }
}
