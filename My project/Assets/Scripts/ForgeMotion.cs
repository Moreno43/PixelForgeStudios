using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Animación de entrada de un solo uso: se desliza desde un desplazamiento, aparece con un fundido
    /// y opcionalmente se asienta desde una escala mayor.
    /// Usa tiempo real, así los menús se animan durante la pausa y la cámara lenta.
    /// </summary>
    public sealed class ForgeMotion : MonoBehaviour
    {
        RectTransform rect;
        // CanvasGroup permite cambiar la transparencia de un elemento y todos sus hijos a la vez.
        CanvasGroup group;
        // target: posición final. from: desplazamiento inicial respecto a ella.
        Vector2 target, from;
        // time empieza en negativo (-delay) para esperar antes de arrancar.
        float time, duration, scaleFrom;

        /// <summary>Inicia la animación en un elemento de UI, reutilizando el componente si ya existe.</summary>
        public static ForgeMotion Play(RectTransform rect,Vector2 from,float delay,float duration=.35f,float scaleFrom=1)
        {
            var motion=rect.GetComponent<ForgeMotion>();
            if(motion==null)motion=rect.gameObject.AddComponent<ForgeMotion>();
            else if(motion.enabled)motion.Apply(1); // Termina una animación en curso antes de reiniciarla desde su posición final.
            motion.rect=rect;motion.target=rect.anchoredPosition;motion.from=from;
            motion.time=-delay;motion.duration=duration;motion.scaleFrom=scaleFrom;
            motion.group=rect.GetComponent<CanvasGroup>();if(motion.group==null)motion.group=rect.gameObject.AddComponent<CanvasGroup>();
            motion.enabled=true;motion.Apply(0);return motion;
        }

        void Update()
        {
            time+=Time.unscaledDeltaTime;
            // Curva "ease-out" cúbica: rápida al principio y suave al llegar.
            float k=Mathf.Clamp01(time/duration);Apply(1-(1-k)*(1-k)*(1-k));
            // Al terminar se desactiva para no gastar Update.
            if(k>=1)enabled=false;
        }

        // Aplica el progreso k (0 = inicio, 1 = final) a posición, escala y transparencia.
        void Apply(float k)
        {
            rect.anchoredPosition=target+from*(1-k);
            rect.localScale=Vector3.one*Mathf.LerpUnclamped(scaleFrom,1,k);
            group.alpha=k;
        }
    }
}
