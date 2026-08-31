using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using DG.Tweening;
using BlindTiming.Game;

namespace BlindTiming.UI
{
    public class CountdownUI : MonoBehaviour
    {
        [SerializeField] private NetworkObject mySeat;

        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text roundInfoText;
        [Header("Role panel (IntervalGuess mode)")]
        [Tooltip("Panel with its own background Image + CanvasGroup, containing roleText as a child. " +
                 "Give it the same RectTransform anchors/size as resultsPanel so it can visually cover it. " +
                 "If left empty, falls back to showing roleText directly with no background/cover behaviour.")]
        [SerializeField] private GameObject rolePanel;
        [Tooltip("Role hint text (\"You set the time\" / \"Guess the moment\" / next-round preview). Lives inside rolePanel.")]
        [SerializeField] private TMP_Text roleText;
        [Tooltip("Seconds after the results panel appears before the role panel is revealed on top of it.")]
        [SerializeField] private float nextRoleRevealDelay = 5f;

        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private TMP_Text resultsText;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip firstBeepClip;
        [SerializeField] private AudioClip secondBeepClip;

        private bool _myTimerStopped;
        private float _myStoppedValue;
        private bool _subscribed;
        private bool _refsOk;
        private Coroutine _roleRevealRoutine;

        private void OnEnable()
        {
            _refsOk = ValidateReferences();
            if (!_refsOk) return;

            GameEvents.RoundResolved += HandleRoundResolved;
            GameEvents.PressAccepted += HandlePressAccepted;
            resultsPanel.SetActive(false);
            countdownText.gameObject.SetActive(false);
            SetRolePanelVisible(false);

            StartCoroutine(SubscribeToGameManagerWhenReady());
        }

        private bool ValidateReferences()
        {
            bool ok = true;
            void Check(Object o, string name)
            {
                if (o == null) { Debug.LogError($"[CountdownUI] Field '{name}' is not assigned on {gameObject.name}!", this); ok = false; }
            }
            Check(countdownText, nameof(countdownText));
            Check(modeText, nameof(modeText));
            Check(timerText, nameof(timerText));
            Check(roundInfoText, nameof(roundInfoText));
            Check(resultsPanel, nameof(resultsPanel));
            Check(resultsText, nameof(resultsText));
            return ok;
        }

        private void OnDisable()
        {
            GameEvents.RoundResolved -= HandleRoundResolved;
            GameEvents.PressAccepted -= HandlePressAccepted;
            CancelRoleReveal();

            if (!_subscribed || GameManager.Instance == null) return;
            GameManager.Instance.State.OnValueChanged -= HandleStateChanged;
            GameManager.Instance.FirstBeepServerTime.OnValueChanged -= HandleFirstBeep;
            GameManager.Instance.SecondBeepServerTime.OnValueChanged -= HandleSecondBeep;
        }

