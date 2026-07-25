namespace BalloonParty.Audio.Editor
{
    // Provider-neutral search result — never a Freesound-shaped DTO — so a future provider
    // (e.g. a text-to-SFX generator) can slot in behind ISfxProvider without touching the UI.
    internal readonly struct SfxCandidate
    {
        public readonly long ProviderId;
        public readonly string Name;
        public readonly string Author;
        public readonly string LicenseName;
        public readonly string LicenseUrl;
        public readonly string SoundUrl;
        public readonly string PreviewUrl;
        public readonly float Duration;
        public readonly bool RequiresAttribution;

        public SfxCandidate(long providerId, string name, string author, string licenseName, string licenseUrl,
            string soundUrl, string previewUrl, float duration, bool requiresAttribution)
        {
            ProviderId = providerId;
            Name = name;
            Author = author;
            LicenseName = licenseName;
            LicenseUrl = licenseUrl;
            SoundUrl = soundUrl;
            PreviewUrl = previewUrl;
            Duration = duration;
            RequiresAttribution = requiresAttribution;
        }
    }
}
