using System.Collections;
using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Smoke test for the GPU disturbance field — the RT ping-pong / Graphics.Blit path that only
    ///     runs under the player loop with a render device. Stamps from several sources (instant and
    ///     the duration-ramped lerp path) and lets the field tick, asserting it starts, blits, and
    ///     publishes its global texture without erroring. This is the integration-level guard for the
    ///     "Stamp before Start" class of bug.
    /// </summary>
    public class DisturbanceFieldPlayModeTests : PlayModeGameTest
    {
        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");

        [UnityTest]
        public IEnumerator StampsAndTicks_PublishGlobalTextureWithoutError()
        {
            yield return LoadGameScene();

            var field = Resolve<DisturbanceFieldService>();

            // Instant stamp (duration 0) and profile-driven stamps (Paint's duration > 0 takes the
            // lerp-scheduler path) — exercises both branches of Stamp.
            field.Stamp(new Vector3(-1f, 0.5f, 0f), 0.4f, 0.8f, Vector2.up);
            field.Stamp(StampSource.Bomb, Vector3.zero, Vector2.zero);
            field.Stamp(StampSource.Paint, new Vector3(1f, 1f, 0f), Vector2.right);

            // Let ITickable.Tick run its diffusion / stamp blits for several frames.
            for (var i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.IsNotNull(field.FieldTexture, "FieldTexture should be a live RT after Start + ticks.");
            Assert.IsNotNull(Shader.GetGlobalTexture(DisturbanceTexId),
                "The field should publish _DisturbanceTex once started and ticking.");
        }
    }
}
