using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BlindTiming.Game;
using BlindTiming.Network;
using BlindTiming.UI;

namespace BlindTiming.Chat
{
    public class NetworkChat : NetworkBehaviour
    {
        [Header("UI references")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private GameObject messagePrefab;

        [Header("Settings")]
        [SerializeField] private int maxMessages = 10;
        [SerializeField] private float visibleTime = 30f;

        private readonly List<GameObject> _messages = new List<GameObject>();
        private Coroutine _hideRoutine;
        private bool _isChatOpen;

        private int _lastEnterHandledFrame = -1;

        public bool IsTyping => _isChatOpen;

        private void Start()
        {
            if (chatPanel == null) Debug.LogError("[NetworkChat] chatPanel is not assigned!");
            if (inputField == null) Debug.LogError("[NetworkChat] inputField is not assigned!");
            if (content == null) Debug.LogError("[NetworkChat] content is not assigned!");
            if (messagePrefab == null) Debug.LogError("[NetworkChat] messagePrefab is not assigned!");
            if (scrollRect == null) Debug.LogError("[NetworkChat] scrollRect is not assigned!");

            inputField.characterLimit = 120;
            inputField.onSubmit.AddListener(OnSubmit);
            SetChatVisible(false);
        }

        private void Update()
        {
            if (!_isChatOpen &&
                !GameplayInputLock.Locked &&
                Input.GetKeyDown(KeyCode.Return) &&
                Time.frameCount != _lastEnterHandledFrame)
            {
                OpenChat();
                return;
            }

            // Esc закрывает чат только пока реально идёт набор текста (_isChatOpen == true).
            // Именно отсутствие этой ветки замораживало камеру: GameplayInputLock оставался
            // true и курсор разлоченным, потому что ничего не сбрасывало это состояние.
            if (_isChatOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInput();
            }
        }

        private void OpenChat()
        {
            _isChatOpen = true;
            SetChatVisible(true);
            CursorLockController.Instance?.UnlockCursor();
            GameplayInputLock.Locked = true;
            UiSfx.Instance?.PlayChatOpen();

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            inputField.text = "";
            inputField.ActivateInputField();
            inputField.Select();
        }

        private void OnSubmit(string text)
        {
            _lastEnterHandledFrame = Time.frameCount;

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (text.Length > 120) text = text.Substring(0, 120);

                SendMessageServerRpc(new FixedString128Bytes(text));
                UiSfx.Instance?.PlayChatSend();

                inputField.text = "";
                inputField.ActivateInputField();
                inputField.Select();
                StartHideTimer();
            }
            else
            {

                CloseInput();
            }
        }

        private void CloseInput()
        {
            _isChatOpen = false;
            inputField.text = "";
            inputField.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
            CursorLockController.Instance?.LockCursor();
            GameplayInputLock.Locked = false;

            StartHideTimer();
        }

        private void StartHideTimer()
        {
            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(visibleTime);
            if (!_isChatOpen) SetChatVisible(false);
            _hideRoutine = null;
        }

        private void SetChatVisible(bool visible)
        {
            CanvasGroup cg = chatPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = chatPanel.AddComponent<CanvasGroup>();

            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }

        private string NicknameFor(ulong clientId)
        {
            if (GameManager.Instance != null)
                return GameManager.Instance.GetNickname(clientId);

            if (LobbyManager.Instance != null)
            {
                foreach (var p in LobbyManager.Instance.Players)
                    if (p.ClientId == clientId) return p.PlayerName.ToString();
            }

            return $"Player {clientId}";
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendMessageServerRpc(FixedString128Bytes message, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            string text = message.ToString();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > 200) text = text.Substring(0, 200);

            string playerName = NicknameFor(senderId);
            string fullText = $"<b>{playerName}:</b> {text}";

            ReceiveMessageClientRpc(fullText);
        }

        [ClientRpc]
        private void ReceiveMessageClientRpc(string fullText)
        {
            AddMessage(fullText);

            if (!_isChatOpen)
            {
                SetChatVisible(true);
                StartHideTimer();
            }
        }

        private void AddMessage(string text)
        {
            GameObject go = Instantiate(messagePrefab, content);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;

            _messages.Add(go);

            if (_messages.Count > maxMessages)
            {
                Destroy(_messages[0]);
                _messages.RemoveAt(0);
            }

            StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
