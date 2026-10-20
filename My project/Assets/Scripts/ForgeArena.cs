using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PixelForge
{
    /// <summary>
    /// Runs a two-fighter match: player input, CPU decisions, rounds, damage, camera, and HUD.
    /// Character selection is supplied by ForgeMatch when the menu loads this scene.
    /// </summary>
    public sealed class ForgeArena : MonoBehaviour
    {
        public GameObject fighterPrefab, sceneryPrefab;
        public Material floorMaterial, skyMaterial;
        Camera arenaCamera;
        ForgeUI overlay;
        public GameObject rockPrefab;
        public static readonly FighterProfile[] Roster = {
            new FighterProfile("BLAZE", "Presión · puños de fuego", new Color(1,.28f,.18f),100,3.6f,1.1f),
            new FighterProfile("NYX", "Velocidad · sombra", new Color(.62f,.35f,1),90,4.7f,.95f),
            new FighterProfile("ATLAS", "Potencia · armadura pesada", new Color(1,.7f,.18f),125,2.7f,1.25f),
            new FighterProfile("KAI", "Equilibrio · artes marciales", new Color(.15f,.8f,.9f),105,3.8f,1),
            new FighterProfile("VERA", "Agilidad · patadas", new Color(.3f,1,.55f),95,4.3f,1.05f)
        };
        enum ScreenState { Fight, Pause, Result }
        ScreenState screen;
        ForgeFighter left,right;
        int pickLeft, pickRight=1, round=1;
        bool versus, roundOver;
        float timer=60, countdown=3, roundWait, aiTime, volume;
        string result="";
        GUIStyle title, heading, text, small, button;
        readonly Color ink = new Color(.035f,.045f,.085f), panel = new Color(.075f,.09f,.15f), accent = new Color(.25f,.95f,.85f);
        void Awake()
        {
            volume=PlayerPrefs.GetFloat("ForgeVolume",.6f); AudioListener.volume=volume;
            Application.targetFrameRate=60;
            var cameraObject=new GameObject("Arena Camera"); var cam=cameraObject.AddComponent<Camera>();arenaCamera=cam;
            cam.transform.position=new Vector3(0,8,-12); cam.transform.LookAt(new Vector3(0,1,0));
            cam.orthographic=false;cam.fieldOfView=48;cam.clearFlags=CameraClearFlags.Skybox;cam.backgroundColor=ink;
            cameraObject.AddComponent<AudioListener>();
            if(skyMaterial!=null)RenderSettings.skybox=skyMaterial;
            RenderSettings.ambientLight=new Color(.65f,.7f,.8f);
            var sun=new GameObject("Arena Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
            // Use the imported terrain when configured; keep a lightweight fallback for new clones.
            if(!ForgeTerrain.Build())ForgeLandscape.Build(rockPrefab);
            pickLeft=ForgeMatch.Left;pickRight=ForgeMatch.Right;versus=ForgeMatch.Versus;
            overlay=new ForgeUI();overlay.Hide();Begin();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void Preview() { Clear(); left=new ForgeFighter(Roster[pickLeft],-3,fighterPrefab); right=new ForgeFighter(Roster[pickRight],3,fighterPrefab); }
        void LateUpdate()
        {
            // Animator evaluates between Update and LateUpdate. Apply combat poses afterward
            // so the imported locomotion clips cannot overwrite the final attack pose.
            if(left==null||right==null)return;left.Pose();right.Pose();
            var middle=(left.Position+right.Position)*.5f;middle.y=1;
            float distance=Vector3.Distance(left.Position,right.Position);
            var direction=right.Position-left.Position;direction.y=0;direction=direction.sqrMagnitude>.01f?direction.normalized:Vector3.forward;
            var desired=left.Position-direction*Mathf.Clamp(6+distance*.35f,7,13)+Vector3.up*3.5f;
            arenaCamera.transform.position=Vector3.Lerp(arenaCamera.transform.position,desired,Time.deltaTime*3);
            arenaCamera.transform.LookAt(middle);
        }
        void Clear() { if(left!=null)left.Dispose(); if(right!=null)right.Dispose(); }
        void Begin() { Preview(); round=1; left.Wins=right.Wins=0; ResetRound(); screen=ScreenState.Fight; }
        void ResetRound()
        {
            // Reset temporary combat state while preserving each fighter's match win count.
            left.Health=left.Profile.Health; right.Health=right.Profile.Health;
            left.Height=right.Height=left.VerticalSpeed=right.VerticalSpeed=0;
            left.Action=right.Action=0; left.Stun=right.Stun=0; left.Blocking=right.Blocking=false;
            left.Position=new Vector3(-3,0,0);right.Position=new Vector3(3,0,0);timer=60;countdown=3;roundOver=false; aiTime=.5f;
        }
        bool Down(Key k) => Keyboard.current!=null && Keyboard.current[k].wasPressedThisFrame;
        bool Held(Key k) => Keyboard.current!=null && Keyboard.current[k].isPressed;
        void Update()
        {
            if(Down(Key.Escape)) {if(screen==ScreenState.Fight){screen=ScreenState.Pause;ShowOverlay(false);}else if(screen==ScreenState.Pause){screen=ScreenState.Fight;overlay.Hide();}}
            // Freeze simulation and animation without stopping the menu's UI input processing.
            bool paused=screen==ScreenState.Pause || screen==ScreenState.Result;
            left.SetPaused(paused);right.SetPaused(paused);
            if(paused)return;
            float dt=Time.deltaTime;
            if(screen!=ScreenState.Fight) {left.Tick(dt,right.Position,0);right.Tick(dt,left.Position,0);return;}
            if(roundOver)
            {
                left.Tick(dt,right.Position,0);right.Tick(dt,left.Position,0);roundWait-=dt;
                if(roundWait<=0) { if(left.Wins>=2 || right.Wins>=2){screen=ScreenState.Result;ShowOverlay(true);} else {round++;ResetRound();} } return;
            }
            countdown-=dt;
            if(countdown>0) {left.Tick(dt,right.Position,0);right.Tick(dt,left.Position,0);return;}
            timer=Mathf.Max(0,timer-dt);
            float lm=Control(left,Key.A,Key.D,Key.W,Key.S,Key.Space,Key.J,Key.K,Key.L,Key.LeftShift,dt);
            float rm=0;
            if(versus) rm=Control(right,Key.LeftArrow,Key.RightArrow,Key.UpArrow,Key.DownArrow,Key.Numpad0,Key.Numpad1,Key.Numpad2,Key.Numpad3,Key.RightShift,dt);
            else
            {
                // Choose attacks and blocking at intervals rather than randomly every frame.
                aiTime-=dt; float distance=Vector3.Distance(left.Position,right.Position);
                if(left.Action==0||right.Action!=0||right.Stun>0)right.Blocking=false;
                if(right.Action==0 && right.Stun<=0 && !right.Blocking && distance>1.5f){rm=1;var direction=left.Position-right.Position;direction.y=0;right.Position+=direction.normalized*right.Profile.Speed*dt;}
                if(aiTime<=0) {right.Blocking=left.Action!=0&&right.Action==0&&right.Stun<=0&&Random.value<.65f; if(distance<2.1f)right.Attack(Random.Range(1,4)); aiTime=Random.Range(.4f,1f); }
            }
            // Resolve horizontal overlap explicitly because Starter Assets colliders are disabled.
            var separation=right.Position-left.Position;separation.y=0;
            if(separation.magnitude<.85f){var push=(separation.sqrMagnitude<.001f?Vector3.right:separation.normalized)*(.85f-separation.magnitude)*.5f;left.Position-=push;right.Position+=push;}
            left.Tick(dt,right.Position,lm);right.Tick(dt,left.Position,rm);
            Hit(left,right);Hit(right,left);
            if(left.Health<=0 || right.Health<=0 || timer<=0)
            {
                // Compare remaining health percentages so high-health characters do not
                // automatically win a timeout simply because their maximum health is larger.
                float lh=left.Health/left.Profile.Health,rh=right.Health/right.Profile.Health;
                if(Mathf.Abs(lh-rh)<.001f)result="EMPATE · SE REPITE LA RONDA";
                else {var winner=lh>rh?left:right;winner.Wins++;result=winner.Profile.Name+" GANA LA RONDA";}
                roundOver=true;roundWait=2.5f;
            }
        }
        float Control(ForgeFighter f,Key back,Key forward,Key up,Key down,Key jump,Key punch,Key kick,Key special,Key block,float dt)
        {
            f.Blocking=Held(block)&&f.Action==0&&f.Stun<=0&&f.Height<=0;
            if(f.Stun>0 || f.Health<=0)return 0;
            if(Down(punch))f.Attack(1);if(Down(kick))f.Attack(2);if(Down(special))f.Attack(3);
            if(Down(jump)&&f.Height<=0 && !f.Blocking)f.VerticalSpeed=7;
            var movement=new Vector3((Held(forward)?1:0)-(Held(back)?1:0),0,(Held(up)?1:0)-(Held(down)?1:0));
            if(f.Action!=0 || f.Blocking)movement=Vector3.zero;
            // Project the camera axes onto the floor, then clamp diagonals to prevent faster movement.
            var cameraForward=arenaCamera.transform.forward;cameraForward.y=0;cameraForward.Normalize();
            var cameraRight=arenaCamera.transform.right;cameraRight.y=0;cameraRight.Normalize();
            movement=Vector3.ClampMagnitude(cameraRight*movement.x+cameraForward*movement.z,1);f.Position+=movement*f.Profile.Speed*dt;return movement.magnitude;
        }
        void ShowOverlay(bool finished)
        {
            overlay.Clear(finished?"VICTORIA · "+(left.Wins>right.Wins?left.Profile.Name:right.Profile.Name):"PAUSA","ARENA DEL BOSQUE");
            if(!finished)overlay.Button(80,270,500,"CONTINUAR",()=>{screen=ScreenState.Fight;overlay.Hide();});
            overlay.Button(80,350,500,"REVANCHA",()=>{Begin();overlay.Hide();});
            overlay.Button(80,430,500,"VOLVER AL MENÚ / PERSONAJES",()=>SceneManager.LoadScene("ForgeMenu"));overlay.Focus();
        }
        void Hit(ForgeFighter attacker,ForgeFighter target)
        {
            // Each attack gets one hit attempt after its wind-up. A miss cannot damage
            // a target later in the same swing; range, height, and facing must all match.
            if(attacker.Action==0 || attacker.HitApplied || attacker.ActionTime<.2f)return;
            attacker.HitApplied=true;
            float reach=attacker.Action==1?1.45f:2.05f;
            var direction=target.Position-attacker.Position;direction.y=0;
            if(direction.magnitude>reach || Mathf.Abs(attacker.Height-target.Height)>.9f || Vector3.Dot(attacker.Root.forward,direction.normalized)<.35f)return;
            float damage=(attacker.Action==1?9:attacker.Action==2?13:18)*attacker.Profile.Power;
            target.Health=Mathf.Max(0,target.Health-damage*(target.Blocking?.2f:1));
            if(!target.Blocking) {target.Stun=.25f;target.Action=0;}
        }
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=72,fontStyle=FontStyle.Bold};title.normal.textColor=Color.white;
            heading=new GUIStyle(title){fontSize=30};
            text=new GUIStyle(GUI.skin.label){fontSize=20,wordWrap=true};text.normal.textColor=Color.white;
            small=new GUIStyle(text){fontSize=15};small.normal.textColor=new Color(.65f,.72f,.8f);
            button=new GUIStyle(GUI.skin.button){fontSize=20,fontStyle=FontStyle.Bold};
        }
        void Box(Rect rect,Color color) {var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        bool Button(float x,float y,float w,string label) => GUI.Button(new Rect(x,y,w,48),label,button);
        void Label(float x,float y,float w,string label,GUIStyle style) => GUI.Label(new Rect(x,y,w,300),label,style);
        void OnGUI()
        {
            if(screen!=ScreenState.Fight)return;
            Styles();
            // Fit the HUD uniformly inside the viewport instead of stretching each axis independently.
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            var offset=new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0);
            GUI.matrix=Matrix4x4.TRS(offset,Quaternion.identity,new Vector3(scale,scale,1));
            Hud();
        }
        void Hud()
        {
            Box(new Rect(0,0,1280,135),panel);
            Label(55,20,470,left.Profile.Name+"   /   J1   ·   "+left.Wins+" rondas",text);
            Label(790,20,470,right.Profile.Name+"   /   "+(versus?"J2":"CPU")+"   ·   "+right.Wins+" rondas",text);
            Box(new Rect(55,65,460,22),ink);Box(new Rect(55,65,460*left.Health/left.Profile.Health,22),left.Profile.Color);
            Box(new Rect(765,65,460,22),ink);Box(new Rect(765+460*(1-right.Health/right.Profile.Health),65,460*right.Health/right.Profile.Health,22),right.Profile.Color);
            Label(594,20,120,Mathf.CeilToInt(timer).ToString("00"),heading);Label(572,77,170,"RONDA "+round,small);
            Label(55,675,1150,"WASD mover en 3D · Espacio salto · Shift bloqueo · J/K/L ataques     |     ESC pausa",small);
            if(roundOver)Label(370,230,900,result,heading);
            else if(countdown>0)Label(600,230,200,Mathf.CeilToInt(countdown).ToString(),title);
            else if(countdown>-.7f)Label(510,230,500,"¡LUCHA!",title);
            Label(1100,100,130,"ESC · PAUSA",small);
        }
    }
}

