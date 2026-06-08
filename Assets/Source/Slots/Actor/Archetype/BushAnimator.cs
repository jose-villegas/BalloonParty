using BalloonParty.Configuration;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Drives idle wind animation on bush leaves. Each leaf rotates around its
    /// attachment point (pivot) using sine oscillation layered with Perlin noise.
    /// Depth modulates amplitude — tip leaves sway most, trunk leaves barely move.
    /// Branches are static and not animated.
    /// </summary>
    internal class BushAnimator : ITickable
    {
        private readonly IBushSettings _settings;
        private readonly BushViewController _controller;

        [Inject]
        internal BushAnimator(IBushSettings settings, BushViewController controller)
        {
            _settings = settings;
            _controller = controller;
        }

        public void Tick()
        {
            var view = _controller.View;
            if (view == null)
            {
                return;
            }

            var entries = view.SlotRenderEntries;
            if (entries.Count == 0)
            {
                return;
            }

            var time = Time.time;
            var frequency = _settings.WindPeriod > 0f ? 1f / _settings.WindPeriod : 1f;
            var amplitude = _settings.WindAmplitude;
            var noiseAmplitude = _settings.WindNoiseAmplitude;
            var scalePulse = _settings.WindScalePulse;

            for (var s = 0; s < entries.Count; s++)
            {
                var entry = entries[s];
                if (entry.LeafCount == 0 || entry.LeafSlots == null)
                {
                    continue;
                }

                AnimateSlotLeaves(entry, time, frequency, amplitude, noiseAmplitude, scalePulse);
            }
        }

        private static void AnimateSlotLeaves(
            BushView.SlotRenderData entry,
            float time,
            float frequency,
            float amplitude,
            float noiseAmplitude,
            float scalePulse)
        {
            var slots = entry.LeafSlots;
            var worldPos = entry.WorldPos;
            var scaleCompensation = entry.ScaleCompensation;
            var pivotOffset = entry.PivotOffset;

            for (var i = 0; i < entry.LeafCount; i++)
            {
                var slot = slots[i];
                var depth = slot.Depth;
                var phase = slot.PhaseOffset;

                var sineRotation = Mathf.Sin(time * frequency + phase) * amplitude * depth;
                var noiseRotation = (Mathf.PerlinNoise(time * 0.3f, phase) - 0.5f)
                                    * 2f * noiseAmplitude * depth;
                var windRotation = sineRotation + noiseRotation;

                var angleDeg = slot.BaseAngle * Mathf.Rad2Deg - 90f + windRotation;

                var scale = slot.Scale * scaleCompensation;
                if (scalePulse > 0f)
                {
                    var pulse = 1f + Mathf.Sin(time * frequency * 2f + phase) * scalePulse * depth;
                    scale *= pulse;
                }

                var leafWorldPos = worldPos + slot.Position;
                var rot = Quaternion.Euler(0f, 0f, angleDeg);
                var pivotShift = rot * new Vector3(0f, -pivotOffset * scale, 0f);
                entry.LeafMatrices[i] = Matrix4x4.TRS(
                    new Vector3(leafWorldPos.x, leafWorldPos.y, 0f) + pivotShift,
                    rot,
                    Vector3.one * scale);
            }
        }
    }
}

