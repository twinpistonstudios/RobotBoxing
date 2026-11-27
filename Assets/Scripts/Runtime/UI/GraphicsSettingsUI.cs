using IronKnuckle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace IronKnuckle.UI
{
    public class GraphicsSettingsUI : MonoBehaviour
    {
        public Button[] qualityButtons; // 0=Low, 1=Med, 2=High, 3=Ultra
        public Slider renderScaleSlider;
        public Text renderScaleLabel;
        public Button[] fpsButtons; // 30, 60, 120

        public Button postFxToggleBtn;
        public Button bloomToggleBtn;
        public Button outlinesToggleBtn;
        public Button shadowsToggleBtn;
        public Button fpsCounterToggleBtn;

        public Slider sfxSlider;
        public Slider bgmSlider;
        public Button resetSaveButton;
        public Button closeButton;

        public System.Action onSaveReset;

        private static readonly int[] FpsValues = { 30, 60, 120 };

        private void Awake()
        {
            if (qualityButtons != null)
            {
                for (int i = 0; i < qualityButtons.Length; i++)
                {
                    int idx = i;
                    qualityButtons[i]?.onClick.AddListener(() => SelectQualityPreset(idx));
                }
            }

            if (fpsButtons != null)
            {
                for (int i = 0; i < fpsButtons.Length; i++)
                {
                    int fps = FpsValues[Mathf.Clamp(i, 0, FpsValues.Length - 1)];
                    fpsButtons[i]?.onClick.AddListener(() => SetTargetFps(fps));
                }
            }

            renderScaleSlider?.onValueChanged.AddListener(OnRenderScaleChanged);
            postFxToggleBtn?.onClick.AddListener(() => ToggleBool("postfx"));
            bloomToggleBtn?.onClick.AddListener(() => ToggleBool("bloom"));
            outlinesToggleBtn?.onClick.AddListener(() => ToggleBool("outlines"));
            shadowsToggleBtn?.onClick.AddListener(() => ToggleBool("shadows"));
            fpsCounterToggleBtn?.onClick.AddListener(() => ToggleBool("fps"));

            sfxSlider?.onValueChanged.AddListener(OnVolumeChanged);
            bgmSlider?.onValueChanged.AddListener(OnVolumeChanged);

            resetSaveButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                GameManager.Instance?.ResetSaveData();
                onSaveReset?.Invoke();
                RefreshUI();
            });

            closeButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                gameObject.SetActive(false);
            });
        }

        public void Open()
        {
            gameObject.SetActive(true);
            RefreshUI();
        }

        public void RefreshUI()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.SaveData == null) return;
            var s = gm.SaveData;

            if (qualityButtons != null)
            {
                for (int i = 0; i < qualityButtons.Length; i++)
                {
                    var img = qualityButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (i == s.graphicsQuality) ? UIThemeHelper.AccentGold : UIThemeHelper.MutedSlate;
                }
            }

            if (fpsButtons != null)
            {
                for (int i = 0; i < fpsButtons.Length; i++)
                {
                    var img = fpsButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (FpsValues[i] == s.targetFps) ? UIThemeHelper.PrimaryCyan : UIThemeHelper.MutedSlate;
                }
            }

            if (renderScaleSlider != null) renderScaleSlider.SetValueWithoutNotify(s.renderScale);
            if (renderScaleLabel != null) renderScaleLabel.text = $"RENDER SCALE: {Mathf.RoundToInt(s.renderScale * 100f)}%";

            StyleToggle(postFxToggleBtn, "POST-FX", s.postProcessingEnabled);
            StyleToggle(bloomToggleBtn, "BLOOM", s.bloomEnabled);
            StyleToggle(outlinesToggleBtn, "OUTLINES", s.outlinesEnabled);
            StyleToggle(shadowsToggleBtn, "SHADOWS", s.shadowsEnabled);
            StyleToggle(fpsCounterToggleBtn, "FPS HUD", s.showFpsCounter);

            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(s.sfxVolume);
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(s.bgmVolume);
        }

        private void StyleToggle(Button btn, string prefix, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? UIThemeHelper.AccentGreen : UIThemeHelper.MutedSlate;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = $"{prefix}: {(active ? "ON" : "OFF")}";
        }

        private void SelectQualityPreset(int idx)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            AudioManager.Instance?.PlaySFX("sfx_ui_click");
            GraphicsSettingsManager.ApplyPresetToData(gm.SaveData, idx);
            ApplyAndSave();
        }

        private void SetTargetFps(int fps)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            AudioManager.Instance?.PlaySFX("sfx_ui_click");
            gm.SaveData.targetFps = fps;
            ApplyAndSave();
        }

        private void OnRenderScaleChanged(float val)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.SaveData.renderScale = Mathf.Clamp(val, 0.55f, 1.0f);
            ApplyAndSave();
        }

        private void ToggleBool(string key)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            AudioManager.Instance?.PlaySFX("sfx_ui_click");
            var s = gm.SaveData;
            switch (key)
            {
                case "postfx": s.postProcessingEnabled = !s.postProcessingEnabled; break;
                case "bloom": s.bloomEnabled = !s.bloomEnabled; break;
                case "outlines": s.outlinesEnabled = !s.outlinesEnabled; break;
                case "shadows": s.shadowsEnabled = !s.shadowsEnabled; break;
                case "fps": s.showFpsCounter = !s.showFpsCounter; break;
            }
            ApplyAndSave();
        }

        private void OnVolumeChanged(float _)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (sfxSlider != null) gm.SaveData.sfxVolume = sfxSlider.value;
            if (bgmSlider != null) gm.SaveData.bgmVolume = bgmSlider.value;
            AudioManager.Instance?.SetVolumes(gm.SaveData.sfxVolume, gm.SaveData.bgmVolume);
            gm.PersistSave();
        }

        private void ApplyAndSave()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            GraphicsSettingsManager.Instance?.ApplySettings(gm.SaveData);
            gm.PersistSave();
            RefreshUI();
        }
    }
}
