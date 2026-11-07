using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelForge
{
    // Las variables estáticas sobreviven al cambio de escena sin mantener vivos los objetos del menú.
    // Así el menú le pasa a la arena qué personajes se eligieron (Left/Right) y si es contra la CPU o 2 jugadores (Versus).
    // OpenSelect permite que la pantalla de victoria vuelva directamente a la selección de personajes.
    public static class ForgeMatch { public static int Left,Right=1; public static bool Versus, OpenSelect; }

    /// <summary>
    /// Construye las pantallas del menú (inicio, selección, versus, opciones y controles)
    /// y carga la arena cuando ambos luchadores están elegidos. Es el script principal de la escena ForgeMenu.
    /// </summary>
    public sealed class ForgeMenu : MonoBehaviour
    {
        ForgeUI ui;
        // 0 = eligiendo el jugador 1, 1 = eligiendo el jugador 2 o el rival de la CPU.
        int selecting;
        // ESC ejecuta la acción "volver" de la pantalla actual; es null donde no se puede volver.
        System.Action back;
        // Selección de personaje: la franja inferior muestra el enfrentamiento según la tarjeta enfocada.
        Text leftName, rightName;
        ForgeShape leftBanner, rightBanner;
        // Opciones: el medidor de volumen se actualiza en su sitio en vez de reconstruir la pantalla.
        ForgeShape[] volumeSegments;
        Text volumeLabel;

        void Start()
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Time.timeScale=1;
            // Crea el Canvas del menú y pone la música del menú.
            ui=new ForgeUI(ForgeUIMode.Menu);ForgeAudio.PlayMenu();
            // Si venimos de "Cambiar luchadores", se abre directamente la selección.
            if(ForgeMatch.OpenSelect){ForgeMatch.OpenSelect=false;selecting=0;Select();}else Home();
        }

        void Update()
        {
            if(back!=null&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)back();
        }

        /// <summary>Pantalla de inicio: título, botones principales y franja con los 5 personajes.</summary>
        void Home()
        {
            ui.Clear();back=null;
            // Una franja lateral inclinada y una palabra gigante casi transparente dan profundidad sin usar imágenes.
            ui.Enter(ui.Shape(770,0,700,720,ForgeTheme.Dim(ForgeTheme.Deep,1.25f,.95f),300,ForgeTheme.Dim(ForgeTheme.Deep,.7f,.95f)),new Vector2(160,0));
            ui.Enter(ui.Shape(756,0,10,720,ForgeTheme.Orange,300),new Vector2(160,0));
            ui.Text(600,330,900,300,"FIGHT",240,new Color(1,1,1,.035f),TextAnchor.MiddleLeft,FontStyle.BoldAndItalic);
            ui.Enter(ForgeUI.Shadow(ui.Text(76,58,700,110,"FORGE",104,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic),ForgeTheme.Dim(ForgeTheme.Orange,.75f),6),new Vector2(-80,0));
            ui.Enter(ForgeUI.Shadow(ui.Text(76,150,760,110,"FIGHTERS",104,ForgeTheme.Orange,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic),ForgeTheme.Night,6),new Vector2(-80,0));
            ui.Enter(ui.Tag(84,268,"NINJA ARENA  ·  COMBATE 3D",ForgeTheme.Cyan),new Vector2(-80,0));
            // Cada texto de botón va con su acción en la misma posición de los arreglos.
            string[] labels={"BATALLA CONTRA LA CPU","VERSUS  ·  2 JUGADORES","CONTROLES","OPCIONES","SALIR"};
            UnityEngine.Events.UnityAction[] actions={
                ()=>{ForgeMatch.Versus=false;selecting=0;Select();},
                ()=>{ForgeMatch.Versus=true;selecting=0;Select();},
                Controls,Options,Quit};
            for(int i=0;i<labels.Length;i++)ui.Button(84,330+i*64,440,52,labels[i],actions[i]);
            // Vista previa del roster en la franja: una tira de color por luchador, escalonadas en diagonal.
            ui.Enter(ui.Text(930,96,320,30,"5 GUERREROS",18,ForgeTheme.Muted,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic),new Vector2(80,0));
            for(int i=0;i<ForgeArena.Roster.Length;i++)
            {
                var f=ForgeArena.Roster[i];float x=960-i*42,y=140+i*96;
                var strip=ui.Shape(x,y,330,78,ForgeTheme.Dim(f.Color,.85f,.95f),40,ForgeTheme.Dim(f.Color,.2f,.4f));
                ForgeUI.Shadow(ui.Text(30,4,280,46,f.Name,36,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic,strip.transform),new Color(0,0,0,.45f),3);
                ui.Text(32,44,280,26,f.Style.ToUpperInvariant(),13,new Color(1,1,1,.85f),TextAnchor.MiddleLeft,FontStyle.Bold,strip.transform);
                ui.Enter(strip,new Vector2(120,0));
            }
            ui.Footer("FLECHAS / RATÓN  navegar        ENTER  elegir        ESC  volver");
            ui.Focus();
        }

        // Cierra el juego; dentro del editor de Unity detiene el modo Play.
        void Quit()
        {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
            #endif
        }

        static readonly string[] StatNames={"VIDA","VELOCIDAD","POTENCIA"};
        // Las estadísticas se muestran respecto a rangos fijos que cubren todo el roster (0-1),
        // así las barras comparan a los luchadores de forma justa.
        static float[] Stats(FighterProfile f)=>new[]{Mathf.InverseLerp(70,130,f.Health),Mathf.InverseLerp(2,5,f.Speed),Mathf.InverseLerp(.8f,1.35f,f.Power)};

        /// <summary>Pantalla de selección de personaje (se usa dos veces: jugador 1 y luego jugador 2/CPU).</summary>
        void Select()
        {
            ui.Clear();
            // Volver desde el jugador 2 regresa al jugador 1; desde el jugador 1 regresa al inicio.
            back=()=>{if(selecting==0)Home();else{selecting=0;Select();}};
            bool second=selecting==1;
            ui.Header("ELIGE TU GUERRERO",second?(ForgeMatch.Versus?"Ahora elige el jugador 2.":"Elige al rival que controlará la CPU."):"¿Quién entra primero a la arena?");
            ui.Enter(ui.Tag(1010,62,second?(ForgeMatch.Versus?"JUGADOR 2":"RIVAL CPU"):"JUGADOR 1",second?(ForgeMatch.Versus?ForgeTheme.Cyan:ForgeTheme.Danger):ForgeTheme.Orange,null,20),new Vector2(60,0));
            // Crea las 5 tarjetas y recuerda cuál enfocar primero (la última elección de este lado).
            GameObject focus=null;
            for(int i=0;i<ForgeArena.Roster.Length;i++)
            {
                int index=i;var card=Card(index,64+i*234,196);
                if(i==(second?ForgeMatch.Right:ForgeMatch.Left))focus=card.gameObject;
            }
            // Franja del enfrentamiento: el lado que se está eligiendo sigue a la tarjeta enfocada.
            leftBanner=ui.Enter(ui.Shape(64,566,540,72,ForgeTheme.Orange,28),new Vector2(-80,0));
            leftName=ui.Text(34,0,480,72,"",38,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic,leftBanner.transform);
            ui.Enter(ForgeUI.Shadow(ui.Text(590,560,100,84,"VS",54,null,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic),ForgeTheme.Orange,4),Vector2.zero,1.6f);
            rightBanner=ui.Enter(ui.Shape(676,566,540,72,ForgeTheme.Cyan,28),new Vector2(80,0));
            rightName=ui.Text(26,0,480,72,"",38,null,TextAnchor.MiddleRight,FontStyle.BoldAndItalic,rightBanner.transform);
            UpdateBanner(second?ForgeMatch.Right:ForgeMatch.Left);
            ui.Button(64,656,220,44,"VOLVER",()=>back(),18);
            ui.Text(760,656,456,44,"ENTER  confirmar        ESC  volver",14,ForgeTheme.Muted,TextAnchor.MiddleRight,FontStyle.Normal);
            ui.Focus(focus);
        }

        /// <summary>Tarjeta de luchador: degradado de su color, nombre, estilo y tres barras de estadísticas. Al enfocarla se eleva; al pulsarla se elige.</summary>
        ForgeButton Card(int index,float x,float y)
        {
            var f=ForgeArena.Roster[index];const float w=212,h=340;
            var rt=ui.Rect("Card · "+f.Name,x,y,w,h);
            // accent: línea blanca bajo la tarjeta que se ilumina al enfocarla.
            var accent=ui.Shape(0,h+8,w,6,Color.white,10,default,rt);
            var body=ui.Shape(0,0,w,h,ForgeTheme.Dim(f.Color,.2f),18,ForgeTheme.Dim(f.Color,.8f),rt,true);body.raycastTarget=true;
            // Inicial gigante del nombre como decoración de fondo.
            ui.Text(14,40,w,220,f.Name.Substring(0,1),230,new Color(1,1,1,.08f),TextAnchor.MiddleCenter,FontStyle.BoldAndItalic,body.transform);
            ForgeUI.Shadow(ui.Text(20,18,w-30,48,f.Name,36,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic,body.transform),new Color(0,0,0,.5f),3);
            ui.Text(20,66,w-36,52,f.Style.ToUpperInvariant(),13,new Color(1,1,1,.85f),TextAnchor.UpperLeft,FontStyle.Bold,body.transform);
            // Barras de estadísticas: fondo oscuro y relleno blanco proporcional (mínimo 15 % para que se vea).
            var stats=Stats(f);
            for(int s=0;s<3;s++)
            {
                float sy=196+s*44;
                ui.Text(20,sy,w-36,18,StatNames[s],12,new Color(1,1,1,.75f),TextAnchor.MiddleLeft,FontStyle.Bold,body.transform);
                ui.Shape(20,sy+20,w-44,9,new Color(0,0,0,.45f),6,default,body.transform);
                ui.Shape(20,sy+20,w-44,9,Color.white,6,default,body.transform).Fill=Mathf.Lerp(.15f,1,stats[s]);
            }
            // Marca el luchador que ya eligió el jugador 1 mientras elige el jugador 2.
            if(selecting==1&&index==ForgeMatch.Left)ui.Tag(w-62,14,"J1",ForgeTheme.Orange,body.transform,13);
            // Convierte la tarjeta en un botón navegable con teclado y ratón.
            var button=rt.gameObject.AddComponent<ForgeButton>();
            button.Body=body;button.Accent=accent;button.targetGraphic=body;button.Lift=new Vector2(0,14);
            button.Normal=ForgeTheme.Dim(f.Color,.2f);button.Highlight=ForgeTheme.Dim(f.Color,.55f);
            button.Focused=()=>UpdateBanner(index);
            button.onClick.AddListener(()=>Choose(index));button.Setup();
            ui.Enter(rt,new Vector2(0,60));return button;
        }

        // Actualiza los nombres y colores de la franja del enfrentamiento según la tarjeta enfocada.
        void UpdateBanner(int focused)
        {
            if(leftName==null)return;
            int left=selecting==0?focused:ForgeMatch.Left;
            var l=ForgeArena.Roster[left];
            leftName.text=l.Name;leftBanner.color=ForgeTheme.Dim(l.Color,.9f);leftBanner.End=ForgeTheme.Dim(l.Color,.25f,.85f);
            // Mientras elige el jugador 1, el lado derecho queda como incógnita "?".
            if(selecting==0){rightName.text="?";rightBanner.color=ForgeTheme.Dim(ForgeTheme.Deep,1,.9f);rightBanner.End=ForgeTheme.Dim(ForgeTheme.Muted,.35f,.9f);return;}
            var r=ForgeArena.Roster[focused];
            rightName.text=r.Name;rightBanner.color=ForgeTheme.Dim(r.Color,.25f,.85f);rightBanner.End=ForgeTheme.Dim(r.Color,.9f);
        }

        // Guarda la elección: primero el jugador 1, después el rival, y entonces muestra la pantalla VS.
        void Choose(int index)
        {
            if(selecting==0){ForgeMatch.Left=index;selecting=1;Select();}
            else{ForgeMatch.Right=index;Versus();}
        }

        /// <summary>Pantalla dividida del enfrentamiento; después se carga la arena.</summary>
        void Versus()
        {
            ui.Clear();back=null;leftName=rightName=null;
            var l=ForgeArena.Roster[ForgeMatch.Left];var r=ForgeArena.Roster[ForgeMatch.Right];
            // Dos mitades inclinadas con el color de cada luchador, que entran desde los lados.
            ui.Enter(ui.Shape(-160,0,880,720,ForgeTheme.Dim(l.Color,.18f),260,ForgeTheme.Dim(l.Color,.85f)),new Vector2(-300,0));
            ui.Enter(ui.Shape(560,0,880,720,ForgeTheme.Dim(r.Color,.85f),260,ForgeTheme.Dim(r.Color,.18f)),new Vector2(300,0));
            ui.Enter(ui.Tag(90,190,"JUGADOR 1",ForgeTheme.Orange,null,20),new Vector2(-120,0));
            ui.Enter(ForgeUI.Shadow(ui.Text(80,224,620,140,l.Name,120,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic),new Color(0,0,0,.5f),6),new Vector2(-160,0));
            ui.Enter(ui.Text(90,360,560,30,l.Style.ToUpperInvariant(),18,new Color(1,1,1,.85f)),new Vector2(-160,0));
            // La etiqueta derecha se alinea a la derecha calculando su ancho igual que ForgeUI.Tag.
            var rightTag=ForgeMatch.Versus?"JUGADOR 2":"CPU";
            ui.Enter(ui.Tag(1190-(28+rightTag.Length*20*.62f),330,rightTag,ForgeMatch.Versus?ForgeTheme.Cyan:ForgeTheme.Danger,null,20),new Vector2(120,0));
            ui.Enter(ForgeUI.Shadow(ui.Text(580,364,620,140,r.Name,120,null,TextAnchor.MiddleRight,FontStyle.BoldAndItalic),new Color(0,0,0,.5f),6),new Vector2(160,0));
            ui.Enter(ui.Text(630,500,560,30,r.Style.ToUpperInvariant(),18,new Color(1,1,1,.85f),TextAnchor.MiddleRight),new Vector2(160,0));
            ui.Enter(ForgeUI.Shadow(ui.Text(490,250,300,220,"VS",170,null,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic),ForgeTheme.Orange,8),Vector2.zero,2.4f);
            ui.Enter(ui.Text(0,640,1280,34,"PREPARANDO LA ARENA DEL BOSQUE…",17,ForgeTheme.Text,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic),new Vector2(0,30));
            StartCoroutine(LoadArena());
        }

        // Corrutina: espera 2.2 s reales (sin depender de Time.timeScale) y carga la escena de la arena.
        IEnumerator LoadArena()
        {
            yield return new WaitForSecondsRealtime(2.2f);
            SceneManager.LoadScene("ForgeArena");
        }

        /// <summary>Pantalla de opciones: pantalla completa/ventana y volumen general.</summary>
        void Options()
        {
            ui.Clear();back=Home;
            ui.Header("OPCIONES","Pantalla y audio.");
            ForgeButton display=null;
            display=ui.Button(84,220,620,56,DisplayLabel(Screen.fullScreen),()=>{
                // Screen.fullScreen se aplica en el siguiente frame, así que el texto usa directamente el modo pedido.
                bool next=!Screen.fullScreen;Screen.fullScreen=next;display.Label.text=DisplayLabel(next);});
            // Medidor de volumen de 10 segmentos.
            ui.Enter(ui.Text(84,310,400,30,"VOLUMEN GENERAL",18,ForgeTheme.Muted,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic),new Vector2(-50,0));
            volumeSegments=new ForgeShape[10];
            for(int i=0;i<10;i++)volumeSegments[i]=ui.Enter(ui.Shape(84+i*50,348,42,34,ForgeTheme.Panel,12),new Vector2(-50,0));
            volumeLabel=ui.Enter(ui.Text(600,344,120,42,"",30,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic),new Vector2(-50,0));
            ui.Button(84,406,240,52,"BAJAR  −",()=>SetVolume(-.1f));
            ui.Button(344,406,240,52,"SUBIR  +",()=>SetVolume(.1f));
            ui.Enter(ui.Text(84,476,700,30,"Controla la música y los efectos de sonido del juego.",15,ForgeTheme.Muted,TextAnchor.MiddleLeft,FontStyle.Normal),new Vector2(-50,0));
            ui.Button(84,600,260,52,"VOLVER",Home);
            ui.Footer("FLECHAS / RATÓN  navegar        ENTER  cambiar        ESC  volver");
            // SetVolume(0) no cambia el volumen, solo pinta el medidor con el valor guardado.
            SetVolume(0);ui.Focus();
        }

        static string DisplayLabel(bool fullScreen)=>"PANTALLA  ·  "+(fullScreen?"COMPLETA":"VENTANA");

        // Sube o baja el volumen, lo guarda en PlayerPrefs (persiste entre sesiones) y lo aplica a todo el audio.
        void SetVolume(float change)
        {
            // Redondea a pasos de 10 % para que pulsar muchas veces nunca quede entre dos segmentos.
            float value=Mathf.Round(Mathf.Clamp01(PlayerPrefs.GetFloat("ForgeVolume",.6f)+change)*10)/10;
            PlayerPrefs.SetFloat("ForgeVolume",value);PlayerPrefs.Save();AudioListener.volume=value;
            for(int i=0;i<volumeSegments.Length;i++)volumeSegments[i].color=i<Mathf.RoundToInt(value*10)?ForgeTheme.Orange:ForgeTheme.Panel;
            volumeLabel.text=Mathf.RoundToInt(value*100)+"%";
        }

        /// <summary>Pantalla de controles: un panel por jugador con sus teclas.</summary>
        void Controls()
        {
            ui.Clear();back=Home;
            ui.Header("CONTROLES","Los dos jugadores comparten el teclado. Haz clic en la vista Game para darle el foco.");
            ControlPanel(84,196,"JUGADOR 1",ForgeTheme.Orange,new[]{
                (new[]{"W","A","S","D"},"Moverse"),(new[]{"ESPACIO"},"Saltar"),(new[]{"SHIFT IZQ."},"Bloquear"),
                (new[]{"J"},"Puño"),(new[]{"K"},"Patada"),(new[]{"L"},"Golpe fuerte"),(new[]{"U"},"Esquivar")});
            ControlPanel(664,196,"JUGADOR 2",ForgeTheme.Cyan,new[]{
                (new[]{"FLECHAS"},"Moverse"),(new[]{"NUM 0"},"Saltar"),(new[]{"SHIFT DER."},"Bloquear"),
                (new[]{"NUM 1"},"Puño"),(new[]{"NUM 2"},"Patada"),(new[]{"NUM 3"},"Golpe fuerte"),(new[]{"NUM 4"},"Esquivar")});
            ui.Button(84,630,240,48,"VOLVER",Home,18);
            ui.Text(360,630,840,48,"ESC  pausa      ·      Gana dos rondas      ·      Acércate al rival para golpear",15,ForgeTheme.Muted,TextAnchor.MiddleRight,FontStyle.Normal);
            ui.Focus();
        }

        // Dibuja un panel de controles: cada fila tiene "fichas" de teclas a la izquierda y la acción a la derecha.
        void ControlPanel(float x,float y,string title,Color color,(string[] keys,string action)[] rows)
        {
            var panel=ui.Enter(ui.Shape(x,y,532,410,ForgeTheme.Dim(ForgeTheme.Deep,1.2f,.9f),0,ForgeTheme.Dim(ForgeTheme.Deep,.8f,.9f),null,true),new Vector2(0,40));
            ui.Shape(0,0,532,5,color,0,default,panel.transform);
            ui.Tag(24,22,title,color,panel.transform,17);
            for(int i=0;i<rows.Length;i++)
            {
                float rowY=78+i*46,chipX=24;
                foreach(var key in rows[i].keys)
                {
                    // El ancho de cada ficha depende de la longitud del texto de la tecla.
                    float w=24+key.Length*10.5f;
                    var chip=ui.Shape(chipX,rowY,w,34,new Color(1,1,1,.1f),8,default,panel.transform);
                    ui.Shape(0,31,w,3,color,8,default,chip.transform);
                    ui.Text(0,0,w,34,key,15,null,TextAnchor.MiddleCenter,FontStyle.Bold,chip.transform);
                    chipX+=w+8;
                }
                ui.Text(250,rowY,260,34,rows[i].action.ToUpperInvariant(),18,null,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic,panel.transform);
            }
        }
    }
}
