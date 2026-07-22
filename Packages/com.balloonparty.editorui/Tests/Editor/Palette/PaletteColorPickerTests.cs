using BalloonParty.EditorUI.Palette;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Palette
{
    [TestFixture]
    public class PaletteColorPickerTests
    {
        [Test]
        public void GetSelectedColor_NullPalette_ReturnsWhite()
        {
            var picker = new PaletteColorPicker();

            var color = picker.GetSelectedColor(null);

            Assert.That(color, Is.EqualTo(Color.white));
        }

        [Test]
        public void GetSelectedColor_EmptyPalette_ReturnsWhite()
        {
            var palette = Substitute.For<IColorPalette>();
            palette.Count.Returns(0);
            var picker = new PaletteColorPicker();

            var color = picker.GetSelectedColor(palette);

            Assert.That(color, Is.EqualTo(Color.white));
        }

        [Test]
        public void GetSelectedColor_ValidPalette_ReturnsSelectedColor()
        {
            var palette = Substitute.For<IColorPalette>();
            palette.Count.Returns(3);
            palette.GetColor(1).Returns(new Color(0.1f, 0.2f, 0.3f, 1f));
            var picker = new PaletteColorPicker
            {
                SelectedIndex = 1
            };

            var color = picker.GetSelectedColor(palette);

            Assert.That(color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 1f)));
        }

        [Test]
        public void GetSelectedColor_IndexOutOfBounds_ClampsToLastPaletteEntry()
        {
            var palette = Substitute.For<IColorPalette>();
            palette.Count.Returns(3);
            palette.GetColor(2).Returns(new Color(0.7f, 0.6f, 0.5f, 1f));
            var picker = new PaletteColorPicker
            {
                SelectedIndex = 10
            };

            var color = picker.GetSelectedColor(palette);

            Assert.That(color, Is.EqualTo(new Color(0.7f, 0.6f, 0.5f, 1f)));
        }
    }
}
