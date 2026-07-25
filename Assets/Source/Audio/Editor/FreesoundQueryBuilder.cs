using System;
using System.Collections.Generic;
using System.Globalization;

namespace BalloonParty.Audio.Editor
{
    // Pure URL/filter assembly for the Freesound APIv2 text search. No I/O — unit-testable.
    internal static class FreesoundQueryBuilder
    {
        private const string SearchEndpoint = "https://freesound.org/apiv2/search/text/";
        private const int MaxPageSize = 150;

        public static string BuildSearchUrl(in SfxFetchRequest request)
        {
            var query = Uri.EscapeDataString(request.Prompt ?? string.Empty);
            var duration = $"duration:[{Format(request.MinDuration)} TO {Format(request.MaxDuration)}]";
            var filter = Uri.EscapeDataString($"{LicenseFilter(request.AllowedLicenses)} {duration}");
            var pageSize = Math.Clamp(request.MaxResults, 1, MaxPageSize);
            return $"{SearchEndpoint}?query={query}&filter={filter}" +
                   "&fields=id,name,username,license,url,duration,previews" +
                   $"&page_size={pageSize}&sort=score";
        }

        // Allowlist, never a denylist: only the exact ship-safe license names, so unknown or legacy
        // values (Sampling+, Attribution Noncommercial) are excluded by construction.
        public static string LicenseFilter(SfxLicense licenses)
        {
            var names = new List<string>(2);
            if ((licenses & SfxLicense.Cc0) != 0)
            {
                names.Add("\"Creative Commons 0\"");
            }

            if ((licenses & SfxLicense.AttributionBy) != 0)
            {
                names.Add("\"Attribution\"");
            }

            // An empty filter would match every license, including non-commercial — never emit one.
            if (names.Count == 0)
            {
                names.Add("\"Creative Commons 0\"");
            }

            return "license:(" + string.Join(" OR ", names) + ")";
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
