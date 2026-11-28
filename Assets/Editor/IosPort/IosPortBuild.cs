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

            // Same text-formatting support the desktop builds compile with.
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            foreach (string define in new[] { "ENABLE_JSON_CATALOG", "ZSTRING_TEXTMESHPRO_SUPPORT" })
            {
                if (!defines.Split(';').Contains(define))
                {
                    defines = string.IsNullOrEmpty(defines) ? define : defines + ";" + define;
                }
            }
            PlayerSettings.SetScriptingDefineSymbols(target, defines);

            // A rhythm game is played sideways; never leave a pixel of the screen unused.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            string version = Environment.GetEnvironmentVariable("YARG_IOS_BUILD_NUMBER");
            if (!string.IsNullOrEmpty(version))
            {
                PlayerSettings.iOS.buildNumber = version;
            }

            // Shown next to the version in the menus, so the installed build is easy to identify.
            string sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
            string stamp = string.IsNullOrEmpty(version) ? "local" : version;
            if (!string.IsNullOrEmpty(sha) && sha.Length >= 7)
            {
                stamp += " (" + sha.Substring(0, 7) + ")";
            }
            System.IO.Directory.CreateDirectory("Assets/StreamingAssets");
            System.IO.File.WriteAllText("Assets/StreamingAssets/ios-build.txt", stamp);
            AssetDatabase.Refresh();

            ExcludeDesktopNativePlugins();

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

        /// <summary>
        /// Many desktop native plugins (Linux .so, macOS .dylib/.bundle, Windows .dll/.lib) are
        /// imported with "Any Platform" on and nothing excluding iOS, so Unity copies them into
        /// the Xcode project and Xcode tries to link the .dylibs (-lVLCUnityPlugin and so on).
        /// Turn iOS off for every native plugin that does not live in an "iOS" folder.
        /// Managed assemblies are left alone; IL2CPP needs them.
        /// </summary>
        private static void ExcludeDesktopNativePlugins()
        {
            int changed = 0;
            foreach (var importer in PluginImporter.GetAllImporters())
            {
                string path = importer.assetPath;
                if (!importer.isNativePlugin || !path.StartsWith("Assets/") ||
                    path.Split('/').Any(part => part.Equals("iOS", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                bool any = importer.GetCompatibleWithAnyPlatform();
                if (any ? importer.GetExcludeFromAnyPlatform(BuildTarget.iOS)
                        : !importer.GetCompatibleWithPlatform(BuildTarget.iOS))
                {
                    continue;
                }

                if (any)
                {
                    importer.SetExcludeFromAnyPlatform(BuildTarget.iOS, true);
                }
                importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
                importer.SaveAndReimport();
                changed++;
                Debug.Log($"[iOS port] Excluded desktop plugin from iOS: {path}");
            }
            Debug.Log($"[iOS port] Excluded {changed} desktop native plugins from iOS");
        }
    }
}
