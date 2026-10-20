using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Stores imported terrain, vegetation, and shader references for runtime loading through Resources.
    /// The editor prepares this asset so runtime scripts never depend on UnityEditor APIs.
    /// </summary>
    public sealed class ForgeEnvironmentAssets : ScriptableObject
    {
        public TerrainData sourceTerrain;
        public TerrainLayer grass,soil;
        public GameObject pine,rock;
        public Shader terrainShader, foliageShader;
    }
}
