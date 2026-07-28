using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.Audio.View
{
    /// <summary>
    ///     Drop-in component that plays a configurable <see cref="GameSoundId"/> when triggered.
    ///     Automatically binds to sibling <see cref="Button"/> onClick if present; can also be wired
    ///     manually to UnityEvents, Animator events, or called from code.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class UiSfxEmitter : MonoBehaviour
    {
        [SerializeField] private GameSoundId _soundId = GameSoundId.None;
        [Tooltip("When true, automatically subscribes to any sibling Button's onClick event on Awake.")]
        [SerializeField] private bool _autoBindButton = true;

        [Inject] private ISoundPlayer _player;

        private Button _button;

        private void Awake()
        {
            if (_autoBindButton && TryGetComponent(out _button))
            {
                _button.onClick.AddListener(Play);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(Play);
            }
        }

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
