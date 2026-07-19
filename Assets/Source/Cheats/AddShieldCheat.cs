#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Adds shields to the shot currently loaded in the thrower. Bumps the reactive count directly;
    ///     the shield view observes it and plays the gain feedback on the change.
    /// </summary>
    internal class AddShieldCheat : ICheat, ICheatControls, IStartable, IDisposable
    {
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;

        private IWriteableProjectileModel _activeProjectile;
        private IDisposable _subscription;
        private int _amount = 1;

        public string Name => "Add Shields";
        public string Section => "Projectile";
        public IReadOnlyList<string> Tags => new[] { "projectile", "shield" };
        public bool Compact => false;

        public AddShieldCheat(ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _loadedSubscriber = loadedSubscriber;
        }

        public void Start()
        {
            _subscription = _loadedSubscriber.Subscribe(
                msg => _activeProjectile = (IWriteableProjectileModel)msg.Model);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void Execute()
        {
            if (_activeProjectile == null)
            {
                return;
            }

            _activeProjectile.ShieldsRemaining.Value += Mathf.Max(1, _amount);
        }

        public void DrawControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Amount", GUILayout.Width(52));
            _amount = CheatLayout.IntField("shield.amount", _amount, min: 1);

            if (GUILayout.Button("Add Shields"))
            {
                Execute();
            }

            GUILayout.EndHorizontal();
        }
    }
}
#endif
