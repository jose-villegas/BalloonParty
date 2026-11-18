using System;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Item.Snipe
{
    /// <summary>What the Snipe roll asks about the last nuke: how much its going off raises the card's chance now.</summary>
    internal interface INukeAftermath
    {
        /// <summary>
        ///     <see cref="IUpgradeSettings.NukeAftermathSnipeChanceScale" /> for
        ///     <see cref="IUpgradeSettings.NukeAftermathSnipeSeconds" /> after a nuke goes off, 1 otherwise.
        /// </summary>
        float SnipeChanceScale { get; }
    }

    /// <summary>
    ///     The stretch after a nuke goes off (<see cref="NukeDetonatedMessage" />) in which the Sharpshooter card's
    ///     Snipe roll is a little likelier: the board refills fast after the blast's breather, and a lance is the
    ///     follow-up that keeps the room the nuke bought. A new run starts with none.
    /// </summary>
    internal sealed class NukeAftermath : INukeAftermath, IStartable, IRunResettable, IDisposable
    {
        private readonly ISubscriber<NukeDetonatedMessage> _detonated;
        private readonly IUpgradeSettings _settings;

        private IDisposable _subscription;
        private float _detonatedAt = float.NegativeInfinity;

        // Tests step the clock; game time, as the Snipe's cooldown, so a pause spends none of it.
        internal Func<float> Now { get; set; } = () => Time.time;
        public int ResetOrder => RunResetOrder.Counters;

        public float SnipeChanceScale => Now() - _detonatedAt < _settings.NukeAftermathSnipeSeconds
            ? Mathf.Max(1f, _settings.NukeAftermathSnipeChanceScale)
            : 1f;

        internal NukeAftermath(ISubscriber<NukeDetonatedMessage> detonated, IUpgradeSettings settings)
        {
            _detonated = detonated;
            _settings = settings;
        }

        public void Start()
        {
            _subscription = _detonated.Subscribe(_ => _detonatedAt = Now());
        }

        public void ResetRun(int generation)
        {
            _detonatedAt = float.NegativeInfinity;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