        private IEnumerator SubscribeToGameManagerWhenReady()
        {
            float timeout = 5f;
            while (GameManager.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (GameManager.Instance == null) yield break;

            GameManager.Instance.State.OnValueChanged += HandleStateChanged;
            GameManager.Instance.FirstBeepServerTime.OnValueChanged += HandleFirstBeep;
            GameManager.Instance.SecondBeepServerTime.OnValueChanged += HandleSecondBeep;
            GameManager.Instance.RoundNumber.OnValueChanged += (_, __) => RefreshRoundInfo();
            GameManager.Instance.SeatAWins.OnValueChanged += (_, __) => RefreshRoundInfo();
            GameManager.Instance.SeatBWins.OnValueChanged += (_, __) => RefreshRoundInfo();
            GameManager.Instance.SetterClientId.OnValueChanged += (_, __) => UpdateRoleText(GameManager.Instance.State.Value);
            _subscribed = true;
            RefreshRoundInfo();
        }

        private void RefreshRoundInfo()
        {
            if (roundInfoText == null || GameManager.Instance == null) return;
            var gm = GameManager.Instance;
            roundInfoText.text = $"Round {gm.RoundNumber.Value}/{gm.TotalRoundsNV.Value}   " +
                $"Score: {gm.SeatAWins.Value} - {gm.SeatBWins.Value}";
        }

        private void Update()
        {
            if (!_refsOk || GameManager.Instance == null) return;

            if (GameManager.Instance.State.Value == GameState.WaitingForInput)
            {
                timerText.gameObject.SetActive(true);

                if (_myTimerStopped)
                {
                    timerText.text = _myStoppedValue.ToString("00.000");
                }
                else
                {
                    double elapsed = NetworkManager.Singleton.LocalTime.Time - GameManager.Instance.TimerStartServerTime.Value;
                    timerText.text = System.Math.Max(0, elapsed).ToString("00.000");
                }
            }
            else
            {
                timerText.gameObject.SetActive(false);
            }
        }

        private void HandlePressAccepted(ulong clientId, float pressedAt)
        {
            if (mySeat != null && mySeat.OwnerClientId == clientId)
            {
                _myTimerStopped = true;
                _myStoppedValue = pressedAt;
            }
        }

        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            if (newState == GameState.Countdown)
            {
                resultsPanel.SetActive(false);
                CancelRoleReveal();
                SetRolePanelVisible(false);
                _myTimerStopped = false;
                modeText.text = $"Mode: {ModeLabel(GameManager.Instance.CurrentMode.Value)}";
                RefreshRoundInfo();
            }

            UpdateRoleText(newState);
        }

