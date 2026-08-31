using System.Collections;
using TMPro;
using UnityEngine;
using BlindTiming.Game;

namespace BlindTiming.UI
{
    [System.Serializable]
    public class ModeDescription
    {
        public RoundMode mode;
        [TextArea(3, 10)] public string text;
    }

    public class HowToPlayUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;

        [Tooltip("Text for each mode. Add one element for each RoundMode value.")]
        [SerializeField] private ModeDescription[] descriptions;

        [Tooltip("Shown while the mode hasn't arrived from the server yet (usually a fraction of a second).")]
        [TextArea(2, 4)]
        [SerializeField] private string loadingText = "Loading mode description...";

        [Tooltip("How many seconds to ignore input after the panel opens - protection against " +
                 "accidentally closing it instantly if the player is still holding Enter/click from the lobby.")]
        [SerializeField] private float inputGraceTime = 0.3f;

        private bool _closed;
        private float _ignoreInputUntil;

        private IEnumerator Start()
        {
            if (panel == null)
            {
                Debug.LogError("[HowToPlayUI] panel is not assigned!");
                yield break;
            }

            if (bodyText != null) bodyText.text = loadingText;
            panel.SetActive(true);

            yield return null;

            GameplayInputLock.Locked = true;
            CursorLockController.Instance?.UnlockCursor();
            _ignoreInputUntil = Time.unscaledTime + inputGraceTime;

            float timeout = 3f;
            while (GameManager.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (GameManager.Instance != null)
                ApplyModeText(GameManager.Instance.CurrentMode.Value);
            else
                Debug.LogWarning("[HowToPlayUI] GameManager.Instance never appeared - showing loading text as-is.");
        }

        private void ApplyModeText(RoundMode mode)
        {
            if (titleText != null) titleText.text = ModeTitle(mode);
            if (bodyText == null) return;

            foreach (var d in descriptions)
            {
                if (d.mode == mode)
                {
                    bodyText.text = d.text;
                    return;
                }
            }

            Debug.LogWarning($"[HowToPlayUI] No description configured for mode {mode} - add one in the descriptions array.");
        }

        private string ModeTitle(RoundMode mode) => mode switch
        {
            RoundMode.Precision => "Mode: Close to perfect",
            RoundMode.IntervalGuess => "Mode: Guess the interval",
            RoundMode.Cooperative => "Mode: Blind co-op",
            _ => "How to play"
        };

        private void Update()
        {
            if (_closed || panel == null || !panel.activeSelf) return;
            if (Time.unscaledTime < _ignoreInputUntil) return;

            if (Input.anyKeyDown)
                Close();
        }

        private void Close()
        {
            _closed = true;
            panel.SetActive(false);
            GameplayInputLock.Locked = false;
            CursorLockController.Instance?.LockCursor();
        }
    }
}
