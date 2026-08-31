using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BlindTiming.Game
{
    public class PlayerSpawnAssigner : NetworkBehaviour
    {
        [Tooltip("Character with the model for player 1 (usually the host)")]
        [SerializeField] private NetworkObject seatA;

        [Tooltip("Character with the model for player 2 (usually the guest)")]
        [SerializeField] private NetworkObject seatB;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            StartCoroutine(AssignWhenSeatsReady());

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        private IEnumerator AssignWhenSeatsReady()
        {
            if (seatA == null || seatB == null)
            {
                Debug.LogError("[PlayerSpawnAssigner] seatA/seatB are not assigned in the inspector!");
                yield break;
            }

            while (!seatA.IsSpawned || !seatB.IsSpawned)
                yield return null;

            AssignExistingClients();
        }

        private void AssignExistingClients()
        {

            var ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            ids.Sort();

            if (ids.Count > 0) AssignSeat(seatA, ids[0]);
            if (ids.Count > 1) AssignSeat(seatB, ids[1]);
        }

        private void OnClientConnected(ulong clientId)
        {
            if (seatB == null || !seatB.IsSpawned) return;

            if (seatB.OwnerClientId == NetworkManager.ServerClientId && seatB.OwnerClientId != clientId)
                AssignSeat(seatB, clientId);
        }

        private void AssignSeat(NetworkObject seat, ulong clientId)
        {
            if (seat == null || !seat.IsSpawned)
            {
                Debug.LogWarning($"[PlayerSpawnAssigner] Tried to assign ownership to an unspawned object ({seat?.name}), skipping.");
                return;
            }

            if (seat.OwnerClientId == clientId) return;

            seat.ChangeOwnership(clientId);
            Debug.Log($"[PlayerSpawnAssigner] Client {clientId} was given character {seat.name}");
        }
    }
}
