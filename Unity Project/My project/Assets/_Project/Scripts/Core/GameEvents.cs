using System;
using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Static event hub so systems (HUD, telemetry, game flow, audio) can react to gameplay
    /// without holding references to each other. Nothing to set up in the scene.
    /// Subscribe in OnEnable and unsubscribe in OnDisable. Whoever owns scene flow (Step 3's
    /// game manager) should call <see cref="Clear"/> when a scene is torn down.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>A ghost hit landed on the player. Argument: world position of the attacker.</summary>
        public static event Action<Vector3> PlayerHit;
        /// <summary>The player threw a candy. Argument: world position it was thrown from.</summary>
        public static event Action<Vector3> CandyThrown;
        /// <summary>The player entered a level section. Argument: section name.</summary>
        public static event Action<string> SectionEntered;
        /// <summary>The player lit a checkpoint. Argument: the checkpoint's transform.</summary>
        public static event Action<Transform> CheckpointReached;
        /// <summary>The player respawned after the lantern went out.</summary>
        public static event Action PlayerRespawned;
        /// <summary>The player reached the farmhouse door with the candle lit.</summary>
        public static event Action LevelCompleted;
        /// <summary>A tutorial prompt was shown. Argument: tutorial id.</summary>
        public static event Action<string> TutorialShown;

        public static void RaisePlayerHit(Vector3 sourcePosition) => PlayerHit?.Invoke(sourcePosition);
        public static void RaiseCandyThrown(Vector3 origin) => CandyThrown?.Invoke(origin);
        public static void RaiseSectionEntered(string sectionName) => SectionEntered?.Invoke(sectionName);
        public static void RaiseCheckpointReached(Transform checkpoint) => CheckpointReached?.Invoke(checkpoint);
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        public static void RaiseLevelCompleted() => LevelCompleted?.Invoke();
        public static void RaiseTutorialShown(string tutorialId) => TutorialShown?.Invoke(tutorialId);

        /// <summary>Drops every subscriber.</summary>
        public static void Clear()
        {
            PlayerHit = null;
            CandyThrown = null;
            SectionEntered = null;
            CheckpointReached = null;
            PlayerRespawned = null;
            LevelCompleted = null;
            TutorialShown = null;
        }

        // Keeps stale subscribers out when "Enter Play Mode Options" skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
