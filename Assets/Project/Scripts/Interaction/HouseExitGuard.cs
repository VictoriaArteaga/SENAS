using UnityEngine;
using Lumina.Player;
using Lumina.UI;

namespace Lumina.Interaction
{
    /// <summary>
    /// Vigila al jugador dentro de una casa: si cruza el umbral de la puerta (o cae fuera del suelo) vuelve a Lúmina.
    /// La puerta de la casa es un hueco en la pared sur, en z = 0; el interior está en z positivo.
    /// </summary>
    public class HouseExitGuard : MonoBehaviour
    {
        private const float ExitZ = -0.3f;
        private const float FallY = -2f;

        private FirstPersonController _player;
        private bool _leaving;

        private void Update()
        {
            if (_leaving) return;
            if (_player == null)
            {
                _player = FindFirstObjectByType<FirstPersonController>();
                if (_player == null) return;
            }

            var position = _player.transform.position;
            if (position.z >= ExitZ && position.y >= FallY) return;

            _leaving = true;
            _player.enabled = false; // quieto mientras dura el fundido
            ScreenFader.Load(SceneNames.World);
        }
    }
}
