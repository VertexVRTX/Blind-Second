using UnityEngine;

namespace BlindTiming.UI
{
    public class CursorLockController : MonoBehaviour
    {
        public static CursorLockController Instance { get; private set; }

        private void Awake() => Instance = this;

        private void Start()
        {
            GameplayInputLock.Locked = false;
            LockCursor();
        }

        private void OnDestroy()
        {

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Instance == this) Instance = null;
        }

        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
