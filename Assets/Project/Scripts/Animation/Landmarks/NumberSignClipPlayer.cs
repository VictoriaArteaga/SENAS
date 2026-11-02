using Lumina.Alphabet;
using UnityEngine;

namespace Lumina.Animation.Landmarks
{
    /// <summary>
    /// Reproduce un <see cref="NumberSignClip"/> en la mano 3D por tiempo normalizado: una sola vez, y mantiene la última pose al terminar.
    /// Se vuelve a lanzar llamando de nuevo a <see cref="Play"/> (en la casa de los números, al pulsar otra vez VER SEÑA).
    /// Va en la raíz de la mano, junto al <see cref="HandPoseDriver"/>.
    /// </summary>
    public sealed class NumberSignClipPlayer : MonoBehaviour
    {
        private HandPoseDriver _driver;
        private NumberSignClip _clip;
        private float _elapsed;
        private bool _skipFirstDelta;

        /// <summary>
        /// Máximo deltaTime que avanza el clip en un fotograma. Un tirón (p. ej. al activar la mano por primera vez) no se consume de golpe:
        /// como mucho cuenta este tiempo.
        /// </summary>
        public const float MaxDeltaSeconds = 0.05f;

        /// <summary>Verdadero mientras el clip avanza (falso al terminar o tras <see cref="Stop"/>).</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Tiempo normalizado actual (0 a 1) del clip en curso o del último reproducido.</summary>
        public float NormalizedTime { get; private set; }

        public NumberSignClip Clip => _clip;

        /// <summary>Empieza el clip desde el principio. Devuelve false, sin tocar la mano, si no se puede reproducir.</summary>
        public bool Play(NumberSignClip clip)
        {
            if (clip == null || !clip.IsPlayable) return false;
            if (_driver == null) _driver = GetComponent<HandPoseDriver>();
            if (_driver == null) _driver = GetComponentInChildren<HandPoseDriver>(true);
            if (_driver == null) return false;

            _clip = clip;
            _elapsed = 0f;
            NormalizedTime = 0f;
            IsPlaying = true;
            _skipFirstDelta = true; // el deltaTime del fotograma en que se pulsa incluye el coste de activar la mano
            _driver.SetPose(clip.Evaluate(0f));
            return true;
        }

        /// <summary>Detiene la reproducción sin cambiar la pose actual.</summary>
        public void Stop() => IsPlaying = false;

        private void Update()
        {
            if (!IsPlaying || _clip == null || _driver == null) return;

            if (_skipFirstDelta) _skipFirstDelta = false;
            else _elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxDeltaSeconds);
            NormalizedTime = Mathf.Clamp01(_elapsed / _clip.DurationSeconds);
            _driver.SetPose(_clip.Evaluate(NormalizedTime));
            if (NormalizedTime >= 1f) IsPlaying = false;
        }
    }
}
