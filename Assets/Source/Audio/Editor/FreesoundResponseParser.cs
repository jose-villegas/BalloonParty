using System.Collections.Generic;
using Newtonsoft.Json;

namespace BalloonParty.Audio.Editor
{
    // Parses a Freesound APIv2 /search/text/ response into provider-neutral candidates, re-filtering
    // by the allowlist (belt-and-suspenders vs the server-side filter) and classifying each license
    // from its deed URL. Newtonsoft (not JsonUtility) because the preview keys are hyphenated
    // ("preview-hq-mp3") and can't bind to C# field names without [JsonProperty] remap.
    internal static class FreesoundResponseParser
    {
        public static IReadOnlyList<SfxCandidate> Parse(string json, SfxLicense allowed)
        {
            var candidates = new List<SfxCandidate>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return candidates;
            }

            var response = JsonConvert.DeserializeObject<SearchResponse>(json);
            if (response?.Results == null)
            {
                return candidates;
            }

            foreach (var result in response.Results)
            {
                if (result == null)
                {
                    continue;
                }

                var license = ClassifyLicense(result.License);
                if (license == SfxLicense.None || (allowed & license) == 0)
                {
                    continue;
                }

                var preview = result.Previews?.HighQualityMp3;
                if (string.IsNullOrEmpty(preview))
                {
                    preview = result.Previews?.LowQualityMp3;
                }

                if (string.IsNullOrEmpty(preview))
                {
                    continue;
                }

                candidates.Add(new SfxCandidate(
                    result.Id, result.Name, result.Username, LicenseName(license), result.License,
                    result.Url, preview, result.Duration, requiresAttribution: license == SfxLicense.AttributionBy));
            }

            return candidates;
        }

        // CC-BY matches only "/licenses/by/" — the -nc / -sa / -nd variants have a different path
        // segment and are correctly rejected here even if one slips past the server filter.
        internal static SfxLicense ClassifyLicense(string licenseUrl)
        {
            if (string.IsNullOrEmpty(licenseUrl))
            {
                return SfxLicense.None;
            }

            if (licenseUrl.Contains("/publicdomain/zero") || licenseUrl.Contains("/cc0"))
            {
                return SfxLicense.Cc0;
            }

            if (licenseUrl.Contains("/licenses/by/"))
            {
                return SfxLicense.AttributionBy;
            }

            return SfxLicense.None;
        }

        private static string LicenseName(SfxLicense license)
        {
            return license == SfxLicense.Cc0 ? "Creative Commons 0" : "Attribution";
        }

        private sealed class SearchResponse
        {
            [JsonProperty("results")] public List<Result> Results;
        }

        private sealed class Result
        {
            [JsonProperty("id")] public long Id;
            [JsonProperty("name")] public string Name;
            [JsonProperty("username")] public string Username;
            [JsonProperty("license")] public string License;
            [JsonProperty("url")] public string Url;
            [JsonProperty("duration")] public float Duration;
            [JsonProperty("previews")] public Previews Previews;
        }

        private sealed class Previews
        {
            [JsonProperty("preview-hq-mp3")] public string HighQualityMp3;
            [JsonProperty("preview-lq-mp3")] public string LowQualityMp3;
        }
    }
}
