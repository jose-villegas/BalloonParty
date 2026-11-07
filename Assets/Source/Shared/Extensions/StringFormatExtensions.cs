using System;

namespace BalloonParty.Shared.Extensions
{
    internal static class StringFormatExtensions
    {
        /// <summary>
        ///     <see cref="string.Format(string, object[])" /> for formats authored in the editor: a malformed one
        ///     (a stray brace, an index past the arguments) comes back as written instead of throwing mid-flow, and
        ///     a null one as empty.
        /// </summary>
        internal static string FormatOrRaw(this string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
