using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Diagnostics;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     The soap bubble round a balloon wearing foam (<see cref="BalloonParty.Slots.Capabilities.ICoatable" />): a
    ///     child sprite drawn with the soap cluster's material showing one bubble, centred on the balloon's body and
    ///     sized to enclose it. <see cref="BalloonView" /> creates it the first time its balloon is coated, so the
    ///     balloons that never are carry nothing; it shows it while the coat lasts and hides it on bind, pop and
    ///     despawn, so a pooled view never comes back coated. A coat a hit takes off puffs away
    ///     (<see cref="PuffAndHide" />) rather than vanishing. All per-instance shader state goes through one
    ///     <see cref="MaterialPropertyBlock" />, so every coat batches on the shared material.
    /// </summary>
    internal sealed class FoamCoatOverlay : MonoBehaviour
    {
        private const float MinScale = 1e-4f;

        // A body with no sprite to measure: a balloon body is about one unit across.
        private const float FallbackBodyRadius = 0.5f;

        private Transform _host;
        private SpriteRenderer _body;
        private IRipeBubbleSettings _settings;
        private SpriteRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Tween _puff;

        /// <summary>Adds the coat as a hidden child of <paramref name="host" />, on <paramref name="body" />'s sorting
        /// layer. With no material or sprite in the settings it warns and never shows.</summary>
        internal static FoamCoatOverlay Create(Transform host, SpriteRenderer body, IRipeBubbleSettings settings)
        {
            var child = new GameObject("FoamCoat");
            child.layer = host.gameObject.layer;
            child.transform.SetParent(host, false);
            var overlay = child.AddComponent<FoamCoatOverlay>();
            overlay.Build(host, body, settings);
            return overlay;
        }

        /// <summary>
        ///     The centre and radius of <paramref name="body" />'s sprite in <paramref name="host" />'s local space, from
        ///     the local position, rotation and scale of each transform between the two. Neither the host's own scale
        ///     nor anything above it enters, so a balloon held at zero scale through its spawn flight, or caught
        ///     mid-pulse, measures as it does at rest. A link at zero scale counts as 1: the body is mid-tween, and its
        ///     rest size is the one to fit. A body that is not under the host measures as a unit balloon at the centre.
        /// </summary>
        internal static void MeasureBody(Transform host, SpriteRenderer body, out Vector2 centre, out float radius)
        {
            centre = Vector2.zero;
            radius = FallbackBodyRadius;
            if (body == null || body.sprite == null)
            {
                return;
            }

            var bounds = body.sprite.bounds;
            var point = bounds.center;
            var size = Mathf.Max(bounds.extents.x, bounds.extents.y);
            for (var link = body.transform; link != host; link = link.parent)
            {
                if (link == null)
                {
                    return;
                }

                var scale = link.localScale;
                var x = UsableScale(scale.x);
                var y = UsableScale(scale.y);
                point = link.localRotation * new Vector3(point.x * x, point.y * y, point.z * UsableScale(scale.z))
                        + link.localPosition;
                size *= Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            }

            centre = point;
            radius = size;
        }

        /// <summary>Re-fits round the body every time: a pooled view's body can rest at a different scale from one
        /// balloon to the next, and the fit is a short walk up a transform or two.</summary>
        internal void Show()
        {
            if (_renderer == null)
            {
                return;
            }

            StopPuff();
            Fit();
            _renderer.enabled = true;
        }

        internal void Hide()
        {
            StopPuff();
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        /// <summary>
        ///     The coat bursts off: it swells to <see cref="IRipeBubbleSettings.CoatPuffScale" /> of its size and fades
        ///     out over <see cref="IRipeBubbleSettings.CoatPuffSeconds" />, then hides. A coat not showing just stays
        ///     hidden; <see cref="Show" /> or <see cref="Hide" /> cut a puff short.
        /// </summary>
        internal void PuffAndHide()
        {
            if (_renderer == null || !_renderer.enabled || _settings.CoatPuffSeconds <= 0f)
            {
                Hide();
                return;
            }

            StopPuff();
            var seconds = _settings.CoatPuffSeconds;
            _puff = DOTween.Sequence()
                .SetTarget(this)
                .SetLink(gameObject)
                .Join(transform.DOScale(transform.localScale * _settings.CoatPuffScale, seconds).SetEase(Ease.OutQuad))
                .Join(_renderer.DOFade(0f, seconds).SetEase(Ease.InQuad))
                .OnComplete(Hide);
        }

        internal void ApplySortingOrder(int order)
        {
            if (_renderer != null)
            {
                _renderer.sortingOrder = order;
            }
        }

        // A puff cut short leaves the coat faded; the next Show must start opaque.
        private void StopPuff()
        {
            if (_puff == null)
            {
                return;
            }

            _puff.Kill();
            _puff = null;
            if (_renderer != null)
            {
                var color = _renderer.color;
                color.a = 1f;
                _renderer.color = color;
            }
        }

        private static float UsableScale(float scale)
        {
            return float.IsFinite(scale) && Mathf.Abs(scale) >= MinScale ? scale : 1f;
        }

        private void Build(Transform host, SpriteRenderer body, IRipeBubbleSettings settings)
        {
            if (settings == null || settings.CoatMaterial == null || settings.CoatSprite == null)
            {
                Log.Warn("FoamCoat",
                    "The ripe bubble settings have no coat material or sprite: coated balloons will not show it.",
                    this);
                return;
            }

            _host = host;
            _body = body;
            _settings = settings;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sharedMaterial = settings.CoatMaterial;
            _renderer.sprite = settings.CoatSprite;
            _renderer.enabled = false;
            if (body != null)
            {
                _renderer.sortingLayerID = body.sortingLayerID;
            }

            PushLook();
        }

        // Sized to CoatScale times the body's radius. Nothing that is not a finite, positive number is written: a bad
        // fit keeps the last good one. Fit also undoes a puff's swell, since Show re-fits.
        private void Fit()
        {
            MeasureBody(_host, _body, out var centre, out var bodyRadius);
            var scale = SoapBubbleShader.ScaleForRadius(
                _settings.CoatMaterial, _settings.CoatSprite, bodyRadius * _settings.CoatScale);

            if (float.IsFinite(centre.x) && float.IsFinite(centre.y))
            {
                transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            }

            if (float.IsFinite(scale) && scale > MinScale)
            {
                transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // Pushed once: the shader runs its own float and spin clock from _Time.y after this.
        private void PushLook()
        {
            _block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            SoapBubbleShader.SetLoneBubble(_block);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
