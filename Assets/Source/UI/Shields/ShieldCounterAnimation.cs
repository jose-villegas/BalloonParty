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
        [Inject] private ShieldCounterLabel[] _labels;

        [Inject] private ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        [Inject] private ISubscriber<BalanceBalloonsMessage> _balanceSubscriber;

        private readonly CompositeDisposable _disposable = new();
        private Animator _animator;
        private int _lastShieldValue;
        private IDisposable _shieldSubscription;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.SetTrigger("Waiting");
        }

        [Inject]
        private void Initialize()
        {
            _loadedSubscriber.Subscribe(OnProjectileLoaded).AddTo(_disposable);
            _balanceSubscriber.Subscribe(_ => OnBalancing()).AddTo(_disposable);
        }

        private void OnDestroy()
        {
            _disposable.Dispose();
            _shieldSubscription?.Dispose();
        }

        private void BindProjectile(ProjectileModel model)
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

        private void OnProjectileLoaded(ProjectileLoadedMessage msg)
        {
            BindProjectile(msg.Model);
            foreach (var label in _labels) label.Bind(msg.Model.ShieldsRemaining);
            _animator.ResetTrigger("Waiting");
            _animator.ResetTrigger("Lost");
            _animator.SetTrigger("Ready");
        }

        private void OnBalancing()
        {
            _animator.SetTrigger("Waiting");
            _shieldSubscription?.Dispose();
            foreach (var label in _labels) label.Unbind();
        }
    }
}