        private void UpdateRoleText(GameState state)
        {
            if (roleText == null || GameManager.Instance == null || mySeat == null) return;
            if (state == GameState.Results) return;

            var gm = GameManager.Instance;

            if (gm.CurrentMode.Value != RoundMode.IntervalGuess)
            {
                CancelRoleReveal();
                SetRolePanelVisible(false);
                return;
            }

            bool isSetter = gm.SetterClientId.Value == mySeat.OwnerClientId;
            string text = state switch
            {
                GameState.SettingStart => isSetter ? "Press the button to start the count" : "Opponent is setting the time...",
                GameState.SettingEnd => isSetter ? "Press the button again to end the count" : "Opponent is setting the time...",
                GameState.WaitingForInput => isSetter ? "Wait for your opponent's guess..." : "Guess the moment and press the button!",
                _ => ""
            };

            CancelRoleReveal();
            roleText.text = text;
            SetRolePanelVisible(!string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// Shows/hides the role panel (or bare roleText if no panel was assigned), with a short fade.
        /// </summary>
        private void SetRolePanelVisible(bool visible)
        {
            GameObject target = rolePanel != null ? rolePanel : (roleText != null ? roleText.gameObject : null);
            if (target == null) return;

            if (!visible)
            {
                target.SetActive(false);
                return;
            }

            target.SetActive(true);
            target.transform.SetAsLastSibling();

            var cg = target.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.DOKill();
                cg.alpha = 0f;
                cg.DOFade(1f, 0.3f);
            }
        }

        private void CancelRoleReveal()
        {
            if (_roleRevealRoutine != null)
            {
                StopCoroutine(_roleRevealRoutine);
                _roleRevealRoutine = null;
            }
        }

        /// <summary>
        /// Waits `delay` seconds after the results panel appeared, then reveals the role panel
        /// on top of it, showing which role this player will have in the next round.
        /// </summary>
        private IEnumerator RevealRolePanelAfterDelay(string text, float delay)
        {
            yield return new WaitForSeconds(delay);

            roleText.text = text;
            SetRolePanelVisible(true);

            _roleRevealRoutine = null;
        }

        private void HandleFirstBeep(double oldValue, double newValue) => FlashBeep(firstBeepClip);
        private void HandleSecondBeep(double oldValue, double newValue) => FlashBeep(secondBeepClip);

        private void FlashBeep(AudioClip clip)
        {
            PlaySound(clip);
            StopCoroutine(nameof(ShowBeepText));
            StartCoroutine(ShowBeepText());
        }

        private IEnumerator ShowBeepText()
        {
            countdownText.text = "BEEP!";
            countdownText.gameObject.SetActive(true);
            PunchText(countdownText.transform);
            yield return new WaitForSeconds(0.4f);
            countdownText.gameObject.SetActive(false);
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        private void PunchText(Transform t)
        {
            t.DOKill();
            t.localScale = Vector3.one * 1.4f;
            t.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
        }

        private string NicknameFor(ulong clientId) =>
            GameManager.Instance != null ? GameManager.Instance.GetNickname(clientId) : $"Player {clientId}";

        private void HandleRoundResolved(float target, RoundMode mode, ulong[] ids, float[] presses,
            bool success, ulong winnerId, bool hasWinner, int pointsAwarded, string outcomeLabel)
        {
            resultsPanel.SetActive(true);
            var cg = resultsPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.DOFade(1f, 0.3f);
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Target time: {target:00.000}");
            for (int i = 0; i < ids.Length; i++)
                sb.AppendLine($"{NicknameFor(ids[i])}: {presses[i]:00.000}");

            sb.AppendLine(outcomeLabel);

            if (mode == RoundMode.Cooperative)
            {
                if (pointsAwarded > 0) sb.AppendLine($"+{pointsAwarded} team point(s)");
            }
            else if (hasWinner)
            {
                sb.AppendLine($"{NicknameFor(winnerId)} +{pointsAwarded}");
            }

            if (GameManager.Instance != null && GameManager.Instance.MatchOver.Value)
            {
                sb.AppendLine();
                sb.AppendLine("=== MATCH OVER ===");
                sb.AppendLine($"Final score: {GameManager.Instance.SeatAWins.Value} - {GameManager.Instance.SeatBWins.Value}");
            }

            resultsText.text = sb.ToString();
            RefreshRoundInfo();
            PlayOutcomeSound(mode, success, winnerId, hasWinner);

            CancelRoleReveal();
            SetRolePanelVisible(false);

            bool matchOver = GameManager.Instance != null && GameManager.Instance.MatchOver.Value;
            if (mode == RoundMode.IntervalGuess && !matchOver && mySeat != null && GameManager.Instance != null)
            {
                bool wasSetterThisRound = GameManager.Instance.SetterClientId.Value == mySeat.OwnerClientId;
                bool willBeSetterNextRound = !wasSetterThisRound;

                string nextRoleText = willBeSetterNextRound
                    ? "Next round: you set the time"
                    : "Next round: you guess the moment";

                _roleRevealRoutine = StartCoroutine(RevealRolePanelAfterDelay(nextRoleText, nextRoleRevealDelay));
            }
        }

        private void PlayOutcomeSound(RoundMode mode, bool success, ulong winnerId, bool hasWinner)
        {
            if (mySeat == null) return;

            bool localWon;
            if (mode == RoundMode.Cooperative)
            {
                localWon = success;
            }
            else if (mode == RoundMode.IntervalGuess)
            {
                bool isSetter = GameManager.Instance != null && GameManager.Instance.SetterClientId.Value == mySeat.OwnerClientId;
                localWon = isSetter ? !hasWinner : (hasWinner && mySeat.OwnerClientId == winnerId);
            }
            else
            {
                localWon = hasWinner && mySeat.OwnerClientId == winnerId;
            }

            if (localWon) UiSfx.Instance?.PlayRoundWin();
            else UiSfx.Instance?.PlayRoundLose();
        }

        private string ModeLabel(RoundMode mode) => mode switch
        {
            RoundMode.Precision => "Close to perfect",
            RoundMode.IntervalGuess => "Interval guess",
            RoundMode.Cooperative => "Blind co-op",
            _ => mode.ToString()
        };
    }
}
