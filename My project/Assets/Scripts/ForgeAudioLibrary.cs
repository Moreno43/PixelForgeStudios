using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Referencias a la música y los efectos de sonido, cargadas en tiempo de ejecución desde Resources.
    /// El editor la rellena a partir de Assets/Audio según el nombre de cada archivo, así los scripts del juego
    /// nunca dependen de las APIs de UnityEditor (que no existen en una build).
    /// Un ScriptableObject es un asset de datos: se guarda como archivo (.asset) en el proyecto.
    /// </summary>
    public sealed class ForgeAudioLibrary : ScriptableObject
    {
        public AudioClip menuMusic;
        // En cada pelea se elige una de estas al azar.
        public AudioClip[] battleMusic;
        // Se permiten varios archivos por sonido; se elige uno al azar para que las repeticiones no suenen mecánicas.
        public AudioClip[] punch, kick, heavy, block, dash, jump, land, footsteps;
    }
}
