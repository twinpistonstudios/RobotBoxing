using IronKnuckle.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IronKnuckle.Core
{
    public class GraphicsSettingsManager : MonoBehaviour
    {
        public static GraphicsSettingsManager Instance { get; private set; }
        private static readonly int DisableOutlineId = Shader.PropertyToID("_DisableOutline");

        public FPSOverlay FpsOverlay { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            FpsOverlay = gameObject.AddComponent<FPSOverlay>();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.SaveData != null)
            {
                ApplySettings(gm.SaveData);
            }
        }

        public static void ApplyPresetToData(PlayerSaveData save, int presetIndex)
        {
            if (save == null) return;
            save.graphicsQuality = Mathf.Clamp(presetIndex, 0, 3);
            switch (save.graphicsQuality)
            {
                case 0: // Low (Battery Saver)
                    save.renderScale = 0.70f;
                    save.postProcessingEnabled = false;
                    save.bloomEnabled = false;
                    save.outlinesEnabled = true;
                    save.shadowsEnabled = false;
                    save.targetFps = 30;
                    break;
                case 1: // Medium
                    save.renderScale = 0.85f;
                    save.postProcessingEnabled = true;
                    save.bloomEnabled = false;
                    save.outlinesEnabled = true;
                    save.shadowsEnabled = true;
                    save.targetFps = 60;
                    break;
                case 2: // High
                    save.renderScale = 1.0f;
                    save.postProcessingEnabled = true;
                    save.bloomEnabled = true;
                    save.outlinesEnabled = true;
                    save.shadowsEnabled = true;
                    save.targetFps = 60;
                    break;
                case 3: // Ultra
                    save.renderScale = 1.0f;
                    save.postProcessingEnabled = true;
                    save.bloomEnabled = true;
                    save.outlinesEnabled = true;
                    save.shadowsEnabled = true;
                    save.targetFps = 120;
                    break;
            }
        }

        public void ApplySettings(PlayerSaveData save)
        {
            if (save == null) return;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Clamp(save.targetFps, 30, 120);

            // Global toon outline toggle (0 = enabled, 1 = disabled)
            Shader.SetGlobalFloat(DisableOutlineId, save.outlinesEnabled ? 0f : 1f);

            // URP Pipeline adjustments
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                urpAsset.renderScale = Mathf.Clamp(save.renderScale, 0.55f, 1.0f);
                urpAsset.msaaSampleCount = save.graphicsQuality >= 3 ? 4 : (save.graphicsQuality >= 2 ? 2 : 1);
                urpAsset.shadowDistance = save.shadowsEnabled ? (save.graphicsQuality >= 2 ? 32f : 18f) : 0f;
            }

            // Scene camera post-processing
            var cam = Camera.main;
            if (cam != null)
            {
                var ucd = cam.GetUniversalAdditionalCameraData();
                if (ucd != null) ucd.renderPostProcessing = save.postProcessingEnabled;
            }

            // Scene Volume Bloom override
            var volumes = Object.FindObjectsByType<Volume>();
            foreach (var vol in volumes)
            {
                if (vol != null && vol.sharedProfile != null && vol.sharedProfile.TryGet<Bloom>(out var bloom))
                {
                    bloom.active = save.postProcessingEnabled && save.bloomEnabled;
                }
            }

            // Main directional light shadows
            var lights = Object.FindObjectsByType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.name.Contains("Key"))
                {
                    l.shadows = save.shadowsEnabled ? LightShadows.Soft : LightShadows.None;
                }
            }

            if (FpsOverlay != null)
            {
                FpsOverlay.SetVisible(save.showFpsCounter);
            }
        }
    }
}
