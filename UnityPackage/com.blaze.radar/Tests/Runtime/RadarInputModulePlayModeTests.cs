using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Blaze.Radar.Tests
{
    public sealed class RadarInputModulePlayModeTests
    {
        private GameObject _eventSystemObject;
        private GameObject _canvasObject;
        private RadarInputModule _module;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(RadarInputModule));
            _module = _eventSystemObject.GetComponent<RadarInputModule>();
            _module.InputMode = RadarInputMode.RadarOnly;

            _canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_canvasObject);
            Object.Destroy(_eventSystemObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DownAndUp_ClicksButton()
        {
            var clicks = 0;
            var button = CreateButton("Center", new Vector2(0.5f, 0.5f));
            button.onClick.AddListener(() => clicks++);
            yield return null;

            ProcessFrame(Pointer(1, 0.5f, 0.5f, RadarPointerPhase.Down));
            ProcessFrame(Pointer(1, 0.5f, 0.5f, RadarPointerPhase.Up));

            Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TwoPointers_ClickIndependentButtons()
        {
            var leftClicks = 0;
            var rightClicks = 0;
            CreateButton("Left", new Vector2(0.25f, 0.5f)).onClick.AddListener(() => leftClicks++);
            CreateButton("Right", new Vector2(0.75f, 0.5f)).onClick.AddListener(() => rightClicks++);
            yield return null;

            ProcessFrame(
                Pointer(10, 0.25f, 0.5f, RadarPointerPhase.Down),
                Pointer(20, 0.75f, 0.5f, RadarPointerPhase.Down));
            ProcessFrame(
                Pointer(10, 0.25f, 0.5f, RadarPointerPhase.Up),
                Pointer(20, 0.75f, 0.5f, RadarPointerPhase.Up));

            Assert.That(leftClicks, Is.EqualTo(1));
            Assert.That(rightClicks, Is.EqualTo(1));
        }

        private Button CreateButton(string name, Vector2 normalizedPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(_canvasObject.transform, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = normalizedPosition;
            rect.anchorMax = normalizedPosition;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 90f);
            return gameObject.GetComponent<Button>();
        }

        private void ProcessFrame(params RadarPointerMessage[] pointers)
        {
            var frame = new RadarPointerFrameMessage();
            frame.pointers.AddRange(pointers);
            _module.InjectFrame(frame);
            _module.Process();
        }

        private static RadarPointerMessage Pointer(int id, float x, float y, RadarPointerPhase phase)
        {
            return new RadarPointerMessage
            {
                pointerId = id,
                normalizedX = x,
                normalizedY = y,
                phase = phase,
                confidence = 1f
            };
        }
    }
}
