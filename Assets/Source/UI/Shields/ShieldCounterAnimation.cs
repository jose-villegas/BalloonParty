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
        private static readonly int GainTrigger = Animator.StringToHash("Gain");
        private static readonly int LostTrigger = Animator.StringToHash("Lost");
        private static readonly int ReadyTrigger = Animator.StringToHash("Ready");
        private static readonly int WaitingTrigger = Animator.StringToHash("Waiting");

        [Inject] private ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        [Inject] private ShieldCounterLabel[] _labels;
        [Inject] private ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;

        private readonly CompositeDisposable _disposable = new();

        private Animator _animator;
        private int _lastShieldValue;
        private IDisposable _shieldSubscription;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.SetTrigger(WaitingTrigger);
        }

        private void OnDestroy()
        {
            _disposable.Dispose();
            _shieldSubscription?.Dispose();
        }

        [Inject]
        private void Initialize()
        {
            _loadedSubscriber.Subscribe(OnProjectileLoaded).AddTo(_disposable);
            _destroyedSubscriber.Subscribe(_ => OnProjectileDestroyed()).AddTo(_disposable);
        }

        private void BindProjectile(IProjectileModel model)
        {
            _shieldSubscription?.Dispose();
            _lastShieldValue = model.ShieldsRemaining.Value;

            _shieldSubscription = model.ShieldsRemaining
                .Skip(1)
                .Subscribe(value =>
                {
                    _animator.SetTrigger(value > _lastShieldValue ? GainTrigger : LostTrigger);
                    _lastShieldValue = value;
                });
        }

        private void OnProjectileLoaded(ProjectileLoadedMessage msg)
        {
            BindProjectile(msg.Model);
            foreach (var label in _labels)
            {
                label.Bind(msg.Model.ShieldsRemaining);
            }

            _animator.ResetTrigger(WaitingTrigger);
            _animator.ResetTrigger(LostTrigger);
            _animator.SetTrigger(ReadyTrigger);
        }

        private void OnProjectileDestroyed()
        {
            _animator.SetTrigger(WaitingTrigger);
            _shieldSubscription?.Dispose();
            foreach (var label in _labels)
            {
                label.Unbind();
            }
        }
    }
}
