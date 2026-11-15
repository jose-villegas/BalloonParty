using System;
using BalloonParty.Configuration.Effects;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Projectile.Clearance
{
    /// <summary>
    ///     Keeps the <see cref="ClearanceField" /> on the balls in flight and publishes it to the effect
    ///     shaders (<c>Include/Clearance.cginc</c>). A <c>Tick</c>, so the field is current before any
    ///     <c>LateTick</c> or <c>LateUpdate</c> reads it.
    /// </summary>
    internal sealed class ClearanceFieldTicker : IStartable, ITickable, IDisposable
    {
        private static readonly int CirclesId = Shader.PropertyToID("_ClearanceCircles");
        private static readonly int CountId = Shader.PropertyToID("_ClearanceCount");
        private static readonly int FloorId = Shader.PropertyToID("_ClearanceFloor");
        private static readonly int SoftnessId = Shader.PropertyToID("_ClearanceSoftness");

        private readonly IProjectileRegistry _registry;
        private readonly ClearanceField _field;
        private readonly IClearanceSettings _settings;

        // Always the full capacity: a global array's length is fixed by its first SetGlobalVectorArray.
        private readonly Vector4[] _circleBuffer = new Vector4[ClearanceCircle.MaxCircles];

        internal ClearanceFieldTicker(IProjectileRegistry registry, ClearanceField field, IClearanceSettings settings)
        {
            _registry = registry;
            _field = field;
            _settings = settings;
        }

        public void Start()
        {
            // Before the first frame draws, so a shader never reads an unset (zero-length) array.
            Publish();
        }

        public void Tick()
        {
            for (var i = 0; i < _registry.InFlightCount; i++)
            {
                _field.Track(_registry.InFlightAt(i), _registry.InFlightPositionAt(i));
            }

            _field.Advance(Time.deltaTime);
            Publish();
        }

        public void Dispose()
        {
            // Globals outlive the scope: without this the next scene (or the editor after play mode) would
            // still fade effects around wherever the last balls were.
            Shader.SetGlobalFloat(CountId, 0f);
        }

        private void Publish()
        {
            var count = _settings.FadeEffects ? _field.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var circle = _field.CircleAt(i);
                _circleBuffer[i] = new Vector4(circle.Center.x, circle.Center.y, circle.Radius, circle.Strength);
            }

            Shader.SetGlobalVectorArray(CirclesId, _circleBuffer);
            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalFloat(FloorId, _settings.EffectFloorAlpha);
            Shader.SetGlobalFloat(SoftnessId, _settings.RimSoftness);
        }
    }
}
