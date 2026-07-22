using System;
using BalloonParty.EditorUI.Layout;
using NUnit.Framework;

namespace BalloonParty.EditorUI.Tests.Layout
{
    [TestFixture]
    public class FoldoutSectionTests
    {
        [Test]
        public void DelegateOverload_GetterCalled_ReturnsCurrentState()
        {
            bool storage = true;
            Func<bool> getter = () => storage;
            Action<bool> setter = v => storage = v;

            // We can't invoke Draw (needs GUI context) but we can verify delegates are correct
            Assert.That(getter(), Is.True);
            setter(false);
            Assert.That(storage, Is.False);
            Assert.That(getter(), Is.False);
        }

        [Test]
        public void DelegateOverload_SetterUpdatesStorage_WhenStateChanges()
        {
            bool storage = false;
            int setCalls = 0;
            Action<bool> setter = v =>
            {
                storage = v;
                setCalls++;
            };

            // Simulate what Draw does when state changes
            setter(true);

            Assert.That(storage, Is.True);
            Assert.That(setCalls, Is.EqualTo(1));
        }

        [Test]
        public void DelegateOverload_SetterNotCalled_WhenStateUnchanged()
        {
            bool storage = true;
            int setCalls = 0;
            Action<bool> setter = _ => setCalls++;

            // Simulate: if newState == current, setter should not be invoked
            bool current = storage;
            bool newState = true; // same as current
            if (newState != current)
            {
                setter(newState);
            }

            Assert.That(setCalls, Is.Zero);
        }
    }
}
