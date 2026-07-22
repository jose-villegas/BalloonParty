using System.Reflection;
using BalloonParty.EditorUI.Utilities;
using NUnit.Framework;

namespace BalloonParty.EditorUI.Tests.Utilities
{
    [TestFixture]
    public class EditorAnimationLoopTests
    {
        private EditorAnimationLoop _loop;

        [SetUp]
        public void SetUp()
        {
            _loop = new EditorAnimationLoop();
        }

        [TearDown]
        public void TearDown()
        {
            _loop.Stop();
        }

        [Test]
        public void Constructor_InitialState_StartsStoppedAndUnpaused()
        {
            Assert.That(_loop.IsPlaying, Is.False);
            Assert.That(_loop.IsPaused, Is.False);
            Assert.That(_loop.TimeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Start_ThenStop_UpdatesPlayingState()
        {
            _loop.Start(_ => true);

            Assert.That(_loop.IsPlaying, Is.True);
            Assert.That(_loop.IsPaused, Is.False);

            _loop.Stop();

            Assert.That(_loop.IsPlaying, Is.False);
            Assert.That(_loop.IsPaused, Is.False);
        }

        [Test]
        public void TogglePause_WhenPlaying_TogglesPausedState()
        {
            _loop.Start(_ => true);

            _loop.TogglePause();
            Assert.That(_loop.IsPaused, Is.True);

            _loop.TogglePause();
            Assert.That(_loop.IsPaused, Is.False);
        }

        [Test]
        public void SimulateTick_CallbackReturnsFalse_StopsLoop()
        {
            float observedDelta = -1f;
            var completionCount = 0;
            _loop.Start(delta =>
            {
                observedDelta = delta;
                return false;
            }, () => completionCount++);

            SimulateTick(_loop, 0.25f);

            Assert.That(observedDelta, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(_loop.IsPlaying, Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [Test]
        public void SimulateTick_WhenPaused_DoesNotInvokeCallback()
        {
            var tickCount = 0;
            _loop.Start(_ =>
            {
                tickCount++;
                return true;
            });
            _loop.TogglePause();

            SimulateTick(_loop, 0.5f);

            Assert.That(tickCount, Is.Zero);
            Assert.That(_loop.IsPlaying, Is.True);
            Assert.That(_loop.IsPaused, Is.True);
        }

        [Test]
        public void SimulateTick_WhenRunning_PassesDeltaThroughToCallback()
        {
            float observedDelta = -1f;
            _loop.Start(delta =>
            {
                observedDelta = delta;
                return true;
            });

            SimulateTick(_loop, 0.75f);

            Assert.That(observedDelta, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(_loop.IsPlaying, Is.True);
        }

        [Test]
        public void Stop_WhenPlaying_InvokesOnComplete()
        {
            var completionCount = 0;
            _loop.Start(_ => true, () => completionCount++);

            _loop.Stop();

            Assert.That(completionCount, Is.EqualTo(1));
        }

        private static void SimulateTick(EditorAnimationLoop loop, float delta)
        {
            var method = typeof(EditorAnimationLoop).GetMethod("SimulateTick", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            method.Invoke(loop, new object[] { delta });
        }
    }
}
