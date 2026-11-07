using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Guarda las referencias al terreno, la vegetación y los shaders importados para cargarlas en el juego desde Resources.
    /// El editor prepara este asset, así los scripts del juego nunca dependen de las APIs de UnityEditor.
    /// </summary>
    public sealed class ForgeEnvironmentAssets : ScriptableObject
    {
        // Se incrementa cuando el editor añade campos, para actualizar un asset existente en vez de recrearlo.
        public const int CurrentVersion = 2;
        public int version;
        // Versión 1: terreno original, capas de pasto y tierra, un pino, una roca y los shaders URP.
        public TerrainData sourceTerrain;
        public TerrainLayer grass,soil;
        public GameObject pine,rock;
        public Shader terrainShader, foliageShader;
        // Versión 2: un bosque más rico.
        public TerrainLayer moss,pebbles;
        public GameObject[] trees, rocks, plants;
        // Copias legibles de las texturas de pasto del Sample, pintadas como pasto del terreno que se mueve con el viento.
        public Texture2D grassDetail, dryGrassDetail;
        // Sustitutos URP de los materiales HDRP del Sample, emparejados por índice. Son assets reales,
        // así las builds conservan las variantes de shader (viento, billboards, recorte alfa) que necesitan.
        public Material[] sourceMaterials, urpMaterials;
        // Material de las motas de polvo luminosas.
        public Material moteMaterial;
    }
}
