using System.Reflection;
using BalloonParty.Audio.Configuration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class AudioMixerSettingsTests
    {
        private AudioMixerSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<AudioMixerSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void ExposedVolumeParamFor_AuthoredIndex_ReturnsConfiguredString()
        {
            SetField(_settings, "_exposedVolumeParams", new[] { "Vol_Gameplay", "Vol_UI", "Vol_Stinger" });

            var param = _settings.ExposedVolumeParamFor(SfxChannel.UI);

            Assert.AreEqual("Vol_UI", param);
        }

        [Test]
        public void ExposedVolumeParamFor_OutOfRangeChannel_ReturnsNull()
        {
            SetField(_settings, "_exposedVolumeParams", new[] { "Vol_Gameplay", "Vol_UI", "Vol_Stinger" });

            var param = _settings.ExposedVolumeParamFor((SfxChannel)99);

            Assert.IsNull(param);
        }

        [Test]
        public void ExposedVolumeParamFor_NullArray_ReturnsNullWithoutThrow()
        {
            // A freshly-created instance never ran OnValidate outside the editor's asset
            // pipeline, so the array is still null — the same headless state a fresh
            // CreateInstance leaves SoundBankConfiguration's `_entries` in.
            Assert.DoesNotThrow(() =>
            {
                var param = _settings.ExposedVolumeParamFor(SfxChannel.Gameplay);
                Assert.IsNull(param);
            });
        }

        [Test]
        public void GroupFor_WithinRangeUnassignedSlot_ReturnsNull()
        {
            // AudioMixerGroup is sealed and can't be instantiated in a unit test, but the
            // array allocation itself works — this exercises the same bounds-check index
            // logic as the string-array path above without needing a constructable element.
            SetField(_settings, "_groups", new AudioMixerGroup[3]);

            var group = _settings.GroupFor(SfxChannel.Stinger);

            Assert.IsNull(group);
        }

        [Test]
        public void GroupFor_OutOfRangeChannel_ReturnsNull()
        {
            SetField(_settings, "_groups", new AudioMixerGroup[3]);

            var group = _settings.GroupFor((SfxChannel)99);

            Assert.IsNull(group);
        }

        [Test]
        public void GroupFor_NullArray_ReturnsNullWithoutThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var group = _settings.GroupFor(SfxChannel.Gameplay);
                Assert.IsNull(group);
            });
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
