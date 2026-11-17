using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.SceneLight;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    // What the CPU-side readers (ISceneLightRuntime) see moves in steps too small to see, so a light that
    // turns a hair every frame doesn't make each of them redo its work every frame.
    [TestFixture]
    public class TimeOfDayServiceTests
    {
        private static readonly Vector2 Rest = new(1f, 0f);

        private ISceneLightSettings _lightSettings;
        private Color _color;
        private TimeOfDayService _service;

        [SetUp]
        public void SetUp()
        {
            _color = new Color(0.5f, 0.5f, 0.5f, 1f);
            _lightSettings = Substitute.For<ISceneLightSettings>();
            _lightSettings.LightDirection.Returns(Rest);
            _lightSettings.EvaluateColor(Arg.Any<Vector2>()).Returns(_ => _color);
            _service = new TimeOfDayService(_lightSettings, Substitute.For<ITimeOfDaySettings>());
        }

        [Test]
        public void Readers_StartAtTheAuthoredLight()
        {
            Assert.AreEqual(Rest, _service.CurrentDirection);
            Assert.AreEqual(_color, _service.CurrentColor);
        }

        [Test]
        public void Readers_ATurnUnderTheStep_SeeNoChange()
        {
            _service.SetDirection(At(TimeOfDayService.ReaderStepDegrees * 0.5f));

            Assert.AreEqual(Rest, _service.CurrentDirection);
        }

        [Test]
        public void Readers_SmallTurnsThatAddUpToTheStep_SeeTheNewDirection()
        {
            var step = TimeOfDayService.ReaderStepDegrees * 0.4f;
            _service.SetDirection(At(step));
            _service.SetDirection(At(step * 2f));
            Assert.AreEqual(Rest, _service.CurrentDirection, "0.8 of a step so far");

            _service.SetDirection(At(step * 3f));

            Assert.AreEqual(At(step * 3f), _service.CurrentDirection);
        }

        [Test]
        public void Readers_AColourChangeUnderHalfAByte_SeeNoChange()
        {
            var before = _color;
            _color.r += TimeOfDayService.ReaderColorStep * 0.5f;

            _service.SetDirection(At(0.01f));

            Assert.AreEqual(before, _service.CurrentColor);
        }

        [Test]
        public void Readers_AColourChangeOfHalfAByte_SeeItWithItsDirection_EvenUnderTheAngleStep()
        {
            _color.b += TimeOfDayService.ReaderColorStep * 1.01f;

            _service.SetDirection(At(0.01f));

            Assert.AreEqual(_color, _service.CurrentColor);
            Assert.AreEqual(At(0.01f), _service.CurrentDirection);
        }

        private static Vector2 At(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
