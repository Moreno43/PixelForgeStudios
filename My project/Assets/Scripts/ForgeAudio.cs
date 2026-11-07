using UnityEngine;

namespace PixelForge
{
    /// <summary>
    /// Reproduce la banda sonora y los efectos de sonido. Sobrevive a los cambios de escena (DontDestroyOnLoad),
    /// así el tema del menú sigue sonando entre pantallas y las pistas hacen un fundido cruzado entre menú y arena.
    /// Tiene el único AudioListener del juego; todos los sonidos son 2D, así que la posición del listener no importa.
    /// Patrón "singleton": existe una sola instancia, creada la primera vez que se necesita.
    /// </summary>
    public sealed class ForgeAudio : MonoBehaviour
    {
        // MusicVolume: volumen de la música respecto a los efectos. FadeSpeed: rapidez del fundido.
        const float MusicVolume=.5f, FadeSpeed=1.2f;
        static ForgeAudio instance;
        ForgeAudioLibrary library;
        // Dos fuentes de música: la pista que sale se desvanece mientras la que entra sube de volumen.
        AudioSource[] music;
        AudioSource effects;
        // current: índice de la fuente de música activa. lastBattle: última pista de combate (para no repetirla).
        int current, lastBattle=-1;
        // Multiplicador del volumen de la música (baja en la pausa).
        float duck=1;

        /// <summary>Acceso a la biblioteca de sonidos (null si no existe el asset).</summary>
        public static ForgeAudioLibrary Library => Get().library;

        // Devuelve la instancia, creándola la primera vez con su listener y sus fuentes de audio.
        static ForgeAudio Get()
        {
            if(instance!=null)return instance;
            var go=new GameObject("Forge Audio");DontDestroyOnLoad(go);
            instance=go.AddComponent<ForgeAudio>();
            go.AddComponent<AudioListener>();
            instance.library=Resources.Load<ForgeAudioLibrary>("ForgeAudio");
            instance.music=new[]{instance.Source(true),instance.Source(true)};
            instance.effects=instance.Source(false);
            AudioListener.volume=PlayerPrefs.GetFloat("ForgeVolume",.6f);
            return instance;
        }

        AudioSource Source(bool loop)
        {
            var source=gameObject.AddComponent<AudioSource>();
            // spatialBlend=0: sonido 2D, se oye igual sin importar la posición.
            source.playOnAwake=false;source.loop=loop;source.spatialBlend=0;
            // La música y los efectos siguen sonando durante la pausa, el hit stop y la cámara lenta.
            source.ignoreListenerPause=true;
            return source;
        }

        /// <summary>El tema del menú. Si ya está sonando, llamarlo otra vez no lo reinicia.</summary>
        public static void PlayMenu(){var audio=Get();audio.Play(audio.library!=null?audio.library.menuMusic:null);}

        /// <summary>Una pista de combate al azar; si hay varias, nunca repite la misma dos veces seguidas.</summary>
        public static void PlayBattle()
        {
            var audio=Get();var tracks=audio.library!=null?audio.library.battleMusic:null;
            if(tracks==null||tracks.Length==0){audio.Play(null);return;}
            int pick=Random.Range(0,tracks.Length);
            // Si salió la anterior, avanza 1..n-1 posiciones (con vuelta) para obtener otra distinta.
            if(tracks.Length>1&&pick==audio.lastBattle)pick=(pick+Random.Range(1,tracks.Length))%tracks.Length;
            audio.lastBattle=pick;audio.Play(tracks[pick],true);
        }

        /// <summary>Baja la música mientras está abierta la pausa o la pantalla de resultado.</summary>
        public static void Duck(bool on){Get().duck=on?.4f:1;}

        /// <summary>Reproduce un sonido al azar de un grupo, con una ligera variación de tono.</summary>
        public static void Play(AudioClip[] clips,float volume=1,float pitch=1)
        {
            if(clips==null||clips.Length==0)return;
            var clip=clips[Random.Range(0,clips.Length)];if(clip==null)return;
            // PlayOneShot permite que varios efectos se superpongan en la misma fuente.
            var source=Get().effects;source.pitch=pitch*Random.Range(.94f,1.06f);source.PlayOneShot(clip,volume);
        }

        // Cambia de pista: la nueva empieza en la otra fuente con volumen 0 y Update hace el fundido cruzado.
        void Play(AudioClip clip,bool restart=false)
        {
            duck=1;
            if(!restart&&music[current].clip==clip&&music[current].isPlaying)return;
            current=1-current;var source=music[current];
            source.clip=clip;source.volume=0;if(clip!=null)source.Play();else source.Stop();
        }

        void Update()
        {
            // Fundido cruzado con tiempo real, para que una pelea en pausa o en cámara lenta no detenga el fundido.
            float step=Time.unscaledDeltaTime*FadeSpeed;
            for(int i=0;i<music.Length;i++)
            {
                // La fuente activa sube hasta su volumen; la otra baja a 0 y se detiene.
                var source=music[i];float target=i==current&&source.clip!=null?MusicVolume*duck:0;
                source.volume=Mathf.MoveTowards(source.volume,target,step*MusicVolume);
                if(i!=current&&source.isPlaying&&source.volume<=0)source.Stop();
            }
        }
    }
}
