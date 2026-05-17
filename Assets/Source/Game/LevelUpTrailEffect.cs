using BalloonParty.Display;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Slows time, zooms in, and pans the camera to follow the score trail
    ///     when the incoming score will trigger a level-up. The trail target is
    ///     a fixed world position corresponding to the screen-space progress bar;
    ///     because panning shifts the camera, a LateUpdate pass offsets the
    ///     tracked trail by the camera delta so it stays visually aligned with
    ///     the bar. DOTween overwrites the position each Update (it stores
    ///     start/end internally), and we re-apply in LateUpdate — each rendered
    ///     frame shows the compensated position. Keeps everything frozen while
    ///     the level-up popup is visible, then smoothly restores on Continue.
    /// </summary>
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

        [Header("Canvas")]
        [SerializeField] private Canvas _uiCanvas;
        [SerializeField] private float _canvasTargetScale = 1.15f;
        [SerializeField] private RectTransform _uiContent;
        [SerializeField] [Range(0f, 1f)] private float _canvasPositionWeight = 0.5f;
        [SerializeField] private float _canvasFollowSpeed = 4f;

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
        private Transform _trackedTrail;
        private Vector3 _lastTrailPosition;
        private string _pendingColor;
        private int _pendingTrailCount;
        private Vector2 _canvasPositionOffset;
        private Vector2 _canvasPositionTarget;
        private CanvasScaler _canvasScaler;
        private Vector2 _baseReferenceResolution;
        private Tween _timeScaleTween;
        private Tween _zoomTween;
        private Tween _canvasScaleTween;

        private void Start()
        {
            _camera = Camera.main;

            if (_uiCanvas != null)
            {
                var root = _uiCanvas.rootCanvas;
                _canvasScaler = root.GetComponent<CanvasScaler>();
                if (_canvasScaler != null)
                {
                    _baseReferenceResolution = _canvasScaler.referenceResolution;
                }
            }

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

            // Lazy acquire — subscription order may cause the trail to not
            // exist yet when OnBalloonScored runs.
            if (_trackedTrail == null && _pendingColor != null)
            {
                _trackedTrail = _scoreTrailService.GetLastSpawnedTrail(_pendingColor);
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

        /// <summary>
        ///     After DOTween updates the trail position, shift it by the camera
        ///     pan delta so it stays visually aligned with the screen-space UI.
        ///     Also offsets the UI content RectTransform toward the trail's
        ///     target bar so it drifts into view during the cinematic. The
        ///     target offset is computed once in <see cref="BeginSlowMotion"/>
        ///     from the bar's screen position relative to screen center.
        /// </summary>
        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            var cameraDelta = _camera.transform.position - _basePosition;
            cameraDelta.z = 0f;

            if (_following && _trackedTrail != null)
            {
                _trackedTrail.position += cameraDelta;
            }

            if (!_active || _uiContent == null)
            {
                return;
            }

            _canvasPositionOffset = Vector2.Lerp(
                _canvasPositionOffset,
                _canvasPositionTarget,
                _canvasFollowSpeed * Time.unscaledDeltaTime);

            _uiContent.anchoredPosition = _canvasPositionOffset;
        }

        private void OnDestroy()
        {
            KillTweens();
            Cinematic.End();

            if (_canvasScaler != null)
            {
                _canvasScaler.referenceResolution = _baseReferenceResolution;
            }

            if (_uiContent != null)
            {
                _uiContent.anchoredPosition = Vector2.zero;
            }

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
            _pendingTrailCount = msg.Points;
            _trackedTrail = _scoreTrailService.GetLastSpawnedTrail(msg.ColorName);

            Cinematic.Begin(CinematicState.LevelUpTrail);

            // Cinematic.Begin paused all active trails — resume the one we
            // are tracking so it keeps flying toward the progress bar.
            _scoreTrailService.ResumeTrail(_trackedTrail);

            BeginSlowMotion(msg.WorldPosition);
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_active || msg.ColorName != _pendingColor)
            {
                return;
            }

            _pendingTrailCount--;
            if (_pendingTrailCount > 0)
            {
                return;
            }

            EndSlowMotion();
        }

        private void OnLevelUpDismissed(LevelUpDismissedMessage msg)
        {
            if (!_active)
            {
                Navigation.TransitionTo(NavigationState.Game);
                return;
            }

            Restore();
        }

        private void BeginSlowMotion(Vector3 focusWorldPosition)
        {
            KillTweens();
            _following = true;
            _lastTrailPosition = focusWorldPosition;
            _canvasPositionOffset = Vector2.zero;
            _canvasPositionTarget = Vector2.zero;

            if (_orthoController != null)
            {
                _orthoController.enabled = false;
            }

            // Snapshot current camera state — must happen after
            // OrthogonalSizeCameraController has applied.
            if (_camera != null)
            {
                _baseOrthoSize = _camera.orthographicSize;
                _basePosition = _camera.transform.position;
            }

            // Compute horizontal UI drift: shift content so the target bar
            // moves toward screen center. The bar's screen offset from center
            // is converted to the content RectTransform's local space.
            if (_uiContent != null && _pendingColor != null && _camera != null)
            {
                var barWorld = _scoreTrailService.GetTargetPosition(_pendingColor);
                var barScreen = _camera.WorldToScreenPoint(barWorld);
                var centerScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var screenDelta = (Vector2)barScreen - centerScreen;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _uiContent, centerScreen, _camera, out var localCenter);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _uiContent, centerScreen + screenDelta, _camera, out var localBar);

                // Negate so the content shifts the bar TOWARD center
                _canvasPositionTarget = -(localBar - localCenter) * _canvasPositionWeight;
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

            if (_canvasScaler != null && _canvasTargetScale > 1f)
            {
                var zoomedResolution = _baseReferenceResolution / _canvasTargetScale;
                _canvasScaleTween = DOTween.To(
                        () => _canvasScaler.referenceResolution,
                        x => _canvasScaler.referenceResolution = x,
                        zoomedResolution,
                        _slowDownDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        /// <summary>
        ///     Trail arrived — stop following but preserve camera position,
        ///     zoom, and timeScale. CheckLevelUp will set timeScale = 0 on the
        ///     same frame, and the popup appears over the zoomed/panned view.
        /// </summary>
        private void EndSlowMotion()
        {
            _following = false;
            _trackedTrail = null;
            KillTweens();
        }

        /// <summary>
        ///     Player pressed Continue — smoothly pan the camera back, restore
        ///     orthographic size, canvas scale, and ease timeScale to 1.
        ///     Transitions navigation back to Game only after all tweens finish.
        /// </summary>
        private void Restore()
        {
            _active = false;
            _following = false;
            _pendingColor = null;
            _trackedTrail = null;
            _canvasPositionOffset = Vector2.zero;
            _canvasPositionTarget = Vector2.zero;

            if (_uiContent != null)
            {
                _uiContent.anchoredPosition = Vector2.zero;
            }

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

            if (_canvasScaler != null)
            {
                _canvasScaleTween = DOTween.To(
                        () => _canvasScaler.referenceResolution,
                        x => _canvasScaler.referenceResolution = x,
                        _baseReferenceResolution,
                        _restoreDuration)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true);
            }
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
            _canvasScaleTween?.Kill();
            _timeScaleTween = null;
            _zoomTween = null;
            _canvasScaleTween = null;
        }
    }
}
