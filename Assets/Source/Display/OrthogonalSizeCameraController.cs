using BalloonParty.Shared;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Display
{
    public class OrthogonalSizeCameraController : IStartable
    {
        private readonly IGameConfiguration _config;

        public OrthogonalSizeCameraController(IGameConfiguration config)
        {
            _config = config;
        }

        public void Start()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var size = _config.DisplayConfiguration.GetOrthogonalSize();

            if (size > 0)
            {
                camera.orthographicSize = size;
            }
        }
    }
}

