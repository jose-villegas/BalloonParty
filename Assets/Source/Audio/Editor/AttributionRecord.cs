namespace BalloonParty.Audio.Editor
{
    // One credited source, serialized into the shippable attribution file. CC-BY needs full TASL
    // (Title/Author/Source/License); CC0 is recorded too (RequiresAttribution false) for provenance.
    internal sealed class AttributionRecord
    {
        public string SoundId;
        public long ProviderId;
        public string Name;
        public string Author;
        public string SoundUrl;
        public string LicenseName;
        public string LicenseUrl;
        public bool RequiresAttribution;
    }
}
