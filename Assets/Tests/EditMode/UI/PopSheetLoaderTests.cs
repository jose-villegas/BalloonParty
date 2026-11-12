using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using BalloonParty.UI.CardReveal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class PopSheetLoaderTests
    {
        private static readonly string[] Paths = { "Pops/pop_00", "Pops/pop_01", "Pops/pop_02" };

        private readonly Dictionary<string, UniTaskCompletionSource<Texture2D>> _pending = new();
        private readonly Dictionary<string, Texture2D> _textures = new();
        private readonly List<string> _loads = new();
        private readonly List<Texture2D> _unloaded = new();
        private PopSheetLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _pending.Clear();
            _textures.Clear();
            _loads.Clear();
            _unloaded.Clear();
            foreach (var path in Paths)
            {
                _textures[path] = new Texture2D(1, 1);
            }

            _loader = new PopSheetLoader(Paths, Load, _unloaded.Add);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var texture in _textures.Values)
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Hold_LoadsThePlayingAndTheNextSheet()
        {
            _loader.Hold(0, 2);

            CollectionAssert.AreEquivalent(new[] { Paths[0], Paths[2] }, _loads);
        }

        [Test]
        public void Hold_None_LoadsNothing()
        {
            _loader.Hold(-1, -1);

            Assert.IsEmpty(_loads);
        }

        [Test]
        public void Hold_ASheetAlreadyHeld_IsNotLoadedAgain()
        {
            _loader.Hold(-1, 1);
            Arrive(Paths[1]);

            _loader.Hold(1, 2);

            CollectionAssert.AreEqual(new[] { Paths[1], Paths[2] }, _loads);
            Assert.IsEmpty(_unloaded);
        }

        [Test]
        public void Hold_LetsTheOthersGo_Unloaded()
        {
            _loader.Hold(0, 1);
            Arrive(Paths[0]);
            Arrive(Paths[1]);

            _loader.Hold(-1, 1);

            CollectionAssert.AreEqual(new[] { _textures[Paths[0]] }, _unloaded);
        }

        [Test]
        public void ASheetLetGoWhileLoading_IsUnloadedAsItArrives()
        {
            _loader.Hold(-1, 0);
            _loader.Hold(-1, 1);
            Assert.IsEmpty(_unloaded, "nothing to unload until it arrives");

            Arrive(Paths[0]);

            CollectionAssert.AreEqual(new[] { _textures[Paths[0]] }, _unloaded);
        }

        [Test]
        public void ASheetWantedAgainWhileLoading_IsKept_AndLoadedOnce()
        {
            _loader.Hold(-1, 0);
            _loader.Hold(-1, 1);
            _loader.Hold(0, 1);

            Arrive(Paths[0]);

            Assert.IsEmpty(_unloaded);
            Assert.AreEqual(1, _loads.FindAll(path => path == Paths[0]).Count,
                "a second load would hand back the same texture, which the first's arrival would unload");
        }

        [Test]
        public void ASheetLetGoOnceArrived_IsLoadedAfreshWhenWantedAgain()
        {
            _loader.Hold(-1, 0);
            Arrive(Paths[0]);
            _loader.Hold(-1, 1);

            _loader.Hold(-1, 0);

            Assert.AreEqual(2, _loads.FindAll(path => path == Paths[0]).Count);
        }

        [Test]
        public void GetAsync_ASheetHeldAhead_IsThereAtOnce()
        {
            _loader.Hold(-1, 2);
            Arrive(Paths[2]);

            var get = _loader.GetAsync(2, CancellationToken.None);

            Assert.AreEqual(UniTaskStatus.Succeeded, get.Status);
            Assert.AreSame(_textures[Paths[2]], get.GetAwaiter().GetResult());
        }

        [Test]
        public void GetAsync_ASheetNotHeld_LoadsIt_AndWaitsForIt()
        {
            var get = _loader.GetAsync(1, CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Pending, get.Status);

            Arrive(Paths[1]);

            Assert.AreEqual(UniTaskStatus.Succeeded, get.Status);
            Assert.AreSame(_textures[Paths[1]], get.GetAwaiter().GetResult());
        }

        [Test]
        public void GetAsync_ASheetStillLoading_KeepsIt_ThoughHoldLetItGo()
        {
            _loader.Hold(-1, 0);
            _loader.Hold(-1, 1);

            var get = _loader.GetAsync(0, CancellationToken.None);
            Arrive(Paths[0]);

            Assert.AreSame(_textures[Paths[0]], get.GetAwaiter().GetResult());
            Assert.IsEmpty(_unloaded);
        }

        [Test]
        public void GetAsync_NoSuchPop_IsNull()
        {
            var get = _loader.GetAsync(Paths.Length, CancellationToken.None);

            Assert.AreEqual(UniTaskStatus.Succeeded, get.Status);
            Assert.IsNull(get.GetAwaiter().GetResult());
            Assert.IsEmpty(_loads);
        }

        [Test]
        public void GetAsync_AMissingSheet_IsNull_AndWarns()
        {
            LogAssert.Expect(LogType.Warning, new Regex("No pop sheet at Resources/Pops/pop_01"));
            var get = _loader.GetAsync(1, CancellationToken.None);

            _pending[Paths[1]].TrySetResult(null);

            Assert.IsNull(get.GetAwaiter().GetResult());
        }

        [Test]
        public void GetAsync_Cancelled_StopsWaiting()
        {
            using var cancel = new CancellationTokenSource();
            var get = _loader.GetAsync(0, cancel.Token);

            cancel.Cancel();

            Assert.AreEqual(UniTaskStatus.Canceled, get.Status);
        }

        [Test]
        public void Dispose_UnloadsEverySheet_EvenOneStillLoading()
        {
            _loader.Hold(0, 1);
            Arrive(Paths[0]);

            _loader.Dispose();
            Arrive(Paths[1]);

            CollectionAssert.AreEquivalent(new[] { _textures[Paths[0]], _textures[Paths[1]] }, _unloaded);
        }

        private UniTask<Texture2D> Load(string path)
        {
            _loads.Add(path);
            var source = new UniTaskCompletionSource<Texture2D>();
            _pending[path] = source;
            return source.Task;
        }

        private void Arrive(string path)
        {
            _pending[path].TrySetResult(_textures[path]);
        }
    }
}
