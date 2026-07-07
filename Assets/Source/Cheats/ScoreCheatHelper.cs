#if UNITY_EDITOR || DEVELOPMENT_BUILD

using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Cheats
{
    internal static class ScoreCheatHelper
    {
        internal static void FillColor(
            PaletteEntry color,
            int target,
            ILevelProgress levelProgress,
            IHitDispatcher hitDispatcher,
            ColorStreakTracker streak)
        {
            var missing = target - levelProgress.GetProgress(color.Name);
            if (missing <= 0)
            {
                return;
            }

            // A popped stand-in that actually scores: an explicit config, because new BalloonModelConfig()
            // hits the struct's zero-init ctor (ScoreValue 0, so every pop granted 0). ScoreValue 1 +
            // HitsToPop 0 clears ResolveScoreAttribution's durability gate. Reset the streak before each
            // pop so the multiplier stays 1 and the fill lands exactly on target instead of overshooting.
            var fakeModel = new BalloonModel(new BalloonModelConfig(scoreValue: 1, hitsToPop: 0));
            fakeModel.Color.Value = color.Name;

            for (var i = 0; i < missing; i++)
            {
                streak.Reset();
                hitDispatcher.Dispatch(new ActorHitMessage(fakeModel,
                    Vector3.zero,
                    Vector3.zero,
                    HitOutcome.Pop,
                    new DamageContext(1, DamageFlags.Normal, color.Name)));
            }
        }
    }
}
#endif
