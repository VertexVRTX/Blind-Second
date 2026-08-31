using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlindTiming.Game;
using BlindTiming.Network;

namespace BlindTiming.UI
{
    public class DisconnectPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button exitButton;

        private void Start()
        {
            if (panel == null) Debug.LogError("[DisconnectPanelUI] panel is not assigned!");
            if (exitButton == null) Debug.LogError("[DisconnectPanelUI] exitButton is not assigned!");

            panel.SetActive(false);
            exitButton.onClick.AddListener(() => RelayManager.Instance.LeaveToMainMenu());

            GameEvents.OpponentDisconnected += HandleOpponentDisconnected;
            GameEvents.ConnectionLost += HandleConnectionLost;
        }

        private void OnDestroy()
        {
            GameEvents.OpponentDisconnected -= HandleOpponentDisconnected;
            GameEvents.ConnectionLost -= HandleConnectionLost;
        }

        private void HandleOpponentDisconnected() => Show("The other player has disconnected.");
        private void HandleConnectionLost() => Show("Connection to the host was lost.");

        private void Show(string message)
        {
            if (messageText != null) messageText.text = message;
            panel.SetActive(true);
            CursorLockController.Instance?.UnlockCursor();
            GameplayInputLock.Locked = true;
        }
    }
}
