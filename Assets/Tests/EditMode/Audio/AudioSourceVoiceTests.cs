using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using BalloonParty.Audio;
using BalloonParty.Audio.View;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    // Only the synchronous null-clip guard is exercised here — everything else on this class
    // (real AudioSource.Play(), the UniTask.Delay-timed return) needs the player loop and is a
    // PlayMode/in-editor concern per Assets/Tests/README.md.
    [TestFixture]
    public class AudioSourceVoiceTests
    {
        private GameObject _go;
        private AudioSourceVoice _voice;
        private AudioSource _source;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestVoice");
            _voice = _go.AddComponent<AudioSourceVoice>();
            _source = _go.AddComponent<AudioSource>();
            SetField(_voice, "_source", _source);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Play_NullClip_InvokesCompletionSynchronouslyWithThisVoice()
        {
            var completedVoices = new List<AudioSourceVoice>();

            _voice.Play(default, loop: false, onComplete: v => completedVoices.Add(v));

            Assert.AreEqual(1, completedVoices.Count);
            Assert.AreSame(_voice, completedVoices[0]);
        }

        [Test]
        public void Play_NullClip_DoesNotScheduleAReturnTimer()
        {
            _voice.Play(default, loop: false, onComplete: _ => { });

            var cts = GetField(_voice, "_returnCts") as CancellationTokenSource;

            Assert.IsNull(cts);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        private static object GetField(object target, string fieldName)
        {
            return target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(target);
        }
    }
}
