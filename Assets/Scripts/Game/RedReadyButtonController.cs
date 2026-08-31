using System.Collections;
using Unity.Netcode;
using UnityEngine;
using DG.Tweening;
using BlindTiming.UI;

namespace BlindTiming.Game
{
    public class RedReadyButtonController : NetworkBehaviour
    {
        [Header("Aim target")]
        [Tooltip("THIS player's camera (the same one as in PlayerCameraController)")]
        [SerializeField] private Camera playerCamera;
        [Tooltip("The red button model itself (or its parent) - it needs a Collider")]
        [SerializeField] private Transform redButtonModel;
        [SerializeField] private float maxInteractDistance = 3f;

        [Header("Press animation")]
        [SerializeField] private float pressDepth = 0.015f;
        [SerializeField] private float pressDuration = 0.08f;

        private bool _alreadyReady;

        public override void OnNetworkSpawn()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        public override void OnNetworkDespawn()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.State.OnValueChanged -= OnGameStateChanged;
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
                Debug.LogError("[RedReadyButtonController] GameManager.Instance never appeared within 5 seconds.");
                yield break;
            }

            GameManager.Instance.State.OnValueChanged += OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState oldState, GameState newState)
        {

            if (newState == GameState.WaitingReady)
                _alreadyReady = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_alreadyReady) return;
            if (GameplayInputLock.Locked) return;

            if (GameManager.Instance == null || GameManager.Instance.State.Value != GameState.WaitingReady)
                return;

            if (Input.GetMouseButtonDown(0))
                TryPressButton();
        }

        private void TryPressButton()
        {
            if (playerCamera == null || redButtonModel == null)
            {
                Debug.LogError("[RedReadyButtonController] playerCamera or redButtonModel is not assigned!");
                return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance))
            {
                bool hitButton = hit.transform == redButtonModel || hit.transform.IsChildOf(redButtonModel);
                if (!hitButton)
                {
                    Debug.Log($"[RedReadyButtonController] Click missed the button, hit: {hit.transform.name}");
                    return;
                }
            }
            else
            {
                Debug.Log("[RedReadyButtonController] Click - the ray didn't hit anything (button too far or out of view).");
                return;
            }

            _alreadyReady = true;
            Debug.Log("[RedReadyButtonController] Hit the red button - sending readiness.");
            GameManager.Instance.SetPlayerReadyServerRpc();
            UiSfx.Instance?.PlayRedButton();
            AnimatePress();
        }

        private void AnimatePress()
        {
            redButtonModel.DOKill();
            Vector3 origin = redButtonModel.localPosition;
            Sequence seq = DOTween.Sequence();
            seq.Append(redButtonModel.DOLocalMoveY(origin.y - pressDepth, pressDuration).SetEase(Ease.OutQuad));
            seq.Append(redButtonModel.DOLocalMoveY(origin.y, pressDuration).SetEase(Ease.OutBounce));
        }
    }
}
