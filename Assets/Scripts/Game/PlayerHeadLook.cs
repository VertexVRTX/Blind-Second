using Unity.Netcode;
using UnityEngine;
using BlindTiming.UI;

namespace BlindTiming.Game
{
    public class PlayerHeadLook : NetworkBehaviour
    {
        [Header("Head model (cosmetic, networked, seen by the opponent)")]
        [SerializeField] private Transform headBone;
        [SerializeField] private float maxYaw = 35f;
        [SerializeField] private float maxPitch = 20f;
        [Tooltip("Base sensitivity - multiplied at runtime by the player's own Settings slider (SettingsData.Sensitivity), 1 = unchanged.")]
        [SerializeField] private float mouseSensitivity = 3f;

        [Header("Camera (local only, actually changes what THIS player sees/aims at)")]
        [Tooltip("The same camera object used by PlayerCameraController/PlayerButtonController/RedReadyButtonController. Leave empty to disable camera look entirely (head model still turns).")]
        [SerializeField] private Transform cameraPivot;
        [Tooltip("How far the camera itself is allowed to turn - keep this modest so the buttons/screen stay reachable/visible.")]
        [SerializeField] private float cameraMaxYaw = 40f;
        [SerializeField] private float cameraMaxPitch = 25f;
        [Tooltip("Base camera sensitivity - multiplied at runtime by SettingsData.CameraSensitivity, a SEPARATE slider from the head model's.")]
        [SerializeField] private float cameraSensitivity = 3f;

        [Header("Shared")]
        [SerializeField] private float smoothSpeed = 8f;

        private NetworkVariable<Vector2> _lookAngles = new NetworkVariable<Vector2>(
            Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Quaternion _headBaseRotation;
        private Vector2 _accumulatedHeadAngles;

        private Quaternion _cameraBaseRotation;
        private Vector2 _accumulatedCameraAngles;

        public override void OnNetworkSpawn()
        {
            if (headBone != null) _headBaseRotation = headBone.localRotation;
            if (cameraPivot != null) _cameraBaseRotation = cameraPivot.localRotation;

            Debug.Log($"[PlayerHeadLook] {gameObject.name} spawned. OwnerClientId = {OwnerClientId}");
        }

        private void Update()
        {
            if (IsOwner && !GameplayInputLock.Locked)
            {
                float headSens = mouseSensitivity * SettingsData.Sensitivity;
                float hmx = Input.GetAxis("Mouse X") * headSens;
                float hmy = Input.GetAxis("Mouse Y") * headSens;

                _accumulatedHeadAngles.x = Mathf.Clamp(_accumulatedHeadAngles.x + hmx, -maxYaw, maxYaw);
                _accumulatedHeadAngles.y = Mathf.Clamp(_accumulatedHeadAngles.y + hmy, -maxPitch, maxPitch);

                if (hmx != 0f || hmy != 0f)
                    _lookAngles.Value = _accumulatedHeadAngles;

                if (cameraPivot != null)
                {
                    float camSens = cameraSensitivity * SettingsData.CameraSensitivity;
                    float cmx = Input.GetAxis("Mouse X") * camSens;
                    float cmy = Input.GetAxis("Mouse Y") * camSens;

                    _accumulatedCameraAngles.x = Mathf.Clamp(_accumulatedCameraAngles.x + cmx, -cameraMaxYaw, cameraMaxYaw);
                    _accumulatedCameraAngles.y = Mathf.Clamp(_accumulatedCameraAngles.y + cmy, -cameraMaxPitch, cameraMaxPitch);

                    Quaternion cameraTargetRot = _cameraBaseRotation * Quaternion.Euler(-_accumulatedCameraAngles.y, _accumulatedCameraAngles.x, 0f);
                    cameraPivot.localRotation = Quaternion.Slerp(cameraPivot.localRotation, cameraTargetRot, Time.deltaTime * smoothSpeed);
                }
            }

            if (headBone != null)
            {
                Vector2 target = _lookAngles.Value;
                Quaternion headTargetRot = _headBaseRotation * Quaternion.Euler(-target.y, target.x, 0f);
                headBone.localRotation = Quaternion.Slerp(headBone.localRotation, headTargetRot, Time.deltaTime * smoothSpeed);
            }
        }
    }
}
