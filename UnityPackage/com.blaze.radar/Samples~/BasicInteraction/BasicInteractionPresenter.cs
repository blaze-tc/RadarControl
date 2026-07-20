using UnityEngine;
using UnityEngine.UI;

namespace Blaze.Radar.Samples
{
    /// <summary>
    /// Converts authored UGUI UnityEvents into readable sample state changes.
    /// </summary>
    public sealed class BasicInteractionPresenter : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RadarDemoLogger demoLogger;
        [SerializeField] private Toggle interactionToggle;
        [SerializeField] private Slider strengthSlider;

        private void OnEnable()
        {
            RefreshControlSummary();
        }

        public void OnRadarButtonClicked()
        {
            demoLogger?.LogUiEvent("Button Click", "Radar Button completed its UnityEvent callback");
        }

        public void OnToggleChanged(bool isOn)
        {
            demoLogger?.LogUiEvent("Toggle Changed", isOn ? "interaction layer ON" : "interaction layer OFF");
        }

        public void OnSliderChanged(float value)
        {
            demoLogger?.LogContinuousUiEvent("Slider Changed", $"value {value:0.000}");
        }

        public void OnScrollChanged(Vector2 normalizedPosition)
        {
            if (demoLogger == null || demoLogger.IsAutoScrolling)
            {
                return;
            }

            demoLogger.LogContinuousUiEvent(
                "Log ScrollRect",
                $"normalized ({normalizedPosition.x:0.000}, {normalizedPosition.y:0.000})");
        }

        private void RefreshControlSummary()
        {
            if (interactionToggle == null || strengthSlider == null)
            {
                return;
            }

            demoLogger?.ShowInteractionStatus(
                $"Ready | Toggle {(interactionToggle.isOn ? "ON" : "OFF")} | Slider {strengthSlider.value:0.00}");
        }
    }
}
