using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Scenario;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>The one place a balloon pop is presented — a palette-colored disturbance stamp, a rainbow
    /// key-light flash, tough smoke, and the hit VFX — shared by projectile pops, board clears, and the
    /// overflow drain so every path looks identical.</summary>
    internal sealed class BalloonPopPresenter
    {
        private readonly IGamePalette _palette;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly SmokeFieldService _smokeField;
        private readonly SceneLightFieldService _sceneLightField;
        private readonly IBalloonsConfiguration _balloonsConfig;

        [Inject]
        internal BalloonPopPresenter(
            IGamePalette palette,
            DisturbanceFieldService disturbanceField,
            SmokeFieldService smokeField,
            SceneLightFieldService sceneLightField,
            IBalloonsConfiguration balloonsConfig)
        {
            _palette = palette;
            _disturbanceField = disturbanceField;
            _smokeField = smokeField;
            _sceneLightField = sceneLightField;
            _balloonsConfig = balloonsConfig;
        }

        // vfxParent lets the burst ride a moving transform (e.g. a level transition) instead of firing in place.
        public void Present(IBalloonModel model, Vector3 worldPos, BalloonView view, Transform vfxParent = null)
        {
            var colorId = model.GetPopColorId();

            _disturbanceField.Stamp(
                StampSource.BalloonPop, worldPos, Vector2.zero,
                paletteIndex: _palette.PaletteIndexOf(colorId));

            // A rainbow is a wildcard with no single colour, so it flashes a neutral key-light burst — a beat
            // of light to sell the whole-palette payout. Radius/intensity/duration authored on the config.
            if (_palette.IsRainbow(colorId))
            {
                _sceneLightField.Flash(
                    worldPos, _balloonsConfig.RainbowPopFlashRadius,
                    _balloonsConfig.RainbowPopFlashIntensity, _balloonsConfig.RainbowPopFlashSeconds);
            }

            if (model is ToughBalloonModel)
            {
                _smokeField.Paint(PaintSource.ToughPop, worldPos);
            }

            view.PlayHitVfxForOutcome(HitOutcome.Pop, vfxParent);
        }
    }
}
