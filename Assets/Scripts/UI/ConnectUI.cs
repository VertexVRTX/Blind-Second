using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlindTiming.Network;
using DG.Tweening;

namespace BlindTiming.UI
{
    public class ConnectUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button createButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField codeInputField;
        [SerializeField] private TMP_Text joinCodeDisplay;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private CanvasGroup panelGroup;

        [Header("Nickname")]
        [Tooltip("Nickname input field - what the player will be called in the lobby and in round results")]
        [SerializeField] private TMP_InputField nicknameInputField;

        [Header("Transition to the lobby (host only, so they have time to see the code)")]
        [Tooltip("'Continue' button, shown next to the code after the room is created")]
        [SerializeField] private Button continueButton;
        [Tooltip("'Copy code' icon button (optional, can be left empty)")]
        [SerializeField] private Button copyCodeButton;

        [Header("Quit application")]
        [SerializeField] private Button quitButton;

        private void OnEnable()
        {
            GameplayInputLock.Locked = false;

            RelayManager.Instance.OnRelayCreated += HandleRelayCreated;
            RelayManager.Instance.OnJoinSuccess += HandleJoinSuccess;
            RelayManager.Instance.OnJoinFailed += HandleJoinFailed;

            createButton.onClick.AddListener(OnCreateClicked);
            joinButton.onClick.AddListener(OnJoinClicked);

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
                continueButton.onClick.AddListener(LoadLobby);
            }
            if (copyCodeButton != null)
                copyCodeButton.onClick.AddListener(CopyCodeToClipboard);

            if (quitButton != null)
                quitButton.onClick.AddListener(AppQuitter.Quit);

            if (nicknameInputField != null)
                nicknameInputField.text = LocalPlayerData.Nickname;

            panelGroup.alpha = 0f;
            panelGroup.DOFade(1f, 0.4f).SetEase(Ease.OutQuad);
        }

        private void OnDisable()
        {
            if (RelayManager.Instance == null) return;
            RelayManager.Instance.OnRelayCreated -= HandleRelayCreated;
            RelayManager.Instance.OnJoinSuccess -= HandleJoinSuccess;
            RelayManager.Instance.OnJoinFailed -= HandleJoinFailed;
        }

        private void SaveNicknameFromField()
        {
            string typed = nicknameInputField != null ? nicknameInputField.text : null;
            LocalPlayerData.Nickname = typed;
        }

        private void OnCreateClicked()
        {
            SaveNicknameFromField();
            statusText.text = "Creating room...";
            createButton.interactable = false;
            RelayManager.Instance.CreateRelay();
        }

        private void OnJoinClicked()
        {
            SaveNicknameFromField();
            statusText.text = "Connecting...";
            joinButton.interactable = false;
            RelayManager.Instance.JoinRelay(codeInputField.text);
        }

        private void HandleRelayCreated(string code)
        {
            joinCodeDisplay.gameObject.SetActive(true);

            joinCodeDisplay.text = $"Room code:\n{code}";
            statusText.text = "Remember or copy the code and share it with your friend.";

            joinCodeDisplay.transform.DOKill();
            joinCodeDisplay.transform.localScale = Vector3.one * 0.8f;
            joinCodeDisplay.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);

            if (continueButton != null)
                continueButton.gameObject.SetActive(true);

        }

        private void CopyCodeToClipboard()
        {
            GUIUtility.systemCopyBuffer = RelayManager.Instance.LastJoinCode;
            statusText.text = "Code copied to clipboard!";
        }

        private void HandleJoinSuccess()
        {

            statusText.text = "Connected! Waiting for the host...";
            joinButton.interactable = false;
            codeInputField.interactable = false;
        }

        private void HandleJoinFailed(string reason)
        {
            statusText.text = reason;
            createButton.interactable = true;
            joinButton.interactable = true;
        }

        private void LoadLobby()
        {

            Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(
                "Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
