using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
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
        [Inject] private DisturbanceFieldService _disturbanceField;

        private readonly List<int> _colorIndices = new();

        private MaterialPropertyBlock _block;
        private float _timeOffset;
        private int _colorCursor;

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

            _timeOffset = UnityEngine.Random.Range(0f, 100f);
            PushBands();
        }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            PushBands();
            RebuildColors();

            // One colour-only stamp (no force — R stays at rest) tags nearby specks with the next
            // available colour each pulse; the RainbowColor profile's Interval paces how fast it cycles.
            _disturbanceField.StartPulse(StampSource.RainbowColor, EmitColor).AddTo(disposables);
        }

        private void RebuildColors()
        {
            _colorIndices.Clear();
            var colors = Palette.ColorNamesForMask(_levelParams.Current.AllowedColorsMask);
            if (colors == null)
            {
                return;
            }

            foreach (var color in colors)
            {
                var index = Palette.PaletteIndexOf(color);
                if (index >= 0)
                {
                    _colorIndices.Add(index);
                }
            }
        }

        // Tags nearby specks with the next available colour so they cycle the palette. Colour-only
        // (the RainbowColor profile authors Strength 0), so it never pushes specks — R stays at rest.
        private void EmitColor()
        {
            if (this == null || _colorIndices.Count == 0)
            {
                return;
            }

            var index = _colorIndices[_colorCursor % _colorIndices.Count];
            _colorCursor++;
            _disturbanceField.Stamp(StampSource.RainbowColor, transform.position, Vector2.zero, index);
        }

        /// <summary>Pushes the banded colours for an explicit palette + allowed-colours mask — the seam the
        /// editor preview uses (edit mode has no DI, so it feeds the palette asset directly).</summary>
        internal void PushBands(IGamePalette palette, int allowedColorsMask)
        {
            if (_renderer == null || palette == null)
            {
                return;
            }

            // Edit-mode preview runs before Awake.
            _block ??= new MaterialPropertyBlock();

            var colors = palette.ColorNamesForMask(allowedColorsMask);

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(Color0Id, ColorAt(palette, colors, 0));
            _block.SetColor(Color1Id, ColorAt(palette, colors, 1));
            _block.SetColor(Color2Id, ColorAt(palette, colors, 2));
            _block.SetColor(Color3Id, ColorAt(palette, colors, 3));
            _block.SetFloat(BandCountId, Mathf.Max(1, colors.Count));
            _block.SetFloat(TimeOffsetId, _timeOffset);
            _renderer.SetPropertyBlock(_block);
        }

        private void PushBands()
        {
            PushBands(Palette, _levelParams.Current.AllowedColorsMask);
        }

        // Clamps to the last allowed colour when fewer than 4 are unlocked — harmless either way, since
        // the shader's _BandCount already excludes unused slots from the cycle.
        private static Color ColorAt(IGamePalette palette, IReadOnlyList<string> colors, int index)
        {
            if (colors == null || colors.Count == 0)
            {
                return Color.white;
            }

            return palette.GetColor(colors[Mathf.Clamp(index, 0, colors.Count - 1)]);
        }
    }
}
