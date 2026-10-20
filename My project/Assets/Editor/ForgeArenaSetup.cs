using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelForge
{
    /// <summary>Provides editor commands for scene creation and configures the menu as the Play Mode entry point.</summary>
    public static class ForgeArenaSetup
    {
        const string ArenaPath = "Assets/Scenes/ForgeArena.unity";

        [InitializeOnLoadMethod]
        static void InitializeStartup()
        {
            // Wait until asset import has completed after an editor/domain reload.
            EditorApplication.delayCall += ConfigureStartup;
        }

        public static void ConfigureStartup()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ConfigureStartup;
                return;
            }
            var arena = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ForgeMenu.unity");
            if(arena == null) return;
            // Play always starts at the menu, even when Playground is being edited.
            EditorSceneManager.playModeStartScene = arena;
        }

        [MenuItem("PixelForge/Create or Open Fight Arena")]
        public static void Create()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path=ArenaPath;
            if(System.IO.File.Exists(path))
            {
                var existing=EditorSceneManager.OpenScene(path);
                if(Object.FindObjectOfType<ForgeArena>()==null) throw new System.InvalidOperationException("The scene does not contain a ForgeArena component.");
                EditorSceneManager.SaveScene(existing,path);
                ConfigureStartup();
                Debug.Log("FORGE_SETUP_OK: Arena scene loaded and validated.");
                return;
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var arena=new GameObject("Forge Fighters").AddComponent<ForgeArena>();
            arena.fighterPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerArmature.prefab");
            arena.sceneryPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Prefabs/Box_350x250x200_Prefab.prefab");
            arena.floorMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Art/Materials/GridBlue_01_Mat.mat");
            arena.skyMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Art/Skybox/SkyboxLiteWarm.mat");
            arena.rockPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainDemoScene_HDRP/Prefabs/Rocks/Models/Rock_A_01.fbx");
            EditorSceneManager.SaveScene(scene,path);
            var previous=EditorBuildSettings.scenes;
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(path,true)};
            foreach(var item in previous)if(item.path!=path)scenes.Add(item);
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
            ConfigureStartup();
            Debug.Log("FORGE_SETUP_OK: Arena created and registered as first build scene.");
        }
        [MenuItem("PixelForge/Open Main Menu")]
        public static void OpenMenu()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Scenes/ForgeMenu.unity");ConfigureStartup();
        }
    }
}
