using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared
{
    public class TweenTracker : MonoBehaviour
    {
        private Sequence _active;
        public bool IsPlaying => _active != null && _active.IsActive() && !_active.IsComplete();

        public void Append(Tween tween)
        {
            if (IsPlaying)
            {
                _active.Append(tween);
            }
            else
            {
                Kill();
                _active = DOTween.Sequence();
                _active.Append(tween);
            }
        }

        public void Replace(Sequence sequence)
        {
            Kill();
            _active = sequence;
        }

        public void Kill()
        {
            if (_active != null && _active.IsActive())
                _active.Kill();
            _active = null;
        }
    }
}