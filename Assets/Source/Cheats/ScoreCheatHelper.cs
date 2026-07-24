#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

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
            // hits the struct's zero-init ctor (ScoreValue 0, so the pop grants 0). HitsToPop 0 clears
            // ResolveScoreAttribution's durability gate. One pop carries the whole shortfall — ClaimProgress
            // caps it at the level threshold (banking any overflow) and the reset streak keeps the multiplier
            // at 1, so it lands exactly on target. Dispatched under InstantScoreTrails so ScoreTrailService
            // confirms the points immediately instead of spawning one flying pen per point (a whole level's
            // worth in a single frame is what broke the game).
            var fakeModel = new BalloonModel(new BalloonModelConfig(scoreValue: missing, hitsToPop: 0));
            fakeModel.Color.Value = color.Name;

            streak.Reset();
            CheatState.InstantScoreTrails = true;
            try
            {
                hitDispatcher.Dispatch(new ActorHitMessage(fakeModel,
                    Vector3.zero,
                    Vector3.zero,
                    HitOutcome.Pop,
                    new DamageContext(1, DamageFlags.Normal, color.Name)));
            }
            finally
            {
                CheatState.InstantScoreTrails = false;
            }
        }
    }
}
#endif
