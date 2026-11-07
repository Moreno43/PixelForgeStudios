using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelForge
{
    /// <summary>
    /// Valores de tiempo e impacto de un ataque. Los tiempos son segundos desde que se pulsa el botón.
    /// </summary>
    public readonly struct AttackData
    {
        // Duration: cuánto dura el ataque completo (preparación + golpe + recuperación).
        // HitTime: momento exacto en que el golpe puede conectar (el "frame de impacto").
        // Reach: alcance en metros. Damage: daño base. Lunge: metros que avanza el cuerpo al golpear.
        // Knockback: fuerza con la que empuja al rival. Stun: segundos que el rival queda aturdido.
        public readonly float Duration, HitTime, Reach, Damage, Lunge, Knockback, Stun;
        public AttackData(float duration,float hitTime,float reach,float damage,float lunge,float knockback,float stun)
        { Duration=duration; HitTime=hitTime; Reach=reach; Damage=damage; Lunge=lunge; Knockback=knockback; Stun=stun; }
    }

    /// <summary>
    /// Representa a un luchador en la arena y guarda su estado de combate (vida, ataque actual, aturdimiento, dash...).
    /// No es un MonoBehaviour: la arena (ForgeArena) lo crea y llama a Tick() y Pose() cada frame.
    /// Las animaciones importadas (Mixamo) mueven el cuerpo; encima se suma una capa procedimental
    /// que rota huesos por código para la guardia, los ataques, el dash, los golpes recibidos y el K.O.
    /// </summary>
    public sealed class ForgeFighter
    {
        // Tabla de ataques. Índice 0 = ninguno, 1 = puño (jab), 2 = patada, 3 = golpe fuerte (gancho).
        // El frame de impacto está al final de la preparación, justo cuando la extremidad está estirada,
        // para que el daño coincida con lo que se ve en pantalla.
        public static readonly AttackData[] Attacks = {
            default,
            new AttackData(.45f,.18f,1.45f,8,.25f,2.2f,.28f),
            new AttackData(.56f,.24f,2.0f,12,.35f,3.4f,.38f),
            new AttackData(.80f,.40f,1.9f,18,.7f,6.5f,.6f)
        };
        // El dash recorre su distancia en su duración; durante la ventana de invulnerabilidad los golpes
        // lo atraviesan, y el tiempo de recarga (cooldown) impide encadenar dashes sin parar.
        // Si existe la animación importada de esquiva, se convierte en una voltereta más larga y baja.
        const float DashDuration=.3f, DashDistance=3.2f, DashInvulnerable=.22f, DashCooldown=.6f, GhostLife=.3f;
        const float RollDuration=.62f, RollDistance=3f, RollInvulnerable=.42f, RollCooldown=.85f;

        // Una "imagen fantasma" (afterimage) que queda detrás del luchador durante el dash.
        sealed class Ghost { public GameObject Shape; public Mesh[] Meshes; public MeshRenderer[] Renderers; public float Age; }

        // Objeto raíz del personaje en la escena.
        public readonly Transform Root;
        // Articulaciones del muñeco de cubos de respaldo (solo se usan si no hay prefab).
        readonly Transform armL, armR, legL, legR, body;
        readonly Animator animator;
        // Huesos del esqueleto humanoide que la capa procedimental rota.
        readonly Transform upperArmL, upperArmR, lowerArmL, lowerArmR, upperLegR, lowerLegR, hips, spine, chest, head;
        readonly Transform leftHand, rightHand, rightFoot;
        // Lista de huesos que se modifican y su rotación original de este frame, para poder restaurarla.
        readonly Transform[] bones;
        readonly Quaternion[] basePose;
        // Copias de los materiales del personaje, para teñirlas con su color sin afectar al prefab.
        readonly Material[] tinted;
        readonly SkinnedMeshRenderer[] skins;
        readonly List<Ghost> ghosts = new List<Ghost>();
        // Permite cambiar el color/transparencia de cada fantasma sin crear un material por fantasma.
        readonly MaterialPropertyBlock ghostBlock = new MaterialPropertyBlock();
        Material ghostMaterial;
        // posed: ya se aplicó la pose procedimental este frame. flashing: está parpadeando en blanco por un golpe.
        // ghostPending: hay que crear un fantasma en el siguiente Pose().
        bool posed, flashing, ghostPending;
        // guardWeight: cuánto están levantados los puños (0-1). queueTime: tiempo que sigue válido un ataque en cola.
        // lastPunch: momento del último jab (para alternar manos). koTime: tiempo desde el K.O.
        // legYaw: giro de cadera al moverse de lado. moveSign: 1 adelante, -1 caminando hacia atrás.
        // dashTime: tiempo dentro del dash (-1 = no está haciendo dash). stride: distancia acumulada para los pasos.
        float guardWeight, queueTime, lastPunch=-10, koTime, legYaw, moveSign=1, dashTime=-1, dashCooldown, ghostTimer, stride;
        // Valores del dash actual (cambian según sea dash simple o voltereta).
        float dashDuration=DashDuration, dashDistance=DashDistance, dashInvulnerable=DashInvulnerable;
        // queued: ataque guardado en el búfer. punchSide: mano del próximo jab (1 o -1). hurtSide: lado hacia el que se dobla al recibir.
        int queued, punchSide=1, hurtSide=1;
        // Orientación al caer en K.O. y orientación durante la voltereta.
        Quaternion koFacing, rollFacing;
        // Las animaciones de combate importadas se reproducen en su propia capa del Animator,
        // cuyo peso (cuánto se nota) se controla desde el código.
        readonly int combatLayer=-1;
        // Indican qué animaciones importadas existen; si falta alguna se usa la versión procedimental.
        readonly bool hasJab, hasBlock, hasDodge, hasHit, hasStance, hasRun;
        // Peso actual de la capa de combate (0 = solo procedimental, 1 = solo animación importada).
        float clipWeight;
        // Estado de la capa de combate que se está reproduciendo ahora.
        string clipState;
        // clipSerial sube cada vez que empieza una acción nueva; si difiere de playedSerial, se reinicia la animación.
        int clipSerial, playedSerial, rollSide=1;
        Vector3 hipsBase, dashDirection, localMotion;

        // Posición en el suelo. Se limita a un círculo de 9 m para que nadie salga de la arena,
        // y la altura siempre es la del salto (Height).
        public Vector3 Position { get => Root.position; set {var horizontal=Vector3.ClampMagnitude(new Vector3(value.x,0,value.z),9);Root.position=new Vector3(horizontal.x,Height,horizontal.z);} }
        // Datos del personaje elegido (nombre, color, vida máxima, velocidad, poder).
        public readonly FighterProfile Profile;
        // Health: vida actual. Height/VerticalSpeed: salto. ActionTime: tiempo dentro del ataque actual.
        // Stun/StunTotal: aturdimiento restante y total. Flash: destello blanco al recibir golpe. BlockShake: temblor al bloquear.
        public float Health, Height, VerticalSpeed, ActionTime, Stun, StunTotal, Flash, BlockShake;
        // Knockback: empuje recibido. Velocity: velocidad de caminar/correr.
        public Vector3 Knockback, Velocity;
        // Action: ataque en curso (0 = ninguno). Wins: rondas ganadas.
        public int Action, Wins;
        // Blocking: está bloqueando. HitApplied: el ataque actual ya intentó golpear (solo se intenta una vez).
        public bool Blocking, HitApplied;
        // Datos del ataque que se está ejecutando.
        public AttackData Current => Attacks[Action];
        public bool Dashing => dashTime >= 0;
        // Solo es invulnerable durante la primera parte del dash.
        public bool Invulnerable => dashTime >= 0 && dashTime < dashInvulnerable;
        public float X { get => Root.position.x; set {var p=Position;p.x=value;Position=p;} }

        /// <summary>Crea el luchador en la posición x. Si hay prefab usa el personaje 3D; si no, arma un muñeco de cubos.</summary>
        public ForgeFighter(FighterProfile profile, float x, GameObject prefab)
        {
            Profile = profile; Health = profile.Health;
            if(prefab!=null)
            {
                var go=Object.Instantiate(prefab,new Vector3(x,0,0),Quaternion.identity);go.name=profile.Name;Root=go.transform;
                // La arena controla el movimiento y la entrada; se desactivan los scripts del prefab
                // (ThirdPersonController, etc.) para que no compitan con ella.
                foreach(var component in go.GetComponentsInChildren<MonoBehaviour>())component.enabled=false;
                foreach(var collider in go.GetComponentsInChildren<Collider>())collider.enabled=false;
                // applyRootMotion=false: las animaciones no mueven al personaje, lo mueve el código.
                // AlwaysAnimate: sigue animando aunque la cámara no lo vea.
                animator=go.GetComponentInChildren<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                // Los eventos de pasos de las animaciones pertenecen a Starter Assets, cuyo script está desactivado.
                animator.fireEvents=false;
                // El controlador generado en el editor añade una capa de combate al mismo grafo de locomoción.
                var combat=Resources.Load<RuntimeAnimatorController>(ForgeCombatClips.Controller);
                if(combat!=null)
                {
                    animator.runtimeAnimatorController=combat;combatLayer=animator.GetLayerIndex(ForgeCombatClips.Layer);
                    // Comprueba si el controlador trae la pose de pelea y la carrera importadas.
                    hasStance=System.Array.Exists(animator.parameters,parameter=>parameter.name==ForgeCombatClips.StanceParameter);
                    hasRun=System.Array.Exists(animator.parameters,parameter=>parameter.name==ForgeCombatClips.RunParameter);
                    if(combatLayer>=0)
                    {
                        animator.SetLayerWeight(combatLayer,0);
                        hasJab=HasClip(ForgeCombatClips.Jab);hasBlock=HasClip(ForgeCombatClips.Block);
                        hasDodge=HasClip(ForgeCombatClips.Dodge);hasHit=HasClip(ForgeCombatClips.Hit);
                    }
                }
                // Copia los materiales (renderer.materials crea copias) y los tiñe con el color del personaje.
                skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();
                var materials=new List<Material>();
                foreach(var renderer in skins)materials.AddRange(renderer.materials);
                tinted=materials.ToArray();Tint(profile.Color);
                // Busca los huesos por su nombre humanoide estándar, así funciona con cualquier personaje Humanoid.
                upperArmL=Bone(HumanBodyBones.LeftUpperArm);upperArmR=Bone(HumanBodyBones.RightUpperArm);
                lowerArmL=Bone(HumanBodyBones.LeftLowerArm);lowerArmR=Bone(HumanBodyBones.RightLowerArm);
                upperLegR=Bone(HumanBodyBones.RightUpperLeg);lowerLegR=Bone(HumanBodyBones.RightLowerLeg);
                hips=Bone(HumanBodyBones.Hips);spine=Bone(HumanBodyBones.Spine);chest=Bone(HumanBodyBones.Chest);head=Bone(HumanBodyBones.Head);
                leftHand=Bone(HumanBodyBones.LeftHand);rightHand=Bone(HumanBodyBones.RightHand);rightFoot=Bone(HumanBodyBones.RightFoot);
                // Todos los huesos que rota la capa procedimental se restauran antes de que el Animator evalúe el siguiente frame.
                bones=new[]{upperArmL,upperArmR,lowerArmL,lowerArmR,upperLegR,lowerLegR,hips,spine,chest,head};
                basePose=new Quaternion[bones.Length];return;
            }
            // Respaldo sin prefab: un robot hecho de cubos con brazos y piernas articulados.
            Root = new GameObject(profile.Name).transform;
            Root.position = new Vector3(x, 0, 0);
            body = Joint("Body", new Vector3(0, 1.35f, 0));
            Part("Torso", body, Vector3.zero, new Vector3(.65f, .8f, .4f), profile.Color);
            Part("Helmet", body, new Vector3(0, .69f, 0), new Vector3(.48f, .46f, .45f), profile.Color);
            Part("Visor", body, new Vector3(0, .72f, -.24f), new Vector3(.38f, .1f, .04f), Color.white);
            Part("Core", body, new Vector3(0, .1f, -.22f), new Vector3(.21f, .27f, .04f), Color.white);
            armL = Joint("Left Arm", new Vector3(-.43f, 1.62f, 0));
            armR = Joint("Right Arm", new Vector3(.43f, 1.62f, 0));
            legL = Joint("Left Leg", new Vector3(-.21f, .98f, 0));
            legR = Joint("Right Leg", new Vector3(.21f, .98f, 0));
            Limb(armL, .65f, profile.Color); Limb(armR, .65f, profile.Color);
            Limb(legL, .85f, new Color(.15f,.18f,.25f)); Limb(legR, .85f, new Color(.15f,.18f,.25f));
        }
        Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);
        // Comprueba si la capa de combate tiene un estado con el nombre de esa animación.
        bool HasClip(CombatClip clip) => animator.HasState(combatLayer,Animator.StringToHash(clip.State));
        // Crea un punto de articulación vacío dentro del muñeco de cubos.
        Transform Joint(string name, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(Root, false); t.localPosition = position; return t; }
        // Una extremidad del muñeco: un cubo largo más un cubo en la punta (mano o pie).
        void Limb(Transform joint, float length, Color color)
        {
            Part("Armor", joint, new Vector3(0,-length*.5f,0), new Vector3(.24f,length,.28f), color);
            Part("End Segment", joint, new Vector3(0,-length,0), new Vector3(.3f,.22f,.35f), Profile.Color);
        }
        /// <summary>Crea un cubo de color sin colisión como pieza del muñeco.</summary>
        public static Transform Part(string name, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localScale = size;
            // Elige un shader Unlit que exista en el pipeline activo (integrado, HDRP o URP).
            var pipeline = GraphicsSettings.currentRenderPipeline;
            string shaderName = pipeline == null ? "Unlit/Color" : pipeline.GetType().Name.Contains("HD") ? "HDRP/Unlit" : "Universal Render Pipeline/Unlit";
            var shader = Shader.Find(shaderName) ?? Shader.Find("Unlit/Color");
            // Cada pipeline llama distinto a la propiedad de color, así que se asignan todas las que existan.
            var mat = new Material(shader); mat.color = color;
            if(mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",color);
            if(mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor",color);
            go.GetComponent<Renderer>().material = mat; return go.transform;
        }
        // Cambia el color de todos los materiales del personaje (URP usa _BaseColor).
        void Tint(Color color)
        {
            if(tinted==null)return;
            foreach(var mat in tinted){if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);else mat.color=color;}
        }
        /// <summary>Borra todo el estado de la ronda y coloca al luchador de pie, mirando hacia el punto indicado.</summary>
        public void Reset(Vector3 position, Vector3 lookAt)
        {
            Health=Profile.Health;Height=VerticalSpeed=Stun=StunTotal=Flash=BlockShake=ActionTime=koTime=dashCooldown=legYaw=0;
            Action=queued=0;Blocking=HitApplied=false;Knockback=Velocity=localMotion=Vector3.zero;guardWeight=moveSign=1;dashTime=-1;
            Position=position;var direction=lookAt-position;direction.y=0;
            Root.rotation=direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):Quaternion.identity;
            Tint(Profile.Color);flashing=false;ClearGhosts();
            clipWeight=0;clipState=null;if(combatLayer>=0)animator.SetLayerWeight(combatLayer,0);
        }
        /// <summary>Pide un ataque (1 puño, 2 patada, 3 fuerte). Si no puede empezar ya, lo guarda en el búfer.</summary>
        public void Attack(int action)
        {
            if(Stun > 0 || Blocking || Health <= 0) return;
            // Se guarda la pulsación hecha durante la recuperación o un dash, para encadenar golpes
            // sin necesidad de pulsar en el frame exacto.
            if(Dashing) { queued = action; queueTime = DashDuration; return; }
            if(Action != 0) { if(ActionTime > Current.HitTime) { queued = action; queueTime = .3f; } return; }
            StartAction(action);
        }
        void StartAction(int action)
        {
            // Los jabs seguidos (menos de 0.6 s entre ellos) alternan de mano, como un uno-dos natural.
            if(action == 1) { punchSide = Time.time-lastPunch < .6f ? -punchSide : 1; lastPunch = Time.time; }
            Action = action; ActionTime = 0; HitApplied = false; queued = 0; clipSerial++;
        }
        /// <summary>Inicia un dash rápido en una dirección horizontal. Devuelve false si ahora no está permitido.</summary>
        public bool Dash(Vector3 direction)
        {
            direction.y = 0;
            if(Dashing || dashCooldown > 0 || Health <= 0 || Stun > 0 || Height > 0 || direction.sqrMagnitude < .01f) return false;
            // El dash puede cancelar la recuperación de un ataque, pero nunca su preparación.
            if(Action != 0) { if(ActionTime <= Current.HitTime) return false; Action = 0; }
            dashDirection = direction.normalized; dashTime = 0; ghostTimer = 0;
            Blocking = false; queued = 0; Knockback = Vector3.zero; clipSerial++;
            if(hasDodge)
            {
                // La animación de voltereta se desplaza hacia un lado, así que el cuerpo gira hasta que ese lado
                // apunte en la dirección del dash, eligiendo la versión espejada si requiere girar menos.
                rollSide = Root.InverseTransformDirection(dashDirection).x >= 0 ? 1 : -1;
                rollFacing = Quaternion.LookRotation(Quaternion.Euler(0,-90*rollSide,0)*dashDirection);
                dashDuration = RollDuration; dashDistance = RollDistance; dashInvulnerable = RollInvulnerable; dashCooldown = RollCooldown;
            }
            else { dashDuration = DashDuration; dashDistance = DashDistance; dashInvulnerable = DashInvulnerable; dashCooldown = DashCooldown; }
            ForgeAudio.Play(ForgeAudio.Library?.dash, .85f);
            return true;
        }
        /// <summary>Aplica la reacción a un golpe recibido. El daño lo calcula la arena.</summary>
        public void TakeHit(AttackData attack, Vector3 direction, bool blocked)
        {
            // Bloqueado: solo un empuje pequeño y un temblor, sin aturdimiento.
            if(blocked) { Knockback = direction*attack.Knockback*.45f; BlockShake = 1; return; }
            // Golpe limpio: aturde, empuja, destella en blanco y cancela el ataque que estuviera haciendo.
            Stun = StunTotal = attack.Stun; Knockback = direction*attack.Knockback; Flash = 1;
            Action = 0; queued = 0; hurtSide = Random.value < .5f ? -1 : 1; clipSerial++;
        }
        /// <summary>
        /// Elige la animación de combate según el estado actual y mezcla la capa de combate hacia ella.
        /// Un ataque, golpe o dash nuevo reinicia su animación aunque el nombre del estado no cambie.
        /// </summary>
        void UpdateClips(float dt)
        {
            if(combatLayer<0)return;
            // state: estado a reproducir (null = ninguno). start: punto de inicio normalizado. speed: velocidad de reproducción.
            string state=null;float start=0,speed=1;
            if(Health<=0){}
            else if(Dashing&&hasDodge)
            {
                // Voltereta: se estira o acelera para que dure exactamente lo mismo que el dash.
                var clip=ForgeCombatClips.Dodge;bool mirror=rollSide!=ForgeCombatClips.DodgeRollSide;
                state=mirror?clip.MirrorState:clip.State;start=clip.Start;speed=(clip.End-clip.Start)/dashDuration;
            }
            else if(Stun>0&&hasHit){var clip=ForgeCombatClips.Hit;state=clip.State;start=clip.Start;speed=2;}
            else if(Action==1&&hasJab)
            {
                // Se ajusta la velocidad para que el brazo quede estirado justo en el frame de impacto del ataque.
                var clip=ForgeCombatClips.Jab;state=punchSide>0?clip.MirrorState:clip.State;start=clip.Start;speed=(clip.Key-clip.Start)/Current.HitTime;
            }
            // Bloqueo: la animación se congela (speed=0) en la pose de guardia.
            else if(Blocking&&hasBlock){var clip=ForgeCombatClips.Block;state=clip.State;start=clip.Key;speed=0;}
            if(state!=null&&(state!=clipState||(playedSerial!=clipSerial&&speed>0)))
            {
                animator.SetFloat(ForgeCombatClips.SpeedParameter,speed);
                // Transición suave de 0.08 s si ya había otra animación de combate; si no, empieza al instante.
                animator.CrossFadeInFixedTime(state,clipWeight>.01f?.08f:0,combatLayer,start);
                clipState=state;playedSerial=clipSerial;
            }
            // Entra rápido para que el golpe se lea al instante; sale más lento para volver a la guardia con suavidad.
            clipWeight=Mathf.MoveTowards(clipWeight,state!=null?1:0,dt*(state!=null?14:6));
            if(state==null&&clipWeight<=0)clipState=null;
            animator.SetLayerWeight(combatLayer,clipWeight);
        }
        /// <summary>
        /// Actualiza la lógica del luchador un frame: ataques, movimiento, dash, salto, empuje y parámetros del Animator.
        /// target es la posición del rival (para mirarlo) y wish la dirección en la que quiere moverse (0 a 1).
        /// </summary>
        public void Tick(float dt, Vector3 target, Vector3 wish)
        {
            // Quita los giros procedimentales del frame anterior antes de que el Animator evalúe la pose base.
            // Así las rotaciones no se acumulan, incluso al pausar y reanudar.
            if(posed){for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=basePose[i];if(hips!=null)hips.localPosition=hipsBase;posed=false;}
            // Los temporizadores bajan con el tiempo.
            Stun = Mathf.Max(0, Stun-dt); Flash = Mathf.Max(0, Flash-dt*7); BlockShake = Mathf.Max(0, BlockShake-dt*5);
            dashCooldown = Mathf.Max(0, dashCooldown-dt); queueTime -= dt;
            if(Action != 0)
            {
                var attack = Current; float previous = ActionTime; ActionTime += dt;
                // Avanza un paso al final de la preparación, para que el golpe lleve el peso del cuerpo.
                // Solo se suma el tramo recorrido este frame entre el 55 % de la preparación y el impacto.
                float lungeStart = attack.HitTime*.55f;
                float travel = Mathf.Clamp(ActionTime,lungeStart,attack.HitTime)-Mathf.Clamp(previous,lungeStart,attack.HitTime);
                if(travel > 0) Position += Root.forward*attack.Lunge*travel/(attack.HitTime-lungeStart);
                if(ActionTime >= attack.Duration)
                {
                    // Terminó el ataque: si había otro en el búfer y sigue siendo válido, empieza ya.
                    Action = 0; int next = queued; queued = 0;
                    if(next != 0 && queueTime > 0 && Stun <= 0 && Health > 0 && !Blocking) StartAction(next);
                }
            }
            // Acelera rápido y frena un poco más rápido, así los pasos arrancan y paran con peso en vez de en seco.
            // "free" = puede moverse libremente (no ataca, no bloquea, no está aturdido ni en dash).
            bool free = Action == 0 && !Blocking && Stun <= 0 && Health > 0 && !Dashing;
            var wishVelocity = free ? Vector3.ClampMagnitude(new Vector3(wish.x,0,wish.z),1)*Profile.Speed : Vector3.zero;
            Velocity = Vector3.MoveTowards(Velocity, wishVelocity, (wishVelocity.sqrMagnitude > Velocity.sqrMagnitude ? 22 : 30)*dt);
            var motion = Velocity;
            if(Dashing)
            {
                // Arranca muy rápido y frena hasta parar; la curva (1-x²) recorre exactamente dashDistance.
                float progress = dashTime/dashDuration;
                motion = dashDirection*(1.5f*dashDistance/dashDuration)*(1-progress*progress);
                Position += motion*dt; dashTime += dt;
                // Cada cierto tiempo deja una imagen fantasma.
                ghostTimer -= dt; if(ghostTimer <= 0) { ghostPending = true; ghostTimer = hasDodge ? .09f : .06f; }
                if(dashTime >= dashDuration)
                {
                    // Al terminar conserva algo de inercia, que el frenado normal se encarga de detener.
                    dashTime = -1; Velocity = dashDirection*Profile.Speed*.4f; int next = queued; queued = 0;
                    if(next != 0 && queueTime > 0 && Stun <= 0 && Health > 0) StartAction(next);
                }
            }
            else
            {
                Position += Velocity*dt;
                // Un sonido de paso por cada zancada en el suelo; al ir más rápido, los pasos suenan más seguido.
                if(Height <= 0 && Health > 0) { stride += Velocity.magnitude*dt; if(stride >= 1.05f) { stride = 0; ForgeAudio.Play(ForgeAudio.Library?.footsteps, .3f); } }
            }
            // El empuje de un golpe desplaza al luchador y se va frenando.
            if(Knockback.sqrMagnitude > .0001f) { Position += Knockback*dt; Knockback = Vector3.MoveTowards(Knockback, Vector3.zero, dt*14); }
            // Física simple del salto: la gravedad (18 m/s²) reduce la velocidad vertical hasta tocar el suelo.
            if(Height > 0 || VerticalSpeed > 0)
            {
                VerticalSpeed -= 18*dt; Height = Mathf.Max(0,Height+VerticalSpeed*dt);
                if(Height <= 0) { VerticalSpeed = 0; stride = 0; ForgeAudio.Play(ForgeAudio.Library?.land, .6f); }
            }
            X = X; // Reasigna la posición para aplicar la altura del salto al Transform.
            UpdateGhosts(dt);
            UpdateClips(dt);
            // speed: rapidez real en m/s. move: la misma rapidez de 0 a 1 respecto a la velocidad máxima del personaje.
            float speed = Health > 0 ? new Vector3(motion.x,0,motion.z).magnitude : 0, move = Mathf.Clamp01(speed/Profile.Speed);
            if(animator!=null)
            {
                if(Health > 0)
                {
                    // Puños arriba en la guardia; bajan un poco al estar aturdido. Las animaciones importadas
                    // ya mueven sus propios brazos (la pose de pelea mantiene la guardia y la carrera los balancea),
                    // así que la guardia procedimental se aparta cuando alguna de ellas se está reproduciendo.
                    float guardTarget=Stun>0?.3f:Action!=0||Blocking?1:move>.1f?(hasRun?0:.7f):hasStance?0:1;
                    guardWeight=Mathf.MoveTowards(guardWeight,guardTarget,dt*(hasStance||hasRun?5:8));
                    var direction=target-Position;direction.y=0;
                    // Gira más lento mientras ataca, para que el golpe se comprometa con su dirección.
                    // La voltereta gira hacia su recorrido y después el fijado de objetivo lo vuelve a orientar al rival.
                    if(Dashing&&hasDodge)Root.rotation=Quaternion.Slerp(Root.rotation,rollFacing,dt*25);
                    else if(direction.sqrMagnitude>.01f)Root.rotation=Quaternion.Slerp(Root.rotation,Quaternion.LookRotation(direction),dt*(Action!=0?5:14));
                    // Los luchadores siempre miran al rival, así que las piernas deben seguir la dirección real del movimiento:
                    // al retroceder la animación corre hacia atrás y al moverse de lado gira la cadera.
                    // La voltereta anima sus propias piernas, así que el giro de cadera se relaja durante ella.
                    var local=Dashing&&hasDodge?Vector3.zero:Root.InverseTransformDirection(motion);local.y=0;
                    // Quieto siempre se reproduce hacia adelante, para que la pose de pelea nunca vaya al revés.
                    moveSign=speed>.3f&&local.z<-.25f*speed?-1:1;
                    float yawTarget=speed>.3f?Mathf.Clamp(Mathf.Atan2(local.x*moveSign,local.z*moveSign)*Mathf.Rad2Deg,-70,70):0;
                    legYaw=Mathf.MoveTowardsAngle(legYaw,yawTarget,dt*420);
                    localMotion=Vector3.Lerp(localMotion,Vector3.ClampMagnitude(local,6),dt*8);
                }
                else
                {
                    // K.O.: cae de espaldas pivotando en los pies, acelerando como por gravedad, con un pequeño rebote.
                    if(koTime==0){var flat=Root.forward;flat.y=0;koFacing=Quaternion.LookRotation(flat.sqrMagnitude>.01f?flat:Vector3.forward);}
                    koTime+=dt;guardWeight=Mathf.MoveTowards(guardWeight,0,dt*6);legYaw=0;localMotion=Vector3.zero;Velocity=Vector3.zero;
                    float t=koTime/.6f;float bounce=t>=1&&t<1.4f?Mathf.Sin((t-1)/.4f*Mathf.PI)*7:0;
                    Root.rotation=koFacing*Quaternion.Euler(-85*Mathf.Min(t*t,1)+bounce,0,0);
                }
                // Destello: mezcla el color del personaje con blanco mientras dura Flash.
                if(Flash>0||flashing){Tint(Color.Lerp(Profile.Color,Color.white,Flash*.85f));flashing=Flash>0;}
                // Parámetros del Animator de Starter Assets: velocidad, sentido de reproducción, suelo y salto.
                float animatedSpeed=Dashing&&hasDodge?0:Mathf.Min(speed,6);
                animator.SetFloat("Speed",animatedSpeed,.1f,dt);
                // Cuando la carrera importada cubre todos los pasos, su velocidad sigue la rapidez real
                // para que las zancadas coincidan con el suelo y los pies no "patinen".
                float pace=hasRun?(speed>.3f?Mathf.Clamp(speed/ForgeCombatClips.RunSpeed,.5f,1.5f):1):Dashing?1.4f:1;
                animator.SetFloat("MotionSpeed",moveSign*pace);
                animator.SetBool("Grounded",Height<=0);animator.SetBool("Jump",VerticalSpeed>0&&Height>0);animator.SetBool("FreeFall",VerticalSpeed<0&&Height>0);return;
            }
            // Animación simple del muñeco de cubos (solo si no hay prefab con Animator).
            int facing=target.x>Position.x?1:-1;
            float wave = Mathf.Sin(Time.time*8)*4;
            float strike = Action == 0 ? 0 : Mathf.Sin(Mathf.Clamp01(ActionTime/Current.Duration)*Mathf.PI);
            armL.localRotation = Quaternion.Euler(0,0,-25+wave);
            armR.localRotation = Quaternion.Euler(0,0,25-wave);
            legL.localRotation = Quaternion.Euler(0,0,move*Mathf.Sin(Time.time*14)*25);
            legR.localRotation = Quaternion.Euler(0,0,-move*Mathf.Sin(Time.time*14)*25);
            if(Blocking) { armL.localRotation=Quaternion.Euler(0,0,-145); armR.localRotation=Quaternion.Euler(0,0,145); }
            if(Action == 1 || Action == 3) (facing > 0 ? armR : armL).localRotation = Quaternion.Euler(0,0,facing*110*strike);
            if(Action == 2) (facing > 0 ? legR : legL).localRotation = Quaternion.Euler(0,0,facing*95*strike);
            body.localRotation = Quaternion.Euler(0,0,Stun > 0 ? -facing*18 : facing*strike*-8);
            Root.localRotation = Quaternion.Euler(0,0,Health <= 0 ? -facing*80 : 0);
        }
        /// <summary>
        /// Divide un ataque en dos pesos: preparación (wind) y extensión (strike).
        /// Preparación suave, golpe rápido que frena justo en el impacto, una pausa breve y una recuperación suave.
        /// </summary>
        static void Phases(AttackData attack, float t, out float wind, out float strike)
        {
            // windEnd: la preparación ocupa el 60 % del tiempo hasta el impacto. hold: cuánto se mantiene estirado.
            float windEnd=attack.HitTime*.6f, hold=attack.HitTime+Mathf.Min(.08f,(attack.Duration-attack.HitTime)*.3f);
            if(t<windEnd){wind=Mathf.SmoothStep(0,1,t/windEnd);strike=0;}
            else if(t<attack.HitTime){float k=1-(t-windEnd)/(attack.HitTime-windEnd);strike=1-k*k*k;wind=1-strike;}
            else if(t<hold){strike=1;wind=0;}
            else{strike=1-Mathf.SmoothStep(0,1,(t-hold)/(attack.Duration-hold));wind=0;}
        }
        /// <summary>
        /// Capa procedimental: se llama en LateUpdate, después de que el Animator calculó la pose,
        /// y añade encima la guardia, la inclinación del cuerpo y los ataques que no tienen animación importada.
        /// </summary>
        public void Pose()
        {
            // Guarda la pose calculada por el Animator antes de añadir los giros de combate de este frame.
            if(animator==null)return;
            if(posed){for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=basePose[i];if(hips!=null)hips.localPosition=hipsBase;}
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null)basePose[i]=bones[i].localRotation;
            if(hips!=null)hipsBase=hips.localPosition;
            posed=true;
            if(Health<=0)return; // La caída del K.O. se hace girando la raíz; los brazos siguen al Animator.
            // Ejes locales del personaje en el mundo: adelante, derecha y arriba.
            var forward=Root.forward;var side=Root.right;var up=Root.up;
            float wind=0,strike=0;if(Action!=0)Phases(Current,ActionTime,out wind,out strike);
            // Primero el tren inferior: la cadera sigue la dirección del movimiento y el torso se gira de vuelta
            // hacia el rival, así moverse de lado se ve como dar pasos laterales en vez de deslizarse.
            Turn(hips,up,legYaw);
            // Luego el torso, porque los brazos heredan su rotación. Una leve inclinación hacia adelante y la
            // respiración mantienen viva la guardia; el cuerpo se inclina hacia donde se mueve y se agacha en el dash.
            // Donde se reproduce una animación importada, la capa procedimental se aparta según su peso.
            float free=1-clipWeight;
            float stance=hasStance?0:guardWeight*free; // La pose de pelea importada ya respira y se inclina sola.
            float yaw=-legYaw,pitch=(3+Mathf.Sin(Time.time*2.4f+Profile.Speed)*1.2f)*stance+localMotion.z*1.3f,roll=-localMotion.x*1.6f,headPitch=0;
            if(Dashing&&!hasDodge)
            {
                // Dash simple: se inclina en la dirección del dash y baja la cadera a mitad del recorrido.
                float dash=1-dashTime/DashDuration;var local=Root.InverseTransformDirection(dashDirection);
                pitch+=local.z*8*dash;roll-=local.x*8*dash;
                if(hips!=null)hips.position-=up*.14f*Mathf.Sin(dashTime/DashDuration*Mathf.PI);
            }
            if(Blocking)pitch+=(hasBlock?0:8)-BlockShake*12;
            // Giros del torso propios de cada ataque procedimental.
            if(Action==1&&!hasJab){yaw+=punchSide*(14*wind-26*strike);pitch+=6*strike;}
            if(Action==2){float knee=Mathf.Max(wind,strike);pitch-=14*knee+6*strike;yaw+=8*strike;}
            if(Action==3){yaw+=35*wind-50*strike;pitch+=-8*wind+16*strike;}
            if(Stun>0&&!hasHit)
            {
                // Retrocede de golpe y se recupera lentamente mientras se acaba el aturdimiento.
                float hurt=Mathf.SmoothStep(0,1,Stun/Mathf.Max(StunTotal,.01f));
                pitch-=20*hurt;yaw+=hurtSide*14*hurt;headPitch=-15*hurt;
            }
            Body(up,yaw);Body(side,pitch);Body(forward,roll);Turn(head,side,headPitch);
            // Guardia: puños arriba delante de la barbilla.
            float guard=guardWeight*free;
            Aim(upperArmL,lowerArmL,forward*.45f-side*.35f-up*.5f,guard);
            Aim(upperArmR,lowerArmR,forward*.45f+side*.35f-up*.5f,guard);
            Aim(lowerArmL,leftHand,forward*.45f+up*.8f,guard);
            Aim(lowerArmR,rightHand,forward*.45f+up*.8f,guard);
            if(Blocking&&!hasBlock)
            {
                // Bloqueo procedimental: antebrazos verticales cubriendo la cara.
                Aim(upperArmL,lowerArmL,forward*.5f-side*.15f+up*.15f,1);
                Aim(upperArmR,lowerArmR,forward*.5f+side*.15f+up*.15f,1);
                Aim(lowerArmL,leftHand,up,1);Aim(lowerArmR,rightHand,up,1);
            }
            if(Action==1&&!hasJab)
            {
                // Recoge el puño abajo junto a las costillas y luego lo lanza recto a la cabeza.
                var upper=punchSide>0?upperArmR:upperArmL;var lower=punchSide>0?lowerArmR:lowerArmL;var hand=punchSide>0?rightHand:leftHand;
                Aim(upper,lower,-forward*.35f+side*punchSide*.25f-up*.6f,wind);Aim(lower,hand,forward*.6f+up*.7f,wind);
                var line=forward+up*.12f-side*punchSide*.08f;
                Aim(upper,lower,line,strike);Aim(lower,hand,line,strike);
            }
            if(Action==3)
            {
                // Gancho: abre el brazo hacia atrás y luego lo barre alrededor con el codo doblado.
                Aim(upperArmR,lowerArmR,-forward*.5f+side*.6f+up*.1f,wind);Aim(lowerArmR,rightHand,forward*.2f+up*.8f,wind);
                Aim(upperArmR,lowerArmR,forward+side*.15f+up*.1f,strike);Aim(lowerArmR,rightHand,forward-side*.35f+up*.1f,strike);
            }
            if(Action==2)
            {
                // Primero sube la rodilla, luego estira la espinilla; al recuperar se recoge igual.
                float knee=Mathf.Max(wind,strike);
                Aim(upperLegR,lowerLegR,forward*.9f+up*.35f,knee);Aim(lowerLegR,rightFoot,-up-forward*.3f,knee);
                Aim(upperLegR,lowerLegR,forward+up*.25f,strike);Aim(lowerLegR,rightFoot,forward+up*.2f,strike);
            }
            // Los fantasmas se crean aquí, después de la capa de combate, para que muestren la pose final del dash.
            if(ghostPending){ghostPending=false;AddGhost();}
        }
        void Body(Vector3 axis,float angle)
        {
            // Reparte el giro del torso entre columna y pecho para que se doble como una curva y no como una bisagra.
            if(Mathf.Abs(angle)<.01f)return;
            if(chest==null){Turn(spine,axis,angle);return;}
            Turn(spine,axis,angle*.5f);Turn(chest,axis,angle*.5f);
        }
        // Gira un hueso un ángulo (en grados) alrededor de un eje del mundo.
        static void Turn(Transform bone,Vector3 axis,float angle){if(bone!=null&&angle!=0)bone.rotation=Quaternion.AngleAxis(angle,axis)*bone.rotation;}
        static void Aim(Transform joint,Transform child,Vector3 direction,float weight)
        {
            // Apunta un segmento real (hombro→codo, codo→muñeca...) hacia una dirección del mundo.
            // Los huesos humanoides importados tienen ejes locales distintos, así que usar ángulos Euler fijos deformaría la pose.
            // weight indica cuánto se acerca a esa dirección (0 = nada, 1 = completamente).
            if(joint==null||child==null||weight<=0)return;
            var segment=child.position-joint.position;if(segment.sqrMagnitude<.000001f)return;
            var rotation=Quaternion.FromToRotation(segment,direction.normalized)*joint.rotation;
            joint.rotation=Quaternion.Slerp(joint.rotation,rotation,Mathf.Clamp01(weight));
        }
        void AddGhost()
        {
            // Copia la malla animada actual (BakeMesh) en objetos translúcidos que se desvanecen detrás del dash.
            if(skins==null||skins.Length==0)return;
            if(ghostMaterial==null)ghostMaterial=GhostMaterial();
            var ghost=new Ghost{Shape=new GameObject(Profile.Name+" Afterimage"),Meshes=new Mesh[skins.Length],Renderers=new MeshRenderer[skins.Length]};
            for(int i=0;i<skins.Length;i++)
            {
                var mesh=new Mesh();skins[i].BakeMesh(mesh);ghost.Meshes[i]=mesh;
                var part=new GameObject("Part");var source=skins[i].transform;
                part.transform.SetParent(ghost.Shape.transform,false);part.transform.SetPositionAndRotation(source.position,source.rotation);part.transform.localScale=source.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.AddComponent<MeshRenderer>();
                var materials=new Material[mesh.subMeshCount];for(int j=0;j<materials.Length;j++)materials[j]=ghostMaterial;
                renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;ghost.Renderers[i]=renderer;
            }
            ghosts.Add(ghost);UpdateGhosts(0);
        }
        Material GhostMaterial()
        {
            // Material URP Unlit configurado por código como transparente (mezcla alfa).
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetOverrideTag("RenderType","Transparent");
            mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=(int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor",Profile.Color);return mat;
        }
        // Envejece los fantasmas: se vuelven más transparentes y se destruyen al terminar su vida.
        void UpdateGhosts(float dt)
        {
            for(int i=ghosts.Count-1;i>=0;i--)
            {
                var ghost=ghosts[i];ghost.Age+=dt;float fade=1-ghost.Age/GhostLife;
                if(fade<=0){DestroyGhost(ghost);ghosts.RemoveAt(i);continue;}
                var color=Color.Lerp(Profile.Color,Color.white,.35f);color.a=.45f*fade;ghostBlock.SetColor("_BaseColor",color);
                foreach(var renderer in ghost.Renderers)renderer.SetPropertyBlock(ghostBlock);
            }
        }
        // Las mallas creadas con BakeMesh no se liberan solas, hay que destruirlas.
        static void DestroyGhost(Ghost ghost){foreach(var mesh in ghost.Meshes)Object.Destroy(mesh);Object.Destroy(ghost.Shape);}
        void ClearGhosts(){foreach(var ghost in ghosts)DestroyGhost(ghost);ghosts.Clear();}
        /// <summary>Congela o reanuda la animación (pausa y "hit stop").</summary>
        public void SetPaused(bool paused){if(animator!=null)animator.speed=paused?0:1;}
        /// <summary>Elimina al luchador de la escena y libera la memoria que usaba.</summary>
        public void Dispose()
        {
            // Los colores usan materiales copiados para cada luchador; se liberan junto con él.
            ClearGhosts();if(ghostMaterial!=null)Object.Destroy(ghostMaterial);
            foreach(var renderer in Root.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)Object.Destroy(material);
            Object.Destroy(Root.gameObject);
        }
    }
    /// <summary>Identidad visual y estadísticas base de un personaje del roster (compartidas por sus instancias).</summary>
    public sealed class FighterProfile
    {
        // Style: descripción corta que se muestra en la selección. Power: multiplicador del daño.
        public string Name, Style; public Color Color; public float Health, Speed, Power;
        public FighterProfile(string name,string style,Color color,float health,float speed,float power)
        { Name=name; Style=style; Color=color; Health=health; Speed=speed; Power=power; }
    }
}
