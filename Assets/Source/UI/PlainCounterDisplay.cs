using BalloonParty.Shared.Extensions;
using BalloonParty.UI.Binding;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>Renders a counter value as plain thousands-separated text (no animation).</summary>
    [RequireComponent(typeof(TMP_Text))]
    internal sealed class PlainCounterDisplay : MonoBehaviour, ICounterDisplay
    {
        private TMP_Text _label;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        public void Display(int value)
        {
            _label.SetThousands(value);
        }

        public void ShowPlaceholder()
        {
            _label.text = "--";
        }
    }
}
