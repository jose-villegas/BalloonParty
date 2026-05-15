using BalloonParty.Configuration;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Display
{
    internal class OrthogonalSizeCameraController : IStartable
    {
        private readonly GameDisplayConfiguration _displayConfig;

        public OrthogonalSizeCameraController(GameDisplayConfiguration displayConfig)
        {
            _displayConfig = displayConfig;
        }

        public void Start()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographicSize = _displayConfig.GetOrthogonalSize();
        }
    }
}
