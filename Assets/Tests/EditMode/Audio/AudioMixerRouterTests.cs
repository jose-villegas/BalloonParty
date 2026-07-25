using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using NUnit.Framework;
using UnityEngine.Audio;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class AudioMixerRouterTests
    {
        // AudioMixer/AudioMixerGroup are sealed UnityEngine.Audio types with no public
        // constructor, so a configured (non-null) Mixer can't be produced in an EditMode
        // test — that path is an in-editor concern (see the class header comment below).
        // What IS testable through the IAudioMixerSettings seam is the degrade-to-no-op
        // guard: SetChannelDucked must behave like NullAudioMixerRouter whenever the
        // asset is unconfigured, regardless of which half of the guard is "missing".
        private class FakeAudioMixerSettings : IAudioMixerSettings
        {
            public AudioMixer Mixer { get; set; }
            public float DuckVolumeDb { get; set; }
            public string Param { get; set; }

            public AudioMixerGroup GroupFor(SfxChannel channel)
            {
                return null;
            }

            public string ExposedVolumeParamFor(SfxChannel channel)
            {
                return Param;
            }
        }

        [Test]
        public void SetChannelDucked_NullMixer_DoesNotThrow()
        {
            var settings = new FakeAudioMixerSettings { Mixer = null, Param = "Vol_Gameplay" };
            var router = new AudioMixerRouter(settings);

            Assert.DoesNotThrow(() => router.SetChannelDucked(SfxChannel.Gameplay, true));
        }

        [Test]
        public void SetChannelDucked_NullMixerAndUnducking_DoesNotThrow()
        {
            var settings = new FakeAudioMixerSettings { Mixer = null, Param = "Vol_Gameplay" };
            var router = new AudioMixerRouter(settings);

            Assert.DoesNotThrow(() => router.SetChannelDucked(SfxChannel.Gameplay, false));
        }

        [Test]
        public void SetChannelDucked_EmptyParam_DoesNotThrow()
        {
            // Mixer is unavoidably null here too (it can't be constructed), so this locks
            // the same early-return as the test above rather than isolating the param
            // guard in isolation — see the class comment.
            var settings = new FakeAudioMixerSettings { Mixer = null, Param = string.Empty };
            var router = new AudioMixerRouter(settings);

            Assert.DoesNotThrow(() => router.SetChannelDucked(SfxChannel.UI, true));
        }

        [Test]
        public void SetChannelDucked_NullParam_DoesNotThrow()
        {
            var settings = new FakeAudioMixerSettings { Mixer = null, Param = null };
            var router = new AudioMixerRouter(settings);

            Assert.DoesNotThrow(() => router.SetChannelDucked(SfxChannel.Stinger, true));
        }

        // GroupFor is a one-line forward to IAudioMixerSettings.GroupFor — no branching,
        // no state. Per the README's "explicit interface forwarding"/"simple delegation"
        // rubric this is too simple to break, so it's deliberately not tested here.
    }
}
