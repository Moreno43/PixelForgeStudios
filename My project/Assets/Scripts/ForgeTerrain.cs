using UnityEngine;

namespace PixelForge
{
    public static class ForgeTerrain
    {
        public static bool Build()
        {
            var assets=Resources.Load<ForgeEnvironmentAssets>("ForgeEnvironment");
            if(assets==null)return false;
            var data=new TerrainData{heightmapResolution=257,size=new Vector3(100,20,100),alphamapResolution=128};
            var heights=new float[257,257];
            float center=assets.sourceTerrain.GetInterpolatedHeight(.5f,.5f)/assets.sourceTerrain.size.y;
            for(int z=0;z<257;z++)for(int x=0;x<257;x++)
            {
                float u=x/256f,v=z/256f;float distance=new Vector2((u-.5f)*100,(v-.5f)*100).magnitude;
                float source=assets.sourceTerrain.GetInterpolatedHeight(u,v)/assets.sourceTerrain.size.y;
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,25,distance));
                heights[z,x]=Mathf.Clamp01(.25f+(source-center)*blend);
            }
            data.SetHeights(0,0,heights);
            var grass=Object.Instantiate(assets.grass);var soil=Object.Instantiate(assets.soil);grass.tileSize=soil.tileSize=new Vector2(5,5);
            data.terrainLayers=new[]{grass,soil};
            var splat=new float[128,128,2];
            for(int z=0;z<128;z++)for(int x=0;x<128;x++){float d=new Vector2((x/127f-.5f)*100,(z/127f-.5f)*100).magnitude;float g=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,17,d));splat[z,x,0]=g;splat[z,x,1]=1-g;}
            data.SetAlphamaps(0,0,splat);
            var go=Terrain.CreateTerrainGameObject(data);go.name="Arena Terrain · Terrain Sample";go.transform.position=new Vector3(-50,-5,-50);
            var terrain=go.GetComponent<Terrain>();terrain.materialTemplate=new Material(assets.terrainShader);terrain.drawInstanced=true;terrain.heightmapPixelError=5;
            for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI*2/32;float radius=18+i%4*3;var pos=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);pos.y=terrain.SampleHeight(pos)+go.transform.position.y;
                var tree=Object.Instantiate(assets.pine,pos,Quaternion.Euler(0,i*53,0));tree.name="Pine_A · Terrain Sample";tree.transform.localScale*=.7f;Convert(tree,assets.foliageShader);
                if(i%3==0){var rockPos=pos*.8f;rockPos.y=terrain.SampleHeight(rockPos)+go.transform.position.y;var rock=Object.Instantiate(assets.rock,rockPos,Quaternion.Euler(0,i*39,0));rock.name="Rock_A_01 · Terrain Sample";Convert(rock,assets.foliageShader);}
            }
            return true;
        }
        static void Convert(GameObject go,Shader shader)
        {
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var originals=renderer.sharedMaterials;var materials=new Material[originals.Length];
                for(int i=0;i<originals.Length;i++)
                {
                    var original=originals[i];var mat=new Material(shader);materials[i]=mat;if(original==null)continue;
                    var texture=original.HasProperty("_BaseColorMap")?original.GetTexture("_BaseColorMap"):original.mainTexture;
                    mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):Color.white);
                    if(original.HasProperty("_NormalMap")){mat.SetTexture("_BumpMap",original.GetTexture("_NormalMap"));mat.EnableKeyword("_NORMALMAP");}
                    bool cutout=original.HasProperty("_AlphaCutoffEnable")&&original.GetFloat("_AlphaCutoffEnable")>0;
                    mat.SetFloat("_Cull",0);mat.SetFloat("_Smoothness",.15f);
                    if(cutout){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.4f);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;}
                }
                renderer.sharedMaterials=materials;
            }
            foreach(var lod in go.GetComponentsInChildren<LODGroup>())lod.RecalculateBounds();
        }
    }
}
