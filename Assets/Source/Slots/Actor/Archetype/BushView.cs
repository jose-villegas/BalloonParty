using BalloonParty.Shared;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Cluster renderer for Bush obstacles. Prebakes branch capsule endpoints
    /// on the CPU and pushes them via <c>_BranchSegments</c> / <c>_BranchCount</c>
    /// so the fragment shader only evaluates <c>CapsuleSDF</c> — no per-pixel
    /// <c>PhyllotaxisLeaf</c> calls.
    /// </summary>
    internal class BushView : ClusterView
    {
        private const int LeafCount = 16;
        private const int BranchCount = 5;
        private const int MaxBranches = 16 * BranchCount;

        private static readonly int SlotRadiusId = Shader.PropertyToID("_SlotRadius");
        private static readonly int RadiusJitterId = Shader.PropertyToID("_RadiusJitter");
        private static readonly int BranchSpreadId = Shader.PropertyToID("_BranchSpread");
        private static readonly int BranchSegmentsId = Shader.PropertyToID("_BranchSegments");
        private static readonly int BranchCountId = Shader.PropertyToID("_BranchCount");

        private readonly Vector4[] _branchSegments = new Vector4[MaxBranches];

        protected override void OnConfigured(MaterialPropertyBlock block)
        {
            var mat = Renderer.sharedMaterial;
            var slotRadius = mat.GetFloat(SlotRadiusId);
            var radiusJitter = mat.GetFloat(RadiusJitterId);
            var branchSpread = mat.GetFloat(BranchSpreadId);

            var branchCount = ComputeBranchSegments(slotRadius, radiusJitter, branchSpread);

            block.SetVectorArray(BranchSegmentsId, _branchSegments);
            block.SetInt(BranchCountId, branchCount);
            Renderer.SetPropertyBlock(block);
        }

        private int ComputeBranchSegments(float slotRadius, float radiusJitter, float branchSpread)
        {
            var count = 0;

            for (var i = 0; i < SlotCount; i++)
            {
                var slot = SlotCentersBuffer[i];
                var radiusScale = slot.w > 0.001f ? slot.w : 1f;

                if (radiusScale < 0.99f)
                {
                    continue;
                }

                var center = new Vector2(slot.x, slot.y);
                var hash = MathUtils.Frac(Mathf.Sin(center.x * 127.1f + center.y * 311.7f) * 43758.5453f);
                var baseRadius = slotRadius * radiusScale + (hash - 0.5f) * 2f * radiusJitter;

                for (var bc = 0; bc < BranchCount; bc++)
                {
                    if (count >= MaxBranches)
                    {
                        break;
                    }

                    var tip = PhyllotaxisCenter(center, baseRadius, hash, bc, branchSpread);
                    _branchSegments[count++] = new Vector4(center.x, center.y, tip.x, tip.y);
                }
            }

            return count;
        }

        private static Vector2 PhyllotaxisCenter(
            Vector2 slotCenter, float baseRadius, float hash,
            int depth, float branchSpread)
        {
            var n = LeafCount - 1 - depth;
            var fn = (float)n;

            var angle = fn * MathUtils.GoldenAngle + hash * MathUtils.TwoPi;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var maxR = Mathf.Sqrt(LeafCount - 1 + 0.5f);
            var dist = baseRadius * branchSpread * Mathf.Sqrt(fn + 0.5f) / maxR;

            return slotCenter + dir * dist;
        }
    }
}
