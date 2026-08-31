using System.Collections;
using Unity.Netcode;
using UnityEngine;
using DG.Tweening;
using BlindTiming.Game;

namespace BlindTiming.Game
{
    public class NextRoundButtonController : NetworkBehaviour
    {
        [Header("Aim target")]
        [SerializeField] private Camera playerCamera;
        [Tooltip("Button model - needs a (non-trigger) Collider")]
        [SerializeField] private Transform buttonModel;
        [SerializeField] private float maxInteractDistance = 3f;

        [Header("Press animation")]
        [SerializeField] private float pressDepth = 0.015f;
        [SerializeField] private float pressDuration = 0.08f;

        private bool _pressedThisResultsScreen;

        public override void OnNetworkSpawn()
        {
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
                Debug.LogError("[NextRoundButtonController] GameManager.Instance never appeared.");
                yield break;
            }

            GameManager.Instance.State.OnValueChanged += OnGameStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.State.OnValueChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState oldState, GameState newState)
        {
            if (newState != GameState.Results)
                _pressedThisResultsScreen = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_pressedThisResultsScreen) return;

            if (GameManager.Instance == null || GameManager.Instance.State.Value != GameState.Results)
                return;

            if (GameManager.Instance.MatchOver.Value)
                return;

            if (Input.GetMouseButtonDown(0))
                TryPressButton();
        }

        private void TryPressButton()
        {
            if (playerCamera == null || buttonModel == null)
            {
                Debug.LogError("[NextRoundButtonController] playerCamera or buttonModel is not assigned!");
                return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance))
            {
                Debug.Log("[NextRoundButtonController] Click - the ray didn't hit anything.");
                return;
            }

            bool hitButton = hit.transform == buttonModel || hit.transform.IsChildOf(buttonModel);
            if (!hitButton)
            {
                Debug.Log($"[NextRoundButtonController] Click missed the button, hit: {hit.transform.name}");
                return;
            }

            _pressedThisResultsScreen = true;
            Debug.Log("[NextRoundButtonController] Hit - requesting next round.");
            GameManager.Instance.RequestNextRoundServerRpc();
            AnimatePress();
        }

        private void AnimatePress()
        {
            buttonModel.DOKill();
            Vector3 origin = buttonModel.localPosition;
            Sequence seq = DOTween.Sequence();
            seq.Append(buttonModel.DOLocalMoveY(origin.y - pressDepth, pressDuration).SetEase(Ease.OutQuad));
            seq.Append(buttonModel.DOLocalMoveY(origin.y, pressDuration).SetEase(Ease.OutBounce));
        }
    }
}
