using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Shared context passed to <see cref="IEffectPreviewModule.Start" />
    ///     when the user clicks Play. Contains everything the module needs
    ///     from the player's config caches and color picker.
    /// </summary>
    internal sealed class EffectPreviewContext
    {
        public Color Tint;
        public ItemSettings Settings;
        public GameConfiguration GameConfig;
    }
}
