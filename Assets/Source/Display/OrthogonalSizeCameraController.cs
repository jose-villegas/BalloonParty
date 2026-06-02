using BalloonParty.Configuration;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    internal class OrthogonalSizeCameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _continuous;

        [Inject] private IGameDisplayConfiguration _displayConfig;

        private void Start()
        {
            Apply();
        }

        private void LateUpdate()
        {
            if (_continuous)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_camera != null)
            {
                _camera.orthographicSize = _displayConfig.GetOrthogonalSize();
            }
        }
    }
}
