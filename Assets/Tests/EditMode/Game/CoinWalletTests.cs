using BalloonParty.Game.Coins;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CoinWalletTests
    {
        [Test]
        public void Construct_LoadsTheSavedBalance()
        {
            var wallet = new CoinWallet(new FakeCoinStore(42));

            Assert.AreEqual(42, wallet.Balance.Value);
        }

        [Test]
        public void Construct_NegativeSavedBalance_ClampsToZero()
        {
            var wallet = new CoinWallet(new FakeCoinStore(-5));

            Assert.AreEqual(0, wallet.Balance.Value);
        }

        [Test]
        public void Add_RaisesTheBalance()
        {
            var wallet = new CoinWallet(new FakeCoinStore(10));

            wallet.Add(3);

            Assert.AreEqual(13, wallet.Balance.Value);
        }

        [TestCase(0)]
        [TestCase(-4)]
        public void Add_NonPositiveAmount_IsIgnored(int amount)
        {
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);

            wallet.Add(amount);
            wallet.Save();

            Assert.AreEqual(10, wallet.Balance.Value);
            Assert.AreEqual(0, store.SaveCount, "Nothing changed, so nothing to write.");
        }

        [Test]
        public void TrySpend_Affordable_LowersTheBalance()
        {
            var wallet = new CoinWallet(new FakeCoinStore(10));

            Assert.IsTrue(wallet.TrySpend(4));
            Assert.AreEqual(6, wallet.Balance.Value);
        }

        [Test]
        public void TrySpend_ExactBalance_Succeeds()
        {
            var wallet = new CoinWallet(new FakeCoinStore(10));

            Assert.IsTrue(wallet.TrySpend(10));
            Assert.AreEqual(0, wallet.Balance.Value);
        }

        [Test]
        public void TrySpend_MoreThanTheBalance_FailsAndKeepsTheBalance()
        {
            var wallet = new CoinWallet(new FakeCoinStore(10));

            Assert.IsFalse(wallet.TrySpend(11));
            Assert.AreEqual(10, wallet.Balance.Value);
        }

        [Test]
        public void TrySpend_NegativeAmount_Fails()
        {
            // A negative spend would be a free credit.
            var wallet = new CoinWallet(new FakeCoinStore(10));

            Assert.IsFalse(wallet.TrySpend(-3));
            Assert.AreEqual(10, wallet.Balance.Value);
        }

        [Test]
        public void TrySpend_Zero_SucceedsWithoutMarkingDirty()
        {
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);

            Assert.IsTrue(wallet.TrySpend(0));
            wallet.Save();

            Assert.AreEqual(0, store.SaveCount);
        }

        [Test]
        public void Save_Unchanged_DoesNotWrite()
        {
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);

            wallet.Save();

            Assert.AreEqual(0, store.SaveCount);
        }

        [Test]
        public void Add_DoesNotWriteUntilSaved()
        {
            // PlayerPrefs.Save is slow on mobile, so earning a coin must never write on its own.
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);

            wallet.Add(5);

            Assert.AreEqual(0, store.SaveCount);
        }

        [Test]
        public void Save_AfterAChange_WritesTheBalanceOnce()
        {
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);
            wallet.Add(5);
            wallet.Add(3);

            wallet.Save();
            wallet.Save();

            Assert.AreEqual(1, store.SaveCount, "The second save has nothing new to write.");
            Assert.AreEqual(18, store.Saved);
        }

        [Test]
        public void TrySpend_Succeeding_WritesAtOnce()
        {
            // A spend lost to a crash would refund the purchase, so it does not wait for a save point.
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);
            wallet.Add(5);

            wallet.TrySpend(4);

            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual(11, store.Saved, "The write carries the coins earned before the spend too.");
        }

        [Test]
        public void TrySpend_Failing_DoesNotWrite()
        {
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);

            wallet.TrySpend(11);

            Assert.AreEqual(0, store.SaveCount);
        }

        [Test]
        public void Save_ChangeAfterASave_WritesAgain()
        {
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);
            wallet.Add(1);
            wallet.Save();

            wallet.Add(1);
            wallet.Save();

            Assert.AreEqual(2, store.SaveCount);
            Assert.AreEqual(12, store.Saved);
        }

        [Test]
        public void SaveThenReload_ThroughPlayerPrefs_RestoresTheBalance()
        {
            const string key = "Coins";
            var hadKey = PlayerPrefs.HasKey(key);
            var previous = PlayerPrefs.GetInt(key, 0);
            try
            {
                PlayerPrefs.DeleteKey(key);
                var first = new CoinWallet(new PlayerPrefsCoinStore());
                Assert.AreEqual(0, first.Balance.Value, "No saved key starts at zero.");

                first.Add(7);
                first.TrySpend(2);
                first.Save();

                var second = new CoinWallet(new PlayerPrefsCoinStore());
                Assert.AreEqual(5, second.Balance.Value);
            }
            finally
            {
                if (hadKey)
                {
                    PlayerPrefs.SetInt(key, previous);
                }
                else
                {
                    PlayerPrefs.DeleteKey(key);
                }
            }
        }

        [Test]
        public void Reload_KeepsSavedCoins_AndLosesUnsavedOnes()
        {
            // The save-point contract: coins earned since the last save don't survive a restart.
            var store = new FakeCoinStore(10);
            var wallet = new CoinWallet(store);
            wallet.Add(5);
            wallet.Save();

            wallet.Add(3);

            Assert.AreEqual(15, new CoinWallet(store).Balance.Value);
        }

        // Load returns what was last saved, like the real store, so a reload test means something.
        private sealed class FakeCoinStore : ICoinStore
        {
            public int Saved { get; private set; }
            public int SaveCount { get; private set; }

            public FakeCoinStore(int initial)
            {
                Saved = initial;
            }

            public int Load()
            {
                return Saved;
            }

            public void Save(int balance)
            {
                Saved = balance;
                SaveCount++;
            }
        }
    }
}
