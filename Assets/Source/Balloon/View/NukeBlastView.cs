using System;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.SceneLight;
using DG.Tweening;
using UnityEngine;
using VContainer;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Draws the nuke's blast (PLAN-NukeBalloon) on the <c>BalloonParty/Sprite/NukeBlast</c> material: a white flood
    ///     that blinds the board as it spreads, a white-hot core and a shock ring at the edge
    ///     (<see cref="GrowingDiscView" />). It carries a scene light whose radius follows the disc, so the board itself
    ///     is lit white as the disc crosses it, fading once the disc covers the board. The blast's start gives the
    ///     disturbance field its strongest push, whose impact report carries the blast that digs the water's crater
    ///     on a water level, and the fuse before it an implosion that pulls the field in.
    /// </summary>
    internal sealed class NukeBlastView : GrowingDiscView, INukeBlastView
    {
        private INukeBlastSettings _settings;
        private INukeFuseSettings _fuse;
        private SceneLightFieldService _sceneLight;
        private DisturbanceFieldService _disturbance;
        private Light _light;
        private IDisposable _lightRegistration;
        private Tween _lightFade;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseLight();
        }

        [Inject]
        internal void Construct(
            IBalloonsConfiguration balloons, SceneLightFieldService sceneLight, DisturbanceFieldService disturbance)
        {
            _settings = balloons.NukeBlast;
            _fuse = balloons.NukeFuse;
            _sceneLight = sceneLight;
            _disturbance = disturbance;
            Build(_settings.BlastMaterial, _settings.SortingLayerName, _settings.SortingOrder, "NukeBlastView");
        }

        public override void Show(Vector3 center)
        {
            base.Show(center);
            ReleaseLight();
            _light = new Light(center, _settings.LightReach, _settings.LightIntensity);
            _lightRegistration = _sceneLight.RegisterLight(_light);
            // The push's impact carries the blast, which on water digs the crater.
            _disturbance.Stamp(
                center, _settings.PushRadius, _settings.PushStrength, Vector2.zero, _settings.PushSeconds,
                blast: new ImpactEventBus.Blast(
                    _settings.ImpactRadius, _settings.ImpactStrength, _settings.ImpactSeconds));
        }

        public override void SetRadius(float radius, float progress)
        {
            base.SetRadius(radius, progress);
            if (_light == null)
            {
                return;
            }

            _light.Radius.Value = radius + _settings.LightReach;
            // Blinding while it spreads, easing to LightAtEdge as it slows at the far edge; Hide fades the rest.
            _light.Intensity.Value =
                _settings.LightIntensity * Mathf.Lerp(1f, _settings.LightAtEdge, progress * progress);
        }

        public override void Hide()
        {
            base.Hide();
            if (_light == null)
            {
                return;
            }

            if (_settings.LightLinger <= 0f)
            {
                ReleaseLight();
                return;
            }

            var light = _light;
            _lightFade?.Kill();
            _lightFade = DOTween.To(() => light.Intensity.Value, value => light.Intensity.Value = value, 0f,
                    _settings.LightLinger)
                .SetEase(Ease.OutQuad)
                .OnComplete(ReleaseLight);
        }

        public void Implode(Vector3 center, float seconds)
        {
            _disturbance.Stamp(center, _fuse.ImplodeRadius, -_fuse.ImplodeStrength, Vector2.zero, seconds);
        }

        private void ReleaseLight()
        {
            _lightFade?.Kill();
            _lightFade = null;
            _lightRegistration?.Dispose();
            _lightRegistration = null;
            _light = null;
        }
    }
}
