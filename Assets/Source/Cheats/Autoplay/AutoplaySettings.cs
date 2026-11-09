#if UNITY_ENABLE_CHECKS
using System.Globalization;

namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>
    ///     How the in-game autoplay plays: the playtest bot's two players (<c>Tools/playtest/README.md</c>), read
    ///     from a launch argument so a phone or an emulator can play itself. The spec is space-separated
    ///     <c>key=value</c> pairs: <c>player=human|best seconds=420 restart=0|1 floor=0.25 mean=0.6 aim=4
    ///     wild=0.05 seed=7</c>. Any pair left out keeps its default (the average human).
    /// </summary>
    internal sealed class AutoplaySettings
    {
        public bool Best { get; private set; }
        public float ReactFloor { get; private set; } = 0.25f;
        public float ReactMean { get; private set; } = 0.6f;
        public float AimSd { get; private set; } = 4f;
        public float WildChance { get; private set; } = 0.05f;
        public float Seconds { get; private set; } = 240f;
        public bool Restart { get; private set; }
        public int? Seed { get; private set; }

        /// <summary>
        ///     False for an empty spec (no autoplay asked for) and for a malformed one: a typo would otherwise
        ///     play a session nobody asked for and report it as the one they did.
        /// </summary>
        internal static bool TryParse(string spec, out AutoplaySettings settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(spec))
            {
                return false;
            }

            var parsed = new AutoplaySettings();
            foreach (var pair in spec.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var split = pair.IndexOf('=');
                if (split <= 0 || !parsed.TrySet(pair.Substring(0, split), pair.Substring(split + 1)))
                {
                    return false;
                }
            }

            settings = parsed;
            return true;
        }

        private static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0f;
        }

        private static bool TryFlag(string text, out bool value)
        {
            value = text == "1";
            return text == "0" || text == "1";
        }

        private bool TrySet(string key, string text)
        {
            float number;
            bool flag;
            switch (key)
            {
                case "player":
                    Best = text == "best";
                    return text == "best" || text == "human";
                case "seconds":
                    Seconds = TryFloat(text, out number) ? number : -1f;
                    return Seconds > 0f;
                case "restart":
                    var ok = TryFlag(text, out flag);
                    Restart = flag;
                    return ok;
                case "floor":
                    ReactFloor = TryFloat(text, out number) ? number : -1f;
                    return ReactFloor >= 0f;
                case "mean":
                    ReactMean = TryFloat(text, out number) ? number : -1f;
                    return ReactMean >= 0f;
                case "aim":
                    AimSd = TryFloat(text, out number) ? number : -1f;
                    return AimSd >= 0f;
                case "wild":
                    WildChance = TryFloat(text, out number) ? number : -1f;
                    return WildChance >= 0f && WildChance <= 1f;
                case "seed":
                    var hasSeed = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed);
                    Seed = hasSeed ? seed : null;
                    return hasSeed;
                default:
                    return false;
            }
        }
    }
}
#endif
