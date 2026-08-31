using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace BlindTiming.Game
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        public NetworkVariable<GameState> State = new NetworkVariable<GameState>(GameState.WaitingReady,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<RoundMode> CurrentMode = new NetworkVariable<RoundMode>(RoundMode.Precision,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<double> FirstBeepServerTime = new NetworkVariable<double>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<double> SecondBeepServerTime = new NetworkVariable<double>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<double> TimerStartServerTime = new NetworkVariable<double>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> ReadyCount = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Tooltip("Who sets the time this round in IntervalGuess mode (clientId). Not used in other modes.")]
        public NetworkVariable<ulong> SetterClientId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> RoundNumber = new NetworkVariable<int>(1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> TotalRoundsNV = new NetworkVariable<int>(5,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> SeatAWins = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> SeatBWins = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> MatchOver = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString32Bytes> SeatANickname = new NetworkVariable<FixedString32Bytes>("Player 1",
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString32Bytes> SeatBNickname = new NetworkVariable<FixedString32Bytes>("Player 2",
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] private NetworkObject seatA;
        [SerializeField] private NetworkObject seatB;

        [SerializeField] private float preBeepDelay = 1f;
        [SerializeField] private float postBeepDelay = 0.5f;
        [SerializeField] private float resultsDisplayDuration = 3f;
        [Tooltip("How long to wait for input in the WaitingForInput phase before forcibly ending the round (hang protection if a player never presses).")]
        [SerializeField] private float inputTimeoutBuffer = 3f;

        [Header("Interval Guess mode (setter/guesser)")]
        [Tooltip("Outer tolerance - a guess within this time of the target counts as success.")]
        [FormerlySerializedAs("bombPerfectWindow")]
        [SerializeField] private float guessTolerance = 1f;
        [Tooltip("Inner, stricter tolerance - a perfect hit, worth bonus points.")]
        [SerializeField] private float guessPerfectTolerance = 0.05f;
        [FormerlySerializedAs("bombPerfectPoints")]
        [SerializeField] private int guessPerfectPoints = 2;
        [FormerlySerializedAs("bombNormalPoints")]
        [SerializeField] private int guessNormalPoints = 1;
        [Tooltip("How long to wait for the setter's input in the SettingStart/SettingEnd phases before counting it as a timeout miss.")]
        [SerializeField] private float settingPhaseTimeout = 20f;
        [Tooltip("The interval set by the first player is clamped to this range (seconds) - protection against degenerate 0.00s intervals or overly long rounds.")]
        [SerializeField] private float minSetterInterval = 0.3f;
        [SerializeField] private float maxSetterInterval = 15f;

        [Header("Other modes")]
        [SerializeField] private int precisionPoints = 1;
        [SerializeField] private int cooperativePoints = 1;
        [SerializeField] private float cooperativeTolerance = 0.15f;

        private float _targetTime;
        private double _setterStartServerTime;
        private bool _initialSetterIsSeatA;

        private readonly Dictionary<ulong, float> _pressTimes = new Dictionary<ulong, float>();
        private readonly HashSet<ulong> _readyPlayers = new HashSet<ulong>();
        private readonly HashSet<ulong> _skipResultsRequests = new HashSet<ulong>();
        private const float MIN_TARGET = 2f;
        private const float MAX_TARGET = 8f;

        private void Awake() => Instance = this;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentMode.Value = MatchSettings.SelectedMode;
                TotalRoundsNV.Value = MatchSettings.TotalRounds;
                SeatANickname.Value = MatchSettings.PlayerANickname;
                SeatBNickname.Value = MatchSettings.PlayerBNickname;
                RoundNumber.Value = 1;
                SeatAWins.Value = 0;
                SeatBWins.Value = 0;
                MatchOver.Value = false;
                State.Value = GameState.WaitingReady;
                _initialSetterIsSeatA = Random.value < 0.5f;

                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                if (NetworkManager.SceneManager != null)
                    NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
            }
            StopAllCoroutines();
            CancelInvoke();
        }

        private void HandleLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId) return;
            NotifyOpponentDisconnectedClientRpc();
        }

        [ClientRpc]
        private void NotifyOpponentDisconnectedClientRpc() => GameEvents.RaiseOpponentDisconnected();

        [ServerRpc(RequireOwnership = false)]
        public void RequestReturnToLobbyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            StopAllCoroutines();
            CancelInvoke();
            NetworkManager.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public string GetNickname(ulong clientId)
        {
            if (seatA != null && seatA.OwnerClientId == clientId) return SeatANickname.Value.ToString();
            if (seatB != null && seatB.OwnerClientId == clientId) return SeatBNickname.Value.ToString();
            return $"Player {clientId}";
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (State.Value != GameState.WaitingReady) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            if (_readyPlayers.Add(senderId))
                ReadyCount.Value = _readyPlayers.Count;

            int totalPlayers = NetworkManager.ConnectedClientsIds.Count;
            if (totalPlayers >= 2 && _readyPlayers.Count >= totalPlayers)
                BeginCountdown();
        }

        [ContextMenu("Server: Begin Countdown")]
        public void BeginCountdown()
        {
            if (!IsServer) return;

            _pressTimes.Clear();
            State.Value = GameState.Countdown;
            StopAllCoroutines();

            if (CurrentMode.Value == RoundMode.IntervalGuess)
            {
                bool seatAIsSetterThisRound = (RoundNumber.Value % 2 == 1) == _initialSetterIsSeatA;
                SetterClientId.Value = seatAIsSetterThisRound
                    ? (seatA != null ? seatA.OwnerClientId : 0)
                    : (seatB != null ? seatB.OwnerClientId : 0);

                StartCoroutine(ServerRoundSequenceIntervalGuess());
            }
            else
            {
                _targetTime = Random.Range(MIN_TARGET, MAX_TARGET);
                StartCoroutine(ServerRoundSequence());
            }
        }

        private IEnumerator ServerRoundSequence()
        {
            yield return new WaitForSeconds(preBeepDelay);
            FirstBeepServerTime.Value = NetworkManager.ServerTime.Time;
            State.Value = GameState.Suspense;

            yield return new WaitForSeconds(_targetTime);
            SecondBeepServerTime.Value = NetworkManager.ServerTime.Time;

            yield return new WaitForSeconds(postBeepDelay);
            TimerStartServerTime.Value = NetworkManager.ServerTime.Time;
            State.Value = GameState.WaitingForInput;

            yield return new WaitForSeconds(_targetTime + inputTimeoutBuffer);
            if (State.Value == GameState.WaitingForInput)
                ResolveRound();
        }

        private IEnumerator ServerRoundSequenceIntervalGuess()
        {
            yield return new WaitForSeconds(preBeepDelay);
            State.Value = GameState.SettingStart;

            float t = 0f;
            while (State.Value == GameState.SettingStart)
            {
                if (t >= settingPhaseTimeout)
                {
                    AbortIntervalGuessRound("Setter did not start the count in time");
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (State.Value == GameState.SettingEnd)
            {
                if (t >= settingPhaseTimeout)
                {
                    AbortIntervalGuessRound("Setter did not end the count in time");
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(postBeepDelay);
            TimerStartServerTime.Value = NetworkManager.ServerTime.Time;
            State.Value = GameState.WaitingForInput;

            yield return new WaitForSeconds(_targetTime + inputTimeoutBuffer);
            if (State.Value == GameState.WaitingForInput)
                ResolveRound();
        }

        private void AbortIntervalGuessRound(string reason)
        {
            State.Value = GameState.Results;
            RevealResultsClientRpc(0f, RoundMode.IntervalGuess, System.Array.Empty<ulong>(),
                System.Array.Empty<float>(), false, 0, false, 0, reason);
            FinishRound();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitSetStartServerRpc(ServerRpcParams rpcParams = default)
        {
            if (State.Value != GameState.SettingStart) return;
            if (rpcParams.Receive.SenderClientId != SetterClientId.Value) return;

            _setterStartServerTime = NetworkManager.ServerTime.Time;
            FirstBeepServerTime.Value = _setterStartServerTime;
            State.Value = GameState.SettingEnd;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitSetEndServerRpc(ServerRpcParams rpcParams = default)
        {
            if (State.Value != GameState.SettingEnd) return;
            if (rpcParams.Receive.SenderClientId != SetterClientId.Value) return;

            double endServerTime = NetworkManager.ServerTime.Time;
            SecondBeepServerTime.Value = endServerTime;

            _targetTime = Mathf.Clamp((float)(endServerTime - _setterStartServerTime), minSetterInterval, maxSetterInterval);

            State.Value = GameState.Suspense;
        }


        [ServerRpc(RequireOwnership = false)]
        public void SubmitPressServerRpc(double clientLocalTime, ServerRpcParams rpcParams = default)
        {
            if (State.Value != GameState.WaitingForInput) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            if (_pressTimes.ContainsKey(senderId)) return;

            if (CurrentMode.Value == RoundMode.IntervalGuess && senderId == SetterClientId.Value) return;

            float pressedAt = (float)(clientLocalTime - TimerStartServerTime.Value);
            _pressTimes[senderId] = Mathf.Max(0f, pressedAt);

            PressAcceptedClientRpc(senderId, pressedAt);

            bool shouldResolve = CurrentMode.Value == RoundMode.IntervalGuess
                ? _pressTimes.Count >= 1
                : _pressTimes.Count >= NetworkManager.ConnectedClientsIds.Count;

            if (shouldResolve)
                ResolveRound();
        }

        public double GetClientRttMs(ulong clientId)
        {
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport
                as Unity.Netcode.Transports.UTP.UnityTransport;
            return transport == null ? 0 : transport.GetCurrentRtt(clientId);
        }

        [ClientRpc]
        private void PressAcceptedClientRpc(ulong clientId, float pressedAt) => GameEvents.RaisePressAccepted(clientId, pressedAt);

        private void ResolveRound()
        {
            State.Value = GameState.Results;

            RoundMode mode = CurrentMode.Value;
            float target = _targetTime;
            var ids = new List<ulong>(_pressTimes.Keys);

            if (mode == RoundMode.Cooperative) { ResolveCooperative(target, ids); return; }
            if (mode == RoundMode.IntervalGuess) { ResolveIntervalGuess(target, ids); return; }
            ResolvePrecision(target, ids);
        }

        private void ResolvePrecision(float target, List<ulong> ids)
        {
            ulong winnerId = 0;
            bool hasWinner = false;
            float bestDiff = float.MaxValue;

            foreach (var kvp in _pressTimes)
            {
                float absDiff = Mathf.Abs(kvp.Value - target);
                if (absDiff < bestDiff)
                {
                    bestDiff = absDiff;
                    winnerId = kvp.Key;
                    hasWinner = true;
                }
            }

            int points = 0;
            string outcomeLabel = "No winner";

            if (hasWinner)
            {
                points = precisionPoints;
                AwardPoint(winnerId, points);
                outcomeLabel = "Closest to the target";
            }

            RevealResultsClientRpc(target, RoundMode.Precision, ids.ToArray(), ToArray(_pressTimes),
                hasWinner, winnerId, hasWinner, points, outcomeLabel);
            FinishRound();
        }

        private void ResolveIntervalGuess(float target, List<ulong> ids)
        {
            bool hasGuess = ids.Count > 0;
            ulong guesserId = hasGuess ? ids[0] : 0;

            int points = 0;
            bool success = false;
            string outcomeLabel;

            if (!hasGuess)
            {
                outcomeLabel = "Guesser did not press in time";
            }
            else
            {
                float diff = Mathf.Abs(_pressTimes[guesserId] - target);

                if (diff <= guessPerfectTolerance)
                {
                    success = true;
                    points = guessPerfectPoints;
                    outcomeLabel = "Perfect hit!";
                }
                else if (diff <= guessTolerance)
                {
                    success = true;
                    points = guessNormalPoints;
                    outcomeLabel = "Guessed correctly";
                }
                else
                {
                    outcomeLabel = "Missed";
                }

                if (success) AwardPoint(guesserId, points);
            }

            RevealResultsClientRpc(target, RoundMode.IntervalGuess, ids.ToArray(), ToArray(_pressTimes),
                success, guesserId, success, points, outcomeLabel);
            FinishRound();
        }

        private void ResolveCooperative(float target, List<ulong> ids)
        {
            float sum = 0f;
            foreach (var t in _pressTimes.Values) sum += t;
            bool success = Mathf.Abs(sum - target) <= cooperativeTolerance;

            int points = 0;
            string outcomeLabel = success ? "Team success" : "Team missed";

            if (success)
            {
                points = cooperativePoints;
                SeatAWins.Value += points;
                SeatBWins.Value += points;
            }

            RevealResultsClientRpc(target, RoundMode.Cooperative, ids.ToArray(), ToArray(_pressTimes),
                success, 0, false, points, outcomeLabel);
            FinishRound();
        }

        private void AwardPoint(ulong clientId, int points)
        {
            if (seatA != null && clientId == seatA.OwnerClientId) SeatAWins.Value += points;
            else if (seatB != null && clientId == seatB.OwnerClientId) SeatBWins.Value += points;
        }

        private void FinishRound()
        {
            _skipResultsRequests.Clear();

            if (RoundNumber.Value >= TotalRoundsNV.Value)
                MatchOver.Value = true;
            else
                Invoke(nameof(ReturnToReadyForNextRound), resultsDisplayDuration);
        }

        private void ReturnToReadyForNextRound()
        {
            if (!IsServer || MatchOver.Value) return;

            CancelInvoke(nameof(ReturnToReadyForNextRound));
            _skipResultsRequests.Clear();
            RoundNumber.Value++;
            _readyPlayers.Clear();
            ReadyCount.Value = 0;
            State.Value = GameState.WaitingReady;
        }

        private float[] ToArray(Dictionary<ulong, float> dict)
        {
            var arr = new float[dict.Count];
            int i = 0;
            foreach (var v in dict.Values) arr[i++] = v;
            return arr;
        }

        [ClientRpc]
        private void RevealResultsClientRpc(float targetTime, RoundMode mode, ulong[] playerIds,
            float[] pressTimes, bool success, ulong winnerId, bool hasWinner, int pointsAwarded, string outcomeLabel)
        {
            GameEvents.RaiseRoundResolved(targetTime, mode, playerIds, pressTimes, success, winnerId, hasWinner, pointsAwarded, outcomeLabel);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestNextRoundServerRpc(ServerRpcParams rpcParams = default)
        {
            if (State.Value != GameState.Results || MatchOver.Value) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            _skipResultsRequests.Add(senderId);

            if (_skipResultsRequests.Count >= NetworkManager.ConnectedClientsIds.Count)
                ReturnToReadyForNextRound();
        }
    }
}
