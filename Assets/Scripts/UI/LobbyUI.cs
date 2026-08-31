using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using BlindTiming.Network;
using BlindTiming.Game;
using Unity.Netcode;
using Unity.Collections;

namespace BlindTiming.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text player1StatusText;
        [SerializeField] private TMP_Text player2StatusText;
        [SerializeField] private TMP_Text hintText;

        [Header("Room code (also visible in the lobby, not just on the create screen)")]
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private Button copyCodeButton;

        [Header("Match settings - only the host can change them, both players see them")]
        [SerializeField] private Button modePrecisionButton;
        [SerializeField] private Button modeBombButton;
        [SerializeField] private Button modeCooperativeButton;
        [SerializeField] private TMP_Text selectedModeText;
        [SerializeField] private Button roundsMinusButton;
        [SerializeField] private Button roundsPlusButton;
        [SerializeField] private TMP_Text roundsCountText;

        [Header("Checkmarks next to the mode buttons (one per button)")]
        [SerializeField] private GameObject modePrecisionCheckmark;
        [SerializeField] private GameObject modeBombCheckmark;
        [SerializeField] private GameObject modeCooperativeCheckmark;

        private bool _localReady;

        private void Start()
        {
            GameplayInputLock.Locked = false;

            startButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
            readyButton.onClick.AddListener(ToggleReady);
            startButton.onClick.AddListener(() => LobbyManager.Instance.StartGameServerRpc());
            leaveButton.onClick.AddListener(() => RelayManager.Instance.LeaveToMainMenu());

            if (quitButton != null)
                quitButton.onClick.AddListener(AppQuitter.QuitFromMatch);

            SetupRoomCodeDisplay();
            SetupMatchSettingsUI();

            LobbyManager.Instance.Players.OnListChanged += _ => RefreshUI();
            LobbyManager.Instance.SelectedMode.OnValueChanged += (_, __) => RefreshMatchSettingsUI();
            LobbyManager.Instance.TotalRounds.OnValueChanged += (_, __) => RefreshMatchSettingsUI();
            RefreshUI();
            RefreshMatchSettingsUI();

            LobbyManager.Instance.SubmitNicknameServerRpc(new FixedString32Bytes(LocalPlayerData.Nickname));
        }

        private void SetupMatchSettingsUI()
        {
            bool isHost = NetworkManager.Singleton.IsHost;

            modePrecisionButton.interactable = isHost;
            modeBombButton.interactable = isHost;
            modeCooperativeButton.interactable = isHost;
            roundsMinusButton.interactable = isHost;
            roundsPlusButton.interactable = isHost;

            modePrecisionButton.onClick.AddListener(() => LobbyManager.Instance.SetModeServerRpc(RoundMode.Precision));
            modeBombButton.onClick.AddListener(() => LobbyManager.Instance.SetModeServerRpc(RoundMode.IntervalGuess));
            modeCooperativeButton.onClick.AddListener(() => LobbyManager.Instance.SetModeServerRpc(RoundMode.Cooperative));

            roundsMinusButton.onClick.AddListener(() =>
                LobbyManager.Instance.SetTotalRoundsServerRpc(LobbyManager.Instance.TotalRounds.Value - 1));
            roundsPlusButton.onClick.AddListener(() =>
                LobbyManager.Instance.SetTotalRoundsServerRpc(LobbyManager.Instance.TotalRounds.Value + 1));
        }

        private void RefreshMatchSettingsUI()
        {
            RoundMode selected = LobbyManager.Instance.SelectedMode.Value;

            selectedModeText.text = $"Mode: {ModeLabel(selected)}";
            roundsCountText.text = $"Rounds: {LobbyManager.Instance.TotalRounds.Value}";

            SetCheckmark(modePrecisionCheckmark, selected == RoundMode.Precision);
            SetCheckmark(modeBombCheckmark, selected == RoundMode.IntervalGuess);
            SetCheckmark(modeCooperativeCheckmark, selected == RoundMode.Cooperative);
        }

        private void SetCheckmark(GameObject checkmark, bool shouldBeVisible)
        {
            if (checkmark == null) return;

            bool wasVisible = checkmark.activeSelf;
            checkmark.SetActive(shouldBeVisible);

            if (shouldBeVisible && !wasVisible)
            {
                checkmark.transform.DOKill();
                checkmark.transform.localScale = Vector3.zero;
                checkmark.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
            }
        }

        private string ModeLabel(RoundMode mode) => mode switch
        {
            RoundMode.Precision => "Close to perfect",
            RoundMode.IntervalGuess => "Interval guess",
            RoundMode.Cooperative => "Blind co-op",
            _ => mode.ToString()
        };

        private void SetupRoomCodeDisplay()
        {
            if (roomCodeText == null) return;

            string code = RelayManager.Instance != null ? RelayManager.Instance.LastJoinCode : null;
            if (string.IsNullOrEmpty(code))
            {
                roomCodeText.gameObject.SetActive(false);
                return;
            }

            roomCodeText.gameObject.SetActive(true);
            roomCodeText.text = $"Room code: {code}";

            if (copyCodeButton != null)
            {
                copyCodeButton.gameObject.SetActive(true);
                copyCodeButton.onClick.AddListener(() =>
                {
                    GUIUtility.systemCopyBuffer = code;
                    hintText.text = "Code copied!";
                });
            }
        }

        private void ToggleReady()
        {
            _localReady = !_localReady;
            LobbyManager.Instance.SetReadyServerRpc(_localReady);

            readyButton.transform.DOKill();
            readyButton.transform.localScale = Vector3.one;
            readyButton.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f);
        }

        private void RefreshUI()
        {
            var players = LobbyManager.Instance.Players;

            player1StatusText.text = players.Count > 0
                ? $"{players[0].PlayerName}: {(players[0].IsReady ? "Ready" : "Not ready")}"
                : "Waiting...";

            player2StatusText.text = players.Count > 1
                ? $"{players[1].PlayerName}: {(players[1].IsReady ? "Ready" : "Not ready")}"
                : "Waiting for the second player...";

            bool allReady = LobbyManager.Instance.AllReady();
            startButton.interactable = allReady && NetworkManager.Singleton.IsHost;
            hintText.text = allReady ? "Everyone is ready! The host can start the game." : "Waiting for both players to be ready.";
        }
    }
}
