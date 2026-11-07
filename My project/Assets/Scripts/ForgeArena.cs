using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PixelForge
{
    /// <summary>
    /// Controla una pelea entre dos luchadores: entrada de los jugadores, decisiones de la CPU, rondas,
    /// daño, cámara y HUD. Es el script principal de la escena ForgeArena.
    /// Los personajes elegidos llegan desde el menú a través de ForgeMatch.
    /// </summary>
    public sealed class ForgeArena : MonoBehaviour
    {
        // Referencias asignadas en el Inspector de la escena.
        public GameObject fighterPrefab, sceneryPrefab;
        public Material floorMaterial, skyMaterial;
        Camera arenaCamera;
        // overlay: pantallas de pausa y victoria. hud: barras de vida, tiempo y anuncios.
        ForgeUI overlay;
        ForgeHud hud;
        public GameObject rockPrefab;
        // Los 5 personajes disponibles: nombre, estilo, color, vida, velocidad y multiplicador de daño.
        public static readonly FighterProfile[] Roster = {
            new FighterProfile("BLAZE", "Presión · puños de fuego", new Color(1,.28f,.18f),100,3.6f,1.1f),
            new FighterProfile("NYX", "Velocidad · sombra", new Color(.62f,.35f,1),90,4.7f,.95f),
            new FighterProfile("ATLAS", "Potencia · armadura pesada", new Color(1,.7f,.18f),125,2.7f,1.25f),
            new FighterProfile("KAI", "Equilibrio · artes marciales", new Color(.15f,.8f,.9f),105,3.8f,1),
            new FighterProfile("VERA", "Agilidad · patadas", new Color(.3f,1,.55f),95,4.3f,1.05f)
        };
        // Estado de la pantalla: peleando, en pausa o mostrando el resultado final.
        enum ScreenState { Fight, Pause, Result }
        // Una esfera de corta duración que marca dónde conectó un golpe.
        struct Spark { public Transform Shape; public float Age, Size; }
        ScreenState screen;
        ForgeFighter left,right;
        // pickLeft/pickRight: índices del roster elegidos. versus: true = dos jugadores, false = contra la CPU.
        int pickLeft, pickRight=1, round=1;
        // roundOver: la ronda terminó. draw: la ronda fue empate. cpuCombo: la CPU encadenará un segundo jab.
        bool versus, roundOver, draw, cpuCombo;
        // timer: segundos de la ronda. countdown: cuenta atrás antes de "¡LUCHA!". roundWait: espera tras el K.O.
        // aiTime: tiempo hasta la próxima decisión de la CPU. hitStop: congelación breve al golpear.
        // shake: intensidad del temblor de cámara. slowMotion: tiempo restante de cámara lenta.
        float timer=60, countdown=3, roundWait, aiTime, volume, hitStop, shake, slowMotion;
        string result="";
        readonly List<Spark> sparks = new List<Spark>();
        Material hitMaterial, blockMaterial;
        readonly Color ink = new Color(.035f,.045f,.085f), accent = new Color(.25f,.95f,.85f);
        void Awake()
        {
            // Volumen guardado en Opciones (0.6 por defecto).
            volume=PlayerPrefs.GetFloat("ForgeVolume",.6f); AudioListener.volume=volume;
            Application.targetFrameRate=60;Time.timeScale=1;
            // La cámara se crea por código; su posición se actualiza en LateUpdate.
            var cameraObject=new GameObject("Arena Camera"); var cam=cameraObject.AddComponent<Camera>();arenaCamera=cam;
            cam.transform.position=new Vector3(0,8,-12); cam.transform.LookAt(new Vector3(0,1,0));
            cam.orthographic=false;cam.fieldOfView=48;cam.backgroundColor=ink;
            if(skyMaterial!=null)RenderSettings.skybox=skyMaterial;
            // Iluminación, niebla, postprocesado y bosque.
            var environment=Resources.Load<ForgeEnvironmentAssets>("ForgeEnvironment");
            ForgeAtmosphere.Apply(cam,environment!=null?environment.moteMaterial:null);
            // Usa el terreno importado si está configurado; si no, un escenario simple de respaldo.
            if(!ForgeTerrain.Build(environment))ForgeLandscape.Build(rockPrefab);
            // Materiales de las chispas: blanco cálido para golpes, turquesa para bloqueos.
            var unlit=Shader.Find("Universal Render Pipeline/Unlit");
            hitMaterial=new Material(unlit);hitMaterial.SetColor("_BaseColor",new Color(1,.95f,.75f));
            blockMaterial=new Material(unlit);blockMaterial.SetColor("_BaseColor",accent);
            // Lee la selección hecha en el menú.
            pickLeft=ForgeMatch.Left;pickRight=ForgeMatch.Right;versus=ForgeMatch.Versus;
            overlay=new ForgeUI(ForgeUIMode.Overlay,10);overlay.Hide();Begin();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void OnDestroy()
        {
            // La cámara lenta nunca debe pasar al menú ni a la siguiente pelea.
            Time.timeScale=1;
            if(hitMaterial!=null)Destroy(hitMaterial);if(blockMaterial!=null)Destroy(blockMaterial);
            if(hud!=null)hud.Dispose();overlay?.Dispose();
        }
        // Crea (o vuelve a crear) a los dos luchadores elegidos.
        void Preview() { Clear(); left=new ForgeFighter(Roster[pickLeft],-3,fighterPrefab); right=new ForgeFighter(Roster[pickRight],3,fighterPrefab); }
        void LateUpdate()
        {
            // El Animator se evalúa entre Update y LateUpdate. Las poses de combate se aplican después,
            // para que las animaciones de locomoción no sobrescriban la pose final del ataque.
            if(left==null||right==null)return;left.Pose();right.Pose();
            // Cámara: se coloca detrás del jugador izquierdo y mira al punto medio entre ambos;
            // se aleja cuanto más separados están.
            var middle=(left.Position+right.Position)*.5f;middle.y=1;
            float distance=Vector3.Distance(left.Position,right.Position);
            var direction=right.Position-left.Position;direction.y=0;direction=direction.sqrMagnitude>.01f?direction.normalized:Vector3.forward;
            var desired=left.Position-direction*Mathf.Clamp(6+distance*.35f,7,13)+Vector3.up*3.5f;
            // Mantiene la cámara dentro del claro para que los troncos nunca tapen la vista.
            var flat=Vector3.ClampMagnitude(new Vector3(desired.x,0,desired.z),19);desired=new Vector3(flat.x,desired.y,flat.z);
            arenaCamera.transform.position=Vector3.Lerp(arenaCamera.transform.position,desired,Time.deltaTime*3);
            arenaCamera.transform.LookAt(middle);
            // El temblor solo rota la vista, así la posición de seguimiento nunca se desplaza.
            if(shake>0&&screen==ScreenState.Fight){arenaCamera.transform.rotation*=Quaternion.Euler(Random.Range(-1f,1f)*shake*8,Random.Range(-1f,1f)*shake*8,0);shake=Mathf.Max(0,shake-Time.unscaledDeltaTime*.8f);}
            // Las chispas crecen y se encogen siguiendo una curva seno durante 0.18 s y luego se destruyen.
            for(int i=sparks.Count-1;i>=0;i--)
            {
                var spark=sparks[i];spark.Age+=Time.unscaledDeltaTime;
                if(spark.Age>=.18f){Destroy(spark.Shape.gameObject);sparks.RemoveAt(i);continue;}
                spark.Shape.localScale=Vector3.one*spark.Size*Mathf.Sin(spark.Age/.18f*Mathf.PI);sparks[i]=spark;
            }
            if(screen!=ScreenState.Result)hud.Refresh(left,right,timer,round,Announcement());
        }
        /// <summary>Mensaje central: número de ronda, "¿listos?", "¡lucha!", y luego K.O. o tiempo seguido del resultado.</summary>
        string Announcement()
        {
            if(roundOver)return roundWait>1.4f?(left.Health<=0||right.Health<=0?"K.O.":"¡TIEMPO!"):result;
            if(countdown>1.6f)return "RONDA "+round;
            if(countdown>0)return "¿LISTOS?";
            return countdown>-.8f?"¡LUCHA!":"";
        }
        void Clear() { if(left!=null)left.Dispose(); if(right!=null)right.Dispose(); }
        // Empieza una pelea nueva (al entrar a la escena o con Revancha).
        void Begin()
        {
            Preview(); round=1; left.Wins=right.Wins=0; ResetRound(); screen=ScreenState.Fight;
            // Cada pelea, incluidas las revanchas, elige una canción de combate nueva.
            ForgeAudio.PlayBattle();
            // El HUD muestra los nombres y colores de los luchadores, así que se reconstruye en cada pelea.
            if(hud!=null)hud.Dispose();hud=new ForgeHud(left.Profile,right.Profile,versus);
        }
        void ResetRound()
        {
            // Reinicia el estado temporal del combate, conservando las rondas ganadas de cada luchador.
            var leftStart=new Vector3(-3,0,0);var rightStart=new Vector3(3,0,0);
            left.Reset(leftStart,rightStart);right.Reset(rightStart,leftStart);
            timer=60;countdown=3;roundOver=draw=false;aiTime=.5f;hitStop=shake=slowMotion=0;Time.timeScale=1;
        }
        // Down: la tecla se pulsó en este frame. Held: la tecla se mantiene pulsada.
        bool Down(Key k) => Keyboard.current!=null && Keyboard.current[k].wasPressedThisFrame;
        bool Held(Key k) => Keyboard.current!=null && Keyboard.current[k].isPressed;
        void Update()
        {
            // ESC abre o cierra la pausa.
            if(Down(Key.Escape)) {if(screen==ScreenState.Fight){screen=ScreenState.Pause;ShowOverlay(false);}else if(screen==ScreenState.Pause)Resume();}
            // Congela la simulación y la animación sin detener la entrada de la interfaz.
            // El "hit stop" usa la misma congelación durante unos frames reales para que cada impacto se sienta.
            bool paused=screen==ScreenState.Pause || screen==ScreenState.Result;
            if(!paused&&hitStop>0)hitStop-=Time.unscaledDeltaTime;
            left.SetPaused(paused||hitStop>0);right.SetPaused(paused||hitStop>0);
            if(paused||hitStop>0)return;
            // Cámara lenta tras un K.O.: el tiempo del juego va al 35 %.
            if(slowMotion>0){slowMotion-=Time.unscaledDeltaTime;Time.timeScale=slowMotion>0?.35f:1;}
            float dt=Time.deltaTime;
            if(roundOver)
            {
                // Tras el K.O. los luchadores siguen animándose sin control; al acabar la espera
                // se pasa a la siguiente ronda o a la pantalla de victoria (al mejor de 3).
                left.Tick(dt,right.Position,Vector3.zero);right.Tick(dt,left.Position,Vector3.zero);roundWait-=dt;
                if(roundWait<=0) { if(left.Wins>=2 || right.Wins>=2){screen=ScreenState.Result;ShowOverlay(true);} else {if(!draw)round++;ResetRound();} } return;
            }
            // Durante la cuenta atrás nadie se puede mover.
            countdown-=dt;
            if(countdown>0) {left.Tick(dt,right.Position,Vector3.zero);right.Tick(dt,left.Position,Vector3.zero);return;}
            timer=Mathf.Max(0,timer-dt);
            // Teclas del jugador 1 (WASD, Espacio, J/K/L, Shift izq., U) y del jugador 2 o la CPU.
            var lm=Control(left,right,Key.A,Key.D,Key.W,Key.S,Key.Space,Key.J,Key.K,Key.L,Key.LeftShift,Key.U);
            var rm=versus?Control(right,left,Key.LeftArrow,Key.RightArrow,Key.UpArrow,Key.DownArrow,Key.Numpad0,Key.Numpad1,Key.Numpad2,Key.Numpad3,Key.RightShift,Key.Numpad4):Cpu(dt);
            // Separa a los luchadores a mano porque los colliders de Starter Assets están desactivados.
            var separation=right.Position-left.Position;separation.y=0;
            if(separation.magnitude<.85f){var push=(separation.sqrMagnitude<.001f?Vector3.right:separation.normalized)*(.85f-separation.magnitude)*.5f;left.Position-=push;right.Position+=push;}
            left.Tick(dt,right.Position,lm);right.Tick(dt,left.Position,rm);
            // Se comprueban los dos golpes antes de aplicar ninguno, así los golpes simultáneos se intercambian
            // en vez de que el golpe del luchador izquierdo cancele el ataque del derecho.
            var leftHit=Connects(left,right);var rightHit=Connects(right,left);
            if(leftHit.HasValue)Strike(left,right,leftHit.Value);
            if(rightHit.HasValue)Strike(right,left,rightHit.Value);
            if(left.Health<=0 || right.Health<=0 || timer<=0)
            {
                // Se comparan porcentajes de vida para que los personajes con más vida máxima
                // no ganen automáticamente cuando se acaba el tiempo.
                float lh=left.Health/left.Profile.Health,rh=right.Health/right.Profile.Health;
                draw=Mathf.Abs(lh-rh)<.001f;
                if(draw)result="EMPATE · SE REPITE";
                else {var winner=lh>rh?left:right;winner.Wins++;result=winner.Profile.Name+" GANA LA RONDA";}
                left.Blocking=right.Blocking=false;roundOver=true;roundWait=2.5f;
            }
        }
        /// <summary>Inteligencia de la CPU (luchador derecho). Devuelve hacia dónde quiere moverse.</summary>
        Vector3 Cpu(float dt)
        {
            // Decide ataques, bloqueos y esquivas cada cierto intervalo, no al azar en cada frame.
            aiTime-=dt; var toPlayer=left.Position-right.Position;toPlayer.y=0;float distance=toPlayer.magnitude;var wish=Vector3.zero;
            // Deja de bloquear cuando el jugador ya no ataca.
            if(left.Action==0||right.Action!=0||right.Stun>0)right.Blocking=false;
            // Se acerca hasta que el jab alcance; quedarse más lejos hacía que la mayoría de sus jabs fallaran.
            if(distance>1.25f)wish=toPlayer.normalized;
            if(aiTime<=0)
            {
                bool threatened=left.Action!=0&&right.Action==0&&right.Stun<=0;
                // A veces esquiva hacia un lado un ataque que viene, en vez de bloquearlo (25 %).
                if(threatened&&distance<2.4f&&Random.value<.25f&&right.Dash(Vector3.Cross(Vector3.up,toPlayer)*(Random.value<.5f?1:-1))){aiTime=Random.Range(.3f,.6f);return wish;}
                // Si no esquiva, bloquea el 65 % de las veces.
                right.Blocking=threatened&&Random.value<.65f;
                // Jabs de cerca, patadas y ganchos a media distancia, y a veces un uno-dos.
                if(!right.Blocking&&distance<2.1f){right.Attack(distance<1.4f&&Random.value<.6f?1:Random.Range(2,4));cpuCombo=right.Action==1&&Random.value<.4f;aiTime=Random.Range(.4f,1f);}
                else aiTime=Random.Range(.25f,.6f);
            }
            // Segundo golpe del uno-dos: se pide justo después del impacto del primero.
            if(cpuCombo&&right.Action==1&&right.ActionTime>right.Current.HitTime){right.Attack(1);cpuCombo=false;}
            return wish;
        }
        /// <summary>Lee las teclas de un jugador y devuelve la dirección deseada; el luchador aplica la aceleración.</summary>
        Vector3 Control(ForgeFighter f,ForgeFighter opponent,Key back,Key forward,Key up,Key down,Key jump,Key punch,Key kick,Key special,Key block,Key dash)
        {
            // Solo puede bloquear en el suelo y sin estar atacando, aturdido o en dash.
            f.Blocking=Held(block)&&f.Action==0&&f.Stun<=0&&f.Height<=0&&!f.Dashing;
            if(f.Stun>0 || f.Health<=0)return Vector3.zero;
            // Proyecta los ejes de la cámara sobre el suelo, así "arriba" siempre es "alejarse de la cámara",
            // y limita las diagonales para que no se mueva más rápido en ellas.
            var input=new Vector3((Held(forward)?1:0)-(Held(back)?1:0),0,(Held(up)?1:0)-(Held(down)?1:0));
            var cameraForward=arenaCamera.transform.forward;cameraForward.y=0;cameraForward.Normalize();
            var cameraRight=arenaCamera.transform.right;cameraRight.y=0;cameraRight.Normalize();
            var movement=Vector3.ClampMagnitude(cameraRight*input.x+cameraForward*input.z,1);
            // Dash en la dirección que se mantiene pulsada, o hacia atrás (lejos del rival) si no se pulsa ninguna.
            if(Down(dash)){var away=f.Position-opponent.Position;away.y=0;f.Dash(movement.sqrMagnitude>.01f?movement:away);}
            if(Down(punch))f.Attack(1);if(Down(kick))f.Attack(2);if(Down(special))f.Attack(3);
            // Salto: velocidad vertical inicial de 7 m/s.
            if(Down(jump)&&f.Height<=0 && !f.Blocking && f.Action==0 && !f.Dashing){f.VerticalSpeed=7;ForgeAudio.Play(ForgeAudio.Library?.jump,.8f);}
            return movement;
        }
        // Cierra la pausa y devuelve la música a su volumen normal.
        void Resume(){screen=ScreenState.Fight;overlay.Hide();ForgeAudio.Duck(false);}
        /// <summary>Muestra el panel de pausa (finished=false) o la pantalla de victoria (finished=true).</summary>
        void ShowOverlay(bool finished)
        {
            overlay.Clear();ForgeAudio.Duck(true);
            if(finished)
            {
                // Victoria: una franja del color del ganador con el marcador final debajo.
                hud.Hide();
                var winner=left.Wins>right.Wins?left:right;var color=winner.Profile.Color;
                overlay.Enter(overlay.Shape(-120,170,1520,220,ForgeTheme.Dim(color,.9f,.95f),0,ForgeTheme.Dim(color,.2f,.9f)),new Vector2(-400,0));
                overlay.Enter(overlay.Text(0,176,1280,50,"VICTORIA",26,new Color(1,1,1,.85f),TextAnchor.MiddleCenter,FontStyle.BoldAndItalic),new Vector2(0,-20));
                overlay.Enter(ForgeUI.Shadow(overlay.Text(0,214,1280,150,winner.Profile.Name,124,null,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic),new Color(0,0,0,.5f),7),Vector2.zero,1.8f);
                overlay.Enter(overlay.Text(0,398,1280,40,left.Profile.Name+"     "+left.Wins+"  –  "+right.Wins+"     "+right.Profile.Name,24,null,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic),new Vector2(0,20));
                overlay.Button(440,468,400,52,"REVANCHA",()=>{Begin();overlay.Hide();});
                overlay.Button(440,532,400,52,"CAMBIAR LUCHADORES",()=>{ForgeMatch.OpenSelect=true;ToMenu();});
                overlay.Button(440,596,400,52,"MENÚ PRINCIPAL",ToMenu);
            }
            else
            {
                // Pausa: panel central con título, datos de la pelea y tres botones.
                var panel=overlay.Enter(overlay.Shape(420,140,440,430,ForgeTheme.Dim(ForgeTheme.Deep,1.2f,.96f),0,ForgeTheme.Dim(ForgeTheme.Night,1,.96f),null,true),Vector2.zero,1.08f);
                overlay.Shape(0,0,440,5,ForgeTheme.Orange,0,default,panel.transform);
                ForgeUI.Shadow(overlay.Text(0,26,440,90,"PAUSA",72,null,TextAnchor.MiddleCenter,FontStyle.BoldAndItalic,panel.transform),ForgeTheme.Dim(ForgeTheme.Orange,.8f),5);
                overlay.Text(0,108,440,30,left.Profile.Name+"  VS  "+right.Profile.Name+"   ·   RONDA "+round,15,ForgeTheme.Muted,TextAnchor.MiddleCenter,FontStyle.Bold,panel.transform);
                overlay.Button(470,300,340,52,"CONTINUAR",Resume);
                overlay.Button(470,364,340,52,"REVANCHA",()=>{Begin();overlay.Hide();});
                overlay.Button(470,428,340,52,"MENÚ PRINCIPAL",ToMenu);
                overlay.Text(420,516,440,30,"ESC  continuar",14,ForgeTheme.Muted,TextAnchor.MiddleCenter,FontStyle.Normal);
            }
            // Selecciona el primer botón para poder navegar con el teclado.
            overlay.Focus();
        }
        void ToMenu(){Time.timeScale=1;SceneManager.LoadScene("ForgeMenu");}
        /// <summary>Comprueba si el ataque del atacante conecta en este frame. Devuelve el ataque o null si falla.</summary>
        AttackData? Connects(ForgeFighter attacker,ForgeFighter target)
        {
            // Cada ataque tiene un solo intento, en su frame de impacto. Si falla, no puede dañar más tarde
            // en el mismo movimiento; el alcance, la altura y la orientación tienen que coincidir.
            if(attacker.Action==0 || attacker.HitApplied || attacker.ActionTime<attacker.Current.HitTime)return null;
            attacker.HitApplied=true;var attack=attacker.Current;
            // Un golpe que llega durante la ventana invulnerable del dash lo atraviesa y falla.
            if(target.Invulnerable)return null;
            var direction=target.Position-attacker.Position;direction.y=0;
            // Falla si está lejos, si uno está saltando y el otro no, o si el atacante no mira al rival (Dot < 0.35).
            if(direction.magnitude>attack.Reach || Mathf.Abs(attacker.Height-target.Height)>.9f || Vector3.Dot(attacker.Root.forward,direction.normalized)<.35f)return null;
            return attack;
        }
        /// <summary>Aplica un golpe que conectó: daño, reacción, efectos visuales y sonido.</summary>
        void Strike(ForgeFighter attacker,ForgeFighter target,AttackData attack)
        {
            var direction=target.Position-attacker.Position;direction.y=0;direction=direction.sqrMagnitude>.0001f?direction.normalized:attacker.Root.forward;
            // La guardia solo funciona si el defensor mira hacia el golpe.
            bool blocked=target.Blocking&&Vector3.Dot(target.Root.forward,-direction)>.2f;
            // Daño = daño del ataque × poder del atacante; bloqueado solo recibe el 20 %.
            target.Health=Mathf.Max(0,target.Health-attack.Damage*attacker.Profile.Power*(blocked?.2f:1));
            target.TakeHit(attack,direction,blocked);
            // Los golpes más fuertes congelan más tiempo y sacuden más la cámara; un K.O. se ve en cámara lenta.
            hitStop=Mathf.Max(hitStop,blocked?.05f:.06f+attack.Damage*.004f);
            shake=Mathf.Max(shake,blocked?.08f:attack.Damage*.012f);
            AddSpark(target.Position+Vector3.up*1.35f-direction*.35f,blocked?blockMaterial:hitMaterial,blocked?.45f:.3f+attack.Damage*.02f);
            // Sonido según el resultado: bloqueo, golpe fuerte, patada o puño.
            var sounds=ForgeAudio.Library;
            if(sounds!=null)ForgeAudio.Play(blocked?sounds.block:attacker.Action==3?sounds.heavy:attacker.Action==2?sounds.kick:sounds.punch,blocked?.8f:1);
            // Un K.O. añade encima un golpe fuerte más grave y lento.
            if(target.Health<=0){slowMotion=.9f;ForgeAudio.Play(sounds?.heavy,1,.7f);}
        }
        // Crea la chispa de impacto: una esfera sin collider que LateUpdate anima y destruye.
        void AddSpark(Vector3 position,Material material,float size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Impact";Destroy(go.GetComponent<Collider>());
            go.transform.position=position;go.transform.localScale=Vector3.zero;go.GetComponent<Renderer>().sharedMaterial=material;
            sparks.Add(new Spark{Shape=go.transform,Size=size});
        }
    }
}
