using System;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Where the initial fill deals the toughs when they walk as one snake: side by side on one line, so they
    ///     rise together and land as one body. <see cref="Line" /> is the deepest board line a snake walks;
    ///     <see cref="Length" /> toughs take it from column <see cref="Start" /> eastward, and any past a full line
    ///     go on the line above it, <see cref="UpperLength" /> of them from the east edge westward, so the body
    ///     bends round the line's east end. A <see cref="Line" /> of -1 means no run.
    /// </summary>
    internal readonly struct ToughRunPlan
    {
        public static readonly ToughRunPlan None = new(-1, 0, 0, 0);

        public readonly int Line;
        public readonly int Start;
        public readonly int Length;
        public readonly int UpperLength;

        private ToughRunPlan(int line, int start, int length, int upperLength)
        {
            Line = line;
            Start = start;
            Length = length;
            UpperLength = upperLength;
        }

        /// <summary>
        ///     The run for <paramref name="toughs" /> toughs on a fill of <paramref name="lineCount" /> lines, the
        ///     deepest a snake walks at <paramref name="lastSnakeRow" />. <paramref name="random01" /> places a run
        ///     shorter than a line along it.
        /// </summary>
        public static ToughRunPlan For(int toughs, int lineCount, int columns, int lastSnakeRow, float random01)
        {
            var line = Math.Min(lineCount - 1, lastSnakeRow);
            if (toughs <= 0 || columns <= 0 || line < 0)
            {
                return None;
            }

            var length = Math.Min(toughs, columns);
            var upperLength = line > 0 ? Math.Min(toughs - length, columns) : 0;
            var room = columns - length;
            var start = Math.Min((int)(random01 * (room + 1)), room);
            return new ToughRunPlan(line, start, length, upperLength);
        }

        /// <summary>The column the run's <paramref name="index" />-th tough on <paramref name="line" /> takes, or -1.</summary>
        public int ColumnAt(int line, int index, int columns)
        {
            if (line == Line && index < Length)
            {
                return Start + index;
            }

            return line == Line - 1 && index < UpperLength ? columns - 1 - index : -1;
        }

        /// <summary>How many of the run's toughs <paramref name="line" /> holds.</summary>
        public int CountOn(int line)
        {
            return line == Line ? Length : line == Line - 1 ? UpperLength : 0;
        }
    }
}
