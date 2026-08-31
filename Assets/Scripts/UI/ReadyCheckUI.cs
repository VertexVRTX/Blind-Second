using TMPro;
using UnityEngine;
using DG.Tweening;
using Unity.Netcode;
using BlindTiming.Game;
using System.Collections;

namespace BlindTiming.UI
{
    public class ReadyCheckUI : MonoBehaviour
    {
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private TMP_Text readyHintText;
        [SerializeField] private TMP_Text readyCountText;

        private void Start()
        {
            if (readyPanel == null) Debug.LogError("[ReadyCheckUI] readyPanel is not assigned!");

            readyPanel.SetActive(true);
            if (readyHintText != null) readyHintText.text = "Press the red button when ready";
            UpdateCountText(0);

            StartCoroutine(SubscribeWhenReady());
        }

        private IEnumerator SubscribeWhenReady()
        {
            float timeout = 5f;
            while (GameManager.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError("[ReadyCheckUI] GameManager.Instance never appeared within 5 seconds. " +
                    "Check that the object with GameManager in the scene has a Network Object component.");
                yield break;
            }

            UpdateCountText(GameManager.Instance.ReadyCount.Value);
            GameManager.Instance.State.OnValueChanged += OnStateChanged;
            GameManager.Instance.ReadyCount.OnValueChanged += (_, newVal) => UpdateCountText(newVal);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.State.OnValueChanged -= OnStateChanged;
        }

        private void UpdateCountText(int readyCount)
        {
            if (readyCountText == null) return;
            int total = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 2;
            readyCountText.text = $"Ready: {readyCount}/{Mathf.Max(total, 2)}";
        }

        private void OnStateChanged(GameState oldState, GameState newState)
        {
            var cg = readyPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = readyPanel.AddComponent<CanvasGroup>();
            cg.DOKill();

            if (newState == GameState.WaitingReady)
            {

                UpdateCountText(0);
                readyPanel.SetActive(true);
                cg.alpha = 0f;
                cg.DOFade(1f, 0.3f);
            }
            else if (oldState == GameState.WaitingReady)
            {
                cg.DOFade(0f, 0.3f).OnComplete(() => readyPanel.SetActive(false));
            }
        }
    }
}
