using UnityEngine;

namespace Lumina.UI
{
    /// <summary>Indicador de carga de tres puntos que pulsan en cadena. Se activa y desactiva con el propio GameObject.</summary>
    public class LoadingDots : MonoBehaviour
    {
        [SerializeField] private RectTransform[] dots;
        [SerializeField] private float speed = 7f;
        [SerializeField] private float amplitude = 0.45f;

        private void Update()
        {
            if (dots == null) return;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                float wave = Mathf.Sin(Time.unscaledTime * speed - i * 0.9f) * 0.5f + 0.5f;
                dots[i].localScale = Vector3.one * (1f - amplitude + amplitude * 2f * wave);
            }
        }
    }
}
