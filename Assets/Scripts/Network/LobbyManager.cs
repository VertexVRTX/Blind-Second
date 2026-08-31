using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlindTiming.Game;

namespace BlindTiming.Network
{
    public struct PlayerLobbyState : INetworkSerializable, System.IEquatable<PlayerLobbyState>
    {
        public ulong ClientId;
        public bool IsReady;
        public FixedString32Bytes PlayerName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref PlayerName);
        }

        public bool Equals(PlayerLobbyState other) =>
            ClientId == other.ClientId && IsReady == other.IsReady && PlayerName.Equals(other.PlayerName);
    }

    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        public NetworkList<PlayerLobbyState> Players;

        public NetworkVariable<RoundMode> SelectedMode = new NetworkVariable<RoundMode>(
            RoundMode.Precision, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> TotalRounds = new NetworkVariable<int>(
            5, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] private string gameSceneName = "Game";

        private void Awake()
        {
            Instance = this;
            Players = new NetworkList<PlayerLobbyState>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                Players.Clear();
                var ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
                ids.Sort();
                foreach (var id in ids)
                {
                    Players.Add(new PlayerLobbyState
                    {
                        ClientId = id,
                        IsReady = false,
                        PlayerName = id == NetworkManager.ServerClientId ? "Player 1" : "Player 2"
                    });
                }

            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            foreach (var p in Players)
                if (p.ClientId == clientId) return;

            Players.Add(new PlayerLobbyState
            {
                ClientId = clientId,
                IsReady = false,
                PlayerName = "Player 2"
            });
        }

        private void OnClientDisconnected(ulong clientId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == clientId)
                {
                    Players.RemoveAt(i);
                    break;
                }
            }

            if (clientId != NetworkManager.ServerClientId)
                NotifyOpponentDisconnectedClientRpc();
        }

        [ClientRpc]
        private void NotifyOpponentDisconnectedClientRpc()
        {
            GameEvents.RaiseOpponentDisconnected();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool isReady, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == senderId)
                {
                    var p = Players[i];
                    p.IsReady = isReady;
                    Players[i] = p;
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitNicknameServerRpc(FixedString32Bytes nickname, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            string clean = nickname.ToString();
            if (string.IsNullOrWhiteSpace(clean)) return;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == senderId)
                {
                    var p = Players[i];
                    p.PlayerName = clean;
                    Players[i] = p;
                    Debug.Log($"[LobbyManager][SERVER] Client {senderId} set nickname \"{clean}\".");
                    break;
                }
            }
        }

        public bool AllReady()
        {
            if (Players.Count < 2) return false;
            foreach (var p in Players)
                if (!p.IsReady) return false;
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetModeServerRpc(RoundMode mode, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning("[LobbyManager] Only the host can change the game mode.");
                return;
            }
            SelectedMode.Value = mode;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetTotalRoundsServerRpc(int rounds, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning("[LobbyManager] Only the host can change the number of rounds.");
                return;
            }
            TotalRounds.Value = Mathf.Clamp(rounds, 1, 20);
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartGameServerRpc()
        {
            if (!IsServer) return;
            if (!AllReady()) return;

            MatchSettings.SelectedMode = SelectedMode.Value;
            MatchSettings.TotalRounds = TotalRounds.Value;

            if (Players.Count > 0) MatchSettings.PlayerANickname = Players[0].PlayerName.ToString();
            if (Players.Count > 1) MatchSettings.PlayerBNickname = Players[1].PlayerName.ToString();

            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }
}
