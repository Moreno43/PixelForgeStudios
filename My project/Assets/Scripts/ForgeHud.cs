using UnityEngine;
using UnityEngine.UI;

namespace PixelForge
{
    /// <summary>
    /// HUD de combate: barras de vida inclinadas y espejadas con estela de daño, rombos de rondas ganadas,
    /// el reloj de la ronda y un anunciador central que aparece con un golpe de escala cada vez que cambia su mensaje.
    /// </summary>
    public sealed class ForgeHud
    {
        readonly ForgeUI ui;
        readonly Side left, right;
        readonly Text timer, roundLabel, announcer;
        // Último mensaje mostrado, para animar solo cuando cambia.
        string announced="";

        // Elementos del HUD de un jugador.
        sealed class Side
        {
            // Fill: vida actual. Trail: estela clara que muestra el daño reciente.
            public ForgeShape Fill, Trail;
            // Pips: rombos de rondas ganadas.
            public ForgeShape[] Pips;
            public Color Color, Bright;
            // Valor que muestra la estela (baja más lento que la vida real).
            public float Shown=1;
        }

        public ForgeHud(FighterProfile leftProfile,FighterProfile rightProfile,bool versus)
        {
            ui=new ForgeUI(ForgeUIMode.Hud);
            // Oscurece la parte superior de la vista para que las barras se lean bien sobre un cielo claro.
            ui.Shape(0,0,1280,170,new Color(0,0,0,0),0,new Color(0,0,0,.55f),null,true);
            left=BuildSide(leftProfile,"J1",ForgeTheme.Orange,false);
            right=BuildSide(rightProfile,versus?"J2":"CPU",versus?ForgeTheme.Cyan:ForgeTheme.Danger,true);
            // Reloj central.
            var clock=ui.Shape(578,12,124,80,ForgeTheme.Dim(ForgeTheme.Night,1,.9f),0,ForgeTheme.Dim(ForgeTheme.Deep,1,.9f),null,true);
            ui.Shape(0,76,124,4,ForgeTheme.Orange,0,default,clock.transform);
            timer=ForgeUI.Shadow(ui.Text(0,0,124,76,"60",50,null,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic,clock.transform),new Color(0,0,0,.6f),3);
            roundLabel=ui.Text(540,96,200,24,"RONDA 1",15,ForgeTheme.Muted,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic);
            // Recordatorio de controles en la parte inferior.
            ui.Text(0,688,1280,24,"J1  WASD mover · ESPACIO salto · SHIFT bloqueo · J/K/L ataques · U esquivar          ESC  pausa",13,ForgeTheme.Dim(ForgeTheme.Muted,1,.75f),TextAnchor.MiddleCenter,FontStyle.Normal);
            announcer=ui.Text(0,230,1280,180,"",112,null,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic);
            // Los resultados largos de ronda se encogen para caber en vez de salirse de la pantalla.
            announcer.resizeTextForBestFit=true;announcer.resizeTextMinSize=40;announcer.resizeTextMaxSize=112;
            announcer.horizontalOverflow=HorizontalWrapMode.Wrap;announcer.verticalOverflow=VerticalWrapMode.Truncate;
            ForgeUI.Shadow(announcer,ForgeTheme.Dim(ForgeTheme.Orange,.85f),6);
            announcer.gameObject.AddComponent<Outline>().effectColor=new Color(0,0,0,.6f);
        }

