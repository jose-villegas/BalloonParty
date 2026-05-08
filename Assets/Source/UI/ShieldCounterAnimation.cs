using System;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;

namespace BalloonParty.UI
{
    [RequireComponent(typeof(Animator))]
    public class ShieldCounterAnimation : MonoBehaviour
    {
        [Inject] private ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        [Inject] private ISubscriber<BalanceBalloonsMessage> _balanceSubscriber;

        private Animator _animator;
        private int _lastShieldValue;
        private readonly CompositeDisposable _disposable = new();
        private IDisposable _shieldSubscription;

        private void Awake() => _animator = GetComponent<Animator>();

        private void Start()
        {
            _animator.SetTrigger("Waiting");

            _loadedSubscriber
                .Subscribe(OnProjectileLoaded)
                .AddTo(_disposable);

            _balanceSubscriber
                .Subscribe(_ => OnBalancing())
                .AddTo(_disposable);
        }

        private void OnDestroy()
        {
            _disposable.Dispose();
            _shieldSubscription?.Dispose();
        }

        public void BindProjectile(ProjectileModel model)
        {
            _shieldSubscription?.Dispose();
            _lastShieldValue = model.ShieldsRemaining.Value;

            _shieldSubscription = model.ShieldsRemaining
                .Skip(1)
                .Subscribe(value =>
                {
                    _animator.SetTrigger(value > _lastShieldValue ? "Gain" : "Lost");
                    _lastShieldValue = value;
                });
        }

        private void OnProjectileLoaded(ProjectileLoadedMessage _)
        {
            _animator.SetTrigger("Ready");
            _lastShieldValue = 1;
        }

        private void OnBalancing()
        {
            _animator.SetTrigger("Waiting");
            _shieldSubscription?.Dispose();
        }
    }
}


