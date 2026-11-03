using System.Collections;
using System.Collections.Generic;
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
        private Coroutine _watchdog;

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
            if (isActiveAndEnabled) _watchdog = StartCoroutine(FallBackIfNoImage(chosen));
        }

        /// <summary>
        /// Si la cámara elegida no arranca (p. ej. un visor de realidad virtual que aparece como cámara y no entrega imagen), prueba
        /// con las demás hasta encontrar una que sí.
        /// </summary>
        private IEnumerator FallBackIfNoImage(string first)
        {
            var tried = new HashSet<string> { first };
            while (true)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                if (_texture == null) yield break;
                if (_texture.isPlaying && _texture.width > 16) yield break;

                string next = null;
                foreach (var device in WebCamTexture.devices)
                    if (!tried.Contains(device.name)) { next = device.name; break; }
                if (next == null) { SetStatus("camera.none"); yield break; }

                tried.Add(next);
                _texture.Stop();
                Destroy(_texture);
                _texture = new WebCamTexture(next);
                view.texture = _texture;
                _texture.Play();
            }
        }

        public void Stop()
        {
            if (_watchdog != null) { StopCoroutine(_watchdog); _watchdog = null; }
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
