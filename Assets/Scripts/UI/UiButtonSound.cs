using UnityEngine;
using UnityEngine.UI;

namespace BlindTiming.UI
{
    [RequireComponent(typeof(Button))]
    public class UiButtonSound : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => UiSfx.Instance?.PlayUiClick());
        }
    }
}
