using System;
using UnityEditor;

namespace BalloonParty.Audio.Editor
{
    // Resolves the Freesound API token WITHOUT it ever living in a committed/synced file: the
    // FREESOUND_API_TOKEN env var takes precedence, else a per-machine EditorPrefs entry the user
    // pastes into the window. Readers are injectable so tests never touch the real environment.
    internal sealed class FreesoundTokenSource
    {
        internal const string EnvVariable = "FREESOUND_API_TOKEN";
        internal const string EditorPrefKey = "BalloonParty.Audio.FreesoundToken";

        private readonly Func<string, string> _envReader;
        private readonly Func<string, string> _prefsReader;

        public bool HasToken => TryResolve(out _, out _);

        public FreesoundTokenSource() : this(Environment.GetEnvironmentVariable, ReadPref)
        {
        }

        internal FreesoundTokenSource(Func<string, string> envReader, Func<string, string> prefsReader)
        {
            _envReader = envReader;
            _prefsReader = prefsReader;
        }

        public bool TryResolve(out string token, out string source)
        {
            var fromEnv = _envReader(EnvVariable);
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                token = fromEnv.Trim();
                source = "environment variable";
                return true;
            }

            var fromPrefs = _prefsReader(EditorPrefKey);
            if (!string.IsNullOrWhiteSpace(fromPrefs))
            {
                token = fromPrefs.Trim();
                source = "EditorPrefs";
                return true;
            }

            token = null;
            source = "none";
            return false;
        }

        private static string ReadPref(string key)
        {
            return EditorPrefs.GetString(key, string.Empty);
        }
    }
}
