using System.Collections;
using Unity.Netcode;
using UnityEngine;
using DG.Tweening;
using BlindTiming.UI;

namespace BlindTiming.Game
{
    public class PlayerButtonController : NetworkBehaviour
    {
        [SerializeField] private Transform physicalButtonModel;
        [SerializeField] private float pressDepth = 0.02f;
        [SerializeField] private float pressDuration = 0.08f;

        private bool _hasPressedThisPhase;

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.State.OnValueChanged -= HandleStateChanged;
        }

        private IEnumerator SubscribeWhenReady()
        {
            float timeout = 5f;
            while (GameManager.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (GameManager.Instance == null) yield break;

            GameManager.Instance.State.OnValueChanged += HandleStateChanged;
        }

        private void HandleStateChanged(GameState oldState, GameState newState) => _hasPressedThisPhase = false;

        private void Update()
        {
            if (!IsOwner) return;
            if (GameplayInputLock.Locked) return;
            if (GameManager.Instance == null) return;
            if (_hasPressedThisPhase) return;

            bool pressedThisFrame = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
            if (!pressedThisFrame) return;

            var gm = GameManager.Instance;

            switch (gm.State.Value)
            {
                case GameState.SettingStart:
                    if (gm.CurrentMode.Value == RoundMode.IntervalGuess && gm.SetterClientId.Value == OwnerClientId)
                    {
                        _hasPressedThisPhase = true;
                        gm.SubmitSetStartServerRpc();
                        UiSfx.Instance?.PlayRoundButton();
                        AnimatePress();
                    }
                    break;

                case GameState.SettingEnd:
                    if (gm.CurrentMode.Value == RoundMode.IntervalGuess && gm.SetterClientId.Value == OwnerClientId)
                    {
                        _hasPressedThisPhase = true;
                        gm.SubmitSetEndServerRpc();
                        UiSfx.Instance?.PlayRoundButton();
                        AnimatePress();
                    }
                    break;

                case GameState.WaitingForInput:
                    if (gm.CurrentMode.Value != RoundMode.IntervalGuess || gm.SetterClientId.Value != OwnerClientId)
                        OnPressButton();
                    break;
            }
        }

        public void OnPressButton()
        {
            if (_hasPressedThisPhase) return;
            _hasPressedThisPhase = true;

            double localTime = NetworkManager.Singleton.LocalTime.Time;
            GameManager.Instance.SubmitPressServerRpc(localTime);

            UiSfx.Instance?.PlayRoundButton();
            AnimatePress();
        }

        private void AnimatePress()
        {
            if (physicalButtonModel == null) return;
            physicalButtonModel.DOKill();
            Vector3 origin = physicalButtonModel.localPosition;
            Sequence seq = DOTween.Sequence();
            seq.Append(physicalButtonModel.DOLocalMoveY(origin.y - pressDepth, pressDuration).SetEase(Ease.OutQuad));
            seq.Append(physicalButtonModel.DOLocalMoveY(origin.y, pressDuration).SetEase(Ease.OutBounce));
        }
    }
}
