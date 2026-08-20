using System;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Rendering;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     Owns the sky-scatter backdrop: a single stationary quad rendered immediately after the
    ///     camera's flat clear colour and behind every other backdrop layer (BackgroundCloud, WallNet,
    ///     SmokeField) — a naive 2D stand-in for atmospheric scattering
    ///     (<c>Shaders/BalloonParty/Display/SkyScatter.shader</c>). All the actual look (ring count,
    ///     colours, wave, cloud distortion) lives on the material via
    ///     <see cref="ISkyScatterSettings.Material" />; this service only sizes and places the quad.
    ///     Sized to the reference world dimensions (<see cref="IGameDisplayConfiguration" />) times an
    ///     overscan margin rather than tracking the camera, so a shake or cinematic pan never reveals an
    ///     edge — the same trade the BackgroundCloud backdrop makes. Registered in
    ///     <c>AppLifetimeScope</c> (the persistent app root) so the sky reads correctly from the Launch
    ///     begin-screen's first frame, not just once Game loads.
    ///
    ///     Deliberately plain C# rather than a MonoBehaviour View, even though it owns a
    ///     <see cref="MeshRenderer" />: the quad is built once and never touched again (no per-frame
    ///     state, no Unity-lifecycle callbacks needed), and a MonoBehaviour View here would require
    ///     hand-authoring a nested GameObject into a prefab's serialized data to satisfy
    ///     <c>RegisterComponentInHierarchy</c> — a materially riskier edit without an editor to verify
    ///     it than the single settings-asset field this design already needs. If a future revision adds
    ///     per-frame behaviour (tracking the camera, reacting to a new input), split the renderer-owning
    ///     half into a View at that point.
    /// </summary>
    internal sealed class SkyScatterService : IStartable, IDisposable
    {
        // "new GameObject" defaults to layer 0 (Default), which NavigationCameraReveal's Launch
        // culling mask excludes — Scenario is the layer BackgroundCloud/WallNet/SmokeField already
        // render on, so the sky stays visible through the same navigation states they do.
        private static readonly int ScenarioLayer = LayerMask.NameToLayer("Scenario");

        private readonly ISkyScatterSettings _settings;
        private readonly IGameDisplayConfiguration _display;

        private GameObject _quad;

        internal SkyScatterService(ISkyScatterSettings settings, IGameDisplayConfiguration display)
        {
            _settings = settings;
            _display = display;
        }

        void IStartable.Start()
        {
            if (_settings?.Material == null)
            {
                Log.Warn("SkyScatter", "disabled: assign a material on the SkyScatterSettings asset.");
                return;
            }

            _quad = QuadRendererBuilder.Build(
                "SkyScatter", _settings.Material, ScenarioLayer,
                _settings.SortingLayerName, _settings.SortingOrder);

            // DontDestroyOnLoad throws outside play mode (PoolManager.Root guards it the same way).
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_quad);
            }

            var overscan = Mathf.Max(1f, _settings.OverscanMultiplier);
            _quad.transform.localScale = new Vector3(
                _display.ReferenceWorldWidth * overscan,
                _display.ReferenceWorldHeight * overscan,
                1f);
        }

        void IDisposable.Dispose()
        {
            if (_quad != null)
            {
                UnityEngine.Object.Destroy(_quad);
                _quad = null;
            }
        }
    }
}
