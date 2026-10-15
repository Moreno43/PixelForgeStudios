using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace PixelForge
{
    public static class ForgeMatch { public static int Left,Right=1; public static bool Versus; }
    public sealed class ForgeMenu : MonoBehaviour
    {
        ForgeUI ui; int selecting;
        void Start(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Time.timeScale=1;ui=new ForgeUI();Home();}
        void Home()
        {
            ui.Clear("FORGE FIGHTERS","NINJA ARENA  /  COMBATE 3D");
            ui.Text(760,190,440,250,"5 GUERREROS\n\nUna arena abierta.\nCombate cuerpo a cuerpo.\nElige tu estilo.",32);
            ui.Button(80,270,480,"BATALLA LIBRE · CPU",()=>{ForgeMatch.Versus=false;selecting=0;Select();});
            ui.Button(80,340,480,"VERSUS · DOS JUGADORES",()=>{ForgeMatch.Versus=true;selecting=0;Select();});
            ui.Button(80,410,480,"OPCIONES",Options);
            ui.Button(80,480,480,"CONTROLES",Controls);
            ui.Button(80,550,480,"SALIR",()=>{
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
                #endif
            });
            ui.Focus();
        }
        void Select()
        {
            ui.Clear("ELIGE TU GUERRERO",selecting==0?"JUGADOR 1":ForgeMatch.Versus?"JUGADOR 2":"RIVAL CPU");
            for(int i=0;i<ForgeArena.Roster.Length;i++)
            {
                int index=i;var f=ForgeArena.Roster[i];float x=70+i*232;
                ui.Panel(x,240,215,225,f.Color*.5f);
                ui.Text(x+15,265,190,70,f.Name,30);
                ui.Text(x+15,340,190,110,f.Style+"\nVIDA "+f.Health,19);
                ui.Button(x,480,215,(selecting==0?ForgeMatch.Left:ForgeMatch.Right)==i?"✓ ELEGIDO":"ELEGIR",()=>{if(selecting==0)ForgeMatch.Left=index;else ForgeMatch.Right=index;Select();});
            }
            ui.Text(70,560,1100,60,ForgeArena.Roster[ForgeMatch.Left].Name+"   VS   "+ForgeArena.Roster[ForgeMatch.Right].Name,28);
            ui.Button(70,625,300,"VOLVER",()=>{if(selecting==0)Home();else{selecting=0;Select();}});
            ui.Button(870,625,330,selecting==0?"CONFIRMAR J1":"¡A LA ARENA!",()=>{if(selecting==0){selecting=1;Select();}else SceneManager.LoadScene("ForgeArena");});ui.Focus();
        }
        void Options()
        {
            ui.Clear("OPCIONES","PANTALLA Y AUDIO");
            ui.Button(80,260,600,"PANTALLA COMPLETA / VENTANA",()=>Screen.fullScreen=!Screen.fullScreen);
            ui.Text(80,350,900,60,"Volumen general (sin banda sonora en este prototipo)",22);
            ui.Button(80,430,285,"VOLUMEN −",()=>SetVolume(-.1f));ui.Button(395,430,285,"VOLUMEN +",()=>SetVolume(.1f));
            ui.Button(80,610,400,"VOLVER",Home);ui.Focus();
        }
        void SetVolume(float change){float value=Mathf.Clamp01(PlayerPrefs.GetFloat("ForgeVolume",.6f)+change);PlayerPrefs.SetFloat("ForgeVolume",value);PlayerPrefs.Save();AudioListener.volume=value;}
        void Controls()
        {
            ui.Clear("CONTROLES","RATÓN / TECLADO · FLECHAS Y ENTER PARA LOS MENÚS");
            ui.Text(80,240,540,300,"JUGADOR 1\nWASD · mover respecto a la cámara\nEspacio · salto\nShift izquierdo · bloqueo\nJ / K / L · puño / patada / fuerte",24);
            ui.Text(680,240,540,300,"JUGADOR 2\nFlechas · mover respecto a la cámara\nNum 0 · salto\nShift derecho · bloqueo\nNum 1 / 2 / 3 · ataques",24);
            ui.Text(80,540,1100,60,"ESC · pausa. Gana dos rondas. Acércate al rival para golpear.",22);
            ui.Button(80,625,400,"VOLVER",Home);ui.Focus();
        }
    }
    // Canvas + InputSystemUIInputModule provides real pointer and keyboard navigation.
    public sealed class ForgeUI
    {
        readonly RectTransform root, content; readonly Font font; GameObject first;
        public ForgeUI()
        {
            if(EventSystem.current==null){var events=new GameObject("UI EventSystem",typeof(EventSystem));events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            var go=new GameObject("Forge UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            root=go.GetComponent<RectTransform>();font=Resources.GetBuiltinResource<Font>("Arial.ttf");
            var contentObject=new GameObject("Centered 1280x720 Layout",typeof(RectTransform));content=contentObject.GetComponent<RectTransform>();content.SetParent(root,false);
            content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.pivot=new Vector2(.5f,.5f);content.sizeDelta=new Vector2(1280,720);content.anchoredPosition=Vector2.zero;
        }
        RectTransform Rect(string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=go.GetComponent<RectTransform>();rt.SetParent(content,false);
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        public void Hide(){root.gameObject.SetActive(false);}
        public void Clear(string title,string subtitle)
        {
            root.gameObject.SetActive(true);foreach(Transform child in content){child.gameObject.SetActive(false);Object.Destroy(child.gameObject);}first=null;
            var bg=Panel(0,0,1280,720,new Color(.035f,.055f,.10f));bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=bg.offsetMax=Vector2.zero;
            Panel(740,0,540,720,new Color(.08f,.16f,.21f));Panel(70,70,8,115,new Color(1,.58f,.12f));
            Text(100,65,1100,80,title,52);Text(100,155,1100,60,subtitle,20);
            Text(80,695,1100,25,"PIXELFORGE STUDIOS     /     BATALLA LIBRE     /     PROTOTIPO",13);
        }
        public RectTransform Panel(float x,float y,float w,float h,Color color){var rt=Rect("Panel",x,y,w,h);rt.gameObject.AddComponent<Image>().color=color;return rt;}
        public void Text(float x,float y,float w,float h,string value,int size)
        {var t=Rect(value,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=Color.white;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;}
        public void Button(float x,float y,float w,string label,UnityEngine.Events.UnityAction action)
        {
            var rt=Panel(x,y,w,55,new Color(.13f,.22f,.29f));var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=rt.GetComponent<Image>();
            var colors=b.colors;colors.highlightedColor=new Color(1,.65f,.2f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.9f,.4f,.1f);b.colors=colors;b.onClick.AddListener(action);
            Text(x+18,y+13,w-36,40,label,20);if(first==null)first=b.gameObject;
        }
        public void Focus(){EventSystem.current.SetSelectedGameObject(first);}
    }
}
