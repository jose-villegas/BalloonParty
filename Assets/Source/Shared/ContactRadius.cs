using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>World-space contact-radius derivation shared by every projectile-facing collider read
    /// in the shot solver (@ref plan_shot_solver_accuracy §3 "Shared radius helper") — a circle's
    /// radius or a capsule's cross-section half-extent, scaled by the transform's world scale (any
    /// other collider shape, or none at all, is collision-inert). Extracted from
    /// <c>ShotBoardGather.ResolveProjectileContactRadius</c> so live gather, Phase A's static
    /// archetypes, and Phase G's synthetic gather all derive it identically.</summary>
    internal static class ContactRadius
    {
        internal static float FromCollider(Collider2D collider, float lossyScaleX)
        {
            return collider switch
            {
                CircleCollider2D circle => circle.radius * lossyScaleX,
                CapsuleCollider2D capsule => Mathf.Min(capsule.size.x, capsule.size.y) * 0.5f * lossyScaleX,
                _ => 0f,
            };
        }
    }
}
