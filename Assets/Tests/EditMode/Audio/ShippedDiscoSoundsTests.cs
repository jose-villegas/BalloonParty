using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.Routing;
using NUnit.Framework;
using UnityEditor;

namespace BalloonParty.Tests.Audio
{
    /// <summary>
    ///     Pins the disco ball's cues in the shipped sound bank (PLAN-DiscoBalloon phase 5). The wave's notes walk the key
    ///     by the degree the router passes, and the walk folds back once it nears its entry's octave window, so a window
    ///     too narrow would repeat the glissando's top notes instead of climbing them.
    /// </summary>
    [TestFixture]
    public class ShippedDiscoSoundsTests
    {
        private const string BankPath = "Assets/Configuration/SoundBankConfiguration.asset";

        private SoundBankConfiguration _bank;

        [SetUp]
        public void SetUp()
        {
            _bank = AssetDatabase.LoadAssetAtPath<SoundBankConfiguration>(BankPath);
            Assert.IsNotNull(_bank, $"{BankPath} should load");
        }

        [TestCase(nameof(GameSoundId.BalloonDeflectDisco))]
        [TestCase(nameof(GameSoundId.BalloonPopDisco))]
        [TestCase(nameof(GameSoundId.DiscoWaveGlissando))]
        [TestCase(nameof(GameSoundId.DiscoWaveTick))]
        public void EachDiscoCue_HasAClip(string name)
        {
            var id = System.Enum.Parse<GameSoundId>(name);
            Assert.IsTrue(_bank.TryGet(id, out var entry) && entry.HasClips, $"{id} should have a clip");
        }

        [TestCase(nameof(GameSoundId.DiscoWaveGlissando))]
        [TestCase(nameof(GameSoundId.DiscoWaveTick))]
        public void TheWavesNotes_ClimbEveryDegree_TwoOctavesRootToRoot(string name)
        {
            var id = System.Enum.Parse<GameSoundId>(name);
            Assert.IsTrue(_bank.TryGet(id, out var entry), $"{id} should be authored");
            Assert.AreEqual(MelodicMode.ScaleWalkUp, entry.MelodicMode, $"{id} should walk the key");
            var picker = new VariationPicker(new System.Random(1), _bank.MelodicScale, _bank.MelodicRootSemitone);

            var previous = int.MinValue;
            var lowest = 0;
            for (var degree = 0; degree <= DiscoSoundRouter.ScaleDegrees; degree++)
            {
                var context = new PickContext(degree, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);
                var semitone = picker.Pick(id, entry, in context).MelodicSemitone;
                Assert.Greater(semitone, previous, $"{id} degree {degree} should sit above degree {degree - 1}");
                if (degree == 0)
                {
                    lowest = semitone;
                }

                previous = semitone;
            }

            Assert.AreEqual(24, previous - lowest, $"{id} should span two octaves");
        }
    }
}
