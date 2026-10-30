using UnityEngine;

namespace Lumina.UI
{
    /// <summary>
    /// Movimiento suave de las manos del menú: flotan y se balancean ligeramente.
    /// </summary>
    public class MenuHandsMotion : MonoBehaviour
    {
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 1.2f;
        [SerializeField] private float swayDegrees = 6f;
        [SerializeField] private float swaySpeed = 0.7f;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        private void Awake()
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        private void Update()
        {
            float t = Time.time;
            transform.position = _homePosition + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
            transform.rotation = Quaternion.AngleAxis(Mathf.Sin(t * swaySpeed) * swayDegrees, Vector3.up) * _homeRotation;
        }
    }
}
