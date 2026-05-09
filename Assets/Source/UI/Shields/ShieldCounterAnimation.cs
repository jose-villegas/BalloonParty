using System;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Shields
{
    [RequireComponent(typeof(Animator))]
    public class ShieldCounterAnimation : MonoBehaviour
    {
        private readonly CompositeDisposable _disposable = new();

        private Animator _animator;
        [Inject] private ISubscriber<BalanceBalloonsMessage> _balanceSubscriber;
        private int _lastShieldValue;
        [Inject] private ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private IDisposable _shieldSubscription;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

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