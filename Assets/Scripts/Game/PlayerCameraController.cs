using Unity.Netcode;
using UnityEngine;

namespace BlindTiming.Game
{
    public class PlayerCameraController : NetworkBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;

        [Header("Gameplay UI (canvas must be Render Mode = Screen Space - Camera)")]
        [SerializeField] private Canvas gameplayCanvas;
        [SerializeField] private float canvasPlaneDistance = 1f;

        public override void OnNetworkSpawn()
        {
            ApplyState();
        }

        public override void OnGainedOwnership() => ApplyState();
        public override void OnLostOwnership() => ApplyState();

        private void ApplyState()
        {
            bool isMine = IsOwner;

            if (playerCamera != null)
                playerCamera.gameObject.SetActive(isMine);

            if (audioListener != null)
                audioListener.enabled = isMine;

            if (isMine)
                AssignCanvasCamera();
        }

        private void AssignCanvasCamera()
        {
            if (gameplayCanvas == null)
            {
                gameplayCanvas = FindGameplayCanvas();
            }

            if (gameplayCanvas == null || playerCamera == null) return;

            if (gameplayCanvas.renderMode != RenderMode.ScreenSpaceCamera)
                gameplayCanvas.renderMode = RenderMode.ScreenSpaceCamera;

            gameplayCanvas.worldCamera = playerCamera;
            gameplayCanvas.planeDistance = canvasPlaneDistance;
        }

        private Canvas FindGameplayCanvas()
        {
            var go = GameObject.FindWithTag("GameplayCanvas");
            return go != null ? go.GetComponent<Canvas>() : null;
        }
    }
}
