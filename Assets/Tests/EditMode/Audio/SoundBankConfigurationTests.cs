using System.Reflection;
using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class SoundBankConfigurationTests
    {
        private SoundBankConfiguration _config;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SoundBankConfiguration>();
            _clip = AudioClip.Create("test", 1, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void TryGet_AuthoredEntryWithClips_ReturnsTrueWithMatchingEntry()
        {
            var entry = CreateEntry(withClip: true);
            SetEntry(GameSoundId.BalloonPop, entry);

            var found = _config.TryGet(GameSoundId.BalloonPop, out var result);

            Assert.IsTrue(found);
            Assert.AreSame(entry, result);
        }

        [Test]
        public void TryGet_EntryWithoutClips_ReturnsFalse()
        {
            // Unity instantiates default (empty) entries for unauthored array slots, so an
            // entry with no clips is the runtime signal for "not a playable sound".
            SetEntry(GameSoundId.BalloonPop, CreateEntry(withClip: false));

            var found = _config.TryGet(GameSoundId.BalloonPop, out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGet_UnauthoredSlot_ReturnsFalse()
        {
            SetEntry(GameSoundId.BalloonPop, CreateEntry(withClip: true));

            var found = _config.TryGet(GameSoundId.ShotFired, out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGet_NoneId_ReturnsFalse()
        {
            SetEntry(GameSoundId.BalloonPop, CreateEntry(withClip: true));

            var found = _config.TryGet(GameSoundId.None, out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGet_OutOfRangeId_ReturnsFalse()
        {
            SetEntry(GameSoundId.BalloonPop, CreateEntry(withClip: true));

            var found = _config.TryGet((GameSoundId)9999, out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        private SfxEntry CreateEntry(bool withClip)
        {
            var entry = new SfxEntry();
            if (withClip)
            {
                SetField(entry, "_clips", new[] { _clip });
            }

            return entry;
        }

        private void SetEntry(GameSoundId id, SfxEntry entry)
        {
            var entries = new SfxEntry[System.Enum.GetValues(typeof(GameSoundId)).Length];
            entries[(int)id] = entry;
            SetField(_config, "_entries", entries);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
