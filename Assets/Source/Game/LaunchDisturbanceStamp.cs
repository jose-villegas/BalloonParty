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
        // World-space distance the finger must travel before the heading is refreshed — accumulates
        // sub-threshold motion instead of reading each frame's raw (frame-rate-dependent, jittery) delta.
        private const float HeadingStep = 0.05f;

        // How sharply the heading turns toward a new drag direction (0 = frozen, 1 = snap).
        private const float HeadingResponse = 0.5f;

        private readonly DisturbanceFieldService _field;

        private Vector3 _lastWorld;
        private Vector2 _heading;
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

            if (!_dragging)
            {
                // First frame of a press: no heading yet, so it stamps a plain radial poke.
                _lastWorld = world;
                _heading = Vector2.zero;
                _dragging = true;
            }
            else
            {
                // Only refresh the heading once the finger has travelled far enough to be a real drag, then
                // ease into it (Slerp stays stable through a sharp reversal). A slow or momentarily still
                // finger keeps its last heading instead of collapsing to a directionless poke.
                var delta = (Vector2)(world - _lastWorld);
                if (delta.sqrMagnitude >= HeadingStep * HeadingStep)
                {
                    _heading = _heading == Vector2.zero
                        ? delta.normalized
                        : (Vector2)Vector3.Slerp(_heading, delta.normalized, HeadingResponse);
                    _lastWorld = world;
                }
            }

            // Route through the configured StampSource (radius/strength/duration authored on
            // DisturbanceFieldSettings) rather than hardcoding — the same profile the projectile wake uses.
            _field.Stamp(StampSource.Projectile, world, _heading);
        }
    }
}
