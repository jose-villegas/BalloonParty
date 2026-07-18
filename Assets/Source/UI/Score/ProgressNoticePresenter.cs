using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared.Pool;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    /// <summary>
    ///     Owns a progress bar's floating notices, split out of <see cref="ColorProgressBar"/> to keep the view lean.
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

        private ProgressNotice _streakNotice;

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

        // Amortized so constructing a bar at level setup never spikes into a hitch. Safe to call more than
        // once for the same color — RegisterChannel no-ops once a key is already registered, so a re-call
        // (e.g. defensive re-construction) tops up rather than growing the pools unboundedly.
        internal async UniTask PrewarmAsync(int pointCount, int streakCount, CancellationToken ct = default)
        {
            RegisterChannel(_pointPoolKey, _pointPrefab);
            RegisterChannel(_streakPoolKey, _streakPrefab);

            await _poolManager.PrewarmAsync(_pointPoolKey, pointCount, ct);
            await _poolManager.PrewarmAsync(_streakPoolKey, streakCount, ct);
        }

        internal void SpawnPointNotice(Vector2 anchoredPosition)
        {
            var notice = _poolManager.GetOrRegister(_pointPoolKey,
                () => new SimplePoolChannel<ProgressNotice>(_pointPrefab));

            notice.SetParent(_parent);
            notice.SetAnchoredPosition(anchoredPosition);
            _activeNotices.Add(notice);
            notice.Show(1,
                () =>
                {
                    _activeNotices.Remove(notice);
                    _poolManager.Return(_pointPoolKey, notice);
                });
        }

        // The streak notice is persistent: it holds (negative hold duration — no auto-dismiss) until
        // the streak grows into a new one or is lost. Showing again flies the current one away and
        // pops a fresh one at the new value.
        internal void ShowStreak(int streak)
        {
            DismissStreak();

            var notice = _poolManager.GetOrRegister(_streakPoolKey,
                () => new SimplePoolChannel<ProgressNotice>(_streakPrefab));

            notice.SetParent(_parent);
            notice.SetAnchoredPosition(Vector2.zero);
            _activeNotices.Add(notice);
            _streakNotice = notice;
            notice.Show(streak, () => ReturnStreakNotice(notice), _color);
        }

        internal void DismissStreak()
        {
            if (_streakNotice != null)
            {
                _streakNotice.Dismiss();
                _streakNotice = null;
            }
        }

        private void RegisterChannel(string key, ProgressNotice prefab)
        {
            if (!_poolManager.IsRegistered(key))
            {
                _poolManager.Register(key, new SimplePoolChannel<ProgressNotice>(prefab));
            }
        }

        private void ReturnStreakNotice(ProgressNotice notice)
        {
            _activeNotices.Remove(notice);
            if (ReferenceEquals(_streakNotice, notice))
            {
                _streakNotice = null;
            }

            _poolManager.Return(_streakPoolKey, notice);
        }

        // Immediate (not animated): snaps every notice straight to completed. Each notice's
        // completion callback removes it from the list.
        internal void DismissAllNotices()
        {
            for (var i = _activeNotices.Count - 1; i >= 0; i--)
            {
                _activeNotices[i].DismissImmediate();
            }
        }

        // Plays each notice's disappear instead of snapping. Notices tick on unscaled time — the
        // level-up popup freezes timeScale to 0 while this is called, so a scaled tick would stall
        // the fade mid-way. Completion callbacks drain the list as each finishes.
        internal void DismissAllAnimated()
        {
            for (var i = _activeNotices.Count - 1; i >= 0; i--)
            {
                _activeNotices[i].Dismiss();
            }

            _streakNotice = null;
        }
    }
}
