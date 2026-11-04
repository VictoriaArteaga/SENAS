using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Auth;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Pantalla «CAMBIAR AVATAR» (Configuración › Personalización): el mismo escenario y selector del registro, pero para quien ya tiene
    /// cuenta. Al abrirse deja marcado el avatar actual; al pulsar SELECCIONAR en otro se guarda en el perfil (PATCH /profile/avatar) y
    /// queda en la sesión, de modo que el mundo y «Mi progreso» lo muestran desde la siguiente carga. VOLVER (o Esc) regresa a Configuración.
    /// </summary>
    public class AvatarChangeController : MonoBehaviour
    {
        [SerializeField] private AvatarSelectorController avatars;
        [SerializeField] private Button backButton;

        private bool _saving;
        private string _pending;

        private void Start()
        {
            if (avatars == null || backButton == null)
            {
                Debug.LogError($"[AvatarChangeController] Referencias incompletas en '{name}'.", this);
                enabled = false;
                return;
            }

            avatars.Focus(ProfileStore.Current?.avatarId ?? AuthSession.AvatarId);
            avatars.Selected += OnSelected;
            backButton.onClick.AddListener(Back);
        }

        private void OnDestroy()
        {
            if (avatars != null) avatars.Selected -= OnSelected;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        private static void Back() => ScreenFader.Load(SceneNames.Settings);

        private async void OnSelected(string avatarId)
        {
            AuthSession.SelectAvatar(avatarId); // en la sesión desde ya, aunque el guardado falle
            _pending = avatarId;
            if (_saving) return; // se guardará al terminar el envío en curso
            _saving = true;
            try
            {
                while (_pending != null)
                {
                    string id = _pending;
                    _pending = null;
                    try
                    {
                        await ProfileStore.UpdateAvatarAsync(id);
                        if (_pending == null) Toast.Show(Loc.T("settings.avatarSaved"));
                    }
                    catch (ApiException e)
                    {
                        Toast.Show(ApiMessages.For(e));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[AvatarChange] No se pudo guardar el avatar: " + e.Message);
                        Toast.Show(Loc.T("settings.avatarNotSaved"));
                    }
                }
            }
            finally
            {
                _saving = false;
            }
        }
    }
}
