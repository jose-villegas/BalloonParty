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
    ///     room between the lowest point of everything of Play's and the highest of everything of Continue's, its
    ///     shadow included: that shifts with the light, and Play's ribbon is what it would fall on.
    ///     <para>
    ///         Kept quick on purpose: the rooms are read as numbers, and the one simulation is a few seconds long.
    ///         The fixtures that run after this one read the Editor's frame time, and a test that holds a frame for
    ///         seconds fails them.
    ///     </para>
    /// </summary>
    [TestFixture]
    public class LaunchBalloonsTests
    {
        private const string ScenePath = "Assets/Scenes/Launcher.unity";
        private const float Frame = 1f / 60f;

        // The room that must be left between the two wherever each is in its room, in canvas units. The scene asks
        // for 16; what a swinging ribbon dips below its hang at rest comes out of that.
        private const float LeastRoom = 12f;

        // How far below its hang at rest a swinging ribbon may reach. Measured over ten minutes it is under 1.
        private const float MostDip = 2f;

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
        public void OnEveryScreen_PlaysRoomKeepsAllOfItAboveAllOfContinue(float aspect)
        {
            var area = CanvasOn(aspect);
            var play = _play.Reach;
            var other = _continue.Reach;
            var playRoom = _play.RoomOn(area);
            var otherRoom = _continue.RoomOn(area);

            // Play at the bottom of its room, Continue at the top of its own: as near as the two can come.
            var playLowest = playRoom.yMin - play.Down;
            var otherHighest = otherRoom.yMax + Mathf.Max(other.Up, other.Shadow);
            Assert.That(playLowest - otherHighest, Is.GreaterThanOrEqualTo(LeastRoom + MostDip),
                "between the bottom of Play's ribbon and the top of Continue's balloon and shadow");

            // Play's shadow is drawn under all of Continue's, but must fall short of Continue's line and ribbon all
            // the same: the draw order is the scene's to change.
            var otherLineTop = otherRoom.yMax + _continue.KnotFromCentre.y;
            Assert.That(playRoom.yMin - play.Shadow - otherLineTop, Is.GreaterThanOrEqualTo(LeastRoom),
                "between Play's shadow and where Continue's line is tied on");

            Assert.That(playRoom.height, Is.GreaterThan(20f), "Play has travel up and down");
            Assert.That(otherRoom.height, Is.GreaterThan(20f), "Continue has travel up and down");
            Assert.That(playRoom.yMax + play.Up, Is.LessThanOrEqualTo(area.yMax), "Play stays on the screen");
            Assert.That(otherRoom.yMin - other.Down, Is.GreaterThanOrEqualTo(area.yMin), "Continue's ribbon stays on it");
        }

        // The rooms count on a ribbon hanging no lower as it swings than at rest, and on nothing of a balloon's
        // rising over its body. A few seconds of the two drifting shows a change to the cloth that breaks either.
        [Test]
        public void Drifting_ARibbonHangsNoLowerThanItsRoomAllowsFor_AndTheTwoStayApart()
        {
            var area = CanvasOn(9f / 16f);
            var play = _play.NewDrift(area, 3);
            var other = _continue.NewDrift(area, 53);
            Assert.That(play.Cloth.Length, Is.GreaterThan(30f), "Play's writing was laid out on its ribbon");
            Assert.That(other.Cloth.Length, Is.GreaterThan(30f), "Continue's writing was laid out on its ribbon");
            var playReach = _play.Reach;
            var otherReach = _continue.Reach;
            var playKnot = _play.KnotFromCentre;
            var otherKnot = _continue.KnotFromCentre;
            var playStart = play.Centre;
            var playFurthest = 0f;

            for (var f = 0; f < 60 * 6; f++)
            {
                play.Step(Frame, playKnot);
                other.Step(Frame, otherKnot);
                playFurthest = Mathf.Max(playFurthest, Vector2.Distance(play.Centre, playStart));

                Extent(play.Cloth, out var playBottom, out _);
                Extent(other.Cloth, out var otherBottom, out var otherTop);
                var worst = Mathf.Max(
                    play.Centre.y - playBottom - playReach.Down, other.Centre.y - otherBottom - otherReach.Down);
                if (worst > MostDip)
                {
                    Assert.Fail($"{f * Frame:0.0} s: a ribbon hangs {worst:0.0} lower than its room allows for");
                }

                if (otherTop > other.Centre.y + otherReach.Radius)
                {
                    Assert.Fail($"{f * Frame:0.0} s: Continue's line or ribbon is over the top of its balloon");
                }

                var room = playBottom - (other.Centre.y + otherReach.Radius);
                if (room < LeastRoom)
                {
                    Assert.Fail($"{f * Frame:0.0} s: {room:0.0} between Play's ribbon and Continue's balloon");
                }
            }

            Assert.That(playFurthest, Is.GreaterThan(10f), "Play drifted");
        }

        [Test]
        public void SharingAScreen_TheUpperRoomAndTheLowerRoom_LeaveTheGapBetweenTheTwoBalloons()
        {
            var upper = new BalloonReach(53f, 54.5f, 91.5f, 110f);
            var lower = new BalloonReach(19f, 20.5f, 60f, 20f);

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
        public void SharingAScreen_TheLowerBalloonsShadow_StaysTheGapUnderTheUpperBalloon()
        {
            var upper = new BalloonReach(53f, 54.5f, 91.5f, 110f);
            var lower = new BalloonReach(19f, 20.5f, 60f, 40f);
            var area = new Rect(-110f, -200f, 220f, 400f);

            var above = upper.RoomAbove(lower, area, 4f, 16f);
            var below = lower.RoomBelow(upper, area, 4f, 16f);

            Assert.That(above.yMin - upper.Down - (below.yMax + lower.Shadow), Is.EqualTo(16f).Within(0.01f));
            Assert.That(below.height, Is.EqualTo(above.height).Within(0.01f), "the travel is still split evenly");
        }

        [Test]
        public void AloneOnTheScreen_ABalloonHasItsWholeHeight()
        {
            var reach = new BalloonReach(53f, 54.5f, 91.5f, 110f);
            var area = new Rect(-110f, -200f, 220f, 400f);

            var room = reach.RoomAlone(area, 4f);

            Assert.That(room.yMax, Is.EqualTo(200f - 4f - 54.5f).Within(0.01f));
            Assert.That(room.yMin, Is.EqualTo(-200f + 4f + 91.5f).Within(0.01f));
        }

        [Test]
        public void OnAScreenTooShortForBoth_NeitherRoomHasTravel()
        {
            var upper = new BalloonReach(53f, 54.5f, 91.5f, 110f);
            var lower = new BalloonReach(19f, 20.5f, 60f, 20f);
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
