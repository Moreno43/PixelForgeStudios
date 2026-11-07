using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PixelForge
{
    /// <summary>Paleta de colores compartida por los menús, las pantallas de pausa/victoria y el HUD de combate.</summary>
    public static class ForgeTheme
    {
        // Night/Deep: azules oscuros de fondo. Panel: fondo de botones y paneles.
        public static readonly Color Night=new Color(.035f,.05f,.1f), Deep=new Color(.08f,.11f,.21f), Panel=new Color(.11f,.15f,.27f,.94f);
        // Colores de acento: naranja (jugador 1), turquesa (jugador 2) y rojo (peligro/CPU).
        public static readonly Color Orange=new Color(1,.55f,.12f), Cyan=new Color(.24f,.95f,.84f), Danger=new Color(1,.3f,.32f);
        public static readonly Color Muted=new Color(.62f,.68f,.82f), Text=Color.white;
        /// <summary>Multiplica el brillo de un color y le asigna una transparencia (alpha) concreta.</summary>
        public static Color Dim(Color color,float brightness,float alpha=1)=>new Color(color.r*brightness,color.g*brightness,color.b*brightness,alpha);
        // Aclara un color mezclándolo con blanco (amount de 0 a 1).
        public static Color Light(Color color,float amount)=>Color.Lerp(color,Color.white,amount);
    }

    // Tipo de interfaz: menú (con fondo), overlay (oscurece la pelea detrás) o HUD (sin fondo ni clics).
    public enum ForgeUIMode { Menu, Overlay, Hud }

    /// <summary>
    /// Construye pantallas de Canvas desde código, sin prefabs. El diseño usa un área de referencia de 1280x720
    /// centrada, con el origen arriba a la izquierda; los fondos se estiran a toda la ventana.
    /// Cada elemento añadido después de Clear entra con un pequeño deslizamiento escalonado.
    /// </summary>
    public sealed class ForgeUI
    {
        // root: el Canvas. content: el área centrada de 1280x720 donde se coloca todo.
        readonly RectTransform root, content;
        readonly Font font;
        // first: primer botón creado (recibe el foco). stagger: retraso acumulado de la animación de entrada.
        GameObject first;
        float stagger;
        public RectTransform Content => content;

        public ForgeUI(ForgeUIMode mode=ForgeUIMode.Menu,int sortingOrder=0)
        {
            // El EventSystem es necesario para que los botones reciban teclado y ratón (con el nuevo Input System).
            if(mode!=ForgeUIMode.Hud&&EventSystem.current==null){var events=new GameObject("UI EventSystem",typeof(EventSystem));events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            var go=new GameObject("Forge UI · "+mode,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            // Canvas dibujado encima de la cámara; sortingOrder decide qué Canvas queda delante.
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=sortingOrder;
            // El HUD no necesita clics, así que no lleva GraphicRaycaster.
            if(mode!=ForgeUIMode.Hud)go.AddComponent<GraphicRaycaster>();
            // Escala la interfaz según el tamaño de la ventana tomando 1280x720 como referencia.
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            root=go.GetComponent<RectTransform>();font=Resources.GetBuiltinResource<Font>("Arial.ttf");
            if(mode==ForgeUIMode.Menu)
            {
                // Fondo del menú: degradado vertical azul noche más rayas diagonales animadas muy tenues.
                var sky=AddGraphic<ForgeShape>(Stretch(root,"Backdrop"));sky.raycastTarget=false;
                sky.color=ForgeTheme.Night;sky.End=ForgeTheme.Deep;sky.Vertical=true;
                var stripes=AddGraphic<ForgeStripes>(Stretch(root,"Stripes"));stripes.raycastTarget=false;stripes.color=new Color(1,1,1,.007f);
            }
            else if(mode==ForgeUIMode.Overlay)
            {
                // Oscurece la pelea detrás de la pausa y la victoria en vez de ocultarla. El proyecto usa espacio
                // de color lineal, donde la transparencia se ve más clara que su valor, por eso la sombra es fuerte.
                var shade=AddGraphic<ForgeShape>(Stretch(root,"Shade"));shade.color=new Color(0,0,0,.84f);
                var stripes=AddGraphic<ForgeStripes>(Stretch(root,"Stripes"));stripes.raycastTarget=false;stripes.color=new Color(1,1,1,.005f);
            }
            // La raíz se expande a toda la ventana, pero el contenido diseñado queda centrado en su tamaño
            // de referencia, para que las ventanas muy anchas o altas no recorten el diseño.
            content=new GameObject("Centered 1280x720 Layout",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(root,false);
            content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.pivot=new Vector2(.5f,.5f);content.sizeDelta=new Vector2(1280,720);content.anchoredPosition=Vector2.zero;
        }

        /// <summary>
        /// Añade un gráfico de UI junto con el CanvasRenderer que lo dibuja. El RequireComponent de Graphic
        /// no se aplica a estas subclases definidas por código, y sin el renderer no se dibuja nada.
        /// </summary>
        static T AddGraphic<T>(Component host) where T:Graphic
        {
            if(host.GetComponent<CanvasRenderer>()==null)host.gameObject.AddComponent<CanvasRenderer>();
            return host.gameObject.AddComponent<T>();
        }

        // Crea un RectTransform que ocupa todo su padre (para fondos).
        static RectTransform Stretch(RectTransform parent,string name)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
        }

        /// <summary>Crea un rectángulo posicionado desde la esquina superior izquierda de su padre (por defecto, el área de diseño).</summary>
        public RectTransform Rect(string name,float x,float y,float w,float h,Transform parent=null)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent!=null?parent:content,false);
            // Ancla y pivote arriba a la izquierda; la "y" se niega porque en UI hacia arriba es positivo.
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }

        public void Hide(){root.gameObject.SetActive(false);}
        public void Show(){root.gameObject.SetActive(true);}
        public void Dispose(){if(root!=null)Object.Destroy(root.gameObject);}

        /// <summary>Quita la pantalla actual. Destroy se aplica al final del frame, así que los controles viejos se desactivan ya para evitar clics perdidos.</summary>
        public void Clear()
        {
            Show();foreach(Transform child in content){child.gameObject.SetActive(false);Object.Destroy(child.gameObject);}
            first=null;stagger=0;
        }

        /// <summary>Reproduce la animación de entrada de un elemento; cada llamada empieza un poco después de la anterior.</summary>
        public T Enter<T>(T element,Vector2 from,float scaleFrom=1) where T:Component
        {
            ForgeMotion.Play((RectTransform)element.transform,from,stagger,.38f,scaleFrom);stagger+=.045f;return element;
        }

        /// <summary>Crea una forma (rectángulo inclinado opcional, con degradado de color a end).</summary>
        public ForgeShape Shape(float x,float y,float w,float h,Color color,float slant=0,Color end=default,Transform parent=null,bool vertical=false)
        {
            var shape=AddGraphic<ForgeShape>(Rect("Shape",x,y,w,h,parent));
            shape.raycastTarget=false;shape.color=color;shape.Slant=slant;shape.End=end;shape.Vertical=vertical;return shape;
        }

        /// <summary>Crea un texto con la fuente Arial integrada.</summary>
        public Text Text(float x,float y,float w,float h,string value,int size,Color? color=null,TextAnchor anchor=TextAnchor.MiddleLeft,FontStyle style=FontStyle.Bold,Transform parent=null)
        {
            // El texto es decorativo: los clics deben atravesarlo hasta el botón que hay debajo.
            var text=AddGraphic<Text>(Rect(value,x,y,w,h,parent));
            text.font=font;text.text=value;text.fontSize=size;text.fontStyle=style;text.color=color??ForgeTheme.Text;text.alignment=anchor;
            text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }

        /// <summary>Añade una sombra dura, el truco principal para títulos llamativos con una sola fuente integrada.</summary>
        public static T Shadow<T>(T text,Color color,float distance=4) where T:Graphic
        { var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=color;shadow.effectDistance=new Vector2(distance,-distance);return text; }

        /// <summary>Título grande de pantalla con una línea de acento debajo y un subtítulo apagado.</summary>
        public void Header(string title,string subtitle)
        {
            Enter(Shadow(Text(80,40,1000,90,title,64,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic),ForgeTheme.Dim(ForgeTheme.Orange,.8f),5),new Vector2(-60,0));
            Enter(Shape(84,128,150,6,ForgeTheme.Orange,6),new Vector2(-60,0));
            Enter(Text(84,140,900,34,subtitle,19,ForgeTheme.Muted,TextAnchor.MiddleLeft,FontStyle.Normal),new Vector2(-60,0));
        }

        /// <summary>Etiqueta inclinada cuyo ancho se ajusta a su texto.</summary>
        public RectTransform Tag(float x,float y,string label,Color color,Transform parent=null,int size=15)
        {
            // Ancho aproximado: margen de 28 px más el 62 % del tamaño de letra por cada carácter.
            float w=28+label.Length*size*.62f;
            var shape=Shape(x,y,w,size+15,color,10,default,parent);
            Text(0,0,w,size+15,label,size,ForgeTheme.Night,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic,shape.transform);
            return shape.rectTransform;
        }

        /// <summary>Botón estándar del menú: cuerpo oscuro inclinado que se desliza a la derecha y se vuelve naranja al enfocarlo.</summary>
        public ForgeButton Button(float x,float y,float w,float h,string label,UnityEngine.Events.UnityAction action,int size=22,Transform parent=null)
        {
            var rt=Rect("Button · "+label,x,y,w,h,parent);
            // accent: barrita naranja a la izquierda que aparece al enfocar el botón.
            var accent=Shape(-16,0,9,h,ForgeTheme.Orange,h*.35f,default,rt);
            var body=Shape(0,0,w,h,ForgeTheme.Panel,h*.35f,default,rt);body.raycastTarget=true;
            var text=Text(30,0,w-44,h,label,size,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic,body.transform);
            var button=rt.gameObject.AddComponent<ForgeButton>();
            button.Body=body;button.Accent=accent;button.Label=text;button.targetGraphic=body;
            button.Normal=ForgeTheme.Panel;button.Highlight=ForgeTheme.Orange;button.LabelHighlight=ForgeTheme.Night;
            button.onClick.AddListener(action);button.Setup();
            if(first==null)first=rt.gameObject;
            Enter(rt,new Vector2(-50,0));return button;
        }

        /// <summary>Ayuda de teclas en el borde inferior más el crédito del estudio.</summary>
        public void Footer(string hint)
        {
            Enter(Text(80,676,820,26,hint,14,ForgeTheme.Muted),new Vector2(0,20));
            Text(820,676,380,26,"PIXELFORGE STUDIOS  ·  PROTOTIPO",12,ForgeTheme.Dim(ForgeTheme.Muted,1,.6f),TextAnchor.MiddleRight);
        }

        /// <summary>Selecciona un elemento (o el primer botón) para poder navegar con el teclado.</summary>
        public void Focus(GameObject target=null)
        {
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(target!=null?target:first);
        }
    }
}
