using UnityEngine;

namespace PixelForge
{
    public sealed class ForgeEnvironmentAssets : ScriptableObject
    {
        public TerrainData sourceTerrain;
        public TerrainLayer grass,soil;
        public GameObject pine,rock;
        public Shader terrainShader, foliageShader;
    }
}
