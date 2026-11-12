#if UNITY_ENABLE_CHECKS
using System;
using System.Globalization;
using System.IO;
using System.Text;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>
    ///     Writes the autoplay's <c>ticks.jsonl</c> under <c>persistentDataPath/autoplay/</c>, one sample per line in
    ///     the shape <c>Tools/playtest/playtest_bot.py</c> reports from, and a <c>done</c> file once the session
    ///     ends, which is what the device runner waits for.
    /// </summary>
    internal sealed class AutoplayTickWriter : IDisposable
    {
        private readonly string _directory;
        private readonly StringBuilder _line = new(160);
        private StreamWriter _writer;

        internal AutoplayTickWriter()
        {
            _directory = Path.Combine(Application.persistentDataPath, "autoplay");
            try
            {
                Directory.CreateDirectory(_directory);
                File.Delete(Path.Combine(_directory, "done"));
                _writer = new StreamWriter(Path.Combine(_directory, "ticks.jsonl"), append: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Autoplay", $"can't write ticks: {exception.Message}");
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }

        internal void WriteSample(in AutoplaySample sample)
        {
            _line.Clear()
                .Append("{\"wall\": ").Append(Wall())
                .Append(", \"t\": ").Append(sample.Time.ToString("F1", CultureInfo.InvariantCulture))
                .Append(", \"level\": ").Append(sample.Level)
                .Append(", \"score\": ").Append(sample.Score)
                .Append(", \"hearts\": ").Append(sample.Hearts)
                .Append(", \"nav\": \"").Append(sample.Navigation).Append('"')
                .Append(", \"phase\": \"").Append(sample.Phase).Append('"')
                .Append(", \"shots\": ").Append(sample.Shots)
                .Append(", \"fill\": ").Append(sample.Fill.ToString("F2", CultureInfo.InvariantCulture))
                .Append(", \"inflight\": ").Append(sample.InFlight)
                .Append('}');
            WriteLine(_line.ToString());
        }

        /// <summary>A run-boundary marker: <c>restart</c> or <c>game_over</c>.</summary>
        internal void WriteMarker(string key)
        {
            WriteLine("{\"wall\": " + Wall() + ", \"" + key + "\": true}");
        }

        internal void Finish()
        {
            Dispose();
            try
            {
                File.WriteAllText(Path.Combine(_directory, "done"), Wall());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Autoplay", $"can't write the done marker: {exception.Message}");
            }
        }

        private static string Wall()
        {
            return (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0).ToString("F2", CultureInfo.InvariantCulture);
        }

        private void WriteLine(string line)
        {
            if (_writer == null)
            {
                return;
            }

            // Flushed per line: the session ends by quitting the app, and a pulled file must be whole.
            try
            {
                _writer.WriteLine(line);
                _writer.Flush();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Autoplay", $"can't write ticks: {exception.Message}");
                Dispose();
            }
        }
    }

    /// <summary>One tick of the game as the playtest report reads it.</summary>
    internal readonly struct AutoplaySample
    {
        internal readonly float Time;
        internal readonly int Level;
        internal readonly int Score;
        internal readonly int Hearts;
        internal readonly string Navigation;
        internal readonly string Phase;
        internal readonly int Shots;
        internal readonly float Fill;
        internal readonly int InFlight;

        internal AutoplaySample(float time, int level, int score, int hearts, string navigation, string phase,
            int shots, float fill, int inFlight)
        {
            Time = time;
            Level = level;
            Score = score;
            Hearts = hearts;
            Navigation = navigation;
            Phase = phase;
            Shots = shots;
            Fill = fill;
            InFlight = inFlight;
        }
    }
}
#endif
