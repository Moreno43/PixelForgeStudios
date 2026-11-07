using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Comandos del editor para crear/abrir las escenas, y configura el menú como escena inicial al pulsar Play.
    /// Los scripts de la carpeta Editor solo existen dentro de Unity, no se incluyen en el juego compilado.
    /// </summary>
    public static class ForgeArenaSetup
    {
        const string ArenaPath = "Assets/Scenes/ForgeArena.unity";

        // [InitializeOnLoadMethod] hace que Unity ejecute este método cada vez que abre el proyecto o recompila.
        [InitializeOnLoadMethod]
        static void InitializeStartup()
        {
            // Espera a que termine la importación de assets tras recargar el editor.
            EditorApplication.delayCall += ConfigureStartup;
        }

        public static void ConfigureStartup()
        {
            // Si Unity sigue compilando o importando, lo vuelve a intentar más tarde.
            if(EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ConfigureStartup;
                return;
            }
            var arena = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ForgeMenu.unity");
            if(arena == null) return;
            // Play siempre empieza en el menú, aunque se esté editando otra escena (por ejemplo Playground).
            EditorSceneManager.playModeStartScene = arena;
        }

        // [MenuItem] añade esta opción al menú superior de Unity: PixelForge > Create or Open Fight Arena.
        [MenuItem("PixelForge/Create or Open Fight Arena")]
        public static void Create()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path=ArenaPath;
            // Si la escena ya existe, solo la abre y comprueba que tenga el componente ForgeArena.
            if(System.IO.File.Exists(path))
            {
                var existing=EditorSceneManager.OpenScene(path);
                if(Object.FindObjectOfType<ForgeArena>()==null) throw new System.InvalidOperationException("The scene does not contain a ForgeArena component.");
                EditorSceneManager.SaveScene(existing,path);
                ConfigureStartup();
                Debug.Log("FORGE_SETUP_OK: Arena scene loaded and validated.");
                return;
            }
            // Si no existe, crea una escena vacía con un objeto ForgeArena y le asigna los assets necesarios.
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var arena=new GameObject("Forge Fighters").AddComponent<ForgeArena>();
            arena.fighterPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerArmature.prefab");
            arena.sceneryPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Prefabs/Box_350x250x200_Prefab.prefab");
            arena.floorMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Art/Materials/GridBlue_01_Mat.mat");
            arena.skyMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Art/Skybox/SkyboxLiteWarm.mat");
            arena.rockPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainDemoScene_HDRP/Prefabs/Rocks/Models/Rock_A_01.fbx");
            EditorSceneManager.SaveScene(scene,path);
            // Registra la escena en Build Settings (como primera), conservando las demás.
            var previous=EditorBuildSettings.scenes;
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(path,true)};
            foreach(var item in previous)if(item.path!=path)scenes.Add(item);
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
            ConfigureStartup();
            Debug.Log("FORGE_SETUP_OK: Arena created and registered as first build scene.");
        }
        // PixelForge > Open Main Menu: abre la escena del menú.
        [MenuItem("PixelForge/Open Main Menu")]
        public static void OpenMenu()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Scenes/ForgeMenu.unity");ConfigureStartup();
        }
    }
}
