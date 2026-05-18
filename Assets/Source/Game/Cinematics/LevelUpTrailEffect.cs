using BalloonParty.Display;
using BalloonParty.Game.Score;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game.Cinematics
{
    internal class LevelUpTrailEffect : MonoBehaviour
    {
        [Header("Slow Motion")]
        [SerializeField] private float _slowTimeScale = 0.3f;
        [SerializeField] private float _slowDownDuration = 0.15f;
        [SerializeField] private float _restoreDuration = 0.35f;

        [Header("Camera")]
        [SerializeField] private Camera _camera;
        [SerializeField] private float _zoomAmount = 0.5f;
        [SerializeField] private float _cameraPanWeight = 0.7f;
        [SerializeField] private float _cameraFollowSpeed = 5f;
        [SerializeField] private AnimationCurve _trackedTrailScaleCurve = AnimationCurve.EaseInOut(0f, 2f, 1f, 1f);

        [Inject] private CinematicDirector _director;
        [Inject] private ISubscriber<ScorePointMessage> _scoredSubscriber;
        [Inject] private ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        [Inject] private ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        [Inject] private OrthogonalSizeCameraController _orthoController;
        [Inject] private ScoreController _scoreController;
        [Inject] private ScoreTrailService _scoreTrailService;

        private float _baseOrthoSize;
        private Vector3 _basePosition;
        private Vector3 _lastTrailPosition;
        private bool _sessionActive;
        private Tween _timeScaleTween;
        private TrailId _tippingTrailId;
        private Transform _trackedTrail;
        private float _trailElapsed;
        private Tween _zoomTween;

        private void Start()
        {
            _scoredSubscriber.Subscribe(OnScorePoint).AddTo(this);
            _trailArrivedSubscriber.Subscribe(OnTrailArrived).AddTo(this);
            _dismissedSubscriber.Subscribe(OnLevelUpDismissed).AddTo(this);
        }

        private void OnDestroy()
        {
            KillTweens();

            if (_director.IsCinematicActive)
            {
                _director.EndCinematic();
            }

            if (_orthoController != null)
            {
                _orthoController.enabled = true;
            }

            Time.timeScale = 1f;
        }

        private void KillTweens()
        {
            _timeScaleTween?.Kill();
            _zoomTween?.Kill();
            _timeScaleTween = null;
            _zoomTween = null;
        }

        private void OnScorePoint(ScorePointMessage msg)
        {
            if (_sessionActive || Cinematic.IsPlaying)
            {
                return;
            }

            var willLevelUp = _scoreController.WillLevelUp();

            if (!willLevelUp)
            {
                return;
            }

            _sessionActive = true;
            _tippingTrailId = new TrailId(msg);
            _trackedTrail = null;
            _lastTrailPosition = msg.WorldPosition;

            _scoreTrailService.Tracker.TrackTrail(_tippingTrailId, OnTippingTrailSpawned);
        }

        private void OnLevelUpDismissed(LevelUpDismissedMessage msg)
        {
            if (!_sessionActive)
            {
                Time.timeScale = 1f;
                Navigation.TransitionTo(NavigationState.Game);
                return;
            }

            if (!_director.IsCinematicActive)
            {
                _sessionActive = false;
                KillTweens();
                RestoreImmediate();
                Time.timeScale = 1f;
                Navigation.TransitionTo(NavigationState.Game);
                return;
            }

            PrepareRestore();

            _director.PlayScene(new CinematicScene(
                onEnd: OnRestoreComplete));
        }

        private void OnRestoreComplete()
        {
            if (_orthoController != null)
            {
                _orthoController.enabled = true;
            }

            _sessionActive = false;
            _director.EndCinematic();
            Navigation.TransitionTo(NavigationState.Game);
        }


        private void OnTippingTrailSpawned(Transform trailTransform)
        {
            _trackedTrail = trailTransform;
            _trailElapsed = 0f;
            _lastTrailPosition = trailTransform.position;


            _director.BeginCinematic(CinematicState.LevelUpTrail);

            _scoreTrailService.PauseTrailsAbove(_tippingTrailId);

            PreparePanIn();

            _scoreTrailService.Tracker.ResumeTrail(_tippingTrailId);

            _director.PlayScene(new CinematicScene(
                onTick: PanInTick));
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            var isScenePlaying = _director.IsScenePlaying;
            var matches = msg.ColorName == _tippingTrailId.Color
                          && msg.Score == _tippingTrailId.Score
                          && msg.Level == _tippingTrailId.Level;

            if (!isScenePlaying)
            {
                return;
            }

            if (!matches)
            {
                return;
            }

            _trackedTrail = null;
            _scoreTrailService.Tracker.ClearTrackedTrail(_tippingTrailId);
            KillTweens();
            _director.CompleteScene();
        }

        private void PanInTick()
        {
            if (_camera == null)
            {
                return;
            }

            _trailElapsed += Time.unscaledDeltaTime;

            if (_trackedTrail != null)
            {
                _lastTrailPosition = _trackedTrail.position;
                var scale = _trackedTrailScaleCurve.Evaluate(_trailElapsed);
                _trackedTrail.localScale = Vector3.one * scale;
            }

            var panTarget = Vector3.Lerp(_basePosition, _lastTrailPosition, _cameraPanWeight);
            panTarget.z = _basePosition.z;

            _camera.transform.position = Vector3.Lerp(
                _camera.transform.position,
                panTarget,
                _cameraFollowSpeed * Time.unscaledDeltaTime);
        }

        private void CaptureBaseState()
        {
            if (_camera != null)
            {
                _baseOrthoSize = _camera.orthographicSize;
                _basePosition = _camera.transform.position;
            }
        }

        private void PreparePanIn()
        {
            KillTweens();

            if (_orthoController != null)
            {
                _orthoController.enabled = false;
            }

            CaptureBaseState();

            _timeScaleTween = DOTween.To(
                    () => Time.timeScale,
                    x => Time.timeScale = x,
                    _slowTimeScale,
                    _slowDownDuration)
                .SetUpdate(true);

            if (_camera != null)
            {
                _zoomTween = DOTween.To(
                        () => _camera.orthographicSize,
                        x => _camera.orthographicSize = x,
                        _baseOrthoSize - _zoomAmount,
                        _slowDownDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        private void PrepareRestore()
        {
            KillTweens();

            _timeScaleTween = DOTween.To(
                    () => Time.timeScale,
                    x => Time.timeScale = x,
                    1f,
                    _restoreDuration)
                .SetUpdate(true)
                .OnComplete(() => _director.CompleteScene());

            if (_camera != null)
            {
                var moveTween = _camera.transform.DOMove(_basePosition, _restoreDuration)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true);

                var sizeTween = DOTween.To(
                        () => _camera.orthographicSize,
                        x => _camera.orthographicSize = x,
                        _baseOrthoSize,
                        _restoreDuration)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true);

                var sequence = DOTween.Sequence().SetUpdate(true);
                sequence.Join(moveTween);
                sequence.Join(sizeTween);

                _zoomTween = sequence;
            }
        }

        private void RestoreImmediate()
        {
            if (_orthoController != null)
            {
                _orthoController.enabled = true;
            }

            if (_camera != null && _baseOrthoSize > 0f)
            {
                _camera.orthographicSize = _baseOrthoSize;
                _camera.transform.position = _basePosition;
            }
        }
    }
}
