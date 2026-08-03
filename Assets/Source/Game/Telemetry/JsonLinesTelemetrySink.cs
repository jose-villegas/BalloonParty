#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game.Telemetry
{
    // Dev-only local sink (R28a) — compiled out of release along with its registration. One
    // StreamWriter opened in Start() and kept for the whole session: the file-open on Android
    // persistentDataPath dominates the write cost and neither flush boundary is an idle frame, so
    // opening/closing per record would pay that cost on the hot path instead of once. Rotation keeps
    // the most recent files sorted by file name, not File.GetLastWriteTime — the name already embeds
    // yyyyMMdd_HHmmss, so an ordinal sort is a chronological sort with no extra stat calls on the
    // scope-start frame.
    internal sealed class JsonLinesTelemetrySink : TelemetrySinkBase, IStartable
    {
        private const int MaxRetainedFiles = 20;
        private const string FileSearchPattern = "telemetry_*.jsonl";

        private readonly TelemetryEnvelopeSerializer _serializer;

        private StreamWriter _writer;

        [Inject]
        internal JsonLinesTelemetrySink(IGamePalette palette)
        {
            _serializer = new TelemetryEnvelopeSerializer(palette);
        }

        // Guarded like the write path, and for a sharper reason: Start() is an IStartable hook, not a
        // sink method, so TelemetrySinkBase's guard does not reach it. VContainer rethrows out of its
        // IStartable loop when no EntryPointExceptionHandler is registered — and none is — so an
        // unwritable persistentDataPath here would abort the loop and leave every entry point
        // registered after this one un-started. RegisterTelemetrySinks runs second, so that is nearly
        // all of them. A dev-only log file must never be able to do that.
        public void Start()
        {
            try
            {
                var directory = Application.persistentDataPath + "/telemetry/";
                Directory.CreateDirectory(directory);
                RotateOldFiles(directory);

                // InvariantCulture: a non-Gregorian device calendar would otherwise write a file named
                // for year 2569 instead of 2026.
                var fileName = "telemetry_" +
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".jsonl";
                _writer = new StreamWriter(Path.Combine(directory, fileName), append: true);
            }
            catch (Exception ex)
            {
                // _writer stays null; the first Write then trips the base guard and disables the sink.
                Log.Warn("Telemetry", $"Could not open the telemetry log; no records will be written: {ex}");
            }
        }

        protected override void WriteCore(in TelemetryEnvelope envelope)
        {
            _writer.WriteLine(_serializer.Serialize(envelope));
            _writer.Flush();
        }

        protected override void DisposeCore()
        {
            _writer?.Dispose();
        }

        // Deletes the oldest files first so that once this session's own file lands, the directory
        // holds at most MaxRetainedFiles. File names sort lexicographically by construction, so this
        // needs no per-file stat call.
        private static void RotateOldFiles(string directory)
        {
            var files = Directory.GetFiles(directory, FileSearchPattern);
            Array.Sort(files, StringComparer.Ordinal);

            var overflow = files.Length - (MaxRetainedFiles - 1);
            for (var i = 0; i < overflow; i++)
            {
                File.Delete(files[i]);
            }
        }
    }
}
#endif
