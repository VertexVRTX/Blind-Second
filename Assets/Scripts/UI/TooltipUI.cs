using TMPro;
using UnityEngine;

namespace BlindTiming.UI
{
    public class TooltipUI : MonoBehaviour
    {
        public static TooltipUI Instance { get; private set; }

        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text text;
        [SerializeField] private Vector2 offset = new Vector2(16f, -16f);

        private RectTransform _canvasRect;
        private Canvas _canvas;

        private void Awake()
        {
            if (panel == null) Debug.LogError("[TooltipUI] panel is not assigned!", this);
            if (text == null) Debug.LogError("[TooltipUI] text is not assigned!", this);

            Instance = this;
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.GetComponent<RectTransform>() : null;
            if (_canvasRect == null)
                Debug.LogWarning("[TooltipUI] No parent Canvas found - make sure this object is a child of a Canvas.", this);

            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(string message, Vector2 screenPosition)
        {
            if (panel == null || text == null) return;

            text.text = message;
            panel.gameObject.SetActive(true);
            SetPosition(screenPosition);
        }

        public void UpdatePosition(Vector2 screenPosition)
        {
            if (panel != null && panel.gameObject.activeSelf)
                SetPosition(screenPosition);
        }

        private void SetPosition(Vector2 screenPosition)
        {
            if (_canvasRect == null)
            {
                panel.position = screenPosition + offset;
                return;
            }

            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera
                : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPosition, cam, out Vector2 localPoint);
            panel.anchoredPosition = localPoint + offset;
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }
    }
}
