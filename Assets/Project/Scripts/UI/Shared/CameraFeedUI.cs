using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Vista previa de cámara en un RawImage (espejada, como un espejo). Solo muestra la imagen: el reconocimiento de señas
    /// se conectará después sobre <see cref="Texture"/>. Usada en Practice y en la prueba de cámara de Settings.
    /// </summary>
    public class CameraFeedUI : MonoBehaviour
    {
        [SerializeField] private RawImage view;
        [SerializeField] private TMP_Text status;

        private WebCamTexture _texture;
        private string _statusKey;

        public bool IsRunning => _texture != null && _texture.isPlaying;
        public Texture Texture => _texture;

        public void Show(string deviceName = null)
        {
            Stop();
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                SetStatus("camera.none");
                return;
            }

            string wanted = string.IsNullOrEmpty(deviceName) ? GameSettings.CameraDevice : deviceName;
            string chosen = devices[0].name;
            foreach (var device in devices)
                if (device.name == wanted) { chosen = device.name; break; }

            _texture = new WebCamTexture(chosen);
            view.texture = _texture;
            view.uvRect = new Rect(1f, 0f, -1f, 1f);
            view.color = Color.white;
            _texture.Play();
            SetStatus(null);
        }

        public void Stop()
        {
            if (_texture != null)
            {
                _texture.Stop();
                Destroy(_texture);
                _texture = null;
            }
            if (view != null) { view.texture = null; view.color = new Color(0f, 0f, 0f, 0.25f); }
            SetStatus("camera.off");
        }

        private void SetStatus(string key)
        {
            _statusKey = key;
            if (status != null) status.text = key == null ? string.Empty : Loc.T(key);
        }

        private void OnEnable() => GameSettings.LanguageChanged += Relocalize;
        private void Relocalize() { if (status != null && _statusKey != null) status.text = Loc.T(_statusKey); }

        private void OnDisable()
        {
            GameSettings.LanguageChanged -= Relocalize;
            Stop();
        }
    }
}
