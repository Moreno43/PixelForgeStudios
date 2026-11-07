using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PixelForge
{
    /// <summary>
    /// Ambiente de bosque al final de la tarde para la arena: un sol cálido y bajo con sombras suaves, luz ambiental
    /// de tres tonos, niebla a distancia, viento para los árboles, postprocesado de URP y motas de polvo flotando en la luz.
    /// </summary>
    public static class ForgeAtmosphere
    {
        // Coincide con el horizonte claro del cielo procedural, así los árboles lejanos se funden con él.
        static readonly Color Haze=new Color(.66f,.72f,.78f);

        /// <summary>Configura toda la iluminación y los efectos de la escena de la arena.</summary>
        public static void Apply(Camera camera,Material motes)
        {
            Sun();
            // Luz ambiental "Trilight": un color para el cielo (arriba), otro para el horizonte y otro para el suelo (abajo).
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.5f,.6f,.76f);
            RenderSettings.ambientEquatorColor=new Color(.44f,.47f,.42f);
            RenderSettings.ambientGroundColor=new Color(.2f,.18f,.13f);
            // Niebla exponencial al cuadrado: casi nada cerca y cada vez más espesa a lo lejos.
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.011f;RenderSettings.fogColor=Haze;
            // Los materiales SpeedTree se balancean con el WindZone de la escena.
            var wind=new GameObject("Forest Wind").AddComponent<WindZone>();
            wind.mode=WindZoneMode.Directional;wind.windMain=.45f;wind.windTurbulence=.35f;wind.windPulseMagnitude=.4f;wind.windPulseFrequency=.12f;
            wind.transform.rotation=Quaternion.Euler(0,40,0);
            // El cielo procedural pinta un "suelo" oscuro bajo el horizonte; se tiñe una copia del material con el color
            // de la niebla, para que cualquier vista más allá del terreno se funda con ella en vez de verse como una franja negra.
            if(RenderSettings.skybox!=null&&RenderSettings.skybox.HasProperty("_GroundColor"))
            {var sky=new Material(RenderSettings.skybox);sky.SetColor("_GroundColor",Haze);RenderSettings.skybox=sky;}
            ConfigureCamera(camera);
            PostProcessing();
            if(motes!=null)Motes(motes);
        }

        static void Sun()
        {
            // Un sol bajo proyecta sombras largas que asientan a los luchadores en el suelo y dan profundidad al claro.
            var sun=new GameObject("Arena Sun").AddComponent<Light>();
            sun.type=LightType.Directional;sun.color=new Color(1,.87f,.7f);sun.intensity=1.9f;
            sun.shadows=LightShadows.Soft;sun.shadowStrength=.82f;sun.shadowNormalBias=.4f;
            // 32° de elevación: luz de media tarde.
            sun.transform.rotation=Quaternion.Euler(32,-38,0);
            // El cielo procedural dibuja el disco del sol a partir de esta luz.
            RenderSettings.sun=sun;
        }

        static void ConfigureCamera(Camera camera)
        {
            // Se ve el cielo de fondo y se dibuja hasta 160 m (más lejos la niebla ya lo cubre todo).
            camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=160;
            // Datos extra de la cámara en URP: activar postprocesado, sombras y antialiasing SMAA (bordes suaves).
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=true;data.renderShadows=true;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
        }

        static void PostProcessing()
        {
            // Un VolumeProfile agrupa los efectos de postprocesado; se crea por código en vez de como asset.
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            // Tonemapping ACES evita que el cielo brillante y las hojas iluminadas se saturen a blanco plano.
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            // Ajustes de color: algo más de exposición, contraste y saturación, con un filtro ligeramente cálido.
            var color=profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.2f);color.contrast.Override(14);color.saturation.Override(14);color.colorFilter.Override(new Color(1,.97f,.92f));
            profile.Add<WhiteBalance>(true).temperature.Override(8);
            // Sombras frías contra luces cálidas, el contraste clásico de final de tarde.
            var split=profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(.42f,.5f,.62f));split.highlights.Override(new Color(.72f,.6f,.45f));split.balance.Override(-15);
            // Bloom: resplandor alrededor de lo que brilla más de 1 (el sol, las motas).
            var bloom=profile.Add<Bloom>(true);
            bloom.threshold.Override(1);bloom.intensity.Override(.55f);bloom.scatter.Override(.68f);bloom.tint.Override(new Color(1,.9f,.75f));
            // Viñeta: oscurece suavemente los bordes de la pantalla.
            var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.27f);vignette.smoothness.Override(.45f);
            // Desenfoca solo el bosque lejano (de 24 a 60 m), así la pelea se ve nítida y el fondo da sensación de profundidad.
            var focus=profile.Add<DepthOfField>(true);
            focus.mode.Override(DepthOfFieldMode.Gaussian);focus.gaussianStart.Override(24);focus.gaussianEnd.Override(60);focus.gaussianMaxRadius.Override(.8f);
            // Volumen global: afecta a toda la escena.
            var volume=new GameObject("Arena Post-processing").AddComponent<Volume>();volume.isGlobal=true;volume.priority=1;volume.sharedProfile=profile;
        }

        /// <summary>Motas lentas y brillantes sobre el claro que captan el bloom y hacen visible la luz del sol.</summary>
        static void Motes(Material material)
        {
            var go=new GameObject("Sunlit Motes");go.transform.position=new Vector3(0,2.6f,0);
            // Se detiene el sistema recién creado para poder configurarlo antes de que emita.
            var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;
            // prewarm: al empezar ya hay motas en el aire, como si llevara un rato funcionando.
            main.loop=true;main.prewarm=true;main.maxParticles=260;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(6,11);main.startSpeed=new ParticleSystem.MinMaxCurve(.03f,.18f);
            main.startSize=new ParticleSystem.MinMaxCurve(.035f,.085f);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.9f,.7f,.85f),new Color(.85f,1,.75f,.55f));
            var emission=particles.emission;emission.rateOverTime=30;
            // Aparecen dentro de una caja de 30×5×30 m sobre el claro.
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(30,5,30);
            // El ruido hace que floten con un movimiento irregular, como polvo real.
            var noise=particles.noise;noise.enabled=true;noise.strength=.22f;noise.frequency=.3f;noise.scrollSpeed=.15f;
            // Aparecen y desaparecen con un fundido para no "parpadear".
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.25f),new GradientAlphaKey(1,.7f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            particles.Play();
        }
    }
}
