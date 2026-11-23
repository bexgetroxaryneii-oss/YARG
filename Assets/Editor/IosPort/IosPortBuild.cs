using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace YARG.Editor.IosPort
{
    /// <summary>
    /// YARG iOS port: the build the cloud workflow runs
    /// (-executeMethod YARG.Editor.IosPort.IosPortBuild.Build).
    /// Exports the Xcode project to build/iOS/YARG-iOS.
    /// Made by Claude Strife - X.
    /// </summary>
    public static class IosPortBuild
    {
        public const string BUNDLE_ID = "com.claudestrife.yargmobile";
        public const string OUTPUT = "build/iOS/YARG-iOS";

        public static void Build()
        {
            var target = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(target, BUNDLE_ID);
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.productName = "YARG Mobile";

            // A rhythm game is played sideways; never leave a pixel of the screen unused.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.iOS.renderExtraFrameOnPause = false;

            string version = Environment.GetEnvironmentVariable("YARG_IOS_BUILD_NUMBER");
            if (!string.IsNullOrEmpty(version))
            {
                PlayerSettings.iOS.buildNumber = version;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OUTPUT,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[iOS port] Build {report.summary.result}: {report.summary.totalErrors} errors, " +
                $"{report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}
