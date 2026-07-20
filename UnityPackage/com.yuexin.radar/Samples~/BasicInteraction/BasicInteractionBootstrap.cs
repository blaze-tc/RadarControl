using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yuexin.Radar.Unity;

namespace Yuexin.Radar.Samples
{
    public sealed class BasicInteractionBootstrap : MonoBehaviour
    {
        private DefaultControls.Resources _resources;

        private void Awake()
        {
            CreateCameraAndWorldTargets();
            var dispatcher = CreateRadarRuntime();
            var canvas = CreateCanvas();
            CreateUserInterface(canvas.transform);
            ConfigureEventSystem(dispatcher);
        }

        private static Camera CreateCameraAndWorldTargets()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(PhysicsRaycaster), typeof(Physics2DRaycaster));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.065f, 0.10f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "3D Cube Target";
            cube.transform.position = new Vector3(2.5f, -1.2f, 0f);
            cube.transform.localScale = Vector3.one * 1.4f;
            cube.GetComponent<Renderer>().material.color = new Color(0.98f, 0.58f, 0.24f);
            cube.AddComponent<SamplePointerReceiver>();

            var twoDimensional = new GameObject("2D Collider Target", typeof(SpriteRenderer), typeof(BoxCollider2D));
            twoDimensional.transform.position = new Vector3(-2.5f, -1.2f, 0f);
            twoDimensional.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(0.22f, 0.83f, 0.84f));
            texture.Apply();
            twoDimensional.GetComponent<SpriteRenderer>().sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f);
            twoDimensional.AddComponent<SamplePointerReceiver>();
            return camera;
        }

        private static RadarFrameDispatcher CreateRadarRuntime()
        {
            var root = new GameObject("Yuexin Radar Runtime");
            root.AddComponent<RadarBridgeLauncher>();
            var dispatcher = root.AddComponent<RadarFrameDispatcher>();
            root.AddComponent<RadarDebugOverlay>();
            return dispatcher;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private void CreateUserInterface(Transform parent)
        {
            var title = DefaultControls.CreateText(_resources);
            title.name = "Instructions";
            title.transform.SetParent(parent, false);
            var titleText = title.GetComponent<Text>();
            titleText.text = "YUEXIN RADAR SDK  ·  Button / Toggle / Slider / ScrollRect / 3D / 2D  ·  Mouse debug is enabled";
            titleText.fontSize = 26;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.78f, 0.94f, 0.97f);
            SetRect(title.GetComponent<RectTransform>(), new Vector2(0.5f, 0.92f), new Vector2(1300f, 70f));

            var button = DefaultControls.CreateButton(_resources);
            button.name = "Radar Button";
            button.transform.SetParent(parent, false);
            button.GetComponentInChildren<Text>().text = "RADAR BUTTON";
            button.GetComponent<Button>().onClick.AddListener(() => Debug.Log("Radar Button clicked"));
            SetRect(button.GetComponent<RectTransform>(), new Vector2(0.25f, 0.7f), new Vector2(260f, 90f));

            var toggle = DefaultControls.CreateToggle(_resources);
            toggle.name = "Radar Toggle";
            toggle.transform.SetParent(parent, false);
            toggle.GetComponentInChildren<Text>().text = "Radar Toggle";
            SetRect(toggle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.7f), new Vector2(260f, 70f));

            var slider = DefaultControls.CreateSlider(_resources);
            slider.name = "Radar Slider";
            slider.transform.SetParent(parent, false);
            SetRect(slider.GetComponent<RectTransform>(), new Vector2(0.75f, 0.7f), new Vector2(320f, 70f));

            var scrollView = DefaultControls.CreateScrollView(_resources);
            scrollView.name = "Radar ScrollRect";
            scrollView.transform.SetParent(parent, false);
            SetRect(scrollView.GetComponent<RectTransform>(), new Vector2(0.5f, 0.42f), new Vector2(560f, 210f));
            var content = scrollView.GetComponent<ScrollRect>().content;
            var contentTextObject = DefaultControls.CreateText(_resources);
            contentTextObject.transform.SetParent(content, false);
            var contentText = contentTextObject.GetComponent<Text>();
            contentText.text = "Drag or scroll this area\n\nRadarInputModule emits BeginDrag, Drag, EndDrag and Drop.\n\nMultiple Radar PointerIds keep independent press and drag state.\n\nBridge coordinates use a bottom-left origin.";
            contentText.fontSize = 22;
            contentText.alignment = TextAnchor.UpperLeft;
            contentText.color = new Color(0.12f, 0.18f, 0.24f);
            var contentRect = contentTextObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 500f);
            contentRect.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 500f);
        }

        private static void ConfigureEventSystem(RadarFrameDispatcher dispatcher)
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(RadarInputModule));
            var inputModule = eventSystemObject.GetComponent<RadarInputModule>();
            inputModule.Dispatcher = dispatcher;
            inputModule.InputMode = RadarInputMode.RadarAndMouseDebug;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }

    public sealed class SamplePointerReceiver : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = _baseScale * 1.12f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _baseScale;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"{name} clicked by pointer {eventData.pointerId}");
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            var camera = Camera.main;
            if (camera != null)
            {
                var world = camera.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 10f));
                transform.position = new Vector3(world.x, world.y, transform.position.z);
            }
        }

        public void OnEndDrag(PointerEventData eventData) { }
    }
}
