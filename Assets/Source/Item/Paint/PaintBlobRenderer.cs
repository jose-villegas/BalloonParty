using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Assigns a random <c>_TimeOffset</c> to the PaintBlob shader via a
    ///     MaterialPropertyBlock so every blob looks different while sharing
    ///     one material (no extra material instances, no SRP batching breakage).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PaintBlobRenderer : MonoBehaviour
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

        [Tooltip("Scale of the random offset. Larger values spread the phase further apart.")]
        [SerializeField] private float _timeOffsetRange = 100f;

        private void Awake()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();

            spriteRenderer.SetFloatAndApply(block, TimeOffsetId, Random.value * _timeOffsetRange);
        }
    }
}
