using UnityEngine;

namespace Lumina.UI
{
    /// <summary>
    /// Pequeño escenario de Sena para la ventana de cierre de sesión: el modelo, una cámara que solo se enciende
    /// mientras la ventana está abierta y una luz de relleno. Vive lejos del menú, que nunca lo ve.
    /// </summary>
    public class LogoutSenaStage : MonoBehaviour
    {
        [SerializeField] private Camera stageCamera;
        [SerializeField] private Animator senaAnimator;

        public Camera StageCamera => stageCamera;
        public Animator SenaAnimator => senaAnimator;

        private void Awake()
        {
            if (stageCamera != null) stageCamera.enabled = false;
            if (senaAnimator == null) senaAnimator = GetComponentInChildren<Animator>();
        }
    }
}
