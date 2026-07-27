using UnityEngine;
using VContainer;

namespace BalloonParty.Audio.View
{
    /// <summary>
    ///     Drop-in component that plays a configurable <see cref="GameSoundId"/> when triggered.
    ///     Wire <see cref="Play"/> to UnityEvents, Animator events, or call it from code.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class UiSfxEmitter : MonoBehaviour
    {
        [SerializeField] private GameSoundId _soundId = GameSoundId.None;

        [Inject] private ISoundPlayer _player;

        public void Play()
        {
            if (_soundId == GameSoundId.None)
            {
                return;
            }

            _player.Play(_soundId, null);
        }
    }
}
