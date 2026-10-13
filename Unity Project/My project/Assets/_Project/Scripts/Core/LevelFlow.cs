using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// The order of the game's scenes and the run clock that spans them:
    /// <code>
    ///   Level00_Tutorial  (untimed, can be skipped)
    ///        |
    ///   Level01_PumpkinPatch  --&gt;  Level02_Orchard     (the timed run)
    /// </code>
    /// The run clock starts when level 1 starts and stops at the end of the last level. Each
    /// level's own time is kept as a split. Scenes are found by name, so nothing has to be wired
    /// in a scene: GameManager asks this class what the open scene is and what comes next.
    /// Restarting level 1 starts a new run. Restarting a later level keeps the time already
    /// spent on it, so a restart can never make a run faster.
    /// A scene that is not listed here (the Sandbox) is untimed and has no next scene.
    /// </summary>
    public static class LevelFlow
    {
        public const string TutorialScene = "Level00_Tutorial";

        private static readonly string[] RunScenes = { "Level01_PumpkinPatch", "Level02_Orchard" };
        private static readonly string[] RunNames = { "Pumpkin Patch", "The Orchard" };

        // Time of each finished level, or -1 if it has not been finished in this run.
        private static readonly float[] s_splits = { -1f, -1f };
        private static readonly int[] s_deaths = new int[2];
        // Time spent (and times extinguished) on the current level before its last restart.
        private static float s_carried;
        private static int s_carriedDeaths;
        private static int s_carriedIndex = -1;

        /// <summary>How many levels the timed run has.</summary>
        public static int RunLength => RunScenes.Length;
        /// <summary>The first level of the timed run.</summary>
        public static string FirstRunScene => RunScenes[0];

        public static bool IsTutorial(string scene) => scene == TutorialScene;

        /// <summary>Position of the scene in the timed run (0 = level 1), or -1 if it is not part of it.</summary>
        public static int RunIndex(string scene) => System.Array.IndexOf(RunScenes, scene);

        /// <summary>Name shown for a level of the run.</summary>
        public static string RunName(int index) => index >= 0 && index < RunNames.Length ? RunNames[index] : string.Empty;

        /// <summary>The scene that follows this one, or null if nothing does.</summary>
        public static string NextScene(string scene)
        {
            if (IsTutorial(scene)) return RunScenes[0];
            int index = RunIndex(scene);
            return index >= 0 && index + 1 < RunScenes.Length ? RunScenes[index + 1] : null;
        }

        /// <summary>
        /// Called when a level of the run is loaded. Level 1 starts a new run; any level drops
        /// its own old split and the splits after it.
        /// </summary>
        public static void EnterLevel(int index)
        {
            if (index < 0 || index >= s_splits.Length) return;
            for (int i = index; i < s_splits.Length; i++)
            {
                s_splits[i] = -1f;
                s_deaths[i] = 0;
            }
            if (index == 0 || s_carriedIndex != index)
            {
                s_carried = 0f;
                s_carriedDeaths = 0;
                s_carriedIndex = index;
            }
        }

        /// <summary>Time spent on this level before its last restart.</summary>
        public static float Carried(int index) => index >= 0 && index == s_carriedIndex ? s_carried : 0f;

        /// <summary>Times the lantern went out on this level before its last restart.</summary>
        public static int CarriedDeaths(int index) => index >= 0 && index == s_carriedIndex ? s_carriedDeaths : 0;

        /// <summary>Called before a level of the run is restarted, with the totals for that level so far.</summary>
        public static void KeepTime(int index, float levelTime, int levelDeaths)
        {
            s_carriedIndex = index;
            s_carried = levelTime;
            s_carriedDeaths = levelDeaths;
        }

        /// <summary>Called when a level of the run is won, with the totals for that level.</summary>
        public static void Finish(int index, float levelTime, int levelDeaths)
        {
            if (index < 0 || index >= s_splits.Length) return;
            s_splits[index] = levelTime;
            s_deaths[index] = levelDeaths;
        }

        /// <summary>Time of a finished level, or -1 if it has not been finished in this run.</summary>
        public static float Split(int index) => index >= 0 && index < s_splits.Length ? s_splits[index] : -1f;

        /// <summary>Sum of the finished levels before this one.</summary>
        public static float TimeBefore(int index)
        {
            float total = 0f;
            for (int i = 0; i < index && i < s_splits.Length; i++)
            {
                if (s_splits[i] > 0f) total += s_splits[i];
            }
            return total;
        }

        /// <summary>Times the lantern went out in the finished levels before this one.</summary>
        public static int DeathsBefore(int index)
        {
            int total = 0;
            for (int i = 0; i < index && i < s_deaths.Length; i++) total += s_deaths[i];
            return total;
        }

        /// <summary>
        /// True if every level before this one was finished in this run. False when a later
        /// level was opened on its own (from the Editor), where the run time is only that level's.
        /// </summary>
        public static bool RunIsWhole(int index)
        {
            for (int i = 0; i < index && i < s_splits.Length; i++)
            {
                if (s_splits[i] < 0f) return false;
            }
            return true;
        }

        /// <summary>Minutes, seconds and tenths: 3:07.4</summary>
        public static string FormatTime(float seconds)
        {
            int tenths = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 10f);
            return $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < s_splits.Length; i++)
            {
                s_splits[i] = -1f;
                s_deaths[i] = 0;
            }
            s_carried = 0f;
            s_carriedDeaths = 0;
            s_carriedIndex = -1;
        }
    }
}
