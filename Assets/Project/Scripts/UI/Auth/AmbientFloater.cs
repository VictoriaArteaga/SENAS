using UnityEngine;

namespace Lumina.UI
{
    /// <summary>Movimiento ambiental suave de un adorno 3D: sube y baja y gira despacio. Costo despreciable.</summary>
    public class AmbientFloater : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.12f;
        [SerializeField] private float frequency = 0.35f;
        [SerializeField] private Vector3 spinDegreesPerSecond = new Vector3(0f, 18f, 0f);
        [SerializeField] private float phase;

        private Vector3 _home;

        private void Awake() => _home = transform.localPosition;

        private void Update()
        {
            float y = Mathf.Sin((Time.time + phase) * frequency * Mathf.PI * 2f) * amplitude;
            transform.localPosition = _home + Vector3.up * y;
            transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
