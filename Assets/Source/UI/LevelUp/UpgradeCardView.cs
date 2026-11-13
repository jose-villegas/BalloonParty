using System;
using BalloonParty.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.LevelUp
{
    /// <summary>
    ///     One upgrade card on the level-up popup. Shows a dealt card and reports its slot when tapped;
    ///     the popup owns what a pick does.
    /// </summary>
    public class UpgradeCardView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _descriptionLabel;

        [Tooltip("Shows how many of this card the run already holds, e.g. \"1/3\". Optional.")]
        [SerializeField] private TMP_Text _stacksLabel;

        private Action<int> _onPicked;
        private int _slot;
        private bool _listening;

        /// <summary>Every required reference is assigned. The popup skips a slot that isn't, since an
        /// unclickable card would leave a level-up that only a pick can close.</summary>
        internal bool IsWired => _button != null && _titleLabel != null && _descriptionLabel != null;

        private void OnDestroy()
        {
            if (_listening && _button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }
        }

        internal void Show(IUpgradeCardSettings card, int heldStacks, int limit, int slot, Action<int> onPicked)
        {
            // Bound here rather than in Awake: a card that starts inactive never runs Awake.
            if (!_listening)
            {
                _button.onClick.AddListener(OnClicked);
                _listening = true;
            }

            _slot = slot;
            _onPicked = onPicked;
            _titleLabel.text = UpgradeCardText.Title(card);
            _descriptionLabel.text = card.Description;
            if (_stacksLabel != null)
            {
                _stacksLabel.text = $"{heldStacks}/{limit}";
            }

            _button.interactable = true;
            gameObject.SetActive(true);
        }

        internal void Hide()
        {
            _onPicked = null;
            gameObject.SetActive(false);
        }

        private void OnClicked()
        {
            _onPicked?.Invoke(_slot);
        }
    }
}
