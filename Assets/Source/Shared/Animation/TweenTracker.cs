using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared.Animation
{
    public class TweenTracker : MonoBehaviour
    {
        private Sequence _active;

        public bool IsPlaying => _active != null && _active.IsActive() && _active.IsPlaying();

        public void Append(Tween tween)
        {
            // DOTween forbids modifying a sequence once it has started — appending to a playing one
            // corrupts the active-tween array. Always replace with a fresh sequence instead.
            Kill();
            _active = DOTween.Sequence();
            _active.Append(tween);
        }

        public void Kill()
        {
            if (_active != null && _active.IsActive())
            {
                _active.Kill();
            }

            _active = null;
        }

        public void Replace(Sequence sequence)
        {
            Kill();
            _active = sequence;
        }
    }
}
