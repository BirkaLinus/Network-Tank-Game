using UnityEngine;

namespace NetworkTankArena
{
    public static class LocalPlayerTracker
    {
        public static string Suffix = ""; //For testing on same computer

        public static string Id
        {
            get
            {
                string key = "player_guid" + Suffix;
                if (!PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.SetString(key, System.Guid.NewGuid().ToString());
                }
                return PlayerPrefs.GetString(key);
            }
        }

    }
}
