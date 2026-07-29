using System;
using UnityEngine;

namespace BalloonParty.Shared.Diagnostics
{
    /// <summary>
    ///     Build provenance data baked into the APK at build time by the release tool.
    ///     Load via <see cref="Load"/> to display or verify which commit produced this binary.
    /// </summary>
    internal sealed class BuildInfo
    {
        private static BuildInfo cached;

        public string Version { get; }
        public string CommitSha { get; }
        public string TreeHash { get; }
        public string BuildTime { get; }
        public string Variant { get; }

        private BuildInfo(Data data)
        {
            Version = data.version ?? "unknown";
            CommitSha = data.commitSha ?? "unknown";
            TreeHash = data.treeHash ?? "unknown";
            BuildTime = data.buildTime ?? "unknown";
            Variant = data.variant ?? "unknown";
        }

        /// <summary>
        ///     Loads the build info baked into <c>Resources/BuildInfo.json</c> at build time.
        ///     Returns a fallback with "unknown" fields if the resource is missing (editor / dev).
        /// </summary>
        internal static BuildInfo Load()
        {
            if (cached != null)
            {
                return cached;
            }

            var asset = Resources.Load<TextAsset>("BuildInfo");
            if (asset == null)
            {
                cached = new BuildInfo(new Data());
                return cached;
            }

            try
            {
                var data = JsonUtility.FromJson<Data>(asset.text);
                cached = new BuildInfo(data);
            }
            catch (Exception)
            {
                cached = new BuildInfo(new Data());
            }

            return cached;
        }

        /// <summary>Short label: <c>1.2.0 (abc1234567)</c>.</summary>
        public override string ToString()
        {
            var shortSha = CommitSha.Length > 10 ? CommitSha[..10] : CommitSha;
            return $"{Version} ({shortSha})";
        }

        [Serializable]
        private class Data
        {
            public string version;
            public string commitSha;
            public string treeHash;
            public string buildTime;
            public string variant;
        }
    }
}
