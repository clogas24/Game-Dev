using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LanternKeeper.EditorTools
{
    /// <summary>
    /// "Lantern Keeper/Build Playtest (Windows)": a release (non-development) Windows build of
    /// the game (tutorial, Level01, Level02) for playtesters, zipped and ready to hand out. Being a non-development build, the
    /// F1 debug overlay is compiled out; the playtest logger still runs.
    /// Batch mode, Editor closed:
    /// <code>
    /// Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod LanternKeeper.EditorTools.LanternKeeperBuild.BuildPlaytestWindows -logFile -
    /// </code>
    /// TestBuildOtherPlatforms (batch mode only, no menu item) makes throwaway macOS and Linux
    /// builds to catch platform problems early.
    /// </summary>
    public static class LanternKeeperBuild
    {
        private const string Level01ScenePath = "Assets/_Project/Scenes/Level01_PumpkinPatch.unity";
        // In play order: the game starts in the first one that exists. LevelFlow.cs has the same order.
        private static readonly string[] GameScenePaths =
        {
            "Assets/_Project/Scenes/Level00_Tutorial.unity",
            Level01ScenePath,
            "Assets/_Project/Scenes/Level02_Orchard.unity"
        };
        private const string CompanyName = "Carson Logas";
        private const string ProductName = "Lantern Keeper";
        private const string Version = "0.2.0";
        private const string PlaytestRoot = "Builds/Playtest";
        private const string WindowsFolder = PlaytestRoot + "/LanternKeeper_Win_v0.2";
        private const string WindowsExe = WindowsFolder + "/LanternKeeper.exe";
        private const string WindowsZip = PlaytestRoot + "/LanternKeeper_Win_v0.2.zip";
        private const string TestRoot = "Builds/Test";
        // The release goes to the "builds" folder at the top of the repository, one folder per platform.
        private const string ReleaseVersion = "1.0.0";
        private const string ReleaseRoot = "../../builds";

        [MenuItem("Lantern Keeper/Build Playtest (Windows)", priority = 60)]
        public static void BuildPlaytestWindows()
        {
            bool ok = BuildWindows();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// The final release: Windows, macOS and Linux builds of the game's own scenes, each in its
        /// own folder under the repository's "builds" folder with nothing but what the game needs to run.
        /// </summary>
        [MenuItem("Lantern Keeper/Build Release (Windows, Mac, Linux)", priority = 61)]
        public static void BuildRelease()
        {
            if (!PrepareBuild(ReleaseVersion)) { ExitBatch(1); return; }
            BuildTarget original = EditorUserBuildSettings.activeBuildTarget;
            bool ok = true;
            ok &= ReleaseBuild(BuildTarget.StandaloneWindows64, "Lantern Keeper Windows", "LanternKeeper.exe");
            ok &= ReleaseBuild(BuildTarget.StandaloneOSX, "Lantern Keeper Mac", "LanternKeeper.app");
            ok &= ReleaseBuild(BuildTarget.StandaloneLinux64, "Lantern Keeper Linux", "LanternKeeper.x86_64");
            // Leave the project on the platform it was on.
            if (EditorUserBuildSettings.activeBuildTarget != original)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(original), original);
            }
            ExitBatch(ok ? 0 : 1);
        }

        private static bool ReleaseBuild(BuildTarget target, string folderName, string fileName)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                Debug.LogError($"[Lantern Keeper] RELEASE {target}: build support for this platform is not installed in this Editor.");
                return false;
            }
            string folder = Path.GetFullPath(Path.Combine(ReleaseRoot, folderName));
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            // One Mac app for both Intel and Apple Silicon; the default is the Editor's own architecture only.
            if (target == BuildTarget.StandaloneOSX)
            {
                EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "x64ARM64");
            }

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GameScenes(),
                locationPathName = Path.Combine(folder, fileName),
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            });
            if (!Report("RELEASE " + target, report)) return false;
            RemoveDebugFolders(folder);
            return true;
        }

        /// <summary>Batch-mode entry point: quick macOS and Linux builds into Builds/Test, if those modules are installed.</summary>
        public static void TestBuildOtherPlatforms()
        {
            if (!PrepareBuild()) { ExitBatch(1); return; }
            BuildTarget original = EditorUserBuildSettings.activeBuildTarget;
            bool ok = true;
            ok &= TestBuild(BuildTarget.StandaloneOSX, TestRoot + "/Mac/LanternKeeper.app");
            ok &= TestBuild(BuildTarget.StandaloneLinux64, TestRoot + "/Linux/LanternKeeper.x86_64");
            // Leave the project on the platform it was on.
            if (EditorUserBuildSettings.activeBuildTarget != original)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(original), original);
            }
            ExitBatch(ok ? 0 : 1);
        }

        private static bool BuildWindows()
        {
            if (!PrepareBuild()) return false;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Debug.LogError("[Lantern Keeper] Windows Build Support is not installed in this Editor.");
                return false;
            }

            if (Directory.Exists(WindowsFolder)) Directory.Delete(WindowsFolder, true);
            if (File.Exists(WindowsZip)) File.Delete(WindowsZip);
            Directory.CreateDirectory(WindowsFolder);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GameScenes(), // the playtest build is the game's own scenes; the sandbox stays out
                locationPathName = WindowsExe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None // release: no Development Build flag, so the F1 overlay is stripped
            });
            if (!Report("Windows playtest", report)) return false;

            RemoveDebugFolders(WindowsFolder);

            ZipFile.CreateFromDirectory(WindowsFolder, WindowsZip, System.IO.Compression.CompressionLevel.Optimal, true);
            long zipBytes = new FileInfo(WindowsZip).Length;
            Debug.Log($"[Lantern Keeper] Playtest build ready: {WindowsExe}\n[Lantern Keeper] Zip: {WindowsZip} ({zipBytes / (1024f * 1024f):0.0} MB)");
            return true;
        }

        // Debug-symbol folders Unity writes next to the build; they are not for players.
        private static void RemoveDebugFolders(string buildFolder)
        {
            foreach (string folder in Directory.GetDirectories(buildFolder))
            {
                string name = Path.GetFileName(folder);
                if (name.EndsWith("_DoNotShip") || name.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame")) Directory.Delete(folder, true);
            }
        }

        private static string[] GameScenes()
        {
            var scenes = new System.Collections.Generic.List<string>(GameScenePaths.Length);
            foreach (string path in GameScenePaths)
            {
                if (File.Exists(path)) scenes.Add(path);
            }
            return scenes.ToArray();
        }

        // Player settings and scene order shared by every build.
        private static bool PrepareBuild(string version = Version)
        {
            if (!File.Exists(Level01ScenePath))
            {
                Debug.LogError($"[Lantern Keeper] {Level01ScenePath} does not exist. Run 'Lantern Keeper/Create Level01 Greybox' first.");
                return false;
            }
            LanternKeeperSetup.EnsureBuildSceneOrder();

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = version;
            // Windowed 1280x720 and resizable for the playtest. Fullscreen comes in Step 6.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool TestBuild(BuildTarget target, string location)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                Debug.Log($"[Lantern Keeper] TEST BUILD {target}: module not installed, skipped.");
                return true;
            }
            string folder = Path.GetDirectoryName(location);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GameScenes(),
                locationPathName = location,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            });
            return Report("TEST BUILD " + target, report);
        }

        private static bool Report(string label, BuildReport report)
        {
            BuildSummary summary = report.summary;
            string text = $"[Lantern Keeper] {label}: {summary.result}, {summary.totalErrors} error(s), {summary.totalWarnings} warning(s), "
                          + $"{summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0} s -> {summary.outputPath}";
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(text);
                return true;
            }
            Debug.LogError(text);
            return false;
        }

        private static void ExitBatch(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
