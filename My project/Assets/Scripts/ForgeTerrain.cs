using System.Collections.Generic;
using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Construye la arena del bosque a partir del Terrain Sample importado, sin modificar los assets originales:
    /// un terreno remuestreado con cuatro capas de textura mezcladas y pasto que se mueve con el viento,
    /// y después árboles, rocas y plantas repartidos alrededor del claro.
    /// </summary>
    public static class ForgeTerrain
    {
        // Size: lado del terreno en metros (amplio para que la cámara elevada nunca vea su borde; la niebla oculta el relieve lejano).
        // Heights/Splat/Details: resolución del mapa de alturas, del mapa de texturas y del mapa de pasto.
        const int Size=240, Heights=257, Splat=512, Details=512;
        // Los luchadores no se alejan más de 9 m del centro ni la cámara más de 19 m, así que los árboles empiezan más allá.
        const float Clearing=9.5f, TreeLine=21;

        /// <summary>Crea todo el escenario. Devuelve false si falta la configuración (entonces se usa ForgeLandscape).</summary>
        public static bool Build(ForgeEnvironmentAssets assets)
        {
            if(assets==null||assets.sourceTerrain==null)return false;
            // Una semilla fija genera el mismo bosque en cada pelea sin alterar la aleatoriedad del combate
            // (se guarda el estado de Random y se restaura al final).
            var previous=Random.state;Random.InitState(1907);
            try
            {
                var terrain=BuildTerrain(assets);
                // Diccionario material HDRP original → material URP convertido.
                var materials=new Dictionary<Material,Material>();
                if(assets.sourceMaterials!=null)for(int i=0;i<assets.sourceMaterials.Length&&i<assets.urpMaterials.Length;i++)materials[assets.sourceMaterials[i]]=assets.urpMaterials[i];
                var forest=new GameObject("Forest · Terrain Sample").transform;
                var trees=assets.trees!=null&&assets.trees.Length>0?assets.trees:new[]{assets.pine};
                var rocks=assets.rocks!=null&&assets.rocks.Length>0?assets.rocks:new[]{assets.rock};
                // Posiciones ya ocupadas, para que nada quede encima de otra cosa.
                var placed=new List<Vector3>();
                // Árboles: un anillo denso que se pierde en la niebla, sobre todo pinos con coníferas y cipreses mezclados.
                Scatter(terrain,forest,trees,70,TreeLine,34,3.2f,.75f,1.15f,false,0,placed,materials,assets.foliageShader);
                Scatter(terrain,forest,trees,110,34,80,4.2f,.8f,1.2f,false,0,placed,materials,assets.foliageShader);
                // Las rocas y plantas originales tienen tamaños muy distintos (algunas "rocas" son acantilados), así que
                // cada instancia se escala a un tamaño objetivo en metros: un borde bajo alrededor del claro,
                // peñascos entre los árboles y matorrales en el borde del bosque.
                Scatter(terrain,forest,rocks,10,Clearing+2.5f,15,2.5f,.9f,2f,true,.25f,placed,materials,assets.foliageShader);
                Scatter(terrain,forest,rocks,14,18,55,4,2.5f,4.5f,true,.6f,placed,materials,assets.foliageShader);
                if(assets.plants!=null&&assets.plants.Length>0)
                    Scatter(terrain,forest,assets.plants,90,Clearing+3,26,1.3f,.7f,1.5f,true,0,placed,materials,assets.foliageShader);
                return true;
            }
            finally{Random.state=previous;}
        }

        static Terrain BuildTerrain(ForgeEnvironmentAssets assets)
        {
            // Remuestrea el terreno original (grande) en uno más pequeño para la arena.
            var data=new TerrainData{heightmapResolution=Heights,size=new Vector3(Size,20,Size),alphamapResolution=Splat};
            var heights=new float[Heights,Heights];
            // Altura del centro del terreno original, usada como referencia para dejar el claro a nivel 0.
            float center=assets.sourceTerrain.GetInterpolatedHeight(.5f,.5f)/assets.sourceTerrain.size.y;
            for(int z=0;z<Heights;z++)for(int x=0;x<Heights;x++)
            {
                // u,v: coordenadas de 0 a 1 dentro del terreno. distance: metros desde el centro.
                float u=x/(Heights-1f),v=z/(Heights-1f);float distance=Distance(u,v);
                float source=assets.sourceTerrain.GetInterpolatedHeight(u,v)/assets.sourceTerrain.size.y;
                // Los 12 metros centrales quedan planos y luego se mezcla de vuelta con el relieve original.
                // Una altura normalizada de 0.25 equivale a Y=0 en el mundo, por el desplazamiento de -5 en Y del terreno.
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,25,distance));
                heights[z,x]=Mathf.Clamp01(.25f+(source-center)*blend);
            }
            data.SetHeights(0,0,heights);
            // Se clonan las capas antes de cambiar su repetición de textura, para no modificar los ajustes del paquete importado.
            var grass=Layer(assets.grass,6);var moss=Layer(assets.moss!=null?assets.moss:assets.grass,7);
            var soil=Layer(assets.soil,4);var pebbles=Layer(assets.pebbles!=null?assets.pebbles:assets.soil,2.5f);
            data.terrainLayers=new[]{grass,moss,soil,pebbles};
            data.SetAlphamaps(0,0,Paint());
            Grass(data,assets);
            // Crea el objeto Terrain centrado en el origen.
            var go=Terrain.CreateTerrainGameObject(data);go.name="Arena Terrain · Terrain Sample";go.transform.position=new Vector3(-Size*.5f,-5,-Size*.5f);
            var terrain=go.GetComponent<Terrain>();terrain.materialTemplate=new Material(assets.terrainShader);terrain.drawInstanced=true;
            // Calidad del terreno: error de píxel, distancia del mapa base y distancia/densidad del pasto.
            terrain.heightmapPixelError=5;terrain.basemapDistance=120;terrain.detailObjectDistance=55;terrain.detailObjectDensity=1;
            return terrain;
        }

        // Distancia en metros desde el centro del terreno para unas coordenadas u,v (0 a 1).
        static float Distance(float u,float v)=>new Vector2((u-.5f)*Size,(v-.5f)*Size).magnitude;
        // Ruido Perlin (valores suaves de 0 a 1) para que las manchas de textura y pasto se vean naturales; seed cambia el patrón.
        static float Noise(float u,float v,float scale,float seed)=>Mathf.PerlinNoise(u*Size*scale+seed,v*Size*scale+seed*1.7f);

        // Copia una capa de terreno con otro tamaño de repetición (tile) en metros.
        static TerrainLayer Layer(TerrainLayer source,float tile)
        { var layer=Object.Instantiate(source);layer.tileSize=new Vector2(tile,tile);return layer; }

        /// <summary>
        /// Tierra compactada en el claro con un borde de piedrecitas, y pasto alrededor salpicado de musgo según el ruido.
        /// Los pesos de cada texel siempre suman uno: pasto, musgo, tierra, piedrecitas.
        /// </summary>
        static float[,,] Paint()
        {
            var splat=new float[Splat,Splat,4];
            for(int z=0;z<Splat;z++)for(int x=0;x<Splat;x++)
            {
                float u=x/(Splat-1f),v=z/(Splat-1f),d=Distance(u,v);
                // Un borde irregular evita que el claro parezca un círculo perfecto.
                float edge=Clearing+1.2f+(Noise(u,v,.12f,11)-.5f)*3;
                // soil: 1 dentro del claro y 0 fuera, con una transición de 3 m.
                float soil=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(edge-1.5f,edge+1.5f,d));
                // pebbles: una franja con forma de campana (gaussiana) justo en el borde del claro.
                float pebbles=Mathf.Exp(-Mathf.Pow((d-edge)/1.4f,2))*Mathf.SmoothStep(.25f,.75f,Noise(u,v,.35f,3))*.8f;
                float moss=Mathf.SmoothStep(.42f,.68f,Noise(u,v,.05f,7));
                float outside=1-soil;
                float g=outside*(1-moss),m=outside*moss,s=soil,p=pebbles;
                // Se normalizan los pesos para que sumen 1.
                float total=g+m+s+p;
                splat[z,x,0]=g/total;splat[z,x,1]=m/total;splat[z,x,2]=s/total;splat[z,x,3]=p/total;
            }
            return splat;
        }

        /// <summary>Pasto del terreno que se mueve con el viento: nada en la zona de pelea, matas sueltas en el borde y espeso fuera.</summary>
        static void Grass(TerrainData data,ForgeEnvironmentAssets assets)
        {
            // Un tipo de pasto verde y otro seco, si sus texturas existen.
            var prototypes=new List<DetailPrototype>();
            if(assets.grassDetail!=null)prototypes.Add(GrassPrototype(assets.grassDetail,new Color(.78f,.9f,.62f),new Color(.86f,.8f,.52f)));
            if(assets.dryGrassDetail!=null)prototypes.Add(GrassPrototype(assets.dryGrassDetail,new Color(.92f,.86f,.62f),new Color(.82f,.7f,.45f)));
            if(prototypes.Count==0)return;
            data.detailPrototypes=prototypes.ToArray();data.SetDetailResolution(Details,16);
            // Ajustes del movimiento del pasto con el viento.
            data.wavingGrassStrength=.4f;data.wavingGrassAmount=.45f;data.wavingGrassSpeed=.45f;data.wavingGrassTint=new Color(.85f,.88f,.72f);
            // Una matriz por tipo de pasto con cuántas matas hay en cada celda.
            var layers=new int[prototypes.Count][,];for(int i=0;i<layers.Length;i++)layers[i]=new int[Details,Details];
            for(int z=0;z<Details;z++)for(int x=0;x<Details;x++)
            {
                float u=x/(Details-1f),v=z/(Details-1f),d=Distance(u,v);
                // rim: 0 dentro del claro, sube hasta 1 a 6 m del borde.
                float rim=Mathf.InverseLerp(Clearing+1.5f,Clearing+6,d);if(rim<=0)continue;
                float patch=Noise(u,v,.08f,23);
                int count=Mathf.RoundToInt(Mathf.SmoothStep(0,1,rim)*Mathf.Lerp(.5f,3,patch));
                // El pasto seco se agrupa en sus propias manchas de ruido en vez de repartirse uniformemente.
                bool dry=layers.Length>1&&Noise(u,v,.06f,41)>.6f;
                layers[dry?1:0][z,x]=count;
            }
            for(int i=0;i<layers.Length;i++)data.SetDetailLayer(0,0,i,layers[i]);
        }

        // Define un tipo de pasto: textura, tamaño mínimo/máximo y colores sano/seco.
        static DetailPrototype GrassPrototype(Texture2D texture,Color healthy,Color dry)=>new DetailPrototype{
            prototypeTexture=texture,renderMode=DetailRenderMode.Grass,usePrototypeMesh=false,
            minWidth=.8f,maxWidth=1.35f,minHeight=.6f,maxHeight=1.1f,noiseSpread=.4f,healthyColor=healthy,dryColor=dry};

        /// <summary>
        /// Coloca prefabs en puntos aleatorios de un anillo alrededor del centro, manteniendo una separación mínima
        /// con todo lo colocado hasta ahora, y cambia sus materiales HDRP por las versiones URP. Con fitSize,
        /// min/max son la dimensión mayor de la instancia en metros; si no, multiplican la escala del prefab.
        /// sink hunde la instancia en el suelo, en metros.
        /// </summary>
        static void Scatter(Terrain terrain,Transform parent,GameObject[] prefabs,int count,float inner,float outer,float spacing,
            float min,float max,bool fitSize,float sink,List<Vector3> placed,Dictionary<Material,Material> materials,Shader fallback)
        {
            // Hasta 30 intentos por objeto, por si no queda sitio libre.
            for(int i=0,attempts=0;i<count&&attempts<count*30;attempts++)
            {
                // Muestrear con raíz cuadrada reparte los puntos por igual en el área del anillo en vez de amontonarlos por dentro.
                float angle=Random.value*Mathf.PI*2,radius=Mathf.Sqrt(Mathf.Lerp(inner*inner,outer*outer,Random.value));
                var position=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                bool crowded=false;foreach(var other in placed)if((other-position).sqrMagnitude<spacing*spacing){crowded=true;break;}
                if(crowded)continue;
                placed.Add(position);i++;
                float size=Random.Range(min,max);
                // Apoya el objeto sobre la altura del terreno en ese punto.
                position.y=terrain.SampleHeight(position)+terrain.transform.position.y-sink;
                var prefab=prefabs[Random.Range(0,prefabs.Length)];if(prefab==null)continue;
                var instance=Object.Instantiate(prefab,position,Quaternion.Euler(0,Random.value*360,0),parent);
                instance.name=prefab.name+" · Terrain Sample";
                if(!fitSize)instance.transform.localScale*=size;
                else{float largest=Largest(instance);if(largest>.01f)instance.transform.localScale*=size/largest;}
                Convert(instance,materials,fallback);
            }
        }

        // Dimensión mayor (ancho, alto o fondo) del objeto en metros, sumando todos sus renderers.
        static float Largest(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return 0;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            return Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
        }

        // Sustituye cada material HDRP del objeto por su versión URP (o crea una al vuelo si no existe).
        static void Convert(GameObject go,Dictionary<Material,Material> materials,Shader fallback)
        {
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var originals=renderer.sharedMaterials;var replaced=new Material[originals.Length];
                for(int i=0;i<originals.Length;i++)
                {
                    var original=originals[i];if(original==null)continue;
                    if(!materials.TryGetValue(original,out replaced[i])){replaced[i]=Fallback(original,fallback);materials[original]=replaced[i];}
                }
                renderer.sharedMaterials=replaced;
            }
            // Recalcula los límites de los LOD (niveles de detalle) tras cambiar la escala.
            foreach(var lod in go.GetComponentsInChildren<LODGroup>())lod.RecalculateBounds();
        }

        /// <summary>
        /// Conversión en tiempo de ejecución para cualquier material que el editor no preparó. El recorte alfa (alpha clipping)
        /// mantiene opacas las siluetas de las hojas sin dibujar el fondo rectangular de su textura.
        /// </summary>
        static Material Fallback(Material original,Shader shader)
        {
            var mat=new Material(shader);
            // Copia textura, color y mapa de normales usando los nombres de propiedad de HDRP → URP.
            var texture=original.HasProperty("_BaseColorMap")?original.GetTexture("_BaseColorMap"):original.mainTexture;
            mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):Color.white);
            if(original.HasProperty("_NormalMap")){mat.SetTexture("_BumpMap",original.GetTexture("_NormalMap"));mat.EnableKeyword("_NORMALMAP");}
            bool cutout=original.HasProperty("_AlphaCutoffEnable")&&original.GetFloat("_AlphaCutoffEnable")>0;
            // _Cull=0: se dibujan ambas caras (las hojas se ven desde los dos lados).
            mat.SetFloat("_Cull",0);mat.SetFloat("_Smoothness",.15f);
            if(cutout){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.4f);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;}
            return mat;
        }
    }
}
