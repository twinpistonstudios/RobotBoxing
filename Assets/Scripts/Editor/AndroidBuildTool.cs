using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IronKnuckle.Editor
{
    public static class AndroidBuildTool
    {
        private static readonly string ProjectBuildsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
        private static readonly string ApkOutputPath = Path.Combine(ProjectBuildsDir, "IronKnuckleBotBoxing.apk");
        private static readonly string DesktopApkPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "IronKnuckleBotBoxing.apk");

        [MenuItem("Tools/Iron Knuckle/Build Android APK")]
        public static void BuildAPKMenu()
        {
            BuildAPKInternal();
        }

        public static void BuildAPKBatch()
        {
            try
            {
                bool ok = BuildAPKInternal();
                if (!ok)
                {
                    EditorApplication.Exit(1);
                    return;
                }
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[AndroidBuildTool] APK build exception: " + ex);
                EditorApplication.Exit(1);
            }
        }

        public static bool BuildAPKInternal()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[AndroidBuildTool] Android Build Support is not installed in this Unity Editor.");
                return false;
            }

            if (!Directory.Exists(ProjectBuildsDir))
            {
                Directory.CreateDirectory(ProjectBuildsDir);
            }

            // Ensure scenes are built
            if (!File.Exists("Assets/Scenes/MainMenu.unity"))
            {
                RobotBoxingProjectBuilder.BuildAll();
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;

            var androidTarget = NamedBuildTarget.Android;
            PlayerSettings.companyName = "IronKnuckleStudio";
            PlayerSettings.productName = "Iron Knuckle: Bot Boxing";
            PlayerSettings.SetApplicationIdentifier(androidTarget, "com.ironknucklestudio.botboxing");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Use IL2CPP ARM64 if NDK present, otherwise Mono ARMv7 fallback
            string androidPlayerDir = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
            bool hasNdk = Directory.Exists(Path.Combine(androidPlayerDir, "NDK"));
            if (hasNdk)
            {
                PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }
            else
            {
                PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.Mono2x);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            }

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            string[] scenes =
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Garage.unity",
                "Assets/Scenes/Arena.unity"
            };

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkOutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log("[AndroidBuildTool] Starting Android APK build -> " + ApkOutputPath);
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[AndroidBuildTool] APK Build failed with result={summary.result}, errors={summary.totalErrors}");
                return false;
            }

            Debug.Log($"[AndroidBuildTool] APK Build SUCCEEDED! Size={summary.totalSize} bytes at {ApkOutputPath}");

            try
            {
                if (!string.IsNullOrEmpty(DesktopApkPath))
                {
                    File.Copy(ApkOutputPath, DesktopApkPath, true);
                    Debug.Log("[AndroidBuildTool] Copied APK to Desktop: " + DesktopApkPath);
                }
            }
            catch (Exception copyEx)
            {
                Debug.LogWarning("[AndroidBuildTool] Could not copy APK to Desktop: " + copyEx.Message);
            }

            return true;
        }
    }
}
