using UnityEngine;
using Lumina.Auth;

namespace Lumina.Player
{
    /// <summary>
    /// Pone el avatar elegido en el registro como cuerpo del jugador. El controlador en primera persona no se toca:
    /// el modelo cuelga del jugador, se coloca ligeramente detrás de la cámara (para que no se vea por dentro la cabeza)
    /// y solo se ve al mirar hacia abajo y por su sombra. Sin avatar elegido (o con un id desconocido) no hace nada,
    /// que es el comportamiento que tenía el mundo antes.
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        [SerializeField] private AvatarCatalog catalog;
        [Tooltip("Cuánto se retrasa el cuerpo respecto a la cámara (m), para que la cámara quede delante de la cara.")]
        [SerializeField] private float backwardOffset = 0.2f;

        public string CurrentId { get; private set; }
        public GameObject Body { get; private set; }

        private void Awake()
        {
            if (catalog == null)
            {
                Debug.LogWarning($"[PlayerAvatar] Falta el catálogo de avatares en '{name}'; el jugador se queda sin cuerpo visible.", this);
                return;
            }

            var entry = catalog.Find(AuthSession.AvatarId);
            if (entry == null) return;

            Body = Instantiate(entry.prefab, transform);
            Body.name = "Avatar_" + entry.id;
            Body.transform.localPosition = new Vector3(0f, 0f, -backwardOffset);
            Body.transform.localRotation = Quaternion.identity;
            CurrentId = entry.id;

            // El cuerpo no debe interferir con la física del jugador ni mover al personaje por animación.
            foreach (var collider in Body.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var animator in Body.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
        }
    }
}
