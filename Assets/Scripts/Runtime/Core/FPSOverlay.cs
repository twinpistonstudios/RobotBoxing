using UnityEngine;
using UnityEngine.UI;

namespace IronKnuckle.Core
{
    public class FPSOverlay : MonoBehaviour
    {
        private Canvas overlayCanvas;
        private Text fpsText;
        private float accum;
        private int frames;
        private float timer;

        public void SetVisible(bool visible)
        {
            if (visible && overlayCanvas == null) CreateOverlay();
            if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(visible);
        }

        private void CreateOverlay()
        {
            var go = new GameObject("FPSOverlayCanvas");
            go.transform.SetParent(transform, false);
            overlayCanvas = go.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 999;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var bgGo = new GameObject("Badge", typeof(RectTransform));
            bgGo.transform.SetParent(go.transform, false);
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(170f, 38f);
            rt.anchoredPosition = new Vector2(-14f, -10f);

            var img = bgGo.AddComponent<Image>();
            img.color = new Color(0.05f, 0.08f, 0.14f, 0.78f);
            img.raycastTarget = false;

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(bgGo.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            fpsText = txtGo.AddComponent<Text>();
            fpsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fpsText.fontSize = 20;
            fpsText.fontStyle = FontStyle.Bold;
            fpsText.alignment = TextAnchor.MiddleCenter;
            fpsText.color = new Color(0.3f, 1f, 0.6f);
            fpsText.raycastTarget = false;
        }

        private void Update()
        {
            if (overlayCanvas == null || !overlayCanvas.gameObject.activeSelf || fpsText == null) return;
            accum += Time.unscaledDeltaTime;
            frames++;
            timer += Time.unscaledDeltaTime;
            if (timer >= 0.4f)
            {
                int fps = Mathf.RoundToInt(frames / Mathf.Max(0.001f, accum));
                fpsText.text = $"{fps} FPS";
                fpsText.color = fps >= 50 ? new Color(0.3f, 1f, 0.55f) : (fps >= 30 ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.3f, 0.3f));
                accum = 0f;
                frames = 0;
                timer = 0f;
            }
        }
    }
}
