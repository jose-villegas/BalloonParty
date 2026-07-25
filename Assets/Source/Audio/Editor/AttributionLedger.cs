using System.Collections.Generic;
using Newtonsoft.Json;

namespace BalloonParty.Audio.Editor
{
    // Pure record store for the shippable attribution file. Dedups by provider id so re-fetching a
    // sound updates rather than duplicates. File I/O lives in the window; this only merges + (de)serializes.
    internal sealed class AttributionLedger
    {
        private readonly List<AttributionRecord> _records;

        public IReadOnlyList<AttributionRecord> Records => _records;

        public AttributionLedger() : this(null)
        {
        }

        internal AttributionLedger(IReadOnlyList<AttributionRecord> records)
        {
            _records = records != null ? new List<AttributionRecord>(records) : new List<AttributionRecord>();
        }

        public static AttributionLedger FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AttributionLedger();
            }

            return new AttributionLedger(JsonConvert.DeserializeObject<List<AttributionRecord>>(json));
        }

        public void Merge(AttributionRecord record)
        {
            if (record == null)
            {
                return;
            }

            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].ProviderId == record.ProviderId)
                {
                    _records[i] = record;
                    return;
                }
            }

            _records.Add(record);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(_records, Formatting.Indented);
        }
    }
}
