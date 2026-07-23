using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Shared context passed to <see cref="IEffectPreviewModule.Start" /> when the user clicks Play.
    /// </summary>
    internal sealed class EffectPreviewContext
    {
        public Color Tint;
        public ItemSettings Settings;
        public SlotGridConfig GameConfig;
    }
}
