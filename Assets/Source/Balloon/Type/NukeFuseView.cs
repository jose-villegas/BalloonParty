using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     The nuke's fuse on its body (PLAN-NukeBalloon). The body shader draws the LED counter and the lamp from what
    ///     is pushed here: <c>_FuseArmed</c> (the lamp green or red, and the LED a heart or the count),
    ///     <c>_FuseDigit</c> (the count) and <c>_FuseBeat</c> (when the last count came, so the lamp blinks with the
    ///     beep). Armed, the god rays behind it turn from their green palette entry to <see cref="_armedColorId" />.
    /// </summary>
    internal class NukeFuseView : MonoBehaviour, IBalloonViewBinding
    {
        private static readonly int FuseArmedId = Shader.PropertyToID("_FuseArmed");
        private static readonly int FuseDigitId = Shader.PropertyToID("_FuseDigit");
        private static readonly int FuseBeatId = Shader.PropertyToID("_FuseBeat");

        [Tooltip("The nuke's body, whose material (BalloonParty/Balloon/NukeBalloon) draws the counter and the lamp.")]
        [SerializeField] private Renderer _body;

        [Tooltip("The god-ray quad, whose colour turns to the armed entry once the fuse runs. GodRaysLook sets its " +
                 "unarmed colour on every spawn.")]
        [SerializeField] private SpriteRenderer _rays;

        [Tooltip("The palette entry tinting the rays once armed; its alpha is their strength.")]
        [PaletteColorName] [SerializeField] private string _armedColorId = GamePalette.NukeArmedRaysColorId;

        [Inject] private IGamePalette _palette;
        [Inject] private IBalloonsConfiguration _balloons;

        private MaterialPropertyBlock _block;

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (model is not NukeBalloonModel nuke || _body == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            // A pooled body may carry the last nuke's fuse: start from the unarmed look.
            _body.SetFloatAndApply(_block, FuseArmedId, 0f);
            _body.SetFloatAndApply(_block, FuseDigitId, Mathf.CeilToInt(_balloons.NukeFuse.Seconds));
            _body.SetFloatAndApply(_block, FuseBeatId, -100f);

            nuke.Armed.Where(armed => armed).Subscribe(_ => OnArmed()).AddTo(disposables);
            nuke.FuseSecondsLeft.Where(seconds => seconds > 0).Subscribe(OnCount).AddTo(disposables);
        }

        private void OnArmed()
        {
            _body.SetFloatAndApply(_block, FuseArmedId, 1f);
            if (_rays == null || _palette == null)
            {
                return;
            }

            // Through the rays' look where there is one, so the light they cast turns with them.
            if (_rays.TryGetComponent<GodRaysLook>(out var look))
            {
                look.SetColor(_armedColorId);
            }
            else
            {
                _rays.color = _palette.GetColor(_armedColorId);
            }
        }

        // The shader's _Time.y is the time since the level loaded.
        private void OnCount(int secondsLeft)
        {
            _body.SetFloatAndApply(_block, FuseDigitId, secondsLeft);
            _body.SetFloatAndApply(_block, FuseBeatId, Time.timeSinceLevelLoad);
        }
    }
}
