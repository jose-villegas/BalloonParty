using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Palette;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     Tints a reward balloon's god rays from its palette entry (the golden balloon's <c>GoldenRays</c>, the
    ///     heart's <c>HeartRays</c>). The rays turn and breathe in their shader (<c>BalloonParty/Sprite/GodRays</c>),
    ///     whose material holds their shape, so nothing here runs per frame and no tween outlives the balloon: they
    ///     hang off the balloon's root, so they scale in with its spawn and go with it when the pool takes it back.
    /// </summary>
    internal class GodRaysLook : MonoBehaviour, IBalloonViewBinding
    {
        [Tooltip("The god-ray quad. Sorted below every balloon and kept out of BalloonView's slot sorting, so " +
                 "the rays show between the balloons around it and never over any of them.")]
        [SerializeField] private SpriteRenderer _rays;

        [Tooltip("The palette entry tinting the rays; its alpha is their strength.")]
        [PaletteColorName] [SerializeField] private string _colorId = GamePalette.GoldenRaysColorId;

        [Inject] private IGamePalette _palette;

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (_rays != null && _palette != null)
            {
                _rays.color = _palette.GetColor(_colorId);
            }
        }
    }
}
