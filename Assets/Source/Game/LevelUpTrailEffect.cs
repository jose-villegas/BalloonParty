using BalloonParty.Display;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game
{
    internal class LevelUpTrailEffect : MonoBehaviour
    {
        [Header("Slow Motion")]
        [SerializeField] private float _slowTimeScale = 0.3f;
        [SerializeField] private float _slowDownDuration = 0.15f;
        [SerializeField] private float _restoreDuration = 0.35f;

        [Header("Camera")]
        [SerializeField] private float _zoomAmount = 0.5f;
        [SerializeField] private float _cameraPanWeight = 0.7f;
        [SerializeField] private float _cameraFollowSpeed = 5f;

        [Inject] private ISubscriber<BalloonScoredMessage> _scoredSubscriber;
        [Inject] private ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        [Inject] private ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        [Inject] private ScoreController _scoreController;
        [Inject] private ScoreTrailService _scoreTrailService;
        [Inject] private OrthogonalSizeCameraController _orthoController;

        private Camera _camera;
        private float _baseOrthoSize;
        private Vector3 _basePosition;
        private bool _following;
        private bool _active;
        private bool _waitingForTip;
        private int _arrivalsBeforeTip;
        private Transform _trackedTrail;
        private Vector3 _lastTrailPosition;
        private string _pendingColor;
        private bool _pendingCinematicStart;
        private Tween _timeScaleTween;
        private Tween _zoomTween;

        private void Start()
        {
            _camera = Camera.main;

            _scoredSubscriber.Subscribe(OnBalloonScored).AddTo(this);
            _trailArrivedSubscriber.Subscribe(OnTrailArrived).AddTo(this);
            _dismissedSubscriber.Subscribe(OnLevelUpDismissed).AddTo(this);
        }

        private void Update()
        {
            if (!_following || _camera == null)
            {
                return;
            }

            if (_trackedTrail == null && _pendingColor != null)
            {
                _trackedTrail = _scoreTrailService.GetLastSpawnedTrail(_pendingColor);

                // Tipping trail just became available — now safe to freeze
                // other trails without blocking the spawn loop.
                if (_trackedTrail != null && _pendingCinematicStart)
                {
                    _pendingCinematicStart = false;
                    Cinematic.Begin(CinematicState.LevelUpTrail);
                    _scoreTrailService.ResumeTrail(_trackedTrail);
                }
            }

            if (_trackedTrail != null)
            {
                _lastTrailPosition = _trackedTrail.position;
            }

            var panTarget = Vector3.Lerp(_basePosition, _lastTrailPosition, _cameraPanWeight);
            panTarget.z = _basePosition.z;

            _camera.transform.position = Vector3.Lerp(
                _camera.transform.position,
                panTarget,
                _cameraFollowSpeed * Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (!_following || _camera == null || _trackedTrail == null)
            {
                return;
            }

            // DOTween overwrites position each Update; re-apply camera
            // compensation so the trail stays aligned with the UI bar.
            var cameraDelta = _camera.transform.position - _basePosition;
            cameraDelta.z = 0f;
            _trackedTrail.position += cameraDelta;
        }

        private void OnDestroy()
        {
            KillTweens();
            Cinematic.End();

            if (_orthoController != null)
            {
                _orthoController.enabled = true;
            }

            Time.timeScale = 1f;
        }

        private void OnBalloonScored(BalloonScoredMessage msg)
        {
            if (_active || Cinematic.IsPlaying || msg.Points <= 0)
            {
                return;
            }

            if (!_scoreController.WillLevelUp(msg.ColorName, msg.Points))
            {
                return;
            }

            _active = true;
            _pendingColor = msg.ColorName;

            var arrivalsNeeded = _scoreController.PointsNeededForLevelUp(msg.ColorName);

            if (arrivalsNeeded <= 1)
            {
                BeginCinematic(_scoreTrailService.GetLastSpawnedTrail(msg.ColorName));
                return;
            }

            _waitingForTip = true;
            _arrivalsBeforeTip = arrivalsNeeded - 1;
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_active || msg.ColorName != _pendingColor)
            {
                return;
            }

            if (_waitingForTip)
            {
                _arrivalsBeforeTip--;
                if (_arrivalsBeforeTip > 0)
                {
                    return;
                }

                _waitingForTip = false;
                var trail = _scoreTrailService.GetLastSpawnedTrail(_pendingColor);
                BeginCinematic(trail);
                return;
            }

            if (!_following)
            {
                return;
            }

            EndSlowMotion();
        }

        private void OnLevelUpDismissed(LevelUpDismissedMessage msg)
        {
            if (!_active)
            {
                Time.timeScale = 1f;
                Navigation.TransitionTo(NavigationState.Game);
                return;
            }

            Restore();
        }

        private void BeginCinematic(Transform trail)
        {
            _trackedTrail = trail;

            if (_trackedTrail != null)
            {
                Cinematic.Begin(CinematicState.LevelUpTrail);
                _scoreTrailService.ResumeTrail(_trackedTrail);
            }
            else
            {
                // Trail hasn't spawned yet (stagger delay). Start slow-mo
                // now; Update will activate the cinematic guard once the
                // trail appears.
                _pendingCinematicStart = true;
            }

            BeginSlowMotion(_trackedTrail != null
                ? _trackedTrail.position
                : Vector3.zero);
        }

        private void BeginSlowMotion(Vector3 focusWorldPosition)
        {
            KillTweens();
            _following = true;
            _lastTrailPosition = focusWorldPosition;

            if (_orthoController != null)
            {
                _orthoController.enabled = false;
            }

            if (_camera != null)
            {
                _baseOrthoSize = _camera.orthographicSize;
                _basePosition = _camera.transform.position;
            }

            _timeScaleTween = DOTween.To(
                    () => Time.timeScale,
                    x => Time.timeScale = x,
                    _slowTimeScale,
                    _slowDownDuration)
                .SetUpdate(true);

            if (_camera == null)
            {
                return;
            }

            var zoomedOrtho = _baseOrthoSize - _zoomAmount;

            _zoomTween = DOTween.To(
                    () => _camera.orthographicSize,
                    x => _camera.orthographicSize = x,
                    zoomedOrtho,
                    _slowDownDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void EndSlowMotion()
        {
            _following = false;
            _trackedTrail = null;
            KillTweens();
        }

        private void Restore()
        {
            _active = false;
            _following = false;
            _waitingForTip = false;
            _pendingCinematicStart = false;
            _pendingColor = null;
            _trackedTrail = null;

            KillTweens();

            _timeScaleTween = DOTween.To(
                    () => Time.timeScale,
                    x => Time.timeScale = x,
                    1f,
                    _restoreDuration)
                .SetUpdate(true)
                .OnComplete(OnRestoreComplete);

            if (_camera == null)
            {
                return;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(
                _camera.transform.DOMove(_basePosition, _restoreDuration)
                    .SetEase(Ease.InOutQuad));
            sequence.Join(
                DOTween.To(
                    () => _camera.orthographicSize,
                    x => _camera.orthographicSize = x,
                    _baseOrthoSize,
                    _restoreDuration).SetEase(Ease.InOutQuad));

            _zoomTween = sequence;
        }

        private void OnRestoreComplete()
        {
            if (_orthoController != null)
            {
                _orthoController.enabled = true;
            }

            Cinematic.End();
            Navigation.TransitionTo(NavigationState.Game);
        }

        private void KillTweens()
        {
            _timeScaleTween?.Kill();
            _zoomTween?.Kill();
            _timeScaleTween = null;
            _zoomTween = null;
        }
    }
}
