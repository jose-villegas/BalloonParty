using BalloonParty.Shared.Animation;
using BalloonParty.UI.Binding;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Renders a counter value via <see cref="RollingTextAnimator"/> (per-digit odometer roll).
    /// </summary>
    [RequireComponent(typeof(RollingTextAnimator))]
    internal sealed class RollingCounterDisplay : MonoBehaviour, ICounterDisplay
    {
        private TMP_Text _label;
        private RollingTextAnimator _animator;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _animator = GetComponent<RollingTextAnimator>();
        }

        public void Display(int value)
        {
            _animator.SetThousands(value);
        }

        public void ShowPlaceholder()
        {
            _label.text = "--";
        }
    }
}
