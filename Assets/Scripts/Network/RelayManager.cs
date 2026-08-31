using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlindTiming.Game;

namespace BlindTiming.Network
{
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }

        [Tooltip("Max players in the room (this is a 1v1 duel -> 2)")]
        [SerializeField] private int maxConnections = 2;

        [Tooltip("Scene to load when the player exits to the main menu")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        public string LastJoinCode { get; private set; }

        public event Action<string> OnRelayCreated;
        public event Action OnJoinSuccess;
        public event Action<string> OnJoinFailed;

        public event Action OnConnectionLost;

        private bool _leavingIntentionally;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BlindTiming.UI.SettingsData.ApplyVolume();
        }

        private async void Start()
        {
            await EnsureSignedIn();
            StartCoroutine(SubscribeToDisconnectEvents());
        }

        private IEnumerator SubscribeToDisconnectEvents()
        {
            while (NetworkManager.Singleton == null)
                yield return null;

            NetworkManager.Singleton.OnClientDisconnectCallback += HandleOwnDisconnect;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleOwnDisconnect;
        }

        private void HandleOwnDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            if (_leavingIntentionally)
            {
                _leavingIntentionally = false;
                return;
            }

            Debug.Log("[RelayManager] Lost connection to the match (host closed the room, or the network dropped).");
            OnConnectionLost?.Invoke();
            GameEvents.RaiseConnectionLost();
        }

        public void LeaveToMainMenu()
        {
            _leavingIntentionally = true;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        private async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[RelayManager] Signed in as {AuthenticationService.Instance.PlayerId}");
        }

        public async void CreateRelay()
        {
            try
            {
                await EnsureSignedIn();

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections - 1);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                LastJoinCode = joinCode;

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

                NetworkManager.Singleton.StartHost();

                OnRelayCreated?.Invoke(joinCode);
                Debug.Log($"[RelayManager] Host started. Join code: {joinCode}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[RelayManager] CreateRelay failed: {e}");
                OnJoinFailed?.Invoke("Could not create the room: " + e.Message);
            }
        }

        public async void JoinRelay(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                OnJoinFailed?.Invoke("Enter the room code.");
                return;
            }

            try
            {
                await EnsureSignedIn();

                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

                NetworkManager.Singleton.StartClient();

                LastJoinCode = joinCode;
                OnJoinSuccess?.Invoke();
                Debug.Log("[RelayManager] Client joined via relay.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[RelayManager] JoinRelay failed: {e}");
                OnJoinFailed?.Invoke("Could not connect. Check the code.");
            }
        }

        public void Shutdown()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
    }
}
