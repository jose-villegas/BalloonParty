using System;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BalloonParty.Shared.Animation
{
    internal enum OscillationChannelType
    {
        Position,
        Scale,
        Rotation
    }

    internal enum OscillationPivot
    {
        /// <summary>Oscillates ±offset/2 around the current value (symmetric).</summary>
        Center,
        /// <summary>Oscillates from the current value to current+offset.</summary>
        Edge
    }

    [Serializable]
    internal struct OscillationChannel
    {
        [Tooltip("Which transform property to oscillate.")]
        public OscillationChannelType Type;

        [Tooltip("Offset from current value at peak of oscillation.")]
        public Vector3 Offset;

        [Tooltip("Center: oscillates ±offset/2 around the start value. Edge: oscillates from start to start+offset.")]
        public OscillationPivot Pivot;

        [Tooltip("Per-channel delay added on top of the shared delay — use to phase-offset channels (e.g. squash/stretch wobble).")]
        public float Delay;
    }

    /// <summary>
    ///     General-purpose oscillation effect. Add one or more channels to bob position,
    ///     breathe scale, or wobble rotation — all sharing the same timing.
    ///     Toggle <c>Preview</c> in the Inspector to see the motion in edit mode.
    /// </summary>
    internal class TweenOscillator : MonoBehaviour
    {
        [SerializeField] private OscillationChannel[] _channels =
        {
            new() { Type = OscillationChannelType.Position, Offset = new Vector3(0f, 0.5f, 0f) }
        };
        [SerializeField] private float _duration = 1f;
        [SerializeField] private float _delay;
        [SerializeField] private Ease _ease = Ease.InOutSine;
        [SerializeField] private bool _playOnEnable = true;

        private RectTransform _rectTransform;
        private Tween[] _tweens;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (Application.isPlaying && _playOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Kill();
#if UNITY_EDITOR
            StopPreview();
#endif
        }

        public void Play()
        {
            Kill();

            _tweens = new Tween[_channels.Length];
            for (var i = 0; i < _channels.Length; i++)
            {
                _tweens[i] = CreateTween(in _channels[i]);
            }
        }

        public void Kill()
        {
            if (_tweens == null)
            {
                return;
            }

            for (var i = 0; i < _tweens.Length; i++)
            {
                if (_tweens[i] != null && _tweens[i].IsActive())
                {
                    _tweens[i].Kill();
                }
            }

            _tweens = null;
        }

        private Tween CreateTween(in OscillationChannel channel)
        {
            bool center = channel.Pivot == OscillationPivot.Center;
            Vector3 half = center ? channel.Offset * 0.5f : Vector3.zero;

            Tween tween = channel.Type switch
            {
                OscillationChannelType.Position => CreatePositionTween(channel.Offset, half, center),
                OscillationChannelType.Scale => CreateScaleTween(channel.Offset, half, center),
                OscillationChannelType.Rotation => CreateRotationTween(channel.Offset, half, center),
                _ => null
            };

            return tween?
                .SetEase(_ease)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(_delay + channel.Delay)
                .SetLink(gameObject);
        }

        private Tween CreatePositionTween(Vector3 offset, Vector3 half, bool center)
        {
            if (_rectTransform != null)
            {
                Vector2 offset2D = offset;
                if (center)
                {
                    _rectTransform.anchoredPosition -= (Vector2)half;
                }

                return _rectTransform.DOAnchorPos(_rectTransform.anchoredPosition + offset2D, _duration);
            }

            if (center)
            {
                transform.localPosition -= half;
            }

            return transform.DOLocalMove(transform.localPosition + offset, _duration);
        }

        private Tween CreateScaleTween(Vector3 offset, Vector3 half, bool center)
        {
            if (center)
            {
                transform.localScale -= half;
            }

            return transform.DOScale(transform.localScale + offset, _duration);
        }

        private Tween CreateRotationTween(Vector3 offset, Vector3 half, bool center)
        {
            if (center)
            {
                transform.localEulerAngles -= half;
            }

            return transform.DOLocalRotate(transform.localEulerAngles + offset, _duration, RotateMode.FastBeyond360);
        }

#if UNITY_EDITOR
        [Header("Editor")]
        [SerializeField] private bool _preview;

        private Vector2 _previewAnchoredPos;
        private Vector3 _previewPosition;
        private Vector3 _previewScale;
        private Vector3 _previewRotation;
        private bool _previewing;
        private double _previewStartTime;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (_preview && !_previewing)
            {
                StartPreview();
            }
            else if (!_preview && _previewing)
            {
                StopPreview();
            }
        }

        private void StartPreview()
        {
            _previewing = true;
            var rt = transform as RectTransform;
            _previewAnchoredPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            _previewPosition = transform.localPosition;
            _previewScale = transform.localScale;
            _previewRotation = transform.localEulerAngles;
            _previewStartTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += PreviewTick;
        }

        private void StopPreview()
        {
            EditorApplication.update -= PreviewTick;

            if (_previewing)
            {
                var rt = transform as RectTransform;
                if (rt != null)
                {
                    rt.anchoredPosition = _previewAnchoredPos;
                }
                else
                {
                    transform.localPosition = _previewPosition;
                }

                transform.localScale = _previewScale;
                transform.localEulerAngles = _previewRotation;
            }

            _previewing = false;
        }

        private void PreviewTick()
        {
            if (this == null || !_preview)
            {
                EditorApplication.update -= PreviewTick;
                _previewing = false;
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _previewStartTime);

            foreach (var channel in _channels)
            {
                float channelElapsed = elapsed - channel.Delay;
                if (channelElapsed < 0f)
                {
                    continue;
                }

                float t = channelElapsed / Mathf.Max(_duration, 0.001f);
                float wave = (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f;

                // Center: remap wave 0→1 to -0.5→+0.5 so oscillation is symmetric around origin.
                float factor = channel.Pivot == OscillationPivot.Center ? wave - 0.5f : wave;

                switch (channel.Type)
                {
                    case OscillationChannelType.Position:
                        var rt = transform as RectTransform;
                        if (rt != null)
                        {
                            rt.anchoredPosition = _previewAnchoredPos + (Vector2)(channel.Offset * factor);
                        }
                        else
                        {
                            transform.localPosition = _previewPosition + channel.Offset * factor;
                        }

                        break;
                    case OscillationChannelType.Scale:
                        transform.localScale = _previewScale + channel.Offset * factor;
                        break;
                    case OscillationChannelType.Rotation:
                        transform.localEulerAngles = _previewRotation + channel.Offset * factor;
                        break;
                }
            }

            SceneView.RepaintAll();
        }
#endif
    }
}
