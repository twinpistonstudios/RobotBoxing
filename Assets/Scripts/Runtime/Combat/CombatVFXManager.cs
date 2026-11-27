using System.Collections;
using System.Collections.Generic;
using IronKnuckle.Data;
using UnityEngine;
using UnityEngine.UI;

namespace IronKnuckle.Combat
{
    public class CombatVFXManager : MonoBehaviour
    {
        public Canvas worldPopupCanvas;
        public Sprite sparkSprite;

        private readonly List<ParticleSystem> sparkPool = new List<ParticleSystem>();
        private int sparkIdx;
        private Font runtimeFont;
        private Coroutine hitstopRoutine;

        private void Awake()
        {
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < 6; i++)
            {
                sparkPool.Add(CreateSparkBurst("SparkBurst_" + i));
            }
        }

        private ParticleSystem CreateSparkBurst(string goName)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 24;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            rend.material = new Material(shader);
            return ps;
        }

        public void SpawnImpactSparks(Vector3 worldPos, Color color, int count = 16)
        {
            if (sparkPool.Count == 0) return;
            var ps = sparkPool[sparkIdx];
            sparkIdx = (sparkIdx + 1) % sparkPool.Count;
            ps.transform.position = worldPos;
            var main = ps.main;
            main.startColor = color;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ps.Play();
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float scale = 1f)
        {
            if (worldPopupCanvas == null || Camera.main == null) return;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 0.25f);
            if (screenPos.z < 0f) return;

            var go = new GameObject("Popup_" + text);
            go.transform.SetParent(worldPopupCanvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.position = screenPos;
            rt.sizeDelta = new Vector2(340f, 90f);
            rt.localScale = Vector3.one * scale;

            var txt = go.AddComponent<Text>();
            txt.font = runtimeFont;
            txt.text = text;
            txt.fontSize = 38;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.05f, 0.08f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);

            StartCoroutine(AnimatePopup(rt, txt));
        }

        private IEnumerator AnimatePopup(RectTransform rt, Text txt)
        {
            float elapsed = 0f;
            float dur = 0.72f;
            Vector3 startPos = rt.position;
            Vector3 startScale = rt.localScale;
            while (elapsed < dur && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = elapsed / dur;
                rt.position = startPos + new Vector3(0f, u * 75f, 0f);
                float pop = u < 0.2f ? Mathf.Lerp(1.35f, 1.0f, u / 0.2f) : 1.0f;
                rt.localScale = startScale * pop;
                Color c = txt.color;
                c.a = u > 0.6f ? Mathf.Lerp(1f, 0f, (u - 0.6f) / 0.4f) : 1f;
                txt.color = c;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        public void TriggerHitstop(float duration, float timeScale = 0.08f)
        {
            if (hitstopRoutine != null) StopCoroutine(hitstopRoutine);
            hitstopRoutine = StartCoroutine(HitstopCoroutine(duration, timeScale));
        }

        private IEnumerator HitstopCoroutine(float duration, float scale)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(duration);
            if (Time.timeScale < 0.95f) Time.timeScale = 1f;
            hitstopRoutine = null;
        }
    }
}
