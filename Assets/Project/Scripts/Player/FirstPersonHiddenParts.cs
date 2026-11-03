using UnityEngine;
using UnityEngine.Rendering;

namespace Lumina.Player
{
    /// <summary>
    /// Partes del modelo que no deben verse cuando el avatar es el cuerpo del jugador en primera persona (cabeza, cabello, visera...),
    /// para que la cámara no quede dentro de ellas. <see cref="PlayerAvatar"/> llama a <see cref="Apply"/> sobre su instancia: las partes
    /// dejan de dibujarse pero siguen proyectando sombra, así la sombra del jugador conserva la silueta completa.
    /// En el prefab (y en el selector de avatares) no se hace nada: todas las partes se ven.
    /// </summary>
    public class FirstPersonHiddenParts : MonoBehaviour
    {
        [SerializeField] private Renderer[] parts = System.Array.Empty<Renderer>();

        public void Apply()
        {
            foreach (var part in parts)
                if (part != null) part.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}
