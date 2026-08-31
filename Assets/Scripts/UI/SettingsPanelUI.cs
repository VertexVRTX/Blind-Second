using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlindTiming.UI
{
    public class SettingsPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        [Header("Volume")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text volumeValueText;

        [Header("Head model sensitivity (cosmetic, seen by the opponent)")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityValueText;

        [Header("Camera sensitivity (the actual view/aim - separate slider)")]
        [SerializeField] private Slider cameraSensitivitySlider;
        [SerializeField] private TMP_Text cameraSensitivityValueText;

        [Header("Buttons")]
        [Tooltip("Optional - the button that opens this panel. Leave empty and call Open() yourself (e.g. from PauseMenuUI) if this panel is opened some other way.")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            if (panel == null) Debug.LogError("[SettingsPanelUI] panel is not assigned!");
            if (volumeSlider == null) Debug.LogError("[SettingsPanelUI] volumeSlider is not assigned!");
            if (sensitivitySlider == null) Debug.LogError("[SettingsPanelUI] sensitivitySlider is not assigned!");
            if (cameraSensitivitySlider == null) Debug.LogError("[SettingsPanelUI] cameraSensitivitySlider is not assigned!");

            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            sensitivitySlider.minValue = SettingsData.MinSensitivity;
            sensitivitySlider.maxValue = SettingsData.MaxSensitivity;
            cameraSensitivitySlider.minValue = SettingsData.MinSensitivity;
            cameraSensitivitySlider.maxValue = SettingsData.MaxSensitivity;

            volumeSlider.value = SettingsData.Volume;
            sensitivitySlider.value = SettingsData.Sensitivity;
            cameraSensitivitySlider.value = SettingsData.CameraSensitivity;
            RefreshLabels();

            volumeSlider.onValueChanged.AddListener(v =>
            {
                SettingsData.Volume = v;
                RefreshLabels();
            });
            sensitivitySlider.onValueChanged.AddListener(v =>
            {
                SettingsData.Sensitivity = v;
                RefreshLabels();
            });
            cameraSensitivitySlider.onValueChanged.AddListener(v =>
            {
                SettingsData.CameraSensitivity = v;
                RefreshLabels();
            });

            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            panel.SetActive(false);
        }

        private void RefreshLabels()
        {
            if (volumeValueText != null) volumeValueText.text = Mathf.RoundToInt(SettingsData.Volume * 100f) + "%";
            if (sensitivityValueText != null) sensitivityValueText.text = SettingsData.Sensitivity.ToString("0.00") + "x";
            if (cameraSensitivityValueText != null) cameraSensitivityValueText.text = SettingsData.CameraSensitivity.ToString("0.00") + "x";
        }

        public void Open()
        {
            volumeSlider.value = SettingsData.Volume;
            sensitivitySlider.value = SettingsData.Sensitivity;
            cameraSensitivitySlider.value = SettingsData.CameraSensitivity;
            RefreshLabels();
            panel.SetActive(true);
        }

        public void Close()
        {
            panel.SetActive(false);
        }

        public bool IsOpen => panel != null && panel.activeSelf;
    }
}
