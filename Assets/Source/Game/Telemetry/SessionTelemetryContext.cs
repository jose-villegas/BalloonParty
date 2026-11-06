using System;

namespace BalloonParty.Game.Telemetry
{
    // Session identity for every emitted envelope (R22). Registered in AppLifetimeScope so it survives
    // the scene reload a restart performs — a Game-scope registration would mint a new session id per
    // scene load and shatter one play session into several in the warehouse.
    //
    // The id is a per-launch Guid and is NEVER persisted (RK-14): a stored id would survive uninstall/
    // reinstall boundaries and become a device fingerprint, which the plan's Principles rule out.
    internal sealed class SessionTelemetryContext
    {
        // Bump when the envelope's shape changes in a way a consumer must branch on. Wire names are
        // append-only, so adding a metric does NOT need a bump; removing or re-keying a field does.
        // 2: hold_seconds and hold_speed_up_flights removed; flights became per ball (gameplay_seconds
        // over [Fired, Destroyed), flight_index in seal order) and streak_breaks per ball.
        internal const int CurrentSchemaVersion = 2;

        private readonly string _sessionId;
        private readonly long _launchTimestampUtcTicks;

        public string SessionId => _sessionId;

        public int SchemaVersion => CurrentSchemaVersion;

        public long LaunchTimestampUtcTicks => _launchTimestampUtcTicks;

        public SessionTelemetryContext()
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _launchTimestampUtcTicks = DateTime.UtcNow.Ticks;
        }
    }
}
