using BalloonParty.Display;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private AnimationCurve _slowDownCurve = AnimationCurve.EaseInOut(0f, 1f, 0.15f, 0.3f);
        [SerializeField] private AnimationCurve _restoreCurve = AnimationCurve.EaseInOut(0f, 0.3f, 0.35f, 1f);

        [Header("Camera")]
        [SerializeField] private Camera _camera;
        [SerializeField] private float _zoomAmount = 0.5f;
        [SerializeField] private float _cameraPanWeight = 0.7f;
        [SerializeField] private float _cameraFollowSpeed = 5f;
        [SerializeField] private AnimationCurve _trackedTrailScaleCurve = AnimationCurve.EaseInOut(0f, 2f, 1f, 1f);

        [Inject] private CinematicDirector _director;
        [Inject] private IGameConfiguration _config;
        [Inject] private ISubscriber<ScorePointMessage> _scoredSubscriber;
        [Inject] private ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        [Inject] private ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        [Inject] private OrthogonalSizeCameraController _orthoController;
        [Inject] private ScoreController _scoreController;
        [Inject] private ScoreTrailService _scoreTrailService;
        [Inject] private PauseService _pauseService;

        private float _baseOrthoSize;
        private Vector3 _basePosition;
        private bool _hasBaseState;
        private Vector3 _lastTrailPosition;
        private float _realElapsed;
        private bool _sessionActive;
        private Tween _timeScaleTween;
        private TrailId _tippingTrailId;
        private TrailFlight _trackedFlight;
        private float _trailElapsed;
        private Vector3 _trailOrigin;
        private Vector3 _trailTargetWorld;
        private Tween _zoomTween;

        private void Start()
        {
            _scoredSubscriber.Subscribe(OnScorePoint).AddTo(this);
            _trailArrivedSubscriber.Subscribe(OnTrailArrived).AddTo(this);
            _dismissedSubscriber.Subscribe(_ => OnDismissed()).AddTo(this);
        }

        private void OnDestroy()
        {
            KillTweens();

            if (_director.IsCinematicActive)
            {
                _director.EndCinematic();
            }

            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
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

            if (!_scoreController.WillLevelUp())
            {
                return;
            }

            _sessionActive = true;
            _tippingTrailId = new TrailId(msg);
            _trackedFlight = null;
            _lastTrailPosition = msg.WorldPosition;

            WaitForTippingTrailAsync().Forget();
        }

        private async UniTaskVoid WaitForTippingTrailAsync()
        {
            var cts = this.GetCancellationTokenOnDestroy();

            await UniTask.WaitUntil(
                () => _scoreTrailService.Flights.Contains(_tippingTrailId),
                cancellationToken: cts);

            if (!_sessionActive)
            {
                return;
            }

            _trackedFlight = _scoreTrailService.Flights.Get(_tippingTrailId);
            if (_trackedFlight == null)
            {
                return;
            }

            BeginCinematicWithTrail();
        }

        private void BeginCinematicWithTrail()
        {
            _trailElapsed = 0f;
            _realElapsed = 0f;
            _trailOrigin = _trackedFlight.Transform.position;
            _lastTrailPosition = _trailOrigin;

            _trailTargetWorld = _scoreTrailService.GetTarget(_tippingTrailId.Color).Center;

            _director.BeginCinematic(CinematicState.LevelUpPanIn);
            _pauseService.Pause(PauseSource.Cinematic);

            _trackedFlight.Transform.GetComponent<FlyingTrail>().DisableMoveTween();
            _trackedFlight.Pause();

            PreparePanIn();
            _director.PlayScene(new CinematicScene(onTick: PanInTick));
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_director.IsScenePlaying)
            {
                return;
            }

            var matches = msg.ColorName == _tippingTrailId.Color
                          && msg.Score == _tippingTrailId.Score
                          && msg.Level == _tippingTrailId.Level;

            if (!matches)
            {
                return;
            }

            _trackedFlight = null;
            KillTweens();
            _scoreTrailService.Flights.CompleteAll();
            _director.CompleteScene();
            _director.EndCinematic();
        }

        private void OnDismissed()
        {
            _director.BeginCinematic(CinematicState.LevelUpRestore);
            _pauseService.Resume(PauseSource.Cinematic);
            PrepareRestore();
            _director.PlayScene(new CinematicScene(onEnd: OnRestoreComplete));
        }

        private void OnRestoreComplete()
        {
            RestoreCamera();
            _sessionActive = false;
            _director.EndCinematic();
            Navigation.TransitionTo(NavigationState.Game);
        }

        private void PanInTick()
        {
            if (_camera == null)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            _realElapsed += dt;

            var slowDownDuration = _slowDownCurve.Duration();
            var curveT = Mathf.Clamp01(_realElapsed / slowDownDuration);
            var speedFactor = _slowDownCurve.Evaluate(curveT);
            _trailElapsed += dt * speedFactor;

            if (_trackedFlight?.Transform != null)
            {
                var progress = Mathf.Clamp01(_trailElapsed / _config.ScorePointTraceDuration);

                _trackedFlight.Transform.localScale =
                    Vector3.one * _trackedTrailScaleCurve.Evaluate(progress);

                var target = _trailTargetWorld;
                target.z = 0f;
                _trackedFlight.Transform.position = Vector3.Lerp(_trailOrigin, target, progress);
                _lastTrailPosition = _trackedFlight.Transform.position;

                if (progress >= 1f)
                {
                    _trackedFlight.Complete();

                    // Guard: if DOComplete found no active tweens the arrived
                    // callback never fired, so force-end the scene here.
                    if (_director.IsScenePlaying)
                    {
                        _trackedFlight = null;
                        KillTweens();
                        _scoreTrailService.Flights.CompleteAll();
                        _director.CompleteScene();
                        _director.EndCinematic();
                    }

                    return;
                }
            }

            var panTarget = Vector3.Lerp(_basePosition, _lastTrailPosition, _cameraPanWeight);
            panTarget.z = _basePosition.z;

            _camera.transform.position = Vector3.Lerp(
                _camera.transform.position,
                panTarget,
                _cameraFollowSpeed * dt);
        }

        private void CaptureBaseState()
        {
            if (_camera != null)
            {
                _baseOrthoSize = _camera.orthographicSize;
                _basePosition = _camera.transform.position;
                _hasBaseState = true;
            }
        }

        private void RestoreCamera()
        {
            if (_camera != null)
            {
                _camera.transform.position = _basePosition;
                _camera.orthographicSize = _baseOrthoSize;
            }

            if (_orthoController != null)
            {
                _orthoController.enabled = true;
            }
        }

        private void PreparePanIn()
        {
            if (_hasBaseState)
            {
                RestoreCamera();
            }

            KillTweens();

            if (_orthoController != null)
            {
                _orthoController.enabled = false;
            }

            CaptureBaseState();

            var slowDownDuration = _slowDownCurve.Duration();

            if (_camera != null)
            {
                _zoomTween = DOTween.To(
                        () => _camera.orthographicSize,
                        x => _camera.orthographicSize = x,
                        _baseOrthoSize - _zoomAmount,
                        slowDownDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        private void PrepareRestore()
        {
            KillTweens();

            var restoreDuration = _restoreCurve.Duration();
            var elapsed = 0f;

            _timeScaleTween = DOTween.To(
                    () => elapsed,
                    x =>
                    {
                        elapsed = x;
                        Time.timeScale = _restoreCurve.Evaluate(x);
                    },
                    restoreDuration,
                    restoreDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(() => _director.CompleteScene());

            if (_camera != null)
            {
                var moveTween = _camera.transform.DOMove(_basePosition, restoreDuration)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true);

                var sizeTween = DOTween.To(
                        () => _camera.orthographicSize,
                        x => _camera.orthographicSize = x,
                        _baseOrthoSize,
                        restoreDuration)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true);

                var sequence = DOTween.Sequence().SetUpdate(true);
                sequence.Join(moveTween);
                sequence.Join(sizeTween);

                _zoomTween = sequence;
            }
            else
            {
                _director.CompleteScene();
            }
        }
    }
}
