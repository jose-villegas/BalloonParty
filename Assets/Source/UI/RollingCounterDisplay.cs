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

        private TMP_Text Label => _label ??= GetComponent<TMP_Text>();
        private RollingTextAnimator Animator => _animator ??= GetComponent<RollingTextAnimator>();

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _animator = GetComponent<RollingTextAnimator>();
        }

        public void Display(int value)
        {
            Animator.SetThousands(value);
        }

        public void ShowPlaceholder()
        {
            Label.text = "--";
        }
    }
}
