namespace PixelForge
{
    /// <summary>
    /// Describe una animación de combate importada: qué archivo la aporta y qué parte se usa.
    /// Los tiempos están en segundos de la animación: se empieza a reproducir en Start, Key es el momento clave
    /// (brazo estirado, punto más bajo, guardia arriba) y End es cuando vuelve a la pose de pelea.
    /// </summary>
    public sealed class CombatClip
    {
        // State: nombre del estado en el Animator. Keywords: palabras (separadas por |) para encontrar el archivo.
        public readonly string State, Keywords;
        public readonly float Start, Key, End;
        // Mirrored: se crea también una versión espejada (lado contrario).
        public readonly bool Mirrored;
        public CombatClip(string state,string keywords,float start,float key,float end,bool mirrored)
        { State=state; Keywords=keywords; Start=start; Key=key; End=end; Mirrored=mirrored; }
        // Nombre del estado espejado, por ejemplo "JabMirror".
        public string MirrorState => State+"Mirror";
    }

    /// <summary>
    /// Animaciones de combate importadas de Mixamo en <see cref="Folder"/>. El editor construye una capa
    /// del Animator a partir de esta tabla; si falta alguna animación, el luchador usa su pose procedimental.
    /// </summary>
    public static class ForgeCombatClips
    {
        // Carpeta donde se colocan los .fbx y nombre del Animator Controller generado (en Resources).
        public const string Folder = "Assets/Character/Animations/Combat";
        public const string Controller = "ForgeFighterAnimator";
        // Nombre de la capa de combate y del parámetro que controla su velocidad.
        public const string Layer = "Combat", SpeedParameter = "CombatSpeed";
        // Punching.fbx: un jab izquierdo; el brazo está estirado a los 0.60 s y vuelve a la guardia a 1.05 s.
        // El estado espejado aporta la mano derecha para alternar el uno-dos.
        public static readonly CombatClip Jab = new CombatClip("Jab","punch|jab",.30f,.60f,1.05f,true);
        // Body Block.fbx: sube la guardia y la vuelve a bajar; se congela en su punto más alto.
        public static readonly CombatClip Block = new CombatClip("Block","block",1.30f,1.30f,1.30f,false);
        // Esquiva 2.fbx: una voltereta lateral, en lo más bajo a los 0.90 s y de pie otra vez a 1.75 s.
        public static readonly CombatClip Dodge = new CombatClip("Dodge","esquiva|dodge",.10f,.90f,1.75f,true);
        // Reaction.fbx: se dobla por un golpe al cuerpo; la sacudida es máxima cerca de 0.45 s.
        public static readonly CombatClip Hit = new CombatClip("Hit","reaction|hit",.20f,.45f,1.50f,false);
        // Animaciones que van en la capa de combate.
        public static readonly CombatClip[] All = { Jab, Block, Dodge, Hit };
        // Fighting Idle.fbx: el balanceo de boxeador en bucle. Sustituye al "idle" del blend tree de locomoción
        // en lugar de ir en la capa de combate, así caminar se mezcla directamente desde ella.
        public static readonly CombatClip Idle = new CombatClip("Idle","idle",0,0,3.3f,false);
        // Existe en el controlador generado cuando la pose de pelea sustituyó al idle por defecto.
        public const string StanceParameter = "FightingStance";
        // Running.fbx: un ciclo de zancada de 0.70 s que recorre 3.34 m (4.8 m/s). Sustituye tanto a caminar como a
        // correr en el blend tree, así todos los pasos la usan; los luchadores ajustan su velocidad de reproducción
        // a su rapidez real (RunSpeed) para que los pies coincidan con el suelo a cualquier ritmo.
        public static readonly CombatClip Run = new CombatClip("Run","running",0,0,.7f,false);
        public const float RunSpeed = 4.8f;
        // Velocidad del blend tree a la que la carrera sustituye por completo a la pose de pelea, para que empiece a dar zancadas enseguida.
        public const float RunBlendSpeed = .6f;
        // Existe en el controlador generado cuando la carrera sustituyó a caminar y correr.
        public const string RunParameter = "FightingRun";
        // +1 si la voltereta sin espejar rueda hacia la derecha del personaje, -1 si rueda hacia la izquierda.
        public const int DodgeRollSide = 1;
    }
}
