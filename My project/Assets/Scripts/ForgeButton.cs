using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelForge
{
    /// <summary>
    /// Botón de menú que desliza y cambia de color su cuerpo al enfocarlo. Pasar el ratón por encima también lo
    /// selecciona, así el ratón y el teclado nunca resaltan dos botones distintos a la vez.
    /// Hereda de Button de Unity, así que conserva onClick y la navegación con flechas.
    /// </summary>
    public sealed class ForgeButton : Button
    {
        // Body: fondo del botón. Accent: barrita decorativa. Label: texto.
        public ForgeShape Body, Accent;
        public Text Label;
        // Colores en estado normal y enfocado.
        public Color Normal, Highlight, LabelNormal=Color.white, LabelHighlight=Color.white;
        // Cuánto se desplaza el cuerpo al enfocarlo (a la derecha en botones, hacia arriba en tarjetas).
        public Vector2 Lift=new Vector2(16,0);
        // Se llama cuando el botón recibe el foco (la selección lo usa para actualizar la franja VS).
        public System.Action Focused;
        // focus: animación actual (0-1). target: valor al que se dirige.
        float focus, target;
        // Posición de reposo del cuerpo.
        Vector2 rest;
        bool ready;

        /// <summary>Se llama después de asignar las partes; desactiva la transición de color propia de Unity.</summary>
        public void Setup()
        {
            rest=Body.rectTransform.anchoredPosition;ready=true;transition=Transition.None;Apply();
        }

        // Unity llama a este método cuando cambia el estado del botón (normal, resaltado, seleccionado, pulsado...).
        protected override void DoStateTransition(SelectionState state,bool instant)
        {
            base.DoStateTransition(state,instant);
            target=state==SelectionState.Highlighted||state==SelectionState.Selected||state==SelectionState.Pressed?1:0;
            if(instant){focus=target;if(ready)Apply();}
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if(EventSystem.current!=null&&interactable)EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            Focused?.Invoke();
        }

        void Update()
        {
            // Avanza la animación hacia el objetivo con tiempo real (funciona aunque el juego esté en pausa).
            if(!ready||Mathf.Approximately(focus,target))return;
            focus=Mathf.MoveTowards(focus,target,Time.unscaledDeltaTime*7);Apply();
        }

        void Apply()
        {
            // Curva "smoothstep" (3k² - 2k³): arranca y termina suave.
            float k=focus*focus*(3-2*focus);
            Body.rectTransform.anchoredPosition=rest+Lift*k;
            Body.color=Color.Lerp(Normal,Highlight,k);
            if(Label!=null)Label.color=Color.Lerp(LabelNormal,LabelHighlight,k);
            if(Accent!=null)Accent.Fill=k;
        }
    }
}