        /// <summary>Construye la placa con el nombre, la barra y los rombos de un jugador. El lado derecho es un espejo del izquierdo respecto al centro.</summary>
        Side BuildSide(FighterProfile profile,string tag,Color tagColor,bool mirrored)
        {
            var side=new Side{Color=ForgeTheme.Dim(profile.Color,.8f),Bright=ForgeTheme.Light(profile.Color,.35f),Pips=new ForgeShape[2]};
            // Convierte una x del lado izquierdo en su posición reflejada del lado derecho.
            float Mirror(float x,float w)=>mirrored?1280-x-w:x;
            // Sentido de la inclinación (las barras se inclinan hacia el centro).
            float lean=mirrored?-1:1;
            var plate=ui.Shape(Mirror(34,260),22,260,36,mirrored?ForgeTheme.Dim(profile.Color,.25f,.85f):ForgeTheme.Dim(profile.Color,.9f),16*lean,mirrored?ForgeTheme.Dim(profile.Color,.9f):ForgeTheme.Dim(profile.Color,.25f,.85f));
            ForgeUI.Shadow(ui.Text(18,0,224,36,profile.Name,24,null,mirrored?TextAnchor.MiddleRight:TextAnchor.MiddleLeft,FontStyle.BoldAndItalic,plate.transform),new Color(0,0,0,.5f),2);
            float tagWidth=28+tag.Length*15*.62f;
            ui.Tag(mirrored?Mirror(34,260)-tagWidth-10:304,26,tag,tagColor);
            // Fondo negro de la barra, estela de daño y relleno de vida (en ese orden de dibujo).
            ui.Shape(Mirror(28,548),64,548,32,new Color(0,0,0,.62f),24*lean);
            side.Trail=ui.Shape(Mirror(36,532),69,532,22,new Color(1,.93f,.82f,.9f),18*lean);
            // Más brillante hacia el centro, donde la barra empieza a vaciarse.
            side.Fill=ui.Shape(Mirror(36,532),69,532,22,mirrored?side.Bright:side.Color,18*lean,mirrored?side.Color:side.Bright);
            // En el lado derecho la barra se vacía hacia la derecha.
            side.Trail.FromRight=side.Fill.FromRight=mirrored;
            for(int i=0;i<2;i++)
            {
                // Cuadrado girado 45° = rombo.
                var pip=ui.Shape(Mirror(44+i*30,16),104,16,16,new Color(1,1,1,.18f));
                pip.rectTransform.localRotation=Quaternion.Euler(0,0,45);side.Pips[i]=pip;
            }
            return side;
        }

        public void Hide(){ui.Hide();}
        public void Dispose(){ui.Dispose();}

        /// <summary>Actualiza todo el HUD con el estado actual; la arena lo llama cada frame.</summary>
        public void Refresh(ForgeFighter leftFighter,ForgeFighter rightFighter,float time,int round,string announcement)
        {
            Refresh(left,leftFighter);Refresh(right,rightFighter);
            int seconds=Mathf.CeilToInt(time);timer.text=seconds.ToString("00");
            // En los últimos 10 segundos el reloj parpadea entre rojo y blanco.
            timer.color=seconds<=10&&seconds>0?Color.Lerp(ForgeTheme.Danger,Color.white,Mathf.PingPong(Time.unscaledTime*2,1)):Color.white;
            roundLabel.text="RONDA "+round;
            if(announcement!=announced)
            {
                // Mensaje nuevo: entra desde 1.9 veces su tamaño.
                announced=announcement;announcer.text=announcement;
                if(announcement.Length>0)ForgeMotion.Play(announcer.rectTransform,Vector2.zero,0,.28f,1.9f);
            }
        }

        static void Refresh(Side side,ForgeFighter fighter)
        {
            // Vida en porcentaje (0 a 1).
            float health=fighter.Health/fighter.Profile.Health;
            // La estela clara mantiene un momento el daño reciente y luego baja hasta la vida real.
            side.Shown=Mathf.MoveTowards(Mathf.Max(side.Shown,health),health,Time.unscaledDeltaTime*.45f);
            side.Fill.Fill=health;side.Trail.Fill=side.Shown;
            // Con poca vida (menos del 25 %) la barra palpita hacia el rojo, para que se note que está cerca del K.O.
            float danger=health<.25f&&health>0?Mathf.PingPong(Time.unscaledTime*3,1):0;
            side.Fill.color=Color.Lerp(side.Fill.FromRight?side.Bright:side.Color,ForgeTheme.Danger,danger);
            // Rombos naranjas = rondas ganadas.
            for(int i=0;i<side.Pips.Length;i++)side.Pips[i].color=i<fighter.Wins?ForgeTheme.Orange:new Color(1,1,1,.18f);
        }
    }
}
