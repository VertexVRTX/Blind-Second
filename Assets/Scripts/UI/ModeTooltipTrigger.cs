using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlindTiming.UI
{
    public class ModeTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [TextArea(2, 5)]
        [SerializeField] private string description;

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(description))
                Debug.LogWarning($"[ModeTooltipTrigger] description is empty on {gameObject.name}.", this);

            var graphic = GetComponent<Graphic>();
            if (graphic == null)
                Debug.LogError($"[ModeTooltipTrigger] No Image/Graphic component on {gameObject.name} - " +
                    "pointer events can't be detected without one. Add an Image component (it can be fully transparent).", this);
            else if (!graphic.raycastTarget)
                Debug.LogError($"[ModeTooltipTrigger] \"Raycast Target\" is OFF on {gameObject.name}'s Image - " +
                    "enable it, otherwise hover events never reach this object.", this);

            if (FindObjectOfType<EventSystem>() == null)
                Debug.LogError("[ModeTooltipTrigger] No EventSystem found in the scene - UI hover/click events " +
                    "don't work at all without one. GameObject -> UI -> Event System.");

            if (TooltipUI.Instance == null)
                Debug.LogError("[ModeTooltipTrigger] TooltipUI.Instance is null - make sure a TooltipUI component " +
                    "exists in this scene and its GameObject is active.", this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"[ModeTooltipTrigger] Pointer entered {gameObject.name}.");

            if (TooltipUI.Instance == null)
            {
                Debug.LogError("[ModeTooltipTrigger] TooltipUI.Instance is null, can't show the tooltip.", this);
                return;
            }

            TooltipUI.Instance.Show(description, eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            TooltipUI.Instance?.UpdatePosition(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }
    }
}
