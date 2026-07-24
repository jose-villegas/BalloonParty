using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.View;
using BalloonParty.Game;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace BalloonParty.Tests.Game
{
    // Scoped to the two conditional branches GameScopeRegistration.RegisterAudio actually owns — the
    // null-prefab guard and the null-bank fallback. It deliberately stops at registration-existence
    // checks (never Build()s): a full resolution/entry-point smoke test would need every message
    // broker every router subscribes to, plus VContainer's deferred entry-point dispatch (which
    // patches the global Unity player loop on first use) — a new, heavier pattern this repo's test
    // suite doesn't have, and one already exercised end-to-end by any PlayMode fixture that loads the
    // real Game scene (GameLifetimeScope.Awake builds the same registration synchronously).
    [TestFixture]
    public class RegisterAudioTests
    {
        private GameObject _voiceGo;

        [SetUp]
        public void SetUp()
        {
            _voiceGo = new GameObject("VoicePrefab");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_voiceGo);
        }

        [Test]
        public void RegisterAudio_NullVoicePrefab_RegistersNothing()
        {
            var builder = new ContainerBuilder();

            builder.RegisterAudio(null, null);

            // The prefab guard must degrade to "audio disabled" (an early return), never a
            // half-registered graph that throws later when something resolves ISoundPlayer.
            Assert.IsFalse(builder.Exists(typeof(ISoundBankConfiguration), includeInterfaceTypes: true));
            Assert.IsFalse(builder.Exists(typeof(ISoundPlayer), includeInterfaceTypes: true));
        }

        [Test]
        public void RegisterAudio_ValidVoicePrefab_RegistersSoundPlayerAndMixerRouter()
        {
            var prefab = _voiceGo.AddComponent<AudioSourceVoice>();
            var bank = ScriptableObject.CreateInstance<SoundBankConfiguration>();
            var builder = new ContainerBuilder();

            try
            {
                builder.RegisterAudio(bank, prefab);

                Assert.IsTrue(builder.Exists(typeof(ISoundPlayer), includeInterfaceTypes: true));
                Assert.IsTrue(builder.Exists(typeof(IMelodicContext), includeInterfaceTypes: true));
                Assert.IsTrue(builder.Exists(typeof(IAudioMixerRouter), includeInterfaceTypes: true));
            }
            finally
            {
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void RegisterAudio_NullSoundBank_StillRegistersAWorkingSoundBankConfiguration()
        {
            var prefab = _voiceGo.AddComponent<AudioSourceVoice>();
            var builder = new ContainerBuilder();

            // No try/finally cleanup for the fallback bank: RegisterAudio owns its creation (same as
            // GameLifetimeScope's own null-asset fallbacks) and the test never captures a reference to it.
            builder.RegisterAudio(null, prefab);

            Assert.IsTrue(builder.Exists(typeof(ISoundBankConfiguration), includeInterfaceTypes: true),
                "A null bank must fall back to a fresh instance, not skip registration and null-ref every voice-cap reader.");
        }
    }
}
