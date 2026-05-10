using UnityEngine;

namespace BalloonParty.Balloon.Items
{
    public class LaserItemRotation : MonoBehaviour
    {
        [SerializeField] private float _rotationSpeed;

        private float _angle;

        private void Update()
        {
            _angle += _rotationSpeed * Time.deltaTime;
            transform.rotation = Quaternion.AngleAxis(_angle, Vector3.forward);
        }

        private void OnEnable()
        {
            _angle = 0f;
        }
    }
}

