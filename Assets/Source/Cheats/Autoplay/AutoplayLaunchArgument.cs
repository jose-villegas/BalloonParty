#if UNITY_ENABLE_CHECKS
using System;
using UnityEngine;

namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>
    ///     The autoplay spec the app was launched with, or null. On Android it's the launch intent's
    ///     <c>autoplay</c> extra (<c>adb shell am start ... -e autoplay "player=human seconds=420"</c>); elsewhere
    ///     the argument after <c>-autoplay</c> on the command line.
    /// </summary>
    internal static class AutoplayLaunchArgument
    {
        private const string Key = "autoplay";

        internal static string Read()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                return intent?.Call<string>("getStringExtra", Key);
            }
            catch (AndroidJavaException)
            {
                return null;
            }
#else
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-" + Key)
                {
                    return args[i + 1];
                }
            }

            return null;
#endif
        }
    }
}
#endif
