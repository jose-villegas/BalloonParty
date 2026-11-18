using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     The nuke's fuse on its body (PLAN-NukeBalloon). The body shader draws the LED counter and the lamp from what
    ///     is pushed here. While it waits the LED shows the hits it still takes, in green (<c>_FuseDigit</c>), each
    ///     dent flashing it (<c>_FuseBeat</c>). The hit that arms it turns the LED to a red heart that flashes, the lamp
    ///     with it (<c>_FuseHeart</c>), for the fuse's <see cref="INukeFuseSettings.HeartSeconds" />. When the count
    ///     starts the LED shows the seconds left in red (<c>_FuseArmed</c>), the lamp blinking with each beep, and
    ///     the seams and the god rays behind it turn from green to red (<see cref="_armedColorId" />).
    /// </summary>
    internal class NukeFuseView : MonoBehaviour, IBalloonViewBinding
    {
        private static readonly int FuseArmedId = Shader.PropertyToID("_FuseArmed");
        private static readonly int FuseDigitId = Shader.PropertyToID("_FuseDigit");
        private static readonly int FuseBeatId = Shader.PropertyToID("_FuseBeat");
        private static readonly int FuseHeartId = Shader.PropertyToID("_FuseHeart");

        [Tooltip("The nuke's body, whose material (BalloonParty/Balloon/NukeBalloon) draws the counter and the lamp.")]
        [SerializeField] private Renderer _body;

        [Tooltip("The god-ray quad, whose colour turns to the armed entry once the count runs. GodRaysLook sets its " +
                 "unarmed colour on every spawn.")]
        [SerializeField] private SpriteRenderer _rays;

        [Tooltip("The palette entry tinting the rays once armed; its alpha is their strength.")]
        [PaletteColorName] [SerializeField] private string _armedColorId = GamePalette.NukeArmedRaysColorId;

        [Inject] private IGamePalette _palette;

        private MaterialPropertyBlock _block;
        private bool _counting;

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (model is not NukeBalloonModel nuke || _body == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            // A pooled body may carry the last nuke's fuse: start from the unarmed look.
            IHasDurability durability = nuke;
            _counting = false;
            _body.SetFloatAndApply(_block, FuseArmedId, 0f);
            _body.SetFloatAndApply(_block, FuseHeartId, 0f);
            _body.SetFloatAndApply(_block, FuseDigitId, durability.HitsRemaining.Value);
            _body.SetFloatAndApply(_block, FuseBeatId, -100f);

            // The first value is the one shown above; arming takes the hits to 0, which the heart shows instead.
            durability.HitsRemaining.Skip(1).Where(hits => hits > 0).Subscribe(OnDent).AddTo(disposables);
            nuke.Armed.Where(armed => armed).Subscribe(_ => OnArmed()).AddTo(disposables);
            nuke.FuseSecondsLeft.Where(seconds => seconds > 0).Subscribe(OnCount).AddTo(disposables);
        }

        private void OnDent(int hitsLeft)
        {
            _body.SetFloatAndApply(_block, FuseDigitId, hitsLeft);
            _body.SetFloatAndApply(_block, FuseBeatId, Time.timeSinceLevelLoad);
        }

        // The heart flashes from this moment (the shader times it from the beat) until the count starts.
        private void OnArmed()
        {
            _body.SetFloatAndApply(_block, FuseHeartId, 1f);
            _body.SetFloatAndApply(_block, FuseBeatId, Time.timeSinceLevelLoad);
        }

        private void StartCounting()
        {
            _counting = true;
            _body.SetFloatAndApply(_block, FuseHeartId, 0f);
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
            if (!_counting)
            {
                StartCounting();
            }

            _body.SetFloatAndApply(_block, FuseDigitId, secondsLeft);
            _body.SetFloatAndApply(_block, FuseBeatId, Time.timeSinceLevelLoad);
        }
    }
}
