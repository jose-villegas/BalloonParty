using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.Routing;
using NUnit.Framework;
using UnityEditor;

namespace BalloonParty.Tests.Audio
{
    /// <summary>
    ///     Pins the mushrooms' own bump in the shipped sound bank (<c>Plans/PLAN-MeadowMushrooms.md</c>, "The hit's
    ///     own sound"): the bank is indexed by <see cref="GameSoundId" />, so an id appended without its entry, or
    ///     with an entry that has no clip, plays silence.
    /// </summary>
    [TestFixture]
    public class ShippedMushroomSoundsTests
    {
        private const string BankPath = "Assets/Configuration/SoundBankConfiguration.asset";

        private SoundBankConfiguration _bank;

        [SetUp]
        public void SetUp()
        {
            _bank = AssetDatabase.LoadAssetAtPath<SoundBankConfiguration>(BankPath);
            Assert.IsNotNull(_bank, $"{BankPath} should load");
        }

        [Test]
        public void TheBump_HasAClip()
        {
            Assert.IsTrue(
                _bank.TryGet(MushroomSoundRouter.BounceSound, out var entry) && entry.HasClips,
                "MushroomBounce should have a clip");
        }

        // The router climbs the bump a whole tone a hit, and a ball can bounce several times before one bump has
        // rung out: the entry's own voices are what keeps a run of them in hand.
        [Test]
        public void TheBump_KeepsFourVoices_DoesNotLoop_AndDoesNotWalkTheKey()
        {
            Assert.IsTrue(_bank.TryGet(GameSoundId.MushroomBounce, out var entry));
            Assert.AreEqual(4, entry.MaxConcurrentVoices);
            Assert.IsFalse(entry.Loop);
            Assert.AreEqual(MelodicMode.None, entry.MelodicMode, "the router sets the note");
        }

        // When the bank runs out of voices, a bump goes before a pop does: it sits a little under it.
        [Test]
        public void TheBump_SitsALittleUnderThePop()
        {
            Assert.IsTrue(_bank.TryGet(GameSoundId.MushroomBounce, out var bump));
            Assert.IsTrue(_bank.TryGet(GameSoundId.BalloonPop, out var pop));
            Assert.Less(bump.Priority, pop.Priority);
        }
    }
}
