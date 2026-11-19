using BalloonParty.Game;
using BalloonParty.UI.ClothRibbon;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     The launch screen's Play and Continue balloons, as the Launcher scene ships them: neither balloon nor its
    ///     ribbon may ever cover the other's ribbon. They share the screen one above the other, so the measure is the
    ///     room between the lowest point of everything of Play's and the highest of everything of Continue's.
    /// </summary>
    [TestFixture]
    public class LaunchBalloonsTests
    {
        private const string ScenePath = "Assets/Scenes/Launcher.unity";
        private const float Frame = 1f / 60f;

        // The room that must be left between the two at every frame, in canvas units. The scene asks for 16; what a
        // swinging ribbon dips below its hang at rest comes out of that.
        private const float LeastRoom = 12f;

        private Scene _scene;
        private LaunchBalloonView _play;
        private LaunchBalloonView _continue;

        [OneTimeSetUp]
        public void OpenTheLauncher()
        {
            // Loading the scene outside Play mode, the in-game debug console's input field (a package's) throws in
            // its constructor and logs it: nothing of the balloons', and it would fail the fixture.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                _scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            foreach (var root in _scene.GetRootGameObjects())
            {
                foreach (var view in root.GetComponentsInChildren<LaunchBalloonView>(true))
                {
                    if (view.GetComponent<LaunchContinueView>() != null)
                    {
                        _continue = view;
                    }
                    else
                    {
                        _play = view;
                    }
                }
            }

            Assert.That(_play, Is.Not.Null, "the Launcher scene has Play's LaunchBalloonView");
            Assert.That(_continue, Is.Not.Null, "the Launcher scene has Continue's LaunchBalloonView");

            // Continue ships hidden until a run is saved; the case that matters is both showing.
            _continue.gameObject.SetActive(true);
        }

        [OneTimeTearDown]
        public void CloseTheLauncher()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        // Width over height, portrait: tall phones, a 9:16 phone, a 10:16 tablet and a 3:4 tablet.
        [TestCase(9f / 21f)]
        [TestCase(9f / 20f)]
        [TestCase(9f / 16f)]
        [TestCase(10f / 16f)]
        [TestCase(3f / 4f)]
        public void OnEveryScreen_AllOfPlayStaysAboveAllOfContinue_WhereverTheyDrift(float aspect)
        {
            var area = CanvasOn(aspect);
            var playRadius = _play.Reach.Radius;
            var otherRadius = _continue.Reach.Radius;
            var playKnot = _play.KnotFromCentre;
            var otherKnot = _continue.KnotFromCentre;
            var least = float.PositiveInfinity;

            for (var seed = 0; seed < 2; seed++)
            {
                var play = _play.NewDrift(area, seed);
                var other = _continue.NewDrift(area, seed + 50);
                Assert.That(play.Cloth.Length, Is.GreaterThan(30f), "Play's writing was laid out on its ribbon");
                Assert.That(other.Cloth.Length, Is.GreaterThan(30f), "Continue's writing was laid out on its ribbon");
                var playWas = play.Centre;
                var otherWas = other.Centre;
                var playTravel = 0f;
                var otherTravel = 0f;

                for (var f = 0; f < 60 * 40; f++)
                {
                    play.Step(Frame, playKnot);
                    other.Step(Frame, otherKnot);
                    playTravel += Vector2.Distance(play.Centre, playWas);
                    otherTravel += Vector2.Distance(other.Centre, otherWas);
                    playWas = play.Centre;
                    otherWas = other.Centre;

                    var room = Lowest(play, playRadius) - Highest(other, otherRadius);
                    least = Mathf.Min(least, room);
                    if (room < LeastRoom)
                    {
                        Assert.Fail(
                            $"seed {seed}, {f * Frame:0.0} s: {room:0.0} between Play's lowest point and Continue's highest");
                    }
                }

                Assert.That(playTravel, Is.GreaterThan(100f), $"seed {seed}: Play hardly drifted");
                Assert.That(otherTravel, Is.GreaterThan(100f), $"seed {seed}: Continue hardly drifted");
            }

            TestContext.Out.WriteLine(
                $"canvas {area.width:0.0} x {area.height:0.0}: least room between the two {least:0.0}");
        }

        [Test]
        public void SharingAScreen_TheUpperRoomAndTheLowerRoom_LeaveTheGapBetweenTheTwoBalloons()
        {
            var upper = new BalloonReach(53f, 54.5f, 91.5f);
            var lower = new BalloonReach(19f, 20.5f, 60f);

            foreach (var height in new[] { 346f, 380f, 400f, 447f, 458f })
            {
                var area = new Rect(-110f, -height / 2f, 220f, height);
                var above = upper.RoomAbove(lower, area, 4f, 16f);
                var below = lower.RoomBelow(upper, area, 4f, 16f);

                Assert.That(above.yMin - upper.Down - (below.yMax + lower.Up), Is.EqualTo(16f).Within(0.01f),
                    $"height {height}: the upper balloon at its lowest over the lower one at its highest");
                Assert.That(above.yMax + upper.Up, Is.EqualTo(area.yMax - 4f).Within(0.01f), "the top edge's room");
                Assert.That(below.yMin - lower.Down, Is.EqualTo(area.yMin + 4f).Within(0.01f), "the bottom edge's room");
                Assert.That(above.height, Is.GreaterThan(20f), $"height {height}: the upper balloon has travel");
                Assert.That(below.height, Is.EqualTo(above.height).Within(0.01f), "the travel is split evenly");
                Assert.That(above.xMin, Is.EqualTo(area.xMin + 57f).Within(0.01f), "the whole width, less the body");
            }
        }

        [Test]
        public void AloneOnTheScreen_ABalloonHasItsWholeHeight()
        {
            var reach = new BalloonReach(53f, 54.5f, 91.5f);
            var area = new Rect(-110f, -200f, 220f, 400f);

            var room = reach.RoomAlone(area, 4f);

            Assert.That(room.yMax, Is.EqualTo(200f - 4f - 54.5f).Within(0.01f));
            Assert.That(room.yMin, Is.EqualTo(-200f + 4f + 91.5f).Within(0.01f));
        }

        [Test]
        public void OnAScreenTooShortForBoth_NeitherRoomHasTravel()
        {
            var upper = new BalloonReach(53f, 54.5f, 91.5f);
            var lower = new BalloonReach(19f, 20.5f, 60f);
            var area = new Rect(-200f, -100f, 400f, 200f);

            Assert.That(upper.RoomAbove(lower, area, 4f, 16f).height, Is.EqualTo(0f));
            Assert.That(lower.RoomBelow(upper, area, 4f, 16f).height, Is.EqualTo(0f));
        }

        // The launch canvas scales its 225 x 400 reference by width and height alike, so its area is the same on
        // every screen and only its shape follows the screen's.
        private static Rect CanvasOn(float aspect)
        {
            var width = Mathf.Sqrt(225f * 400f * aspect);
            var height = width / aspect;
            return new Rect(-width / 2f, -height / 2f, width, height);
        }

        private static float Lowest(BalloonDrift drift, float radius)
        {
            var lowest = drift.Centre.y - radius;
            Extent(drift.Cloth, out var bottom, out _);
            return Mathf.Min(lowest, bottom);
        }

        private static float Highest(BalloonDrift drift, float radius)
        {
            var highest = drift.Centre.y + radius;
            Extent(drift.Cloth, out _, out var top);
            return Mathf.Max(highest, top);
        }

        private static void Extent(RibbonCloth cloth, out float bottom, out float top)
        {
            bottom = float.PositiveInfinity;
            top = float.NegativeInfinity;
            for (var i = 0; i < cloth.LinePoints; i++)
            {
                var y = cloth.LinePoint(i).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }

            for (var column = 0; column < cloth.Columns; column++)
            {
                for (var row = 0; row < 3; row++)
                {
                    var y = cloth.ClothPoint(column, row).y;
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
        }
    }
}
