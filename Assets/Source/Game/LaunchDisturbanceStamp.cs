using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Lets the player poke the cloud/disturbance field with a finger on the launch screen while the
    ///     game pre-warms — a small idle delight. Active ONLY while <see cref="NavigationState.Launch" /> is
    ///     current, so it stops the instant the game starts and the launcher unloads. Reuses the shared
    ///     disturbance stamp (the same call the dev <c>DisturbanceStampCheat</c> uses); no CPU sim of its own.
    /// </summary>
    internal sealed class LaunchDisturbanceStamp : ITickable
    {
        private readonly DisturbanceFieldService _field;

        private Vector3 _lastWorld;
        private bool _dragging;

        public LaunchDisturbanceStamp(DisturbanceFieldService field)
        {
            _field = field;
        }

        public void Tick()
        {
            if (_field == null || Navigation.Current.Value != NavigationState.Launch)
            {
                _dragging = false;
                return;
            }

            // Input.GetMouseButton(0) covers the primary touch on device as well as the editor mouse.
            if (!Input.GetMouseButton(0))
            {
                _dragging = false;
                return;
            }

            // Don't stamp a tap the UI already owns — pressing Play would otherwise leave a stamp under the
            // button as the launcher tears down.
            if (InputHelper.PointerIsOverUI())
            {
                _dragging = false;
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var world = camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
            world.z = 0f;

            // Direction from the drag delta (zero on the first frame of a press) so the stamp trails the finger.
            var direction = _dragging ? ((Vector2)(world - _lastWorld)).normalized : Vector2.zero;
            _lastWorld = world;
            _dragging = true;

            // Route through the configured StampSource (radius/strength/duration authored on
            // DisturbanceFieldSettings) rather than hardcoding — the same profile the projectile wake uses.
            _field.Stamp(StampSource.Projectile, world, direction);
        }
    }
}
