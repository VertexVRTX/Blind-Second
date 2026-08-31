using Unity.Netcode;
using UnityEngine;

namespace BlindTiming.UI
{
    public static class AppQuitter
    {
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public static void QuitFromMatch()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            Quit();
        }
    }
}
