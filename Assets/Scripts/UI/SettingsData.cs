using UnityEngine;

namespace BlindTiming.UI
{
    public static class SettingsData
    {
        private const string VolumeKey = "BlindTiming_Volume";
        private const string SensitivityKey = "BlindTiming_Sensitivity";
        private const string CameraSensitivityKey = "BlindTiming_CameraSensitivity";

        public const float MinSensitivity = 0.25f;
        public const float MaxSensitivity = 2.5f;

        private static float? _volume;
        private static float? _sensitivity;
        private static float? _cameraSensitivity;

        public static float Volume
        {
            get
            {
                _volume ??= PlayerPrefs.GetFloat(VolumeKey, 1f);
                return _volume.Value;
            }
            set
            {
                _volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, _volume.Value);
                PlayerPrefs.Save();
                ApplyVolume();
            }
        }

        public static float Sensitivity
        {
            get
            {
                _sensitivity ??= PlayerPrefs.GetFloat(SensitivityKey, 1f);
                return _sensitivity.Value;
            }
            set
            {
                _sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                PlayerPrefs.SetFloat(SensitivityKey, _sensitivity.Value);
                PlayerPrefs.Save();
            }
        }

        public static float CameraSensitivity
        {
            get
            {
                _cameraSensitivity ??= PlayerPrefs.GetFloat(CameraSensitivityKey, 1f);
                return _cameraSensitivity.Value;
            }
            set
            {
                _cameraSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                PlayerPrefs.SetFloat(CameraSensitivityKey, _cameraSensitivity.Value);
                PlayerPrefs.Save();
            }
        }

        public static void ApplyVolume() => AudioListener.volume = Volume;
    }
}
