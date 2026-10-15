using UnityEngine;

namespace PixelForge
{
    // Joint poses are animated in code so the prototype needs no external clips.
    public sealed class ForgeFighter
    {
        public readonly Transform Root;
        readonly Transform armL, armR, legL, legR, body;
        readonly Animator animator;
        readonly Transform[] bones;
        readonly Quaternion[] basePose;
        bool posed;
        float guardWeight;
        Transform leftHand,rightHand,rightFoot;
        public Vector3 Position { get => Root.position; set {var horizontal=Vector3.ClampMagnitude(new Vector3(value.x,0,value.z),9);Root.position=new Vector3(horizontal.x,Height,horizontal.z);} }
        public readonly FighterProfile Profile;
        public float Health, Height, VerticalSpeed, ActionTime, Stun;
        public int Action, Wins;
        public bool Blocking, HitApplied;
        public float X { get => Root.position.x; set {var p=Position;p.x=value;Position=p;} }
        public ForgeFighter(FighterProfile profile, float x, GameObject prefab)
        {
            Profile = profile; Health = profile.Health;
            if(prefab!=null)
            {
                var go=Object.Instantiate(prefab,new Vector3(x,0,0),Quaternion.identity);go.name=profile.Name;Root=go.transform;
                foreach(var component in go.GetComponentsInChildren<MonoBehaviour>())component.enabled=false;
                foreach(var collider in go.GetComponentsInChildren<Collider>())collider.enabled=false;
                animator=go.GetComponentInChildren<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                // Imported footsteps belong to Starter Assets, whose Start is disabled here.
                animator.fireEvents=false;
                foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var mat in renderer.materials)
                {if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",profile.Color);else mat.color=profile.Color;}
                bones=new[]{animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),animator.GetBoneTransform(HumanBodyBones.RightUpperArm),animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),animator.GetBoneTransform(HumanBodyBones.RightLowerArm),animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),animator.GetBoneTransform(HumanBodyBones.Spine)};
                leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                basePose=new Quaternion[bones.Length];return;
            }
            Root = new GameObject(profile.Name).transform;
            Root.position = new Vector3(x, 0, 0);
            body = Joint("Cuerpo", new Vector3(0, 1.35f, 0));
            Part("Torso", body, Vector3.zero, new Vector3(.65f, .8f, .4f), profile.Color);
            Part("Casco", body, new Vector3(0, .69f, 0), new Vector3(.48f, .46f, .45f), profile.Color);
            Part("Visor", body, new Vector3(0, .72f, -.24f), new Vector3(.38f, .1f, .04f), Color.white);
            Part("Nucleo", body, new Vector3(0, .1f, -.22f), new Vector3(.21f, .27f, .04f), Color.white);
            armL = Joint("Brazo izquierdo", new Vector3(-.43f, 1.62f, 0));
            armR = Joint("Brazo derecho", new Vector3(.43f, 1.62f, 0));
            legL = Joint("Pierna izquierda", new Vector3(-.21f, .98f, 0));
            legR = Joint("Pierna derecha", new Vector3(.21f, .98f, 0));
            Limb(armL, .65f, profile.Color); Limb(armR, .65f, profile.Color);
            Limb(legL, .85f, new Color(.15f,.18f,.25f)); Limb(legR, .85f, new Color(.15f,.18f,.25f));
        }
        Transform Joint(string name, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(Root, false); t.localPosition = position; return t; }
        void Limb(Transform joint, float length, Color color)
        {
            Part("Armadura", joint, new Vector3(0,-length*.5f,0), new Vector3(.24f,length,.28f), color);
            Part("Extremo", joint, new Vector3(0,-length,0), new Vector3(.3f,.22f,.35f), Profile.Color);
        }
        public static Transform Part(string name, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localScale = size;
            // Unlit material also works in the project's HDRP/URP setup.
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            string shaderName = pipeline == null ? "Unlit/Color" : pipeline.GetType().Name.Contains("HD") ? "HDRP/Unlit" : "Universal Render Pipeline/Unlit";
            var shader = Shader.Find(shaderName) ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader); mat.color = color;
            if(mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",color);
            if(mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor",color);
            go.GetComponent<Renderer>().material = mat; return go.transform;
        }
        public void Attack(int action)
        { if(Action != 0 || Stun > 0 || Blocking || Health <= 0) return; Action = action; ActionTime = 0; HitApplied = false; }
        public void Tick(float dt, Vector3 target, float move)
        {
            if(posed){for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=basePose[i];posed=false;}
            Stun = Mathf.Max(0, Stun-dt);
            if(Action != 0) { ActionTime += dt; if(ActionTime > .55f) Action = 0; }
            if(Height > 0 || VerticalSpeed > 0) { VerticalSpeed -= 18*dt; Height = Mathf.Max(0,Height+VerticalSpeed*dt); }
            X = X; // Synchronize the animated jump height.
            if(animator!=null)
            {
                guardWeight=Mathf.MoveTowards(guardWeight,move>.1f&&Action==0&&!Blocking?0:1,dt*8);
                var direction=target-Position;direction.y=0;
                if(direction.sqrMagnitude>.01f)Root.rotation=Quaternion.Slerp(Root.rotation,Quaternion.LookRotation(direction),dt*14);
                animator.SetFloat("Speed",move*Profile.Speed,.12f,dt);animator.SetFloat("MotionSpeed",1);
                animator.SetBool("Grounded",Height<=0);animator.SetBool("Jump",VerticalSpeed>0&&Height>0);animator.SetBool("FreeFall",VerticalSpeed<0&&Height>0);return;
            }
            int facing=target.x>Position.x?1:-1;
            float wave = Mathf.Sin(Time.time*8)*4;
            float strike = Action == 0 ? 0 : Mathf.Sin(Mathf.Clamp01(ActionTime/.45f)*Mathf.PI);
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
        public void Pose()
        {
            if(animator==null)return;
            if(posed)for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=basePose[i];
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null)basePose[i]=bones[i].localRotation;
            posed=true;
            float strike=Action==0?0:Mathf.Sin(Mathf.Clamp01(ActionTime/.55f)*Mathf.PI);
            var forward=Root.forward;var side=Root.right;var up=Vector3.up;
            // Aim actual elbow/wrist segments in world space; imported humanoid bones
            // have different local axes, so fixed Euler offsets distort their poses.
            Aim(bones[0],bones[2],forward*.45f-side*.35f-up*.5f,guardWeight);
            Aim(bones[1],bones[3],forward*.45f+side*.35f-up*.5f,guardWeight);
            Aim(bones[2],leftHand,forward*.45f+up*.8f,guardWeight);
            Aim(bones[3],rightHand,forward*.45f+up*.8f,guardWeight);
            if(Blocking)
            {
                Aim(bones[0],bones[2],forward*.5f-side*.15f+up*.15f,1);
                Aim(bones[1],bones[3],forward*.5f+side*.15f+up*.15f,1);
                Aim(bones[2],leftHand,up,1);Aim(bones[3],rightHand,up,1);
            }
            if(Action==1||Action==3){Aim(bones[1],bones[3],forward,strike);Aim(bones[3],rightHand,forward,strike);}
            if(Action==2){Aim(bones[4],bones[5],forward+up*.1f,strike);Aim(bones[5],rightFoot,forward-up*.15f,strike);}
            if(Stun>0)Rotate(6,new Vector3(-25,0,0));
            if(Health<=0)Root.rotation=Quaternion.LookRotation(Vector3.forward)*Quaternion.Euler(0,0,80);
        }
        static void Aim(Transform joint,Transform child,Vector3 direction,float weight)
        {
            if(joint==null||child==null||weight<=0)return;
            var segment=child.position-joint.position;if(segment.sqrMagnitude<.000001f)return;
            var rotation=Quaternion.FromToRotation(segment,direction.normalized)*joint.rotation;
            joint.rotation=Quaternion.Slerp(joint.rotation,rotation,Mathf.Clamp01(weight));
        }
        public void SetPaused(bool paused){if(animator!=null)animator.speed=paused?0:1;}
        void Rotate(int index,Vector3 euler){if(bones[index]!=null)bones[index].localRotation*=Quaternion.Euler(euler);}
        public void Dispose()
        {
            foreach(var renderer in Root.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)Object.Destroy(material);
            Object.Destroy(Root.gameObject);
        }
    }
    public sealed class FighterProfile
    {
        public string Name, Style; public Color Color; public float Health, Speed, Power;
        public FighterProfile(string name,string style,Color color,float health,float speed,float power)
        { Name=name; Style=style; Color=color; Health=health; Speed=speed; Power=power; }
    }
}
