using System;
using BalloonParty.Shared.GameState;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI
{
    // An Animator whose appear state would otherwise play during the Launch/prewarm phase and be spent
    // before the player sees it. Rather than disabling the Animator — which leaves the design-time
    // (visible) pose on screen and causes a show-then-hide flash — this freezes it on the appear's first
    // (hidden) frame and unfreezes on the move to Game, so it stays hidden until it animates in on arrival.
    // Attach beside the Animator (e.g. the ProgressBar).
    internal sealed class AnimatorGameEntryGate : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private IDisposable _subscription;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator == null)
            {
                return;
            }

            // Enabled but paused holds the default (appear) state at frame 0 — the hidden start — instead
            // of the resting pose. Sample it once now so the hidden frame is shown before the first render.
            _animator.enabled = true;
            _animator.speed = 0f;
            _animator.Update(0f);
        }

        private void OnEnable()
        {
            _subscription = Navigation.Current
                .Where(state => state == NavigationState.Game)
                .Take(1)
                .Subscribe(_ => PlayAppear());
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void PlayAppear()
        {
            if (_animator != null)
            {
                _animator.speed = 1f;
            }
        }
    }
}
