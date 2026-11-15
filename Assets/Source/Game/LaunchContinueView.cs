using System;
using BalloonParty.UI;
using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>
    ///     The launch screen's Continue button, beside Play: shown only while a run is saved, labelled with the
    ///     level it picks up at. Wire <see cref="Continue" /> to the button's <c>ScaleDownButton</c> On Scale
    ///     Complete, as Play is. <see cref="LaunchContinueController" /> decides when it shows and what a press does.
    /// </summary>
    public class LaunchContinueView : MonoBehaviour
    {
        [Tooltip("Its authored text is the template: {0} is the saved level. Optional.")]
        [SerializeField] private TMP_Text _levelLabel;

        private readonly Subject<Unit> _pressed = new();

        private FormattedLabel? _level;

        internal IObservable<Unit> Pressed => _pressed;

        private void OnDestroy()
        {
            _pressed.Dispose();
        }

        public void Continue()
        {
            _pressed.OnNext(Unit.Default);
        }

        // The template is read the first time the label is set, before anything has overwritten it; the button
        // may start inactive, so Awake can't be relied on for that.
        internal void Show(int level)
        {
            _level ??= new FormattedLabel(_levelLabel);
            _level.Value.Set(level);
            gameObject.SetActive(true);
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
