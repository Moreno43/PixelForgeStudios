using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelForge
{
    /// <summary>
    /// Crea y actualiza el asset de referencias del entorno (Resources/ForgeEnvironment) cuando Unity termina de importar.
    /// También convierte los materiales HDRP del Terrain Sample en materiales URP guardados como assets,
    /// porque el proyecto renderiza con URP y los materiales HDRP se verían rosas (shader no compatible).
    /// </summary>
    public static class ForgeEnvironmentSetup
    {
        // AssetPath: asset que lee el juego. Sample: carpeta del paquete Terrain Sample. Converted: dónde se guardan las conversiones.
        const string AssetPath="Assets/Resources/ForgeEnvironment.asset";
        const string Sample="Assets/TerrainDemoScene_HDRP/";
        const string Converted="Assets/Environment/ForgeURP";

        [InitializeOnLoadMethod]
        static void Initialize(){EditorApplication.delayCall+=Prepare;}

        // PixelForge > Prepare Terrain Assets: prepara solo lo que falta.
        [MenuItem("PixelForge/Prepare Terrain Assets")]
        public static void Prepare(){Prepare(false);}

        // PixelForge > Rebuild Forest Materials: fuerza la conversión de todos los materiales otra vez.
        [MenuItem("PixelForge/Rebuild Forest Materials")]
        public static void Rebuild(){Prepare(true);}

        static void Prepare(bool force)
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall-=Prepare;EditorApplication.delayCall+=Prepare;return;}
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            var asset=AssetDatabase.LoadAssetAtPath<ForgeEnvironmentAssets>(AssetPath);
            if(asset==null)
            {
                // Primera vez: crea el asset con las referencias básicas (terreno, capas, pino, roca y shaders URP).
                asset=ScriptableObject.CreateInstance<ForgeEnvironmentAssets>();
                asset.sourceTerrain=Load<TerrainData>("Terrain/Data/Terrain_3_3_9b11d05b-b237-4de7-82ed-a402f9cedebd.asset");
                asset.grass=Load<TerrainLayer>("Terrain/Layers/Grass_A.terrainlayer");
                asset.soil=Load<TerrainLayer>("Terrain/Layers/Grass_Soil_A.terrainlayer");
                asset.pine=Load<GameObject>("Prefabs/Trees/Pines/Pine_A/Pine_A.prefab");
                asset.rock=Load<GameObject>("Prefabs/Rocks/Rock_A_01.prefab");
                asset.terrainShader=Shader.Find("Universal Render Pipeline/Terrain/Lit");asset.foliageShader=Shader.Find("Universal Render Pipeline/Lit");
                if(asset.sourceTerrain==null||asset.grass==null||asset.soil==null||asset.pine==null||asset.rock==null)throw new System.InvalidOperationException("A Terrain Sample resource is missing. The incomplete configuration has not been saved.");
                AssetDatabase.CreateAsset(asset,AssetPath);
            }
            // Si el asset ya está al día no se toca, para no sobrescribir ajustes manuales del Inspector al recargar.
            else if(asset.version>=ForgeEnvironmentAssets.CurrentVersion&&!force)return;
            Upgrade(asset);
            asset.version=ForgeEnvironmentAssets.CurrentVersion;
            EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
            Debug.Log("FORGE_ENVIRONMENT_OK: Terrain Sample configured for URP ("+asset.urpMaterials.Length+" converted materials).");
        }

        // Carga un asset del Terrain Sample por su ruta relativa.
        static T Load<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(Sample+path);

        // Conserva la referencia actual si ya existe; si no, la carga del Sample.
        static T Keep<T>(T current,string path) where T:Object=>current!=null?current:Load<T>(path);

        // Igual que Keep pero para listas de prefabs; avisa de los que no encuentre.
        static GameObject[] KeepAll(GameObject[] current,params string[] paths)
        {
            if(current!=null&&current.Length>0)return current;
            var found=new List<GameObject>();
            foreach(var path in paths){var prefab=Load<GameObject>(path);if(prefab!=null)found.Add(prefab);else Debug.LogWarning("FORGE_ENVIRONMENT: missing "+Sample+path);}
            return found.ToArray();
        }

        /// <summary>Rellena solo las referencias que siguen vacías, conservando las asignadas a mano en el Inspector.</summary>
        static void Upgrade(ForgeEnvironmentAssets asset)
        {
            if(!AssetDatabase.IsValidFolder(Converted))AssetDatabase.CreateFolder("Assets/Environment","ForgeURP");
            asset.moss=Keep(asset.moss,"Terrain/Layers/Grass_Moss_A.terrainlayer");
            asset.pebbles=Keep(asset.pebbles,"Terrain/Layers/Pebbles_B.terrainlayer");
            // Variedad de árboles, rocas y plantas que se reparten por el bosque.
            asset.trees=KeepAll(asset.trees,"Prefabs/Trees/Pines/Pine_A/Pine_A.prefab","Prefabs/Trees/Pines/Pine_B/Pine_B.prefab",
                "Prefabs/Trees/Pines/Pine_C/Pine_C.prefab","Prefabs/Trees/Pines/Pine_D/Pine_D.prefab","Prefabs/Trees/Conifer/Conifer.prefab","Prefabs/Trees/Cypress/Cypress.prefab");
            asset.rocks=KeepAll(asset.rocks,"Prefabs/Rocks/Rock_A_01.prefab","Prefabs/Rocks/Rock_B_01.prefab","Prefabs/Rocks/Rock_C_01.prefab",
                "Prefabs/Rocks/Rock_C_02.prefab","Prefabs/Rocks/Rock_Overgrown_A.prefab","Prefabs/Rocks/Rock_Overgrown_B.prefab");
            asset.plants=KeepAll(asset.plants,"Prefabs/Details/Fern_A.prefab","Prefabs/Details/Fern_B.prefab","Prefabs/Details/Bush_A.prefab",
                "Prefabs/Details/Bush_B.prefab","Prefabs/Details/Heather_A.prefab","Prefabs/Details/Shrub.prefab","Prefabs/Foliage/Red_Bush/Red_Bush.prefab");
            if(asset.grassDetail==null)asset.grassDetail=ReadableCopy("Prefabs/Details/Textures/Grass_A_BaseColor.tif","Grass_Detail");
            if(asset.dryGrassDetail==null)asset.dryGrassDetail=ReadableCopy("Prefabs/Details/Textures/Grass_Dry_BaseColor.tif","GrassDry_Detail");
            ConvertMaterials(asset);
            asset.moteMaterial=MoteMaterial();
        }

        /// <summary>
        /// El pasto del terreno se empaqueta en un atlas en tiempo de ejecución, lo que exige texturas legibles (Read/Write).
        /// Se hace una copia para no cambiar los ajustes de importación originales del Sample.
        /// </summary>
        static Texture2D ReadableCopy(string source,string name)
        {
            var target=Converted+"/"+name+Path.GetExtension(source);
            if(AssetDatabase.LoadAssetAtPath<Texture2D>(target)==null&&!AssetDatabase.CopyAsset(Sample+source,target)){Debug.LogWarning("FORGE_ENVIRONMENT: could not copy "+source);return null;}
            var importer=(TextureImporter)AssetImporter.GetAtPath(target);
            if(!importer.isReadable||!importer.alphaIsTransparency||importer.wrapMode!=TextureWrapMode.Clamp)
            {importer.isReadable=true;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        // Reúne todos los materiales distintos de árboles, rocas y plantas y crea su versión URP.
        static void ConvertMaterials(ForgeEnvironmentAssets asset)
        {
            var sources=new List<Material>();
            foreach(var group in new[]{asset.trees,asset.rocks,asset.plants})foreach(var prefab in group)
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)if(material!=null&&!sources.Contains(material))sources.Add(material);
            var converted=new Material[sources.Count];
            // Los árboles SpeedTree usan el shader SpeedTree8 de URP; el resto, URP Lit.
            for(int i=0;i<sources.Count;i++)converted[i]=Save(sources[i],IsSpeedTree(sources[i])?SpeedTree(sources[i]):Lit(sources[i]));
            asset.sourceMaterials=sources.ToArray();asset.urpMaterials=converted;
        }

        // Detecta materiales de árboles SpeedTree por sus propiedades o por el nombre del shader.
        static bool IsSpeedTree(Material source)=>source.HasProperty("_HueVariationColor")&&source.HasProperty("_MainTex")||source.shader.name.Contains("SpeedTree");

        /// <summary>Guarda un material convertido, actualizando en su sitio una conversión anterior para que las referencias sigan funcionando.</summary>
        static Material Save(Material source,Material converted)
        {
            // Se añade parte del GUID del original al nombre, porque distintos árboles repiten nombres como "Bark".
            var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            var path=Converted+"/"+source.name+"_"+guid.Substring(0,6)+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing==null){AssetDatabase.CreateAsset(converted,path);return converted;}
            existing.shader=converted.shader;existing.CopyPropertiesFromMaterial(converted);existing.shaderKeywords=converted.shaderKeywords;
            existing.renderQueue=converted.renderQueue;existing.enableInstancing=true;
            Object.DestroyImmediate(converted);EditorUtility.SetDirty(existing);return existing;
        }

        /// <summary>
        /// Los grafos SpeedTree del Sample comparten nombres de propiedades con el shader SpeedTree8 de URP, así que las
        /// texturas se copian directamente. El shader de URP añade el viento del WindZone y la variación de tono por árbol.
        /// </summary>
        static Material SpeedTree(Material source)
        {
            // enableInstancing: dibuja muchos árboles iguales en una sola llamada (mejor rendimiento).
            var material=new Material(Shader.Find("Universal Render Pipeline/Nature/SpeedTree8")){enableInstancing=true};
            material.SetTexture("_MainTex",source.GetTexture("_MainTex"));
            if(source.HasProperty("_Color"))material.SetColor("_Color",source.GetColor("_Color"));
            // Cada efecto opcional necesita su textura, su interruptor y su keyword del shader activados.
            if(Texture(source,"_BumpMap",out var normal)){material.SetTexture("_BumpMap",normal);material.SetFloat("_NormalMapKwToggle",1);material.EnableKeyword("EFFECT_BUMP");}
            if(Texture(source,"_ExtraTex",out var extra)){material.SetTexture("_ExtraTex",extra);material.EnableKeyword("EFFECT_EXTRA_TEX");}
            if(Texture(source,"_SubsurfaceTex",out var subsurface))
            {material.SetTexture("_SubsurfaceTex",subsurface);material.SetFloat("_SubsurfaceKwToggle",1);material.EnableKeyword("EFFECT_SUBSURFACE");}
            material.SetColor("_HueVariationColor",source.GetColor("_HueVariationColor"));material.SetFloat("_HueVariationKwToggle",1);material.EnableKeyword("EFFECT_HUE_VARIATION");
            // Billboard: la versión plana del árbol que se usa a gran distancia.
            bool billboard=source.IsKeywordEnabled("EFFECT_BILLBOARD");
            if(billboard){material.SetFloat("_BillboardKwToggle",1);material.EnableKeyword("EFFECT_BILLBOARD");}
            // Las hojas son tarjetas que se ven desde ambos lados; la corteza y los billboards solo por delante.
            bool leaves=source.IsKeywordEnabled("_ALPHATEST_ON")&&!billboard;
            material.SetFloat("_TwoSided",leaves?0:2);
            // Los billboards lejanos solo necesitan un balanceo barato; la geometría cercana usa el modelo de viento completo.
            material.SetFloat("_WindQuality",billboard?2:3);material.EnableKeyword(billboard?"_WINDQUALITY_FAST":"_WINDQUALITY_BETTER");
            material.renderQueue=(int)RenderQueue.AlphaTest;
            return material;
        }

        /// <summary>Convierte un material estilo HDRP Lit a URP Lit, buscando sus texturas por nombres de propiedad comunes.</summary>
        static Material Lit(Material source)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){enableInstancing=true};
            var baseMap=FindTexture(source,"_BaseColorMap","_MainTex","_BaseMap","basecolor","albedo","diffuse","color");
            var normal=FindTexture(source,"_NormalMap","_BumpMap","normal");
            material.SetTexture("_BaseMap",baseMap);
            var tint=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white;
            if(normal!=null){material.SetTexture("_BumpMap",normal);material.EnableKeyword("_NORMALMAP");}
            material.SetFloat("_Smoothness",.18f);
            bool cutout=source.IsKeywordEnabled("_ALPHATEST_ON")||source.HasProperty("_AlphaCutoffEnable")&&source.GetFloat("_AlphaCutoffEnable")>0;
            // La caliza clara del Sample estaba iluminada con la exposición física de HDRP; con este sol de URP más brillante
            // se ve casi blanca, así que las superficies opacas (las rocas) se oscurecen para encajar en el bosque.
            if(!cutout)tint=new Color(tint.r*.6f,tint.g*.6f,tint.b*.58f,tint.a);
            material.SetColor("_BaseColor",tint);
            if(cutout)
            {
                // El recorte alfa mantiene opacas las siluetas de las hojas sin dibujar el fondo de su textura.
                material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",source.HasProperty("_AlphaCutoff")?source.GetFloat("_AlphaCutoff"):.45f);
                material.EnableKeyword("_ALPHATEST_ON");material.SetFloat("_Cull",0);material.renderQueue=(int)RenderQueue.AlphaTest;
            }
            return material;
        }

        // Obtiene una textura si el material tiene esa propiedad y no está vacía.
        static bool Texture(Material source,string property,out Texture texture)
        { texture=source.HasProperty(property)?source.GetTexture(property):null;return texture!=null; }

        /// <summary>
        /// Devuelve la primera textura cuyo nombre de propiedad coincide con una de las pistas, exacto o como parte del nombre.
        /// Los materiales de Shader Graph suelen tener nombres de propiedad generados, así que también se revisa
        /// el nombre de archivo de la textura (por ejemplo "Fern_BaseColor").
        /// </summary>
        static Texture FindTexture(Material source,params string[] hints)
        {
            var names=source.GetTexturePropertyNames();
            foreach(var hint in hints)foreach(var name in names)
            {
                var texture=source.GetTexture(name);if(texture==null)continue;
                // Las pistas que empiezan por "_" deben coincidir exactamente; el resto basta con que aparezcan.
                bool match=hint.StartsWith("_")?name==hint:name.ToLowerInvariant().Contains(hint)||texture.name.ToLowerInvariant().Contains(hint);
                if(match)return texture;
            }
            return null;
        }

        /// <summary>Material de partículas aditivo y brillante para las motas de polvo que flotan en la luz del sol.</summary>
        static Material MoteMaterial()
        {
            const string path=Converted+"/SunlitMotes.mat";
            var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap",AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            // Un color HDR mayor que 1 permite que el bloom capte las motas; el color de vértice de las partículas se limita a 1.
            material.SetColor("_BaseColor",new Color(2.4f,2.1f,1.6f,1));
            // Mezcla aditiva (SrcAlpha + One): la mota suma luz a lo que hay detrás, por eso brilla.
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",2);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.One);material.SetFloat("_ZWrite",0);
            material.SetOverrideTag("RenderType","Transparent");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.EnableKeyword("_BLENDMODE_ADD");
            material.renderQueue=(int)RenderQueue.Transparent;
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing==null){AssetDatabase.CreateAsset(material,path);return material;}
            existing.shader=material.shader;existing.CopyPropertiesFromMaterial(material);existing.shaderKeywords=material.shaderKeywords;existing.renderQueue=material.renderQueue;
            existing.SetOverrideTag("RenderType","Transparent");Object.DestroyImmediate(material);EditorUtility.SetDirty(existing);return existing;
        }
    }
}
