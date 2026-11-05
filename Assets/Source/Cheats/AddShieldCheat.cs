#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using System.Collections.Generic;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Adds shields to the latest shot in flight, else the one loaded in the thrower. Bumps the reactive count directly;
    ///     the shield view observes it and plays the gain feedback on the change.
    /// </summary>
    internal class AddShieldCheat : ICheat, ICheatControls
    {
        private readonly IProjectileRegistry _projectiles;

        private int _amount = 1;

        public string Name => "Add Shields";
        public string Section => "Projectile";
        public IReadOnlyList<string> Tags => new[] { "projectile", "shield" };
        public bool Compact => false;

        public AddShieldCheat(IProjectileRegistry projectiles)
        {
            _projectiles = projectiles;
        }

        public void Execute()
        {
            if (_projectiles.ShotOrCurrent(null) is not IWriteableProjectileModel shot)
            {
                return;
            }

            shot.ShieldsRemaining.Value += Mathf.Max(1, _amount);
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
