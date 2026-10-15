using UnityEditor;
using UnityEngine;

namespace PixelForge
{
    public static class ForgeEnvironmentSetup
    {
        [InitializeOnLoadMethod]
        static void Initialize(){EditorApplication.delayCall+=Prepare;}
        [MenuItem("PixelForge/Preparar terreno de los assets")]
        public static void Prepare()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Prepare;return;}
            const string path="Assets/Resources/ForgeEnvironment.asset";
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            var asset=AssetDatabase.LoadAssetAtPath<ForgeEnvironmentAssets>(path);
            if(asset!=null)return;
            asset=ScriptableObject.CreateInstance<ForgeEnvironmentAssets>();
            asset.sourceTerrain=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/TerrainDemoScene_HDRP/Terrain/Data/Terrain_3_3_9b11d05b-b237-4de7-82ed-a402f9cedebd.asset");
            asset.grass=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainDemoScene_HDRP/Terrain/Layers/Grass_A.terrainlayer");
            asset.soil=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainDemoScene_HDRP/Terrain/Layers/Grass_Soil_A.terrainlayer");
            asset.pine=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainDemoScene_HDRP/Prefabs/Trees/Pines/Pine_A/Pine_A.prefab");
            asset.rock=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainDemoScene_HDRP/Prefabs/Rocks/Rock_A_01.prefab");
            asset.terrainShader=Shader.Find("Universal Render Pipeline/Terrain/Lit");asset.foliageShader=Shader.Find("Universal Render Pipeline/Lit");
            if(asset.sourceTerrain==null||asset.grass==null||asset.soil==null||asset.pine==null||asset.rock==null)throw new System.InvalidOperationException("Falta un recurso del Terrain Sample. No se ha guardado una configuración incompleta.");
            AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssets();Debug.Log("FORGE_ENVIRONMENT_OK: Terrain Sample configured for URP.");
        }
    }
}
