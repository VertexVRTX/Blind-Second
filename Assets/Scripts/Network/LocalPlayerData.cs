using UnityEngine;

namespace BlindTiming.Network
{
    public static class LocalPlayerData
    {
        private const string PrefsKey = "BlindTiming_Nickname";
        private const int MaxLength = 20;

        private static string _nickname;

        public static string Nickname
        {
            get
            {
                if (string.IsNullOrEmpty(_nickname))
                    _nickname = PlayerPrefs.GetString(PrefsKey, "Player");
                return _nickname;
            }
            set
            {
                string clean = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
                if (clean.Length > MaxLength) clean = clean.Substring(0, MaxLength);

                _nickname = clean;
                PlayerPrefs.SetString(PrefsKey, clean);
                PlayerPrefs.Save();
            }
        }
    }
}
