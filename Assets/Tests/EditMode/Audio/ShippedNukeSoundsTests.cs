using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using NUnit.Framework;
using UnityEditor;

namespace BalloonParty.Tests.Audio
{
    /// <summary>
    ///     Pins the nuke's cues in the shipped sound bank (PLAN-NukeBalloon phases 5 and 6): the bank is indexed by
    ///     <see cref="GameSoundId" />, so an id appended without its entry would play silence.
    /// </summary>
    [TestFixture]
    public class ShippedNukeSoundsTests
    {
        private const string BankPath = "Assets/Configuration/SoundBankConfiguration.asset";

        private SoundBankConfiguration _bank;

        [SetUp]
        public void SetUp()
        {
            _bank = AssetDatabase.LoadAssetAtPath<SoundBankConfiguration>(BankPath);
            Assert.IsNotNull(_bank, $"{BankPath} should load");
        }

        [TestCase(nameof(GameSoundId.BalloonDeflectNuke))]
        [TestCase(nameof(GameSoundId.BalloonPopNuke))]
        [TestCase(nameof(GameSoundId.NukeArmed))]
        [TestCase(nameof(GameSoundId.NukeCountdownBeep))]
        [TestCase(nameof(GameSoundId.NukeBreatherWind))]
        public void EachNukeCue_HasAClip(string name)
        {
            var id = System.Enum.Parse<GameSoundId>(name);
            Assert.IsTrue(_bank.TryGet(id, out var entry) && entry.HasClips, $"{id} should have a clip");
        }

        // The router lowers the clank a semitone per dent; a key walk on top would move it the other way.
        [Test]
        public void TheClank_DoesNotWalkTheKey()
        {
            Assert.IsTrue(_bank.TryGet(GameSoundId.BalloonDeflectNuke, out var entry));
            Assert.AreEqual(MelodicMode.None, entry.MelodicMode);
        }
    }
}
