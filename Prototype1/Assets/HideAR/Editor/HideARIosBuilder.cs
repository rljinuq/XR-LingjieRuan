#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.Management;

namespace HideAR.Editor
{
    public static class HideARIosBuilder
    {
        const string ScenePath = "Assets/START_HERE_HideAR_AR.unity";
        const string OutputPath = "/Users/lingjieruan/Desktop/XR-LingjieRuan/Builds/HideAR_IP1_iOS";
        const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("HideAR/Build iOS Xcode Project")]
        public static void BuildIosProject()
        {
            Directory.CreateDirectory(OutputPath);

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            HideARARSceneBuilder.BuildScene();
            EnsureIosArKitLoader();
            EnsureUrpArBackgroundRendererFeature();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.lingjieruan.hidear.ip1");
            PlayerSettings.iOS.cameraUsageDescription = "HideAR uses the camera to place and find virtual objects on real-world surfaces.";
            PlayerSettings.iOS.appleDeveloperTeamID = "8RNTC2G3F8";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            BuildPlayerOptions options = new()
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"HideAR iOS build result: {summary.result}, output: {OutputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"HideAR iOS build failed: {summary.result}");
            }

            VerifyIosExport();
        }

        static void EnsureIosArKitLoader()
        {
            XRGeneralSettingsPerBuildTarget buildTargetSettings =
                AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");

            if (buildTargetSettings == null)
            {
                throw new System.Exception("Missing XRGeneralSettingsPerBuildTarget.asset. Open Project Settings > XR Plug-in Management once, then build again.");
            }

            if (!buildTargetSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.iOS))
            {
                buildTargetSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.iOS);
            }

            XRManagerSettings managerSettings = buildTargetSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.iOS);
            bool hasArKit = managerSettings.activeLoaders.Any(loader => loader is ARKitLoader);
            if (!hasArKit)
            {
                bool assigned = XRPackageMetadataStore.AssignLoader(managerSettings, "UnityEngine.XR.ARKit.ARKitLoader", BuildTargetGroup.iOS);
                if (!assigned)
                {
                    throw new System.Exception("Could not assign ARKit loader for iOS. Check Project Settings > XR Plug-in Management > iOS > ARKit.");
                }
            }

            managerSettings.automaticLoading = true;
            managerSettings.automaticRunning = true;
            EnsureScriptingDefine(BuildTargetGroup.iOS, "UNITY_XR_ARKIT_LOADER_ENABLED");
            EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(buildTargetSettings);
            AssetDatabase.SaveAssets();
        }

        static void EnsureUrpArBackgroundRendererFeature()
        {
            VerifyRendererHasArBackgroundFeature(MobileRendererPath);
            VerifyRendererHasArBackgroundFeature(PcRendererPath);
        }

        static void VerifyRendererHasArBackgroundFeature(string rendererPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(rendererPath);
            bool hasFeature = assets.Any(asset => asset != null && asset.GetType().FullName == "UnityEngine.XR.ARFoundation.ARBackgroundRendererFeature");
            if (!hasFeature)
            {
                throw new System.Exception($"{rendererPath} is missing ARBackgroundRendererFeature. The iPhone build would show a flat color instead of the camera feed.");
            }
        }

        static void VerifyIosExport()
        {
            string projectFile = Path.Combine(OutputPath, "Unity-iPhone.xcodeproj/project.pbxproj");
            string arkitLibrary = Path.Combine(OutputPath, "Libraries/com.unity.xr.arkit/Runtime/iOS/Xcode2600/libUnityARKit.a");
            string arkitBridge = Path.Combine(OutputPath, "Libraries/com.unity.xr.arkit/Runtime/iOS/UnityARKit.m");

            if (!File.Exists(arkitLibrary))
            {
                throw new System.Exception("iOS export is missing libUnityARKit.a. Do not test this build on phone.");
            }

            if (!File.Exists(arkitBridge))
            {
                throw new System.Exception("iOS export is missing UnityARKit.m. Do not test this build on phone.");
            }

            if (!File.Exists(projectFile))
            {
                throw new System.Exception("iOS export is missing Unity-iPhone.xcodeproj/project.pbxproj.");
            }

            string project = File.ReadAllText(projectFile);
            string[] requiredProjectReferences =
            {
                "ARKit.framework",
                "AVFoundation.framework",
                "libUnityARKit.a",
                "UnityARKit.m"
            };

            foreach (string requiredReference in requiredProjectReferences)
            {
                if (!project.Contains(requiredReference))
                {
                    throw new System.Exception($"iOS export does not reference {requiredReference}. Do not test this build on phone.");
                }
            }

            Debug.Log("HideAR iOS export verification passed: ARKit native library, bridge, and frameworks are present.");
        }

        static void EnsureScriptingDefine(BuildTargetGroup buildTargetGroup, string define)
        {
            string existing = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
            string[] defines = existing
                .Split(';')
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (defines.Contains(define))
            {
                return;
            }

            string updated = string.IsNullOrWhiteSpace(existing) ? define : $"{existing};{define}";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, updated);
            Debug.Log($"HideAR iOS build added scripting define: {define}");
        }
    }
}
#endif
