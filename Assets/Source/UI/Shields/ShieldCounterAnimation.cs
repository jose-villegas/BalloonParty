using System;
using BalloonParty.Game.Upgrades;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Shields
{
    /// <summary>
    ///     Shows how many shields every new ball starts with, and plays <c>Gain</c> or <c>Lost</c> when a card
    ///     changes it. Shields gained and spent in flight belong to that ball and play on it
    ///     (<c>ProjectileShieldView</c>), so with several balls in the air no single HUD number is wrong.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ShieldCounterAnimation : MonoBehaviour
    {
        private static readonly int GainTrigger = Animator.StringToHash("Gain");
        private static readonly int LostTrigger = Animator.StringToHash("Lost");
        private static readonly int ReadyTrigger = Animator.StringToHash("Ready");

        [Inject] private IShieldsPerBall _shieldsPerBall;
        [Inject] private ShieldCounterLabel[] _labels;

        private Animator _animator;
        private int _lastValue;
        private IDisposable _subscription;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        // Not at injection: the scope injects from its own Awake, which can run before this Awake and the
        // labels' (where each finds its display), and binding pushes the value into the labels at once.
        private void Start()
        {
            foreach (var label in _labels)
            {
                label.Bind(_shieldsPerBall.ShieldsPerBall);
            }

            _lastValue = _shieldsPerBall.ShieldsPerBall.Value;
            _subscription = _shieldsPerBall.ShieldsPerBall.Skip(1).Subscribe(OnShieldsPerBallChanged);
            _animator.SetTrigger(ReadyTrigger);
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        private void OnShieldsPerBallChanged(int value)
        {
            _animator.SetTrigger(value > _lastValue ? GainTrigger : LostTrigger);
            _lastValue = value;
        }
    }
}
