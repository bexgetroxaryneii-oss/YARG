#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

namespace YARG.Editor.IosPort
{
    /// <summary>
    /// YARG iOS port: finishes the exported Xcode project.
    /// Embeds and links the dynamic frameworks ios-port/build-natives.sh builds
    /// (BASS, its add-ons and yarg_audio), links SQLite, and lets songs be
    /// copied in through the Files app.
    /// </summary>
    public static class IosPortPostprocess
    {
        private const string FRAMEWORKS_SOURCE = "ios-port/Frameworks";
        private const string FRAMEWORKS_IN_PROJECT = "Frameworks/IosPort";

        // The iOS BASS framework has no DirectSound/EAX; these report failure like BASS does
        // when a feature is unavailable. YARG never calls them on iOS.
        private const string BASS_WINDOWS_STUBS =
@"#include <stddef.h>
void *BASS_GetDSoundObject(unsigned int object) { (void) object; return NULL; }
int BASS_GetEAXParameters(unsigned int *env, float *vol, float *decay, float *damp)
{ (void) env; (void) vol; (void) decay; (void) damp; return 0; }
int BASS_SetEAXParameters(int env, float vol, float decay, float damp)
{ (void) env; (void) vol; (void) decay; (void) damp; return 0; }
";

        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            AddFrameworks(buildPath);
            UpdateInfoPlist(buildPath);
        }

        private static void AddFrameworks(string buildPath)
        {
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTarget = project.GetUnityMainTargetGuid();
            string frameworkTarget = project.GetUnityFrameworkTargetGuid();
            string linkPhase = project.GetFrameworksBuildPhaseByTarget(frameworkTarget);

            string source = Path.Combine(Directory.GetCurrentDirectory(), FRAMEWORKS_SOURCE);
            if (Directory.Exists(source))
            {
                foreach (string framework in Directory.GetDirectories(source, "*.framework"))
                {
                    string name = Path.GetFileName(framework);
                    string relative = Path.Combine(FRAMEWORKS_IN_PROJECT, name);
                    CopyDirectory(framework, Path.Combine(buildPath, relative));

                    string file = project.AddFile(relative, relative, PBXSourceTree.Source);
                    // Linked to UnityFramework so IL2CPP's direct ("__Internal") P/Invoke
                    // calls resolve, and embedded in the app so dyld finds them.
                    project.AddFileToBuildSection(frameworkTarget, linkPhase, file);
                    project.AddFileToEmbedFrameworks(mainTarget, file);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[iOS port] {FRAMEWORKS_SOURCE} is missing: BASS will not load.");
            }

            foreach (string target in new[] { mainTarget, frameworkTarget })
            {
                project.AddBuildProperty(target, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/" + FRAMEWORKS_IN_PROJECT);
                project.SetBuildProperty(target, "ENABLE_BITCODE", "NO");
            }
            project.AddBuildProperty(mainTarget, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            project.AddFrameworkToProject(frameworkTarget, "libsqlite3.tbd", false);

            // Windows-only BASS entry points ManagedBass declares: IL2CPP links every P/Invoke
            // directly, so UnityFramework needs a definition even though nothing calls them.
            const string stubs = "Classes/IosPort/BassWindowsStubs.c";
            Directory.CreateDirectory(Path.Combine(buildPath, "Classes/IosPort"));
            File.WriteAllText(Path.Combine(buildPath, stubs), BASS_WINDOWS_STUBS);
            project.AddFileToBuild(frameworkTarget, project.AddFile(stubs, stubs, PBXSourceTree.Source));

            project.WriteToFile(projectPath);
        }

        private static void UpdateInfoPlist(string buildPath)
        {
            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            // Songs go in the app's folder in the Files app ("On My iPhone > YARG").
            root.SetBoolean("UIFileSharingEnabled", true);
            root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            root.SetBoolean("UIRequiresFullScreen", true);
            root.SetBoolean("UIStatusBarHidden", true);
            root.SetString("NSMicrophoneUsageDescription", "YARG uses the microphone for vocals.");
            root.SetString("NSBluetoothAlwaysUsageDescription", "YARG uses Bluetooth for instrument controllers.");

            plist.WriteToFile(plistPath);
        }

        private static void CopyDirectory(string source, string destination)
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(source, destination));
            }
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(source, destination), true);
            }
        }
    }
}
#endif
