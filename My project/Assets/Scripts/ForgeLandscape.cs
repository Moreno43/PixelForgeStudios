using UnityEngine;

namespace PixelForge
{
    public static class ForgeLandscape
    {
        static Transform Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.position=position;go.transform.localScale=scale;
            Object.Destroy(go.GetComponent<Collider>());var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",color);go.GetComponent<Renderer>().material=mat;return go.transform;
        }
        public static void Build(GameObject rockPrefab)
        {
            Shape("Clairière · sol",PrimitiveType.Cylinder,new Vector3(0,-.22f,0),new Vector3(27,.2f,27),new Color(.63f,.64f,.35f));
            Shape("Zone de combat · terre",PrimitiveType.Cylinder,new Vector3(0,-.025f,0),new Vector3(22,.02f,22),new Color(.78f,.68f,.45f));
            for(int i=0;i<28;i++)
            {
                float angle=i*Mathf.PI*2/28;var pos=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(14+i%3);
                Shape("Tronc",PrimitiveType.Cylinder,pos+Vector3.up*2.4f,new Vector3(.65f,2.4f,.65f),new Color(.3f,.23f,.14f));
                Shape("Canopée",PrimitiveType.Sphere,pos+Vector3.up*5.1f,new Vector3(4,3.4f,4),new Color(.16f+i%3*.025f,.38f+i%3*.035f,.2f));
                if(i%2==0 && rockPrefab!=null)
                {
                    var instance=Object.Instantiate((Object)rockPrefab,pos*.85f,Quaternion.Euler(0,i*47,0));
                    var rock=instance as GameObject;
                    if(rock==null){Object.Destroy(instance);Debug.LogWarning("El recurso de roca no es un GameObject. Se omite la decoración para permitir el combate.");continue;}
                    rock.name="Rock_A_01 · asset del proyecto";rock.transform.localScale=Vector3.one*2;
                    foreach(var r in rock.GetComponentsInChildren<Renderer>()){var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",new Color(.42f,.49f,.43f));var mats=r.sharedMaterials;for(int j=0;j<mats.Length;j++)mats[j]=mat;r.sharedMaterials=mats;}
                }
            }
            for(int i=0;i<12;i++)
            {float angle=i*Mathf.PI*2/12;Shape("Colline lointaine",PrimitiveType.Sphere,new Vector3(Mathf.Cos(angle)*38,3,Mathf.Sin(angle)*38),new Vector3(25,13+i%3*4,22),new Color(.35f,.49f,.43f));}
        }
    }
}
