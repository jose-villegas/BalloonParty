using System;
using BalloonParty.Shared.GameState;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI
{
    // An Animator whose default (appear) state would otherwise play during the Launch/prewarm phase and
    // be spent before the player sees it. Holds the Animator disabled until navigation reaches Game, then
    // enables it so the appear plays on arrival. Attach beside the Animator (e.g. the ProgressBar).
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

            if (_animator != null)
            {
                _animator.enabled = false;
            }
        }

        private void OnEnable()
        {
            _subscription = Navigation.Current
                .Where(state => state == NavigationState.Game)
                .Take(1)
                .Subscribe(_ => EnableAnimator());
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void EnableAnimator()
        {
            if (_animator != null)
            {
                _animator.enabled = true;
            }
        }
    }
}
