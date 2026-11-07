using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Construye Resources/ForgeFighterAnimator: una copia del controlador de locomoción de Starter Assets más una
    /// capa "Combat" con un estado por cada animación importada. Los luchadores controlan el peso de esa capa desde código.
    /// También sustituye en el blend tree el idle por Fighting Idle y caminar/correr por Running.
    /// </summary>
    public static class ForgeCombatAnimatorSetup
    {
        // Source: controlador original de Starter Assets. Locomotion: nombre de su blend tree de movimiento.
        const string Source="Assets/Character/Animations/StarterAssetsThirdPerson.controller";
        const string Locomotion="Idle Walk Run Blend";
        // Ruta del controlador generado (en Resources para poder cargarlo en el juego con Resources.Load).
        static string Target => "Assets/Resources/"+ForgeCombatClips.Controller+".controller";

        [InitializeOnLoadMethod]
        static void Initialize(){EditorApplication.delayCall+=Refresh;}

        /// <summary>Reconstruye el controlador solo si las animaciones importadas ya no coinciden con él.</summary>
        public static void Refresh(){Build(false);}

        // PixelForge > Rebuild Combat Animator: lo reconstruye siempre.
        [MenuItem("PixelForge/Rebuild Combat Animator")]
        public static void Rebuild(){Build(true);}

        static void Build(bool force)
        {
            // No se puede modificar assets mientras Unity compila, importa o entra en Play; se reintenta después.
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)
            {EditorApplication.delayCall-=Refresh;EditorApplication.delayCall+=Refresh;return;}
            var clips=FindClips();
            if(!force&&UpToDate(clips))return;
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            // Borra el controlador anterior y lo vuelve a crear desde la copia del original.
            AssetDatabase.DeleteAsset(Target);
            if(clips.Count==0){Debug.Log("FORGE_COMBAT: no combat clips in "+ForgeCombatClips.Folder+"; fighters use procedural attacks.");return;}
            if(!AssetDatabase.CopyAsset(Source,Target)){Debug.LogError("FORGE_COMBAT: could not copy "+Source);return;}
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Target);
            // Parámetro de velocidad de la capa de combate, con valor por defecto 1.
            controller.AddParameter(ForgeCombatClips.SpeedParameter,AnimatorControllerParameterType.Float);
            var parameters=controller.parameters;parameters[parameters.Length-1].defaultFloat=1;controller.parameters=parameters;
            controller.AddLayer(ForgeCombatClips.Layer);
            // Las capas se devuelven como copias, así que el arreglo modificado debe volver a asignarse.
            // Override: la capa sustituye la pose de las capas inferiores en la medida de su peso.
            var layers=controller.layers;var layer=layers[layers.Length-1];
            layer.defaultWeight=1;layer.blendingMode=AnimatorLayerBlendingMode.Override;controller.layers=layers;
            var machine=layer.stateMachine;
            // Estado vacío por defecto: sin animación de combate.
            var empty=machine.AddState("Empty");empty.writeDefaultValues=false;machine.defaultState=empty;
            foreach(var pair in clips)
            {
                // Idle y Run no van en la capa de combate, sino en el blend tree de locomoción.
                if(pair.Key==ForgeCombatClips.Idle){ReplaceIdle(controller,pair.Value);continue;}
                if(pair.Key==ForgeCombatClips.Run){ReplaceRun(controller,pair.Value);continue;}
                AddState(machine,pair.Key.State,pair.Value,false);
                if(pair.Key.Mirrored)AddState(machine,pair.Key.MirrorState,pair.Value,true);
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            Debug.Log("FORGE_COMBAT_OK: Combat layer built with "+clips.Count+" clip(s).");
        }

        // Busca el blend tree de locomoción en la capa base.
        static BlendTree LocomotionTree(AnimatorController controller)
        {
            foreach(var child in controller.layers[0].stateMachine.states)
                if(child.state.name==Locomotion&&child.state.motion is BlendTree tree)return tree;
            return null;
        }

        // Animación usada a velocidad 0 (de pie).
        static Motion StanceMotion(BlendTree tree)
        {
            if(tree==null)return null;
            foreach(var child in tree.children)if(Mathf.Approximately(child.threshold,0))return child.motion;
            return null;
        }

        /// <summary>Cambia la entrada de "de pie" (Speed 0) del blend tree de locomoción por la pose de pelea.</summary>
        static void ReplaceIdle(AnimatorController controller,AnimationClip idle)
        {
            var tree=LocomotionTree(controller);
            if(tree==null){Debug.LogWarning("FORGE_COMBAT: '"+Locomotion+"' blend tree not found; the fighting stance was not applied.");return;}
            // Los hijos se devuelven como copias, así que el arreglo modificado debe volver a asignarse.
            var children=tree.children;
            for(int i=0;i<children.Length;i++)if(Mathf.Approximately(children[i].threshold,0))children[i].motion=idle;
            tree.children=children;EditorUtility.SetDirty(tree);
            // El parámetro sirve de "marca" para que el juego sepa que la pose de pelea está activa.
            controller.AddParameter(ForgeCombatClips.StanceParameter,AnimatorControllerParameterType.Bool);
        }

        /// <summary>Índice de la entrada más rápida (correr) del blend tree de locomoción.</summary>
        static int RunIndex(ChildMotion[] children)
        {
            int index=-1;
            for(int i=0;i<children.Length;i++)if(index<0||children[i].threshold>children[index].threshold)index=i;
            return index;
        }

        /// <summary>
        /// Sustituye las entradas de caminar y correr por el ciclo de carrera, dejando solo pose de pelea y carrera.
        /// Los luchadores ajustan la velocidad del ciclo según su rapidez, así que no hace falta un caminar aparte.
        /// </summary>
        static void ReplaceRun(AnimatorController controller,AnimationClip run)
        {
            var tree=LocomotionTree(controller);
            if(tree==null){Debug.LogWarning("FORGE_COMBAT: '"+Locomotion+"' blend tree not found; the run cycle was not applied.");return;}
            var children=tree.children;int index=RunIndex(children);if(index<0)return;
            var stride=children[index];stride.motion=run;stride.threshold=ForgeCombatClips.RunBlendSpeed;
            // Conserva la entrada de velocidad 0 y añade la carrera; el resto (caminar) se descarta.
            var kept=new List<ChildMotion>();
            foreach(var child in children)if(Mathf.Approximately(child.threshold,0))kept.Add(child);
            kept.Add(stride);
            tree.children=kept.ToArray();EditorUtility.SetDirty(tree);
            controller.AddParameter(ForgeCombatClips.RunParameter,AnimatorControllerParameterType.Bool);
        }

        // Añade un estado a la capa de combate (opcionalmente espejado) cuya velocidad controla CombatSpeed.
        static void AddState(AnimatorStateMachine machine,string name,AnimationClip clip,bool mirror)
        {
            var state=machine.AddState(name);state.motion=clip;state.mirror=mirror;state.writeDefaultValues=false;
            state.speedParameter=ForgeCombatClips.SpeedParameter;state.speedParameterActive=true;
        }

        /// <summary>Asocia cada entrada de la tabla con el primer FBX de combate cuyo nombre de archivo contiene una de sus palabras clave.</summary>
        static Dictionary<CombatClip,AnimationClip> FindClips()
        {
            var found=new Dictionary<CombatClip,AnimationClip>();
            if(!AssetDatabase.IsValidFolder(ForgeCombatClips.Folder))return found;
            var paths=new List<string>();
            foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{ForgeCombatClips.Folder}))paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            // Orden alfabético para que el resultado sea siempre el mismo.
            paths.Sort(System.StringComparer.OrdinalIgnoreCase);
            var entries=new List<CombatClip>(ForgeCombatClips.All){ForgeCombatClips.Idle,ForgeCombatClips.Run};
            foreach(var entry in entries)foreach(var path in paths)
            {
                var file=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if(!System.Array.Exists(entry.Keywords.Split('|'),keyword=>file.Contains(keyword)))continue;
                // Un FBX contiene varios sub-assets; se toma la animación real (no la de vista previa).
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if(asset is AnimationClip clip&&!clip.name.StartsWith("__preview__")){found[entry]=clip;break;}
                if(found.ContainsKey(entry))break;
            }
            return found;
        }

        // Comprueba si el controlador existente ya refleja exactamente las animaciones encontradas.
        static bool UpToDate(Dictionary<CombatClip,AnimationClip> clips)
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Target);
            if(controller==null)return clips.Count==0;
            AnimatorControllerLayer combat=null;
            foreach(var layer in controller.layers)if(layer.name==ForgeCombatClips.Layer)combat=layer;
            if(combat==null)return false;
            var states=new Dictionary<string,Motion>();
            foreach(var child in combat.stateMachine.states)states[child.state.name]=child.state.motion;
            // La pose de pelea vive en el blend tree de la capa base, marcada por un parámetro del controlador.
            bool stance=System.Array.Exists(controller.parameters,parameter=>parameter.name==ForgeCombatClips.StanceParameter);
            if(clips.TryGetValue(ForgeCombatClips.Idle,out var idle)){if(!stance||StanceMotion(LocomotionTree(controller))!=idle)return false;}
            else if(stance)return false;
            // El ciclo de carrera también vive en el blend tree, marcado por su propio parámetro.
            bool running=System.Array.Exists(controller.parameters,parameter=>parameter.name==ForgeCombatClips.RunParameter);
            var tree=LocomotionTree(controller);var children=tree!=null?tree.children:new ChildMotion[0];int run=RunIndex(children);
            if(clips.TryGetValue(ForgeCombatClips.Run,out var runClip))
            {if(!running||run<0||children[run].motion!=runClip||!Mathf.Approximately(children[run].threshold,ForgeCombatClips.RunBlendSpeed)||children.Length!=2)return false;}
            else if(running)return false;
            // Estados esperados en la capa de combate: "Empty" más uno (o dos si es espejada) por animación.
            int expected=1;
            foreach(var pair in clips)
            {
                if(pair.Key==ForgeCombatClips.Idle||pair.Key==ForgeCombatClips.Run)continue;
                expected+=pair.Key.Mirrored?2:1;
                if(!states.TryGetValue(pair.Key.State,out var motion)||motion!=pair.Value)return false;
            }
            return states.Count==expected;
        }
    }
}
