using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item.Paint;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class PaintItemHandlerTests
    {
        private SlotGrid _grid;
        private GamePalette _palette;
        private ItemConfiguration _itemConfig;
        private PaintItemHandler _handler;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(gameConfig);

            _palette = ScriptableObject.CreateInstance<GamePalette>();
            var colors = new[]
            {
                CreatePaletteEntry("Red", Color.red),
                CreatePaletteEntry("Blue", Color.blue)
            };
            SetField(_palette, "_colors", colors);

            _itemConfig = ScriptableObject.CreateInstance<ItemConfiguration>();
            var paintSettings = CreateItemSettings(ItemType.Paint);
            SetField(_itemConfig, "_items", new List<ItemSettings> { paintSettings });

            _handler = new PaintItemHandler(
                _palette,
                _itemConfig,
                _grid,
                new PoolManager());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_palette);
            Object.DestroyImmediate(_itemConfig);
        }

        [Test]
        public void Activate_PaintsNeighborsToSourceColor()
        {
            var source = PlaceBalloon(2, 2, "Red");
            var neighbor = PlaceBalloon(1, 2, "Blue");

            _handler.Setup(source, Vector3.zero);
            _handler.Activate();

            Assert.AreEqual("Red", neighbor.Color.Value);
        }

        [Test]
        public void Activate_SkipsSameColorNeighbors()
        {
            var source = PlaceBalloon(2, 2, "Red");
            var sameColor = PlaceBalloon(1, 2, "Red");

            _handler.Setup(source, Vector3.zero);
            _handler.Activate();

            Assert.AreEqual("Red", sameColor.Color.Value);
        }

        [Test]
        public void Activate_SkipsNonPaintableNeighbors()
        {
            var source = PlaceBalloon(2, 2, "Red");
            var tough = PlaceToughBalloon(1, 2);

            _handler.Setup(source, Vector3.zero);
            _handler.Activate();

            Assert.IsFalse(tough is IHasWriteableColor);
        }        [Test]
        public void Activate_EmptyColor_DoesNothing()
        {
            var source = PlaceBalloon(2, 2, "");
            var neighbor = PlaceBalloon(1, 2, "Blue");

            _handler.Setup(source, Vector3.zero);
            _handler.Activate();

            Assert.AreEqual("Blue", neighbor.Color.Value);
        }

        [Test]
        public void Activate_NoNeighbors_DoesNotThrow()
        {
            var source = PlaceBalloon(0, 0, "Red");

            _handler.Setup(source, Vector3.zero);

            Assert.DoesNotThrow(() => _handler.Activate());
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

