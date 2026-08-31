using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BlindTiming.Game;
using BlindTiming.Network;
using BlindTiming.Chat;

namespace BlindTiming.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitToMenuButton;
        [Tooltip("Only shown/usable for the host - clicking it sends BOTH players back to the Lobby scene.")]
        [SerializeField] private Button returnToLobbyButton;
        [SerializeField] private Button quitButton;
        [Tooltip("The same reusable settings panel component, placed in this scene.")]
        [SerializeField] private SettingsPanelUI settingsPanel;

        private bool _isOpen;
        private NetworkChat _chat;

        private void Start()
        {
            if (menuPanel == null) Debug.LogError("[PauseMenuUI] menuPanel is not assigned!");

            menuPanel.SetActive(false);

            resumeButton.onClick.AddListener(Close);
            exitToMenuButton.onClick.AddListener(() => RelayManager.Instance.LeaveToMainMenu());
            returnToLobbyButton.onClick.AddListener(RequestReturnToLobby);

            if (quitButton != null)
                quitButton.onClick.AddListener(AppQuitter.QuitFromMatch);

            if (settingsButton != null && settingsPanel != null)
                settingsButton.onClick.AddListener(settingsPanel.Open);

            _chat = FindObjectOfType<NetworkChat>();

            StartCoroutine(SetupHostOnlyButton());
        }

        private IEnumerator SetupHostOnlyButton()
        {

            yield return null;
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            returnToLobbyButton.gameObject.SetActive(isHost);
        }

        private void RequestReturnToLobby()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[PauseMenuUI] GameManager.Instance is null, cannot return to lobby.");
                return;
            }

            returnToLobbyButton.interactable = false;
            exitToMenuButton.interactable = false;
            Debug.Log("[PauseMenuUI] Requesting return to lobby...");
            GameManager.Instance.RequestReturnToLobbyServerRpc();
        }

        private void Update()
        {
            if (_chat != null && _chat.IsTyping) return;

            if (Input.GetKeyDown(KeyCode.Escape))
                Toggle();
        }

        private void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        private void Open()
        {
            _isOpen = true;
            menuPanel.SetActive(true);
            CursorLockController.Instance?.UnlockCursor();
            GameplayInputLock.Locked = true;
        }

        private void Close()
        {
            _isOpen = false;
            menuPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.Close();
            CursorLockController.Instance?.LockCursor();
            GameplayInputLock.Locked = false;
        }
    }
}
