#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Shared.Thermal
{
    /// <summary>
    ///     Reads thermal state from Android <c>PowerManager</c> via JNI. Polling is rate-limited and
    ///     cached (<see cref="PollMinIntervalSeconds" />): <c>getThermalHeadroom</c> is documented to be
    ///     called no more than once per second and returns <c>NaN</c> when queried too soon or when the
    ///     device has no thermal sensor exposed — both are mapped to <c>0</c> (fully cool).
    /// </summary>
    /// <remarks>
    ///     The whole class is compiled only for on-device Android builds; <c>dotnet build</c> (which
    ///     defines <c>UNITY_EDITOR</c>) skips it, so the JNI interop below is validated only by an
    ///     in-editor Android build or on the device — never by the headless compile check.
    /// </remarks>
    internal sealed class AndroidThermalSource : IThermalSource, IDisposable
    {
        private const int ForecastSeconds = 10;
        private const float PollMinIntervalSeconds = 2f;

        private readonly AndroidJavaObject _powerManager;
        private float _lastPollTime = float.NegativeInfinity;
        private float _headroom;
        private int _status = -1;

        public AndroidThermalSource()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                // Context.POWER_SERVICE == "power".
                _powerManager = activity.Call<AndroidJavaObject>("getSystemService", "power");
            }
            catch (System.Exception e)
            {
                Log.Warn("ThermalGovernor", $"PowerManager unavailable: {e.Message}");
                _powerManager = null;
            }
        }

        // The JNI global ref is scope-owned; VContainer disposes singletons on scope teardown, so
        // release deterministically instead of leaning on AndroidJavaObject's finalizer.
        public void Dispose()
        {
            _powerManager?.Dispose();
        }

        public float Headroom
        {
            get
            {
                Poll();
                return _headroom;
            }
        }

        public int Status
        {
            get
            {
                Poll();
                return _status;
            }
        }

        private void Poll()
        {
            var now = Time.realtimeSinceStartup;
            if (now - _lastPollTime < PollMinIntervalSeconds)
            {
                return;
            }

            _lastPollTime = now;

            if (_powerManager == null)
            {
                _headroom = 0f;
                _status = -1;
                return;
            }

            try
            {
                _status = _powerManager.Call<int>("getCurrentThermalStatus");
            }
            catch (System.Exception)
            {
                _status = -1;
            }

            try
            {
                var headroom = _powerManager.Call<float>("getThermalHeadroom", ForecastSeconds);
                _headroom = float.IsNaN(headroom) ? 0f : headroom;
            }
            catch (System.Exception)
            {
                _headroom = 0f;
            }
        }
    }
}
#endif
