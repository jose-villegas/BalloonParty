using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     A colourable balloon that starts in rainbow mode and pushes the level's allowed colours into
    ///     the banded shader — see <see cref="BalloonView" /> for the material swap that makes it visible.
    /// </summary>
    internal class RainbowBalloonVariant : ColorableBalloonVariant, IBalloonViewBinding
    {
        private static readonly int Color0Id = Shader.PropertyToID("_Color0");
        private static readonly int Color1Id = Shader.PropertyToID("_Color1");
        private static readonly int Color2Id = Shader.PropertyToID("_Color2");
        private static readonly int Color3Id = Shader.PropertyToID("_Color3");
        private static readonly int BandCountId = Shader.PropertyToID("_BandCount");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

        [SerializeField] private SpriteRenderer _renderer;

        // Palette is inherited from ColorableBalloonVariant (protected) — a second [Inject] field of
        // the same type here would make VContainer's injector throw "Duplicate injection found".
        [Inject] private IActiveLevelParameters _levelParams;
        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;

        private MaterialPropertyBlock _block;
        private float _timeOffset;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        public override void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask)
        {
            // Deliberately skip base.Initialize — a rainbow has no concrete colour. The reserved
            // wildcard id is the sole marker of rainbow identity; colour interactions detect it via
            // IGamePalette.IsRainbow rather than reading an arbitrary spawn colour.
            if (model is IPaintable colorable)
            {
                colorable.Color.Value = GamePalette.RainbowColorId;
            }

            _timeOffset = Random.Range(0f, 100f);
            PushBands();
        }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            PushBands();

            // LevelDifficultyResolver also reacts to this message to re-resolve Current, and MessagePipe
            // doesn't guarantee subscriber order — defer a frame so the new allowed set has landed.
            _levelUpSubscriber
                .Subscribe(_ => RepushBandsNextFrame().Forget())
                .AddTo(disposables);
        }

        private async UniTaskVoid RepushBandsNextFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);

            if (this == null)
            {
                return;
            }

            PushBands();
        }

        private void PushBands()
        {
            if (_renderer == null)
            {
                return;
            }

            var colors = Palette.ColorNamesForMask(_levelParams.Current.AllowedColorsMask);

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(Color0Id, ColorAt(colors, 0));
            _block.SetColor(Color1Id, ColorAt(colors, 1));
            _block.SetColor(Color2Id, ColorAt(colors, 2));
            _block.SetColor(Color3Id, ColorAt(colors, 3));
            _block.SetFloat(BandCountId, Mathf.Max(1, colors.Count));
            _block.SetFloat(TimeOffsetId, _timeOffset);
            _renderer.SetPropertyBlock(_block);
        }

        // Clamps to the last allowed colour when fewer than 4 are unlocked — harmless either way, since
        // the shader's _BandCount already excludes unused slots from the cycle.
        private Color ColorAt(IReadOnlyList<string> colors, int index)
        {
            if (colors == null || colors.Count == 0)
            {
                return Color.white;
            }

            return Palette.GetColor(colors[Mathf.Clamp(index, 0, colors.Count - 1)]);
        }
    }
}
