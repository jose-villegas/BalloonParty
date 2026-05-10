using BalloonParty.Shared;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    [RequireComponent(typeof(Camera))]
    public class OrthogonalSizeCameraController : MonoBehaviour
    {
        [Inject] private IGameConfiguration _config;

        private void Start()
        {
            var camera = GetComponent<Camera>();
            var size = _config.DisplayConfiguration.GetOrthogonalSize();

            if (size > 0)
            {
                camera.orthographicSize = size;
            }
        }
    }
}

