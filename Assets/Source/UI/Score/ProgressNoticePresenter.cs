using System.Collections.Generic;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    /// <summary>
    ///     Owns a progress bar's floating notices, split out of <see cref="ColorProgressBar"/> to
    ///     keep the view lean. Streak notices are tracked so a new streak can dismiss the ones
    ///     already shown; point notices are fire-and-forget.
    /// </summary>
    internal class ProgressNoticePresenter
    {
        private readonly PoolManager _poolManager;
        private readonly ProgressNotice _pointPrefab;
        private readonly ProgressNotice _streakPrefab;
        private readonly Transform _parent;
        private readonly Color _color;
        private readonly string _pointPoolKey;
        private readonly string _streakPoolKey;
        private readonly List<ProgressNotice> _activeNotices = new();

        internal ProgressNoticePresenter(
            PoolManager poolManager,
            ProgressNotice pointPrefab,
            ProgressNotice streakPrefab,
            Transform parent,
            string colorName,
            Color color)
        {
            _poolManager = poolManager;
            _pointPrefab = pointPrefab;
            _streakPrefab = streakPrefab;
            _parent = parent;
            _color = color;
            _pointPoolKey = $"PointNotice_{colorName}";
            _streakPoolKey = $"StreakNotice_{colorName}";
        }

        internal void SpawnPointNotice(Vector2 anchoredPosition)
        {
            var notice = _poolManager.GetOrRegister(_pointPoolKey,
                () => new SimplePoolChannel<ProgressNotice>(_pointPrefab));

            notice.SetParent(_parent);
            notice.SetAnchoredPosition(anchoredPosition);
            notice.Show(1, () => _poolManager.Return(_pointPoolKey, notice));
        }

        internal void SpawnStreakNotice(int streak)
        {
            var notice = _poolManager.GetOrRegister(_streakPoolKey,
                () => new SimplePoolChannel<ProgressNotice>(_streakPrefab));

            notice.SetParent(_parent);
            notice.SetAnchoredPosition(Vector2.zero);
            _activeNotices.Add(notice);
            notice.Show(streak,
                () =>
                {
                    _activeNotices.Remove(notice);
                    _poolManager.Return(_streakPoolKey, notice);
                },
                _color);
        }

        internal void DismissFullyShownNotices()
        {
            for (var i = _activeNotices.Count - 1; i >= 0; i--)
            {
                if (_activeNotices[i].IsFullyShown)
                {
                    _activeNotices[i].Dismiss();
                }
            }
        }

        internal void DismissAllNotices()
        {
            for (var i = _activeNotices.Count - 1; i >= 0; i--)
            {
                _activeNotices[i].Dismiss();
            }
        }
    }
}
