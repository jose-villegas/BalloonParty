using UnityEngine;

/// <summary>
/// Assigns a random <c>_TimeOffset</c> to the PaintBlob shader via a
/// MaterialPropertyBlock so every balloon looks different while sharing
/// one material (no extra material instances, no SRP batching breakage).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PaintBlobRenderer : MonoBehaviour
{
    [Tooltip("Scale of the random offset. Larger values spread the phase further apart.")]
    [SerializeField] private float _timeOffsetRange = 100f;

    private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

    private void Awake()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();
        var block = new MaterialPropertyBlock();

        // Read any existing block values so we don't stomp them.
        spriteRenderer.GetPropertyBlock(block);
        block.SetFloat(TimeOffsetId, Random.value * _timeOffsetRange);
        spriteRenderer.SetPropertyBlock(block);
    }
}
