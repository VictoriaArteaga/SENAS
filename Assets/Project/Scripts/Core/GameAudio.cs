using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumina.Core
{
    /// <summary>
    /// Audio del juego. Se crea solo al arrancar y sobrevive a los cambios de escena (no hay que añadirlo a ninguna):
    ///  · música de fondo (Resources/Audio/fondo) en bucle durante todo el juego;
    ///  · efecto de seña superada (paso-sena) y de nivel completado (paso-nivel).
    /// Los volúmenes salen de GameSettings (Música y Efectos de la pantalla de configuración).
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const float MusicGain = 0.6f; // el fondo queda por debajo de los efectos
        private static GameAudio _instance;

        private bool _suppressed; // la música está en pausa a petición de una escena (p. ej. mientras suena el video de una lección)
        private bool _pausedBySuppression;
        private AudioSource _music;
        private AudioSource _sfx;
        private AudioClip _signPassed, _levelCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameAudio>();
        }

        /// <summary>
        /// Pausa o reanuda la música de fondo. La casa de aprendizaje la pausa mientras dura la lección (los videos traen sonido propio y la
        /// música lo taparía) y la reanuda al cerrarla. Al cambiar de escena se reanuda sola.
        /// </summary>
        public static void SetMusicSuppressed(bool suppressed)
        {
            if (_instance != null) _instance._suppressed = suppressed;
        }

        /// <summary>Una seña alcanzó la coincidencia mínima.</summary>
        public static void PlaySignPassed() => _instance?.PlaySfx(_instance._signPassed);

        /// <summary>Se completó el nivel (todas sus señas superadas).</summary>
        public static void PlayLevelCompleted() => _instance?.PlaySfx(_instance._levelCompleted);

        private void Awake()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;
            _music.clip = Resources.Load<AudioClip>("Audio/fondo");

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _signPassed = Resources.Load<AudioClip>("Audio/paso-sena");
            _levelCompleted = Resources.Load<AudioClip>("Audio/paso-nivel");

            if (_music.clip == null) Debug.LogWarning("[GameAudio] No se encontró Resources/Audio/fondo.");
            ApplyVolumes();
            if (_music.clip != null) _music.Play();
            SceneManager.sceneLoaded += (_, __) => _suppressed = false;
        }

        private float _nextVolumeCheck;

        private void Update()
        {
            if (_suppressed)
            {
                if (_music.isPlaying) _music.Pause();
                _pausedBySuppression = true;
            }
            else if (_pausedBySuppression)
            {
                _pausedBySuppression = false;
                _music.UnPause(); // sigue donde se quedó
            }
            // La música se reanuda sola si el sistema la pausó (por ejemplo al perder el foco la ventana).
            else if (_music.clip != null && !_music.isPlaying && Application.isFocused) _music.Play();
            if (Time.unscaledTime < _nextVolumeCheck) return;
            _nextVolumeCheck = Time.unscaledTime + 0.25f;
            ApplyVolumes();
        }

        private void ApplyVolumes() => _music.volume = GameSettings.MusicVolume * MusicGain;

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            _sfx.PlayOneShot(clip, GameSettings.SfxVolume);
        }
    }
}
