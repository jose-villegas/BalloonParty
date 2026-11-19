using System;
using UniRx;
using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>
    ///     The launch screen's Continue: a small white balloon shown only while a run is saved. This is its press and
    ///     whether it shows; the balloon's drift and its ribbon are the <see cref="LaunchBalloonView" /> on the same
    ///     object, so hiding this hides them and frees Play of keeping clear of it. Wire <see cref="Continue" /> to
    ///     the balloon's <c>ScaleDownButton</c> On Scale Complete, as Play is.
    ///     <see cref="LaunchContinueController" /> decides when it shows and what a press does.
    /// </summary>
    public class LaunchContinueView : MonoBehaviour
    {
        private readonly Subject<Unit> _pressed = new();

        internal IObservable<Unit> Pressed => _pressed;

        private void OnDestroy()
        {
            _pressed.Dispose();
        }

        public void Continue()
        {
            _pressed.OnNext(Unit.Default);
        }

        internal void Show()
        {
            gameObject.SetActive(true);
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
