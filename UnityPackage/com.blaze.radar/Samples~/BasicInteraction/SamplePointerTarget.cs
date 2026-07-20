using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blaze.Radar.Samples
{
    /// <summary>
    /// Uses standard EventSystem interfaces for both PhysicsRaycaster and Physics2DRaycaster targets.
    /// </summary>
    public sealed class SamplePointerTarget : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [Header("Target")]
        [SerializeField] private Camera eventCamera;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Text interactionStatusText;

        [Header("Feedback")]
        [SerializeField] private Color normalColor = new Color32(56, 211, 214, 255);
        [SerializeField] private Color hoverColor = new Color32(142, 246, 232, 255);
        [SerializeField] private Color pressedColor = new Color32(247, 184, 89, 255);

        private Vector3 _baseScale;
        private float _screenDepth;
        private Material _runtimeMaterial;

        private void Awake()
        {
            _baseScale = transform.localScale;
            eventCamera = eventCamera != null ? eventCamera : Camera.main;
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                var unlitShader = Shader.Find("Unlit/Color");
                _runtimeMaterial = unlitShader != null
                    ? new Material(unlitShader)
                    : new Material(targetRenderer.sharedMaterial);
                targetRenderer.material = _runtimeMaterial;
            }

            _screenDepth = eventCamera != null
                ? Mathf.Abs(eventCamera.transform.position.z - transform.position.z)
                : 10f;
            SetColor(normalColor);
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = _baseScale * 1.08f;
            SetColor(hoverColor);
            Report($"{name}: Pointer Enter", eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _baseScale;
            SetColor(normalColor);
            Report($"{name}: Pointer Exit", eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SetColor(pressedColor);
            Report($"{name}: Pointer Down", eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetColor(hoverColor);
            Report($"{name}: Pointer Up", eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Report($"{name}: Pointer Click", eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Report($"{name}: Begin Drag", eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventCamera != null)
            {
                var screenPoint = new Vector3(eventData.position.x, eventData.position.y, _screenDepth);
                var worldPoint = eventCamera.ScreenToWorldPoint(screenPoint);
                transform.position = new Vector3(worldPoint.x, worldPoint.y, transform.position.z);
            }

            Report($"{name}: Drag", eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Report($"{name}: End Drag", eventData);
        }

        private void SetColor(Color color)
        {
            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.color = color;
            }
        }

        private void Report(string message, PointerEventData eventData)
        {
            if (interactionStatusText != null)
            {
                interactionStatusText.text = $"{message} · Pointer {eventData.pointerId}";
            }
        }
    }
}
