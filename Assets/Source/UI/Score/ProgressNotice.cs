using System;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    /// <summary>
    ///     Floating progress notice: rises in while fading up, holds, then rises out while fading down.
    ///     Hand-ticked instead of Animator-driven — with dozens alive during trail storms, an Animator
    ///     re-writes (and canvas-dirties) every frame even through the hold, while this writes nothing
    ///     once the pose settles. Timings and the two event cues (fully-shown, completed) are lifted
    ///     from the retired ScoreUp/ScoreStreakUp/ScoreDisappear clips.
    /// </summary>
    public class ProgressNotice : MonoBehaviour, IPoolable
    {
        // The ScoreUp clip fired its OnAnimationScoreFully event 0.05s in.
        private const float FullyShownDelay = 0.05f;

        [SerializeField] private ColorableRenderer[] _colorableRenderer;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Transform _labelTransform;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private AnimationCurve _scaleCurve;
        [SerializeField] private AnimationCurve _labelOffsetXCurve;

        [Tooltip("Fade/rise-in length. The retired clips ran 5 frames at 60 fps.")]
        [SerializeField] private float _appearDuration = 0.0833f;

        [Tooltip("How long the notice holds fully shown before rising out. Negative = hold until " +
                 "Dismiss() (the streak notice).")]
        [SerializeField] private float _holdDuration = 0.25f;

        [Tooltip("Fade/rise-out length. The retired clips ran 5 frames at 60 fps.")]
        [SerializeField] private float _exitDuration = 0.0833f;

        [Tooltip("Anchored-Y travel: enters from -this below the rest position, exits to +this above.")]
        [SerializeField] private float _riseOffset = 10f;

        private RectTransform _rect;
        private Action _onComplete;
        private Phase _phase;
        private float _phaseElapsed;
        private float _shownElapsed;
        private float _exitFromAlpha;
        private float _exitFromY;
        private int _lastShownScore = int.MinValue;

        public bool IsFullyShown { get; private set; }

        private void Awake()
        {
            _rect = (RectTransform)transform;

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        // Unscaled on purpose — the retired Animator ran UpdateMode.UnscaledTime so notices keep
        // animating through the level-up freeze (timeScale = 0).
        private void Update()
        {
            if (_phase == Phase.Idle)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            _shownElapsed += dt;
            _phaseElapsed += dt;

            if (!IsFullyShown && _shownElapsed >= FullyShownDelay)
            {
                IsFullyShown = true;
            }

            switch (_phase)
            {
                case Phase.Appear:
                    TickAppear();
                    break;
                case Phase.Hold:
                    // No writes while holding — the pose is settled, so the canvas stays clean.
                    if (_holdDuration >= 0f && _phaseElapsed >= _holdDuration)
                    {
                        BeginExit();
                    }

                    break;
                case Phase.Exit:
                    TickExit();
                    break;
            }
        }

        public void OnSpawned()
        {
            IsFullyShown = false;
            _onComplete = null;
            _phase = Phase.Idle;
        }

        public void OnDespawned() { }

        public void SetAnchoredPosition(Vector2 position)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = position;
        }

        public void Show(int score, Action onComplete, Color? color = null)
        {
            _onComplete = onComplete;

            if (color.HasValue && _colorableRenderer != null && _colorableRenderer.Length > 0)
            {
                foreach (var colorable in _colorableRenderer)
                {
                    colorable.SetColor(color.Value);
                }
            }

            // Point notices always show "1", so a pooled reuse skips the string alloc + TMP reparse.
            if (score != _lastShownScore)
            {
                _label.text = score.ToString("N0");
                _lastShownScore = score;
            }

            transform.localScale = Vector3.one;
            _labelTransform.localScale = Vector3.one * _scaleCurve.Evaluate(score);

            var labelRect = (RectTransform)_labelTransform;
            labelRect.anchoredPosition = new Vector2(_labelOffsetXCurve.Evaluate(score), labelRect.anchoredPosition.y);

            // Like the retired clips, entry animates anchored Y absolutely: -riseOffset up to a rest
            // of 0 in the bar's space — only the X set by the spawner survives.
            IsFullyShown = false;
            _shownElapsed = 0f;
            _phaseElapsed = 0f;
            _phase = Phase.Appear;
            ApplyPose(0f, -_riseOffset);
        }

        public void Dismiss()
        {
            if (_phase == Phase.Exit || _phase == Phase.Idle)
            {
                return;
            }

            BeginExit();
        }

        // Returns to the pool at once, skipping the disappear animation — used when the notice must
        // clear immediately regardless of what phase it is in.
        public void DismissImmediate()
        {
            _phase = Phase.Idle;
            Complete();
        }

        private void TickAppear()
        {
            // Mathf.SmoothStep is the exact interpolation of the retired clips' auto-clamped keys.
            var t = Mathf.SmoothStep(0f, 1f, _phaseElapsed / _appearDuration);
            ApplyPose(t, Mathf.Lerp(-_riseOffset, 0f, t));

            if (_phaseElapsed >= _appearDuration)
            {
                ApplyPose(1f, 0f);
                _phase = Phase.Hold;
                _phaseElapsed = 0f;
            }
        }

        private void TickExit()
        {
            var t = Mathf.SmoothStep(0f, 1f, _phaseElapsed / _exitDuration);
            ApplyPose(Mathf.Lerp(_exitFromAlpha, 0f, t), Mathf.Lerp(_exitFromY, _riseOffset, t));

            if (_phaseElapsed >= _exitDuration)
            {
                _phase = Phase.Idle;
                Complete();
            }
        }

        // Exits from the CURRENT pose, so dismissing mid-appear fades from where it is instead of
        // snapping to fully-shown first (the old Play(ScoreDisappear) started its curves at alpha 1).
        private void BeginExit()
        {
            _exitFromAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            _exitFromY = _rect.anchoredPosition.y;
            _phase = Phase.Exit;
            _phaseElapsed = 0f;
        }

        private void ApplyPose(float alpha, float y)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
            }

            var position = _rect.anchoredPosition;
            position.y = y;
            _rect.anchoredPosition = position;
        }

        private void Complete()
        {
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        private enum Phase
        {
            Idle,
            Appear,
            Hold,
            Exit
        }
    }
}
