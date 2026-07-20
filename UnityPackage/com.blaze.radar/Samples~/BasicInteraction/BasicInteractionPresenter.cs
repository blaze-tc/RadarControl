using UnityEngine;
using UnityEngine.UI;

namespace Blaze.Radar.Samples
{
    /// <summary>
    /// Presents the state of the authored UGUI sample scene.
    /// UI controls call these public methods through persistent UnityEvent listeners.
    /// </summary>
    public sealed class BasicInteractionPresenter : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RadarFrameDispatcher radarDispatcher;
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Text interactionStatusText;
        [SerializeField] private Toggle interactionToggle;
        [SerializeField] private Slider strengthSlider;

        private void OnEnable()
        {
            if (radarDispatcher != null)
            {
                radarDispatcher.ConnectionChanged += OnConnectionChanged;
            }

            OnConnectionChanged(radarDispatcher != null && radarDispatcher.IsConnected);
            RefreshControlSummary();
        }

        private void OnDisable()
        {
            if (radarDispatcher != null)
            {
                radarDispatcher.ConnectionChanged -= OnConnectionChanged;
            }
        }

        public void OnRadarButtonClicked()
        {
            SetInteractionStatus("Button received IPointerDown / IPointerUp / IPointerClick.");
        }

        public void OnToggleChanged(bool isOn)
        {
            SetInteractionStatus($"Toggle changed through UGUI: {(isOn ? "ON" : "OFF")}");
        }

        public void OnSliderChanged(float value)
        {
            SetInteractionStatus($"Slider value: {value:0.00}");
        }

        public void OnScrollChanged(Vector2 normalizedPosition)
        {
            SetInteractionStatus($"ScrollRect position: {normalizedPosition.y:0.00}");
        }

        private void OnConnectionChanged(bool connected)
        {
            if (connectionStatusText == null)
            {
                return;
            }

            connectionStatusText.text = connected ? "IPC  CONNECTED" : "IPC  WAITING";
            connectionStatusText.color = connected
                ? new Color32(82, 224, 179, 255)
                : new Color32(247, 184, 89, 255);
        }

        private void RefreshControlSummary()
        {
            if (interactionToggle == null || strengthSlider == null)
            {
                return;
            }

            SetInteractionStatus(
                $"Ready · Toggle {(interactionToggle.isOn ? "ON" : "OFF")} · Slider {strengthSlider.value:0.00}");
        }

        private void SetInteractionStatus(string message)
        {
            if (interactionStatusText != null)
            {
                interactionStatusText.text = message;
            }
        }
    }
}
