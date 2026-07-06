using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Item.Paint;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class PaintItemHandlerTests
    {
        private SlotGrid _grid;
        private PaintItemHandler _handler;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(gameConfig, new BalancePathHolder());

            var palette = Substitute.For<IGamePalette>();
            var colors = new List<PaletteEntry>
            {
                CreatePaletteEntry("Red", Color.red),
                CreatePaletteEntry("Blue", Color.blue)
            };
            palette.Colors.Returns(colors);

            var itemConfig = Substitute.For<IItemConfiguration>();
            var paintSettings = CreateItemSettings(ItemType.Paint);
            // A blob radius large enough that the whole aimed triangle is covered, so painting is decided
            // by aim/colour rather than the exact packing lattice.
            SetField(paintSettings.Paint, "_spreadBlobRadius", 5f);
            itemConfig[ItemType.Paint].Returns(paintSettings);
            itemConfig.Items.Returns(new List<ItemSettings> { paintSettings });

            var disturbanceSettings = Substitute.For<IDisturbanceFieldSettings>();
            var displayConfig = Substitute.For<IGameDisplayConfiguration>();

            _handler = new PaintItemHandler(
                palette,
                itemConfig,
                _grid,
                new PoolManager(),
                new DisturbanceFieldService(disturbanceSettings, displayConfig, new ImpactEventBus()));
        }

        [Test]
        public void Activate_PaintsCoveredTargetsToSourceColor()
        {
            var source = PlaceBalloon(2, 2, "Red");
            var target = PlaceBalloon(1, 2, "Blue");

            _handler.Activate(HitToward(source, new Vector2Int(2, 2), new Vector2Int(1, 2)));

            Assert.AreEqual("Red", target.Color.Value);
        }

        [Test]
        public void Activate_SkipsSameColorTargets()
        {
            var source = PlaceBalloon(2, 2, "Red");
            var sameColor = PlaceBalloon(1, 2, "Red");

            _handler.Activate(HitToward(source, new Vector2Int(2, 2), new Vector2Int(1, 2)));

            Assert.AreEqual("Red", sameColor.Color.Value);
        }

        [Test]
        public void Activate_SkipsNonPaintableTargets()
        {
            var source = PlaceBalloon(2, 2, "Red");
            var tough = PlaceToughBalloon(1, 2);

            Assert.DoesNotThrow(() => _handler.Activate(HitToward(source, new Vector2Int(2, 2), new Vector2Int(1, 2))));
            Assert.IsFalse(tough is IPaintable);
        }

        [Test]
        public void Activate_EmptyColor_DoesNothing()
        {
            var source = PlaceBalloon(2, 2, "");
            var target = PlaceBalloon(1, 2, "Blue");

            _handler.Activate(HitToward(source, new Vector2Int(2, 2), new Vector2Int(1, 2)));

            Assert.AreEqual("Blue", target.Color.Value);
        }

        [Test]
        public void Activate_NoTargets_DoesNotThrow()
        {
            var source = PlaceBalloon(0, 0, "Red");

            Assert.DoesNotThrow(() => _handler.Activate(HitToward(source, new Vector2Int(0, 0), new Vector2Int(1, 0))));
        }

        // Hits at the source slot's world position, aiming the triangle down the axis toward another
        // slot — so a balloon at that slot lands on the triangle's median and gets painted.
        private ItemActivationContext HitToward(IBalloonModel source, Vector2Int from, Vector2Int toward)
        {
            var hit = _grid.IndexToWorldPosition(from);
            var direction = _grid.IndexToWorldPosition(toward) - hit;
            return new ItemActivationContext(source, hit, direction);
        }

        private BalloonModel PlaceBalloon(int col, int row, string color)
        {
            var model = new BalloonModel();
            model.Color.Value = color;
            _grid.Place(model, null, new Vector2Int(col, row));
            return model;
        }

        private ToughBalloonModel PlaceToughBalloon(int col, int row)
        {
            var model = new ToughBalloonModel(new BalloonModelConfig());
            _grid.Place(model, null, new Vector2Int(col, row));
            return model;
        }

        private static ItemSettings CreateItemSettings(ItemType type)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", type);
            SetField(settings, "_turnCheckEvery", 0);
            SetField(settings, "_weight", 0f);
            SetField(settings, "_maximumAllowed", 0);
            return settings;
        }

        private static PaletteEntry CreatePaletteEntry(string name, Color color)
        {
            var entry = new PaletteEntry();
            SetField(entry, "_name", name);
            SetField(entry, "_color", color);
            return entry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
