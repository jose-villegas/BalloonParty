using UnityEngine;

namespace BalloonParty.Game.Run
{
    /// <summary><see cref="IRunSave" /> as one JSON string in <c>PlayerPrefs</c>, beside the best-run record.</summary>
    internal sealed class PlayerPrefsRunSave : IRunSave
    {
        private const string Key = "RunSave";

        public bool TryLoad(out RunSnapshot snapshot)
        {
            if (RunSnapshot.TryParse(PlayerPrefs.GetString(Key, string.Empty), out snapshot))
            {
                return true;
            }

            // Dropped once, so a bad save doesn't warn on every launch.
            if (PlayerPrefs.HasKey(Key))
            {
                Clear();
            }

            return false;
        }

        // Saved at once: the point is surviving a kill that gives no warning.
        public void Write(RunSnapshot snapshot)
        {
            PlayerPrefs.SetString(Key, snapshot.ToJson());
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return;
            }

            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
