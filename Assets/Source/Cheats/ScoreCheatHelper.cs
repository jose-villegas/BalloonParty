#if UNITY_EDITOR || DEVELOPMENT_BUILD

using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Cheats
{
    internal static class ScoreCheatHelper
    {
        internal static void FillColor(
            PaletteEntry color,
            int target,
            ScoreController scoreController,
            IPublisher<BalloonHitMessage> hitPublisher)
        {
            var missing = target - scoreController.GetProgress(color.Name);
            if (missing <= 0)
            {
                return;
            }

            var fakeModel = new BalloonModel();
            fakeModel.Color.Value = color.Name;

            for (var i = 0; i < missing; i++)
            {
                hitPublisher.Publish(new BalloonHitMessage(fakeModel, Vector3.zero, Vector3.zero));
            }
        }
    }
}
#endif
