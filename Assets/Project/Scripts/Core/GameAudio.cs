using UnityEngine;

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
        }

        private float _nextVolumeCheck;

        private void Update()
        {
            // La música se reanuda sola si el sistema la pausó (por ejemplo al perder el foco la ventana).
            if (_music.clip != null && !_music.isPlaying && Application.isFocused) _music.Play();
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
