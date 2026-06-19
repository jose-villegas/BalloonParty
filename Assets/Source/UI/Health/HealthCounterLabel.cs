using System;
using BalloonParty.Game.Health;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Shows the player's current hit points as a numeric label (no fixed maximum), mirroring
    ///     <c>ShieldCounterLabel</c>. Binds to <see cref="PlayerHealthController.Current"/> on inject.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    internal class HealthCounterLabel : MonoBehaviour
    {
        private TMP_Text _label;
        private IDisposable _subscription;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _label.text = "--";
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        [Inject]
        public void Construct(PlayerHealthController health)
        {
            Bind(health.Current);
        }

        public void Bind(IReadOnlyReactiveProperty<int> hitPoints)
        {
            _subscription?.Dispose();
            _subscription = hitPoints.Subscribe(hp => _label.text = hp.ToString("N0"));
        }

        public void Unbind()
        {
            _subscription?.Dispose();
            _subscription = null;
            _label.text = "--";
        }
    }
}
