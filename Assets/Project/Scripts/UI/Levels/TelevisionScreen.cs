using UnityEngine;
using UnityEngine.Video;

namespace Lumina.UI
{
    /// <summary>
    /// Televisor de las casas de aprendizaje: proyecta el video de una seña en su pantalla. Reproduce un
    /// <see cref="VideoClip"/> o una URL sobre un RenderTexture y deja la pantalla apagada (negra) cuando no hay video.
    /// Quien decide qué video mostrar (p. ej. el panel del nivel) solo llama a <see cref="Play(VideoClip)"/>,
    /// <see cref="Play(string)"/> o <see cref="Stop"/>.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class TelevisionScreen : MonoBehaviour
    {
        [SerializeField] private Renderer screen;
        [SerializeField] private Material offMaterial;
        [SerializeField] private Material onMaterial;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private bool loop = true;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private VideoPlayer _player;

        public bool IsPlaying => _player != null && (_player.isPlaying || _player.isPrepared);

        private void Awake()
        {
            _player = GetComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = loop;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = renderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            for (ushort track = 0; track < _player.controlledAudioTrackCount; track++)
                _player.SetDirectAudioVolume(track, volume);
            _player.prepareCompleted += OnPrepared;
            SetScreen(false);
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            if (_player != null) _player.prepareCompleted -= OnPrepared;
        }

        public void Play(VideoClip clip)
        {
            if (clip == null) { Stop(); return; }
            _player.Stop();
            _player.source = VideoSource.VideoClip;
            _player.clip = clip;
            _player.Prepare();
        }

        public void Play(string url)
        {
            if (string.IsNullOrEmpty(url)) { Stop(); return; }
            _player.Stop();
            _player.source = VideoSource.Url;
            _player.url = url;
            _player.Prepare();
        }

        public void Stop()
        {
            if (_player != null) _player.Stop();
            ClearTexture();
            SetScreen(false);
        }

        private void OnPrepared(VideoPlayer source)
        {
            source.Play();
            SetScreen(true);
        }

        private void SetScreen(bool on)
        {
            var material = on ? onMaterial : offMaterial;
            if (screen != null && material != null) screen.sharedMaterial = material;
        }

        private void ClearTexture()
        {
            if (renderTexture == null) return;
            var active = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = active;
        }
    }
}
