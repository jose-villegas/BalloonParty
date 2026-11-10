using BalloonParty.Configuration.HeadsUp;
using NSubstitute;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>Settings with a chosen cap and hand-built styles, for the queue and the service.</summary>
    internal static class HeadsUpTestSettings
    {
        internal static IHeadsUpSettings With(int maxConcurrent, params HeadsUpStyleSettings[] styles)
        {
            var byId = HeadsUpSettings.Index(styles);
            var settings = Substitute.For<IHeadsUpSettings>();
            settings.MaxConcurrent.Returns(maxConcurrent);
            settings.Style(Arg.Any<HeadsUpStyleId>()).Returns(call => byId[(int)call.Arg<HeadsUpStyleId>()]);
            return settings;
        }

        internal static HeadsUpStyleSettings Style(HeadsUpStyleId id, int priority = 1)
        {
            return new HeadsUpStyleSettings(id, "{0}", default, priority);
        }
    }
}
