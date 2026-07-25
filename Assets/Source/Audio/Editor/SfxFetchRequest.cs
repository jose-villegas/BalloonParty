namespace BalloonParty.Audio.Editor
{
    internal readonly struct SfxFetchRequest
    {
        public readonly string Prompt;
        public readonly SfxLicense AllowedLicenses;
        public readonly int MaxResults;
        public readonly float MinDuration;
        public readonly float MaxDuration;

        public SfxFetchRequest(string prompt, SfxLicense allowedLicenses, int maxResults, float minDuration,
            float maxDuration)
        {
            Prompt = prompt;
            AllowedLicenses = allowedLicenses;
            MaxResults = maxResults;
            MinDuration = minDuration;
            MaxDuration = maxDuration;
        }
    }
}
