using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper.EditorTools
{
    // Step 4 part of the "Lantern Keeper/" menu: the greybox of the real level.
    //
    // UNLIKE every other item in this menu, the level is built ONCE and then tuned by hand:
    //   - "Create Level01 Greybox" refuses to run if the scene file already exists.
    //   - "Rebuild Level01 Greybox (DESTRUCTIVE)" asks first (Editor) or needs -forceRebuild (batch mode).
    //   - "Run All Setup" and "Build Sandbox Scene" never open or save the level.
    // "Validate Level01" only reads the scene: it checks ghosts, plates, checkpoints, sections and
    // the walking route, and prints the pacing estimate. Run it after moving things by hand.
    //
    // Layout: the level runs along +Z. Ground tops are at y = 0. Sections 1-2 are a 14 m strip
    // (x -7..7), sections 3-5 a 28 m strip (x -14..14). docs/LEVEL_DESIGN.md has the map.
    //
    // LevelKit (the pieces: ground, bridges, ghosts, gates, triggers) and the validation are
    // shared with the tutorial and Level02, whose layouts are in LanternKeeperSetup.Levels.cs.
    public static partial class LanternKeeperSetup
    {
        private const string Level01SceneName = "Level01_PumpkinPatch";
        private const string Level01ScenePath = ScenesDir + "/" + Level01SceneName + ".unity";
        private const string ForceRebuildArg = "-forceRebuild";
        private const string GoldenPathRootName = "DesignNotes (EditorOnly)";
        private const float PlayerWalkSpeed = 2f;    // ThirdPersonController.MoveSpeed
        private const float PlayerSprintSpeed = 5.335f;
        private const float GhostNoticeRange = 7f;   // GhostAI.noticeRange

        private static readonly string[] Level01SectionIds =
        {
            "S1_PumpkinPatch", "S2_SpiritBridge", "S3_Graveyard", "S4_CandyPorch", "S5_Cornfield"
        };

        // Seconds a first-time player is expected to spend NOT walking in each section: reading a
        // prompt, watching a ghost, waiting for a ghost to reach and eat candy (4 s), hesitating on
        // a bridge. Estimates, to be replaced by the logger's S1..S5_time_s after Playtest 1.
        private static readonly float[] Level01WaitSeconds = { 17f, 20f, 30f, 55f, 45f };

        // Longest stretch of the walking route with nothing underfoot that is expected in each
        // section: the S1 sprint-jump ditch (3 m) and the S5 jump gaps (2 m). Bridge tiles are
        // about 0.1 m apart.
        private static readonly float[] Level01AllowedGap = { 3.3f, 0.35f, 0.35f, 0.35f, 2.3f };

        /// <summary>What the generator and the validation need to know about one level scene.</summary>
        private sealed class LevelSpec
        {
            public string SceneName;
            public string[] SectionIds;
            public float[] WaitSeconds;  // per section: seconds a first-time player spends not walking
            public float[] AllowedGap;   // per section: longest stretch of the route with nothing underfoot
            public float MinSeconds;     // pacing target at walking speed
            public float MaxSeconds;

            public string ScenePath => ScenesDir + "/" + SceneName + ".unity";
        }

        private static readonly LevelSpec Level01Spec = new LevelSpec
        {
            SceneName = Level01SceneName,
            SectionIds = Level01SectionIds,
            WaitSeconds = Level01WaitSeconds,
            AllowedGap = Level01AllowedGap,
            MinSeconds = 360f,
            MaxSeconds = 600f
        };

        // ------------------------------------------------------------------ menu items

        [MenuItem(Menu + "Create Level01 Greybox", priority = 40)]
        public static void CreateLevel01Greybox()
        {
            if (File.Exists(Level01ScenePath))
            {
                Debug.LogWarning($"[Lantern Keeper] {Level01ScenePath} already exists and was NOT touched. " +
                                 "The level is tuned by hand; use 'Rebuild Level01 Greybox (DESTRUCTIVE)' to throw it away and start again.");
                return;
            }
            BuildLevel01();
        }

        [MenuItem(Menu + "Rebuild Level01 Greybox (DESTRUCTIVE)", priority = 41)]
        public static void RebuildLevel01Greybox()
        {
            bool exists = File.Exists(Level01ScenePath);
            if (Application.isBatchMode)
            {
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), ForceRebuildArg) < 0)
                {
                    Debug.LogError($"[Lantern Keeper] Rebuild refused: in batch mode it only runs with the {ForceRebuildArg} command-line flag. {Level01ScenePath} was NOT touched.");
                    return;
                }
            }
            else if (exists && !EditorUtility.DisplayDialog("Rebuild Level01 Greybox",
                         $"This DELETES everything you changed by hand in\n{Level01ScenePath}\nand generates the greybox again from code.\n\nThis cannot be undone (except through git).",
                         "Delete and rebuild", "Cancel"))
            {
                return;
            }
            BuildLevel01();
        }

        /// <summary>Read-only: checks the level and prints the pacing estimate. Returns the number of problems.</summary>
        [MenuItem(Menu + "Validate Level01 (read-only)", priority = 42)]
        public static void ValidateLevel01MenuItem() => ValidateLevel01();

        public static int ValidateLevel01()
        {
            if (!File.Exists(Level01ScenePath))
            {
                Debug.LogWarning($"[Lantern Keeper] {Level01ScenePath} does not exist yet; run 'Create Level01 Greybox'.");
                return 1;
            }
            if (SceneManager.GetActiveScene().path != Level01ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return 1;
                EditorSceneManager.OpenScene(Level01ScenePath, OpenSceneMode.Single);
            }
            return ValidateOpenLevel(Level01Spec);
        }

        /// <summary>
        /// The game's own order first (tutorial, Level01, Level02: the game starts in scene 0),
        /// the Sandbox after them, anything else last. Disables the unused template scene.
        /// </summary>
        public static void EnsureBuildSceneOrder()
        {
            var old = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var first = new List<string> { TutorialSpec.ScenePath, Level01ScenePath, Level02Spec.ScenePath, SandboxScenePath };
            var result = new List<EditorBuildSettingsScene>(old.Count + first.Count);
            foreach (string path in first)
            {
                if (File.Exists(path)) result.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (EditorBuildSettingsScene scene in old)
            {
                if (first.Contains(scene.path)) continue;
                // The empty Unity template scene was index 0 until now; it is kept in the list but switched off.
                bool enabled = scene.enabled && scene.path != "Assets/Scenes/SampleScene.unity";
                result.Add(new EditorBuildSettingsScene(scene.path, enabled));
            }

            bool same = result.Count == old.Count;
            for (int i = 0; same && i < result.Count; i++)
            {
                same = result[i].path == old[i].path && result[i].enabled == old[i].enabled;
            }
            if (!same) EditorBuildSettings.scenes = result.ToArray();
        }

        // ------------------------------------------------------------------ build

        private static void BuildLevel01()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureTagsAndLayers();
            if (!EnsureTmpEssentials())
            {
                Debug.LogError("[Lantern Keeper] TMP Essential Resources are still importing. Run 'Run All Setup' once, then create the level again.");
                return;
            }

            var kit = new LevelKit(Level01SectionIds, false);
            if (!kit.LoadPrefabs())
            {
                // First run on a fresh checkout: make the prefabs, then try once more.
                CreatePlaceholderAssets();
                CreatePlayerPrefab();
                CreateGhostPrefab();
                CreateWorldPrefabs();
                if (!kit.LoadPrefabs())
                {
                    Debug.LogError("[Lantern Keeper] Level01 not built: a prefab is missing. Run 'Run All Setup' and check the Console.");
                    return;
                }
            }
            kit.MakeMaterials();

            EnsureFolder(ScenesDir);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            kit.CreateGroups();
            kit.BuildLighting("S5_Farmhouse_Beacon (goal light)", new Vector3(0f, 4.5f, 381.5f));
            kit.BuildSection1();
            kit.BuildSection2();
            kit.BuildSection3();
            kit.BuildSection4();
            kit.BuildSection5();
            kit.BuildBoundaries();
            kit.BuildPlayerAndSystems();
            kit.BuildGoldenPath();

            if (!BakeLevelNavMesh(kit.Environment, Level01SceneName))
            {
                Debug.LogError("[Lantern Keeper] Level01 not saved: the NavMesh bake failed.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Level01ScenePath))
            {
                Debug.LogError($"[Lantern Keeper] Could not save {Level01ScenePath}.");
                return;
            }
            EnsureBuildSceneOrder();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lantern Keeper] Level01 greybox saved to {Level01ScenePath}. It is build scene 0; the Sandbox follows it.");

            // Opened again from disk: a rebuild saves over the old NavMesh asset, which leaves the
            // scene in memory without a NavMesh, and the check should see what was saved anyway.
            EditorSceneManager.OpenScene(Level01ScenePath, OpenSceneMode.Single);
            ValidateOpenLevel(Level01Spec);
        }

        // Same recipe as the sandbox: only the colliders under Environment are baked, so spirit
        // platforms (under Gameplay) are never walkable for ghosts.
        private static bool BakeLevelNavMesh(Transform environment, string sceneName)
        {
            string navMeshPath = ScenesDir + "/" + sceneName + "-NavMesh.asset";
            Physics.SyncTransforms();
            var surface = environment.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << 0; // Default
            surface.BuildNavMesh();

            NavMeshData data = surface.navMeshData;
            if (data == null) return false;
            data.name = sceneName + "-NavMesh";
            AssetDatabase.CreateAsset(data, navMeshPath);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navMeshPath);
            EditorUtility.SetDirty(surface);
            return true;
        }

        /// <summary>
        /// Everything needed while a level is being generated: prefabs, materials, group parents
        /// and the pieces. The Level01 layout is below; the tutorial's and Level02's are in
        /// LanternKeeperSetup.Levels.cs.
        /// </summary>
        private sealed partial class LevelKit
        {
            private const float StripNarrow = 7f;   // half width of sections 1-2
            private const float StripWide = 14f;    // half width of sections 3-5
            private const float WallHeight = 3f;
            private const float ShallowDepth = 1.6f; // creek, pond, trench: water, and water puts the lantern out
            private const float PitDepth = 6f;       // deadly pits
            // The lantern goes out below this height. It is between the S1 ditch floor (-1, safe:
            // you jump back out) and the water floors (-1.6), so falling in water costs a life
            // like a pit does, and every crossing has to be made on its bridge.
            private const float FallDeathHeight = -1.3f;
            private const float PuzzleCandySense = 11f;

            private GameObject _player, _platform, _lamp, _candle, _hud, _mainCamera, _followCamera;
            private GameObject _ghost, _plate, _gate, _checkpoint, _section, _tutorial, _goal, _bowl, _menus;
            private Material _ground, _hazard, _shallow, _obstacle, _stone, _corn, _wall, _goalMat, _checkpointPad, _bowlMat, _porch;
            private Transform _gameplay, _triggers, _lighting, _systems, _notes;
            private Transform _env, _play;      // current section's parents
            private readonly List<KeyValuePair<string, Vector2[]>> _paths = new List<KeyValuePair<string, Vector2[]>>(5);
            private readonly string[] _sectionIds;
            private readonly bool _keepMaterials;

            public Transform Environment { get; private set; }

            /// <param name="sectionIds">The level's section ids, in order.</param>
            /// <param name="keepMaterials">
            /// True leaves the M_LK_* materials that already exist as they are (Level01 shares
            /// them and its dressing has tuned their colours); false resets them to the greybox colours.
            /// </param>
            public LevelKit(string[] sectionIds, bool keepMaterials)
            {
                _sectionIds = sectionIds;
                _keepMaterials = keepMaterials;
            }

            public bool LoadPrefabs()
            {
                _player = Load(PlayerPrefabPath);
                _platform = Load(SpiritPlatformPrefabPath);
                _lamp = Load(PorchLampPrefabPath);
                _candle = Load(CandlePickupPrefabPath);
                _hud = Load(HudPrefabPath);
                _ghost = Load(GhostPrefabPath);
                _plate = Load(PressurePlatePrefabPath);
                _gate = Load(GatePrefabPath);
                _checkpoint = Load(CheckpointPrefabPath);
                _section = Load(SectionTriggerPrefabPath);
                _tutorial = Load(TutorialTriggerPrefabPath);
                _goal = Load(GoalTriggerPrefabPath);
                _bowl = Load(CandyBowlPrefabPath);
                _menus = Load(GameMenusPrefabPath);
                _mainCamera = FindStarterPrefab("MainCamera");
                _followCamera = FindStarterPrefab("PlayerFollowCamera");
                return _player != null && _platform != null && _lamp != null && _candle != null && _hud != null
                       && _ghost != null && _plate != null && _gate != null && _checkpoint != null && _section != null
                       && _tutorial != null && _goal != null && _bowl != null && _menus != null
                       && _mainCamera != null && _followCamera != null;
            }

            private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // The greybox colour code. Spirit platforms keep their own cyan material.
            public void MakeMaterials()
            {
                _ground = LevelMaterial("M_LK_Ground", new Color(0.17f, 0.17f, 0.19f));                                       // safe ground: dark grey
                _hazard = LevelMaterial("M_LK_Hazard", new Color(0.42f, 0.04f, 0.04f), new Color(0.16f, 0.01f, 0.01f));       // pits and their edges: dark red
                _shallow = LevelMaterial("M_LK_Shallow", new Color(0.08f, 0.17f, 0.34f));                                     // creek, pond, trench: a fall costs time, not a life
                _obstacle = LevelMaterial("M_LK_Obstacle", new Color(0.36f, 0.27f, 0.17f));                                   // crates, fences: jump over or walk round
                _stone = LevelMaterial("M_LK_Stone", new Color(0.46f, 0.46f, 0.5f));                                          // gravestones: cover
                _corn = LevelMaterial("M_LK_Corn", new Color(0.36f, 0.34f, 0.1f));                                            // corn walls
                _goalMat = LevelMaterial("M_LK_Goal", new Color(1f, 0.55f, 0.15f), new Color(1.3f, 0.6f, 0.1f));              // the goal: warm orange
                _checkpointPad = LevelMaterial("M_LK_Checkpoint", new Color(0.2f, 1f, 0.45f), new Color(0.15f, 0.9f, 0.35f));
                _bowlMat = LevelMaterial("M_LK_CandyBowl", new Color(1f, 0.25f, 0.75f), new Color(0.9f, 0.15f, 0.6f));        // candy is pink, so the bowl is too
                _porch = LevelMaterial("M_LK_Porch", new Color(0.3f, 0.26f, 0.24f));
                _wall = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/M_Wall.mat");
            }

            private Material LevelMaterial(string name, Color color, Color? emission = null)
            {
                if (_keepMaterials)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");
                    if (existing != null) return existing;
                }
                return MakeMaterial(name, color, false, emission);
            }

            public void CreateGroups()
            {
                Environment = new GameObject("Environment").transform;
                _gameplay = new GameObject("Gameplay").transform;
                _triggers = new GameObject("Triggers").transform;
                _lighting = new GameObject("Lighting").transform;
                _systems = new GameObject("Systems").transform;
            }

            private void BeginSection(string sectionId)
            {
                _env = Child(Environment, sectionId);
                _play = Child(_gameplay, sectionId);
            }

            private static Transform Child(Transform parent, string name)
            {
                Transform child = new GameObject(name).transform;
                child.SetParent(parent, false);
                return child;
            }

            // ------------------------------------------------------------------ lighting

            /// <summary>Night lighting, the moon, and a warm light over the goal at <paramref name="beaconPosition"/>.</summary>
            public void BuildLighting(string beaconName, Vector3 beaconPosition)
            {
                // Night, but a little brighter than the sandbox so testers can read the colour code.
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.12f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.05f);
                RenderSettings.fogDensity = 0.012f;

                var moon = new GameObject("Moon (Directional Light)");
                moon.transform.SetParent(_lighting, false);
                moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var moonLight = moon.AddComponent<Light>();
                moonLight.type = LightType.Directional;
                moonLight.color = new Color(0.55f, 0.65f, 1f);
                moonLight.intensity = 0.25f;
                moonLight.shadows = LightShadows.Soft;

                // The goal can be seen glowing across the last pit.
                var beacon = new GameObject(beaconName);
                beacon.transform.SetParent(_lighting, false);
                beacon.transform.position = beaconPosition;
                var beaconLight = beacon.AddComponent<Light>();
                beaconLight.type = LightType.Point;
                beaconLight.color = new Color(1f, 0.6f, 0.2f);
                beaconLight.intensity = 8f;
                beaconLight.range = 30f;
                beaconLight.shadows = LightShadows.None;
            }

            // ------------------------------------------------------------------ S1 Pumpkin Patch (z -8..48)
            // Teach: move, jump, sprint, camera, the candle drains, candle stubs refill it. No ghosts.

            public void BuildSection1()
            {
                BeginSection(Level01SectionIds[0]);
                float w = StripNarrow;

                Ground("S1_Ground_Start", -w, w, -8f, 28f);
                // A dry ditch 3 m wide and 1 m deep: a walking jump (1.6 m) falls in, a sprinting
                // jump (4.3 m) clears it, and you can simply jump back out.
                Slab("S1_Ditch_Floor", _env, -w, w, 28f, 31f, -1f, 1f, _ground);
                Ground("S1_Ground_ToCreek", -w, w, 31f, 52f);

                Block("S1_PumpkinCrates_Row (jump, 0.5 m)", -w, w, 9.5f, 10.5f, 0.5f, _obstacle);
                Block("S1_Fence (jump, 0.8 m)", -w, w, 15.85f, 16.15f, 0.8f, _obstacle);

                // Candle nook, just off the path to the left.
                Block("S1_CrateStack_NookSouth", -w, -2.5f, 19f, 20f, 1.7f, _obstacle);
                Block("S1_CrateStack_NookNorth", -w, -2.5f, 25f, 26f, 1.7f, _obstacle);
                AddPickup("S1_CandlePickup_Nook", new Vector3(-5f, 0.7f, 22.5f));

                // Two staggered walls: an S-bend that needs the camera.
                Block("S1_PumpkinWall_A", -w, 2f, 36f, 37f, 1.7f, _obstacle);
                Block("S1_PumpkinWall_B", -2f, w, 42f, 43f, 1.7f, _obstacle);

                AddCheckpoint("S1_Checkpoint_Start", new Vector3(3f, 0f, 1.5f), 1.6f, true);
                AddSection(0, "Pumpkin Patch", -w, w, 1.5f);
                AddTutorial("T1_MoveJumpSprint", -w, w, 5f,
                    "{jump} to jump. Hold Left Shift to sprint: a sprinting jump goes further.");
                AddTutorial("T2_Candle", -w, w, 20.5f,
                    "Your candle burns down as you go. Candle stubs refill it.");

                Route(0, V(0, 0), V(0, 9), V(0, 16), V(0, 20), V(-5, 22.5f), V(1, 24), V(1, 28), V(1, 31.5f),
                    V(4.5f, 35), V(4.5f, 38), V(-4.5f, 41), V(-4.5f, 44.5f), V(0, 48));
            }

            // ------------------------------------------------------------------ S2 Spirit Bridge / Creek (z 48..106)
            // Teach: light reveals platforms. Apply: follow a bridge round two corners, then cross a real pit.

            public void BuildSection2()
            {
                BeginSection(Level01SectionIds[1]);
                float w = StripNarrow;

                // Bridge 1 over the creek. The water puts the lantern out: there is no wading across.
                Slab("S2_Creek_Floor (shallow)", _env, -w, w, 52f, 58f, -ShallowDepth, 0.5f, _shallow);
                Bridge("S2_Bridge1", 0f, 2f, 52f, 58f, 4, 0.12f);
                // 4 m reveal radius: it reaches the first platform and none of the second, so the
                // player sees what a spirit platform looks like before their own light gets there.
                PlacePrefab(_lamp, "S2_PorchLamp_FirstPlatform", _play, new Vector3(2.6f, 0f, 50.4f));

                Ground("S2_Ground_IslandA", -w, w, 58f, 64f);

                // Bridge 2 over the pond: north, east, north again. Only about 6 m is lit at a time.
                Slab("S2_Pond_Floor (shallow)", _env, -w, w, 64f, 80f, -ShallowDepth, 0.5f, _shallow);
                Transform bridge2 = Child(_play, "S2_Bridge2 (dogleg)");
                int tile = 1;
                for (int i = 0; i < 4; i++) Tile($"S2_Bridge2_P{tile++}", bridge2, -5f, -3f, 64.05f + 2.1f * i, 66.05f + 2.1f * i);
                for (int i = 0; i < 4; i++) Tile($"S2_Bridge2_P{tile++}", bridge2, -2.9f + 2.1f * i, -0.9f + 2.1f * i, 70.35f, 72.35f);
                for (int i = 0; i < 3; i++) Tile($"S2_Bridge2_P{tile++}", bridge2, 3.4f, 5.4f, 72.45f + 2.1f * i, 74.45f + 2.1f * i);
                Tile($"S2_Bridge2_P{tile}", bridge2, 3.4f, 5.4f, 78.75f, 79.95f);
                // A dead-end spur off the first corner, with a candle stub on it.
                Tile("S2_Bridge2_Spur1", bridge2, -5f, -3f, 72.45f, 74.45f);
                Tile("S2_Bridge2_Spur2", bridge2, -5f, -3f, 74.55f, 76.55f);
                AddPickup("S2_CandlePickup_BridgeSpur", new Vector3(-4f, 0.7f, 75.55f));

                Ground("S2_Ground_BankB", -w, w, 80f, 90f);

                // Bridge 3 over a real pit, 16 m: the far side cannot be seen until you walk.
                Slab("S2_Pit_Floor (deadly)", _env, -w, w, 90f, 106f, -PitDepth, 0.5f, _hazard);
                HazardEdge("S2_Pit_Edge_South", -w, w, 89.5f, 90f);
                HazardEdge("S2_Pit_Edge_North", -w, w, 106f, 106.5f);
                Bridge("S2_Bridge3", 3f, 2f, 90f, 106f, 7, 0.075f);

                AddCheckpoint("S2_Checkpoint_Creek", new Vector3(-2.5f, 0f, 47f), 3.2f, false);
                // Its lamp (5 m reveal radius) lights the first tiles of bridge 3 once the player lights it.
                AddCheckpoint("S2_Checkpoint_DeepBridge", new Vector3(4.5f, 0f, 88.3f), 3f, false);
                AddSection(1, "The Creek", -w, w, 48.5f);
                AddTutorial("T3_LightReveals", -w, w, 49.8f, "Spirit platforms exist only inside light.");

                Route(1, V(0, 48), V(0, 52), V(0, 58), V(-4, 64), V(-4, 71.35f), V(-4, 75.55f), V(-4, 71.35f),
                    V(4.4f, 71.35f), V(4.4f, 80), V(4.4f, 86), V(3, 90), V(3, 106));
            }

            // ------------------------------------------------------------------ S3 Graveyard (z 106..196)
            // Teach: ghosts and the flare. Apply: flare to get past. Remix of S2: the flare erases a bridge.

            public void BuildSection3()
            {
                BeginSection(Level01SectionIds[2]);
                float w = StripWide;

                Ground("S3_Ground_GateAndYards", -w, w, 106f, 180f);

                // Graveyard wall with a 4 m gate opening.
                Block("S3_GraveyardWall_West", -w, -2f, 116f, 117f, WallHeight, _wall);
                Block("S3_GraveyardWall_East", 2f, w, 116f, 117f, WallHeight, _wall);

                // Yard 1: gravestones are cover (ghosts in this level need a clear line of sight).
                Vector2[] stones =
                {
                    V(-8, 124), V(-3, 124), V(4, 124), V(9, 124),
                    V(-10, 131), V(-5, 131), V(1.8f, 131), V(6, 131), V(11, 131),
                    V(-7, 136), V(-1.8f, 136), V(3.5f, 136), V(8, 136)
                };
                for (int i = 0; i < stones.Length; i++) Gravestone($"S3_Gravestone_{i + 1:00}", stones[i]);

                // Ghost A: a long, slow, east-west patrol the player can watch from the gate, 24 m away.
                AddGhost("S3_Ghost_A", 9f, V(-9, 141), V(9, 141));

                // Two crypts leave a corridor 3.2 m wide: there is no walking round ghost B.
                Block("S3_Crypt_West", -w, -1.6f, 150f, 163f, WallHeight, _wall);
                Block("S3_Crypt_East", 1.6f, w, 150f, 163f, WallHeight, _wall);
                AddGhost("S3_Ghost_B", 9f, V(0, 153.5f), V(0, 160.5f));

                // Yard 2: ghost C circles in front of a short bridge. Flaring at it erases the bridge.
                AddPickup("S3_CandlePickup_AfterCorridor", new Vector3(-10.5f, 0.7f, 165.5f));
                Gravestone("S3_Gravestone_14", V(-6.5f, 170.5f));
                Gravestone("S3_Gravestone_15", V(6, 169));
                AddGhost("S3_Ghost_C", 9f, V(-8, 174), V(8, 174), V(0, 177.5f));

                Slab("S3_Trench_Floor (shallow)", _env, -w, w, 180f, 188f, -ShallowDepth, 0.5f, _shallow);
                Bridge("S3_Bridge_Trench", 0f, 2.4f, 180f, 188f, 4, 0.08f);
                Ground("S3_Ground_FarBank", -w, w, 188f, 196f);

                // In front of the gate opening, so nobody gets into the graveyard without lighting it;
                // 27 m from the nearest patrol point.
                AddCheckpoint("S3_Checkpoint_GraveyardGate", new Vector3(1.2f, 0f, 114.2f), 3.6f, false);
                AddSection(2, "Graveyard", -w, w, 107.5f);
                AddTutorial("T4_Flare", -w, w, 127f,
                    "Hold {flare} to flare. Ghosts flee from it, but it burns your candle 8x faster.");

                Route(2, V(3, 106), V(3, 109), V(-0.3f, 113.5f), V(0, 116.5f), V(0, 127), V(0, 146), V(0, 164), V(-10.5f, 165.5f),
                    V(0, 172), V(0, 180), V(0, 188), V(0, 196));
            }

            // ------------------------------------------------------------------ S4 Candy Porch -> Cemetery Gate (z 196..300)
            // Teach: candy lures a ghost. Apply: a ghost on a plate opens a gate. Remix: the two-plate gate.

            public void BuildSection4()
            {
                BeginSection(Level01SectionIds[3]);
                float w = StripWide;

                Ground("S4_Ground_PorchAndLure", -w, w, 196f, 233f);
                Ground("S4_Ground_OnePlateYard", -w, w, 233f, 262f);
                Ground("S4_Ground_TwoPlateYard", -w, w, 262f, 300f);

                // The porch: rails funnel the player between the porch lantern and the candy bowl.
                Decal("S4_Porch_Floor", -5f, 5f, 199f, 207f, _porch);
                Block("S4_Porch_Rail_West", -w, -2.5f, 207f, 208f, 1.7f, _obstacle);
                Block("S4_Porch_Rail_East", 2.5f, w, 207f, 208f, 1.7f, _obstacle);
                AddCheckpoint("S4_Checkpoint_Porch", new Vector3(1.4f, 0f, 205f), 4f, false);
                AddBowl("S4_CandyBowl_Porch", new Vector3(-1.5f, 0f, 205.3f));

                // Lure: ghost D paces in front of a 3.2 m opening. Candy thrown to one side pulls it away.
                Block("S4_Wall_NarrowPath_West", -w, -1.6f, 232f, 233f, WallHeight, _wall);
                Block("S4_Wall_NarrowPath_East", 1.6f, w, 232f, 233f, WallHeight, _wall);
                AddGhost("S4_Ghost_D", 9f, V(-3.5f, 227.5f), V(3.5f, 227.5f));

                // One-plate gate. The plate is 16 m from the doorway: step on it and the gate opens,
                // but it shuts again before even a sprinting player gets there. Ghost E's route
                // stays 8.5 m from the plate, so a player on the plate is never noticed, and its
                // candy sense is raised to 11 m so candy anywhere on the plate is found.
                AddBowl("S4_CandyBowl_Gate", new Vector3(3.5f, 0f, 236.5f));
                GameObject plateOne = AddPlate("S4_Plate_One", new Vector3(8f, 0f, 246f));
                AddPickup("S4_CandlePickup_ByPlate", new Vector3(11.5f, 0.7f, 246f));
                AddGhost("S4_Ghost_E", PuzzleCandySense, V(-0.5f, 246), V(-8, 240), V(-8, 254));
                Block("S4_Wall_GateOne_West", -w, -1.9f, 261.75f, 262.25f, 3.2f, _wall);
                Block("S4_Wall_GateOne_East", 1.9f, w, 261.75f, 262.25f, 3.2f, _wall);
                GameObject gateOne = PlacePrefab(_gate, "S4_Gate_OnePlate", _play, new Vector3(0f, 0f, 262f));
                ConfigureGate(gateOne, 1, false, plateOne);

                // A 5 m corridor behind the gate with the checkpoint in it: it cannot be missed, so
                // a death in the two-plate yard never sends the player back through gate one.
                Block("S4_Hedge_West", -w, -2.5f, 262.25f, 270f, WallHeight, _corn);
                Block("S4_Hedge_East", 2.5f, w, 262.25f, 270f, WallHeight, _corn);
                AddCheckpoint("S4_Checkpoint_TwoPlate", new Vector3(1.2f, 0f, 265.5f), 3.6f, false);

                // Two-plate gate. It latches open (stayOpenOnceOpened). The ghosts patrol the far
                // corners, 8.5 m from the centre lane and from their plate.
                AddBowl("S4_CandyBowl_TwoPlate", new Vector3(-1.5f, 0f, 273f));
                GameObject plateA = AddPlate("S4_Plate_TwoA", new Vector3(-7f, 0f, 280f));
                GameObject plateB = AddPlate("S4_Plate_TwoB", new Vector3(7f, 0f, 280f));
                AddGhost("S4_Ghost_F", PuzzleCandySense, V(-8.5f, 288.5f), V(-8.5f, 293.5f));
                AddGhost("S4_Ghost_G", PuzzleCandySense, V(8.5f, 288.5f), V(8.5f, 293.5f));
                Block("S4_Wall_GateTwo_West", -w, -1.9f, 299.75f, 300.25f, 3.2f, _wall);
                Block("S4_Wall_GateTwo_East", 1.9f, w, 299.75f, 300.25f, 3.2f, _wall);
                GameObject gateTwo = PlacePrefab(_gate, "S4_Gate_TwoPlate", _play, new Vector3(0f, 0f, 300f));
                ConfigureGate(gateTwo, 2, true, plateA, plateB);

                AddSection(3, "Candy Porch", -w, w, 197f);
                // Placed inside the porch checkpoint's trigger, so this prompt replaces "Porch lantern lit" and not the other way round.
                AddTutorial("T5_Candy", -w, w, 203.5f,
                    "Take candy from the bowl. {throw} throws it, {drop} drops it. Ghosts stop to eat.");
                AddTutorial("T6_Plate", -w, w, 238.5f, "A plate opens its gate while something stands on it.");

                Route(3, V(0, 196), V(0, 204), V(0, 207.5f), V(4, 216), V(4, 226), V(0, 232.5f), V(4.3f, 236),
                    V(8, 246), V(11.5f, 246), V(4, 254), V(0, 261), V(0, 265), V(0, 272), V(-1, 280), V(7, 280),
                    V(0, 286), V(0, 300));
            }

            // ------------------------------------------------------------------ S5 Cornfield -> Farmhouse (z 300..392)
            // Everything combined: a corn maze with a ghost, a pair of ghosts to split with candy, a
            // jump bridge over the last pit that ghosts cannot follow onto, and the farmhouse door.

            public void BuildSection5()
            {
                BeginSection(Level01SectionIds[4]);
                float w = StripWide;

                Ground("S5_Ground_Cornfield", -w, w, 300f, 356f);

                // Entry glade.
                AddCheckpoint("S5_Checkpoint_Entry", new Vector3(1f, 0f, 303.5f), 4f, false);
                AddPickup("S5_CandlePickup_Entry", new Vector3(-6f, 0.7f, 304f));
                AddBowl("S5_CandyBowl_Entry", new Vector3(-3f, 0f, 307f));

                // Corn maze: four walls, 4 m corridors, ONE way through, and a ghost in each
                // corridor. In at the east end of wall 1, west along the whole first corridor, back
                // east along the second to the gap at x -4..0, then a few steps to the way out.
                // The second corridor's east half and the third's west half are dead ends (2c and
                // 3c close what used to be the short cuts).
                Corn("S5_Corn_1", -w, 9.5f, 310f);
                Corn("S5_Corn_2a", -9.5f, 0f, 316f);
                Corn("S5_Corn_2b", 4f, w, 316f);
                Corn("S5_Corn_2c", 0f, 4f, 316f);
                Corn("S5_Corn_3a", -w, -4f, 322f);
                Corn("S5_Corn_3b", 0f, 9.5f, 322f);
                Corn("S5_Corn_3c", 9.5f, w, 322f);
                Corn("S5_Corn_4a", -w, 3f, 328f);
                Corn("S5_Corn_4b", 7.5f, w, 328f);
                AddGhost("S5_Ghost_K", 9f, V(-11.5f, 313.5f), V(5f, 313.5f));
                AddGhost("S5_Ghost_H", 9f, V(-11.5f, 319.5f), V(11.5f, 319.5f));
                AddGhost("S5_Ghost_L", 9f, V(-11f, 325.5f), V(-5f, 325.5f));
                AddPickup("S5_CandlePickup_MazeDeadEnd", new Vector3(-12f, 0.7f, 325.5f));

                // Midway glade.
                AddCheckpoint("S5_Checkpoint_Mid", new Vector3(5.25f, 0f, 330.6f), 3f, false);
                AddBowl("S5_CandyBowl_Mid", new Vector3(0f, 0f, 333.5f));

                // The pair: two ghosts crossing behind a 4 m gap. One candy takes one ghost.
                Corn("S5_Corn_5a", -w, -2f, 338f);
                Corn("S5_Corn_5b", 2f, w, 338f);
                AddGhost("S5_Ghost_I", 9f, V(-7, 344), V(7, 344));
                AddGhost("S5_Ghost_J", 9f, V(7, 349), V(-7, 349));

                // The last pit. Three runs of platforms with two 2 m jumps: a walking jump (1.6 m)
                // falls short, a sprinting jump (4.3 m) clears them, and each run is long enough
                // to land one on. Ghosts stop at the edge.
                PlacePrefab(_lamp, "S5_PorchLamp_FinalBridge", _play, new Vector3(2.2f, 0f, 354.6f));
                Slab("S5_Pit_Floor (deadly)", _env, -w, w, 356f, 372f, -PitDepth, 0.5f, _hazard);
                HazardEdge("S5_Pit_Edge_South", -w, w, 355.5f, 356f);
                HazardEdge("S5_Pit_Edge_North", -w, w, 372f, 372.5f);
                Transform bridge = Child(_play, "S5_Bridge_Final (two jumps)");
                Tile("S5_BridgeFinal_A1", bridge, -1.2f, 1.2f, 356.05f, 357.65f);
                Tile("S5_BridgeFinal_A2", bridge, -1.2f, 1.2f, 357.7f, 359.3f);
                Tile("S5_BridgeFinal_B1", bridge, -1.2f, 1.2f, 361.3f, 363.42f);
                Tile("S5_BridgeFinal_B2", bridge, -1.2f, 1.2f, 363.47f, 365.6f);
                Tile("S5_BridgeFinal_C1", bridge, -1.2f, 1.2f, 367.6f, 369.77f);
                Tile("S5_BridgeFinal_C2", bridge, -1.2f, 1.2f, 369.82f, 371.98f);

                // Farmhouse: a dark block with the goal door in warm orange.
                Ground("S5_Ground_FarmYard", -w, w, 372f, 392f);
                Block("S5_Farmhouse", -8f, 8f, 384.3f, 392f, 5f, _wall);
                Decal("S5_Farmhouse_Porch (goal)", -3f, 3f, 379.5f, 384.3f, _goalMat);
                PlacePrefab(_goal, "S5_Goal_FarmhouseDoor", _triggers, new Vector3(0f, 0f, 384f));

                AddSection(4, "Cornfield", -w, w, 301.5f);

                Route(4, V(0, 300), V(0, 304), V(-6, 304), V(-3, 305.9f), V(11.75f, 306.5f), V(11.75f, 313.5f),
                    V(-11.75f, 313.5f), V(-11.75f, 319.5f), V(-2, 319.5f), V(-2, 325.5f), V(-12, 325.5f), V(4.3f, 325.5f),
                    V(4.3f, 332), V(0.8f, 334.2f), V(0, 338.5f), V(0, 356), V(0, 372), V(0, 382.6f));
            }

            // ------------------------------------------------------------------ boundaries, player, systems

            // Invisible walls round the whole strip. They reach below the creek and pit floors, so
            // nobody walks out of the map along the bottom of a pit either.
            public void BuildBoundaries()
            {
                Transform parent = Child(Environment, "Boundaries (invisible)");
                float n = StripNarrow, w = StripWide, t = 0.5f;
                Invisible("Boundary_South", parent, -n - t, n + t, -8f - t, -8f);
                Invisible("Boundary_S1S2_West", parent, -n - t, -n, -8f, 106f);
                Invisible("Boundary_S1S2_East", parent, n, n + t, -8f, 106f);
                Invisible("Boundary_Step_West", parent, -w - t, -n, 106f - t, 106f);
                Invisible("Boundary_Step_East", parent, n, w + t, 106f - t, 106f);
                Invisible("Boundary_S3S5_West", parent, -w - t, -w, 106f, 392f);
                Invisible("Boundary_S3S5_East", parent, w, w + t, 106f, 392f);
                Invisible("Boundary_North", parent, -w - t, w + t, 392f, 392f + t);
            }

            public void BuildPlayerAndSystems()
            {
                GameObject player = PlacePrefab(_player, "Player", null, Vector3.zero);
                GameObject mainCamera = PlacePrefab(_mainCamera, "MainCamera", null, new Vector3(0f, 2f, -4f));
                if (mainCamera.TryGetComponent(out Camera camera))
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.01f, 0.012f, 0.03f);
                    RecordOverrides(camera);
                }
                GameObject followCamera = PlacePrefab(_followCamera, "PlayerFollowCamera", null, new Vector3(0.5f, 1.375f, -4f));
                Transform cameraTarget = FindDeepChild(player.transform, "PlayerCameraRoot");
                if (followCamera.TryGetComponent(out CinemachineCamera vcam) && cameraTarget != null)
                {
                    vcam.Follow = cameraTarget;
                    RecordOverrides(vcam);
                }
                else
                {
                    Debug.LogWarning("[Lantern Keeper] Could not wire the follow camera; set PlayerFollowCamera > Tracking Target to Player/PlayerCameraRoot by hand.");
                }

                var manager = new GameObject("GameManager");
                manager.transform.SetParent(_systems, false);
                var gameManager = manager.AddComponent<GameManager>();
                SetRef(gameManager, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
                // Creek, pond and trench floors (-1.6) and pit floors (-6) are both below it; the S1 ditch (-1) is not.
                SetFloat(gameManager, "fallDeathHeight", FallDeathHeight);
                manager.AddComponent<DebugOverlay>();

                var logger = new GameObject("PlaytestLogger");
                logger.transform.SetParent(_systems, false);
                logger.AddComponent<PlaytestLogger>();

                PlacePrefab(_menus, "GameMenus", _systems, Vector3.zero);
                PlacePrefab(_hud, "HUD", _systems, Vector3.zero);
            }

            // The route the pacing estimate is measured along, kept in the scene as empty,
            // EditorOnly objects (stripped from builds). Move them when the layout changes and
            // run "Validate Level01" to get a new estimate.
            public void BuildGoldenPath()
            {
                _notes = new GameObject(GoldenPathRootName).transform;
                _notes.gameObject.tag = "EditorOnly";
                Transform root = Child(_notes, "GoldenPath");
                foreach (KeyValuePair<string, Vector2[]> path in _paths)
                {
                    Transform section = Child(root, path.Key);
                    for (int i = 0; i < path.Value.Length; i++)
                    {
                        Transform point = Child(section, $"{path.Key.Substring(0, 2)}_Path_{i:00}");
                        point.position = new Vector3(path.Value[i].x, 0f, path.Value[i].y);
                    }
                }
            }

            // ------------------------------------------------------------------ pieces

            private static Vector2 V(float x, float z) => new Vector2(x, z);

            private void Route(int section, params Vector2[] points)
            {
                _paths.Add(new KeyValuePair<string, Vector2[]>(_sectionIds[section], points));
            }

            /// <summary>Safe ground: top at y = 0, 2 m thick so banks are solid walls seen from a creek.</summary>
            private void Ground(string name, float xMin, float xMax, float zMin, float zMax)
            {
                Slab(name, _env, xMin, xMax, zMin, zMax, 0f, 2f, _ground);
            }

            private static GameObject Slab(string name, Transform parent, float xMin, float xMax, float zMin, float zMax,
                float top, float thickness, Material material)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = name;
                box.transform.SetParent(parent, false);
                box.transform.position = new Vector3((xMin + xMax) * 0.5f, top - thickness * 0.5f, (zMin + zMax) * 0.5f);
                box.transform.localScale = new Vector3(xMax - xMin, thickness, zMax - zMin);
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
                box.isStatic = true;
                return box;
            }

            /// <summary>A solid box standing on the ground.</summary>
            private void Block(string name, float xMin, float xMax, float zMin, float zMax, float height, Material material)
            {
                Slab(name, _env, xMin, xMax, zMin, zMax, height, height, material);
            }

            /// <summary>A corn wall: 1 m thick starting at z, 3 m tall. Blocks movement and ghost sight.</summary>
            private void Corn(string name, float xMin, float xMax, float z)
            {
                Block(name, xMin, xMax, z, z + 1f, WallHeight, _corn);
            }

            private void Gravestone(string name, Vector2 at)
            {
                // 1.7 m tall: above a ghost's eye line (1.2 m), so it hides the player.
                Block(name, at.x - 0.6f, at.x + 0.6f, at.y - 0.2f, at.y + 0.2f, 1.7f, _stone);
            }

            /// <summary>A coloured patch on the ground with no collider (porch floor, goal porch).</summary>
            private void Decal(string name, float xMin, float xMax, float zMin, float zMax, Material material)
            {
                GameObject box = Slab(name, _env, xMin, xMax, zMin, zMax, 0.02f, 0.04f, material);
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            private void HazardEdge(string name, float xMin, float xMax, float zMin, float zMax)
            {
                Decal(name, xMin, xMax, zMin, zMax, _hazard);
            }

            /// <summary>A walkable slope (about 27 degrees) from (zLow, yLow) up to (zHigh, yHigh).</summary>
            private void Ramp(string name, float xMin, float xMax, float zLow, float zHigh, float yLow, float yHigh)
            {
                const float thickness = 0.3f;
                var along = new Vector3(0f, yHigh - yLow, zHigh - zLow);
                Quaternion rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
                Vector3 middle = new Vector3((xMin + xMax) * 0.5f, (yLow + yHigh) * 0.5f, (zLow + zHigh) * 0.5f);

                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = name;
                box.transform.SetParent(_env, false);
                box.transform.SetPositionAndRotation(middle - rotation * Vector3.up * (thickness * 0.5f), rotation);
                box.transform.localScale = new Vector3(xMax - xMin, thickness, along.magnitude);
                box.GetComponent<MeshRenderer>().sharedMaterial = _ground;
                box.isStatic = true;
            }

            private static void Invisible(string name, Transform parent, float xMin, float xMax, float zMin, float zMax)
            {
                const float bottom = -8f, top = 6f;
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3((xMin + xMax) * 0.5f, (bottom + top) * 0.5f, (zMin + zMax) * 0.5f);
                go.AddComponent<BoxCollider>().size = new Vector3(xMax - xMin, top - bottom, zMax - zMin);
                go.isStatic = true;
            }

            /// <summary>One spirit platform. Top at y = 0, like the ground it joins.</summary>
            private void Tile(string name, Transform parent, float xMin, float xMax, float zMin, float zMax)
            {
                GameObject platform = PlacePrefab(_platform, name, parent, new Vector3((xMin + xMax) * 0.5f, -0.15f, (zMin + zMax) * 0.5f));
                platform.transform.localScale = new Vector3(xMax - xMin, 0.3f, zMax - zMin);
                RecordOverrides(platform.transform);
            }

            /// <summary>A straight bridge along +Z: equal tiles with equal gaps, from zMin to zMax.</summary>
            private void Bridge(string name, float x, float width, float zMin, float zMax, int tiles, float gap)
            {
                Transform parent = Child(_play, name);
                float length = (zMax - zMin - gap * (tiles + 1)) / tiles;
                for (int i = 0; i < tiles; i++)
                {
                    float start = zMin + gap + i * (length + gap);
                    Tile($"{name}_P{i + 1}", parent, x - width * 0.5f, x + width * 0.5f, start, start + length);
                }
            }

            private void AddPickup(string name, Vector3 position) => PlacePrefab(_candle, name, _play, position);

            private void AddBowl(string name, Vector3 position)
            {
                GameObject bowl = PlacePrefab(_bowl, name, _play, position);
                Transform dish = bowl.transform.Find("Bowl");
                if (dish == null || !dish.TryGetComponent(out MeshRenderer renderer)) return;
                renderer.sharedMaterial = _bowlMat;
                RecordOverrides(renderer);
            }

            private GameObject AddPlate(string name, Vector3 position)
            {
                GameObject plate = PlacePrefab(_plate, name, _play, position);
                var component = plate.GetComponent<PressurePlate>();
                var so = new SerializedObject(component);
                so.FindProperty("idleColor").colorValue = new Color(1f, 0.85f, 0.1f); // bright yellow: "step here"
                so.ApplyModifiedPropertiesWithoutUndo();
                RecordOverrides(component);
                return plate;
            }

            /// <summary>
            /// A porch-lantern checkpoint facing +Z (the way on), with a bright green pad so it reads
            /// as an interactable before it is lit. Most use a trigger radius above the prefab's 1.6 m
            /// so that every way past the checkpoint goes through it.
            /// </summary>
            private void AddCheckpoint(string name, Vector3 position, float radius, bool litAtStart)
            {
                GameObject checkpoint = PlacePrefab(_checkpoint, name, _play, position);
                if (checkpoint.TryGetComponent(out SphereCollider trigger) && !Mathf.Approximately(trigger.radius, radius))
                {
                    trigger.radius = radius;
                    RecordOverrides(trigger);
                }
                if (litAtStart)
                {
                    var component = checkpoint.GetComponent<Checkpoint>();
                    var so = new SerializedObject(component);
                    so.FindProperty("litAtStart").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    RecordOverrides(component);
                }

                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = name + "_Pad";
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                pad.transform.SetParent(_play, false);
                pad.transform.position = position + Vector3.up * 0.015f;
                pad.transform.localScale = new Vector3(1.4f, 0.015f, 1.4f);
                var renderer = pad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _checkpointPad;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            /// <summary>
            /// A ghost at its first patrol point, with waypoints named "{name}_Patrol_{n}". Ghosts
            /// in this level need a clear line of sight (walls, corn, gravestones and closed gates
            /// block it); the sandbox leaves that off.
            /// </summary>
            private void AddGhost(string name, float candySenseRange, params Vector2[] waypoints)
            {
                var points = new Object[waypoints.Length];
                for (int i = 0; i < waypoints.Length; i++)
                {
                    Transform point = Child(_play, $"{name}_Patrol_{i + 1}");
                    point.position = new Vector3(waypoints[i].x, 0f, waypoints[i].y);
                    points[i] = point;
                }

                GameObject ghost = PlacePrefab(_ghost, name, _play, new Vector3(waypoints[0].x, 0f, waypoints[0].y));
                var ai = ghost.GetComponent<GhostAI>();
                SetRefArray(ai, "patrolPoints", points);
                SetLayerMask(ai, "lineOfSightBlockers", 1 << 0); // Default
                SetFloat(ai, "candySenseRange", candySenseRange);
                RecordOverrides(ai);
            }

            private void AddSection(int index, string displayName, float xMin, float xMax, float z)
            {
                string id = _sectionIds[index];
                GameObject go = PlacePrefab(_section, "Section_" + id, _triggers, new Vector3((xMin + xMax) * 0.5f, 0f, z));
                go.transform.localScale = new Vector3((xMax - xMin) / 8f, 1f, 1f); // the prefab's box is 8 m wide
                RecordOverrides(go.transform);
                var trigger = go.GetComponent<SectionTrigger>();
                SetString(trigger, "sectionId", id);
                SetString(trigger, "displayName", displayName);
                RecordOverrides(trigger);
            }

            private void AddTutorial(string id, float xMin, float xMax, float z, string text)
            {
                GameObject go = PlacePrefab(_tutorial, "Tutorial_" + id, _triggers, new Vector3((xMin + xMax) * 0.5f, 0f, z));
                go.transform.localScale = new Vector3((xMax - xMin) / 8f, 1f, 1f);
                RecordOverrides(go.transform);
                var trigger = go.GetComponent<TutorialTrigger>();
                SetString(trigger, "tutorialId", id);
                SetString(trigger, "text", text);
                RecordOverrides(trigger);
            }
        }

        // ------------------------------------------------------------------ validation and pacing

        /// <summary>
        /// Checks the open scene and logs a report. Everything is read from the scene, so it stays
        /// valid after hand edits. Returns the number of problems.
        /// </summary>
        private static int ValidateOpenLevel(LevelSpec spec)
        {
            string[] sectionIds = spec.SectionIds;
            Physics.SyncTransforms();
            var problems = new List<string>();
            var report = new StringBuilder(2048);

            Scene scene = SceneManager.GetActiveScene();
            var ghosts = new List<GhostAI>();
            var plates = new List<PressurePlate>();
            var gates = new List<Gate>();
            var checkpoints = new List<Checkpoint>();
            var sections = new List<SectionTrigger>();
            var goals = new List<GoalTrigger>();
            var managers = new List<GameManager>();
            var loggers = new List<PlaytestLogger>();
            var dummies = new List<TestDummy>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ghosts.AddRange(root.GetComponentsInChildren<GhostAI>(true));
                plates.AddRange(root.GetComponentsInChildren<PressurePlate>(true));
                gates.AddRange(root.GetComponentsInChildren<Gate>(true));
                checkpoints.AddRange(root.GetComponentsInChildren<Checkpoint>(true));
                sections.AddRange(root.GetComponentsInChildren<SectionTrigger>(true));
                goals.AddRange(root.GetComponentsInChildren<GoalTrigger>(true));
                managers.AddRange(root.GetComponentsInChildren<GameManager>(true));
                loggers.AddRange(root.GetComponentsInChildren<PlaytestLogger>(true));
                dummies.AddRange(root.GetComponentsInChildren<TestDummy>(true));
            }

            // --- systems and triggers
            if (managers.Count != 1) problems.Add($"Expected 1 GameManager, found {managers.Count}.");
            if (loggers.Count != 1) problems.Add($"Expected 1 PlaytestLogger, found {loggers.Count}.");
            if (goals.Count != 1) problems.Add($"Expected 1 GoalTrigger, found {goals.Count}.");
            if (dummies.Count > 0) problems.Add("TestDummy must not be in a level.");
            foreach (string id in sectionIds)
            {
                int count = 0;
                foreach (SectionTrigger section in sections) if (section.SectionId == id) count++;
                if (count != 1) problems.Add($"Section id '{id}' is used by {count} SectionTriggers (expected 1).");
            }
            if (checkpoints.Count < sectionIds.Length) problems.Add($"Only {checkpoints.Count} checkpoints; every section needs one.");

            // --- ghosts: patrol points on the NavMesh, joined by complete paths, clear of plates and respawn points
            int patrolPoints = 0;
            float closestRespawn = float.MaxValue;
            float closestPlate = float.MaxValue;
            var homes = new Dictionary<GhostAI, Vector3>();
            foreach (GhostAI ghost in ghosts)
            {
                var points = new List<Vector3>();
                SerializedProperty array = new SerializedObject(ghost).FindProperty("patrolPoints");
                for (int i = 0; i < array.arraySize; i++)
                {
                    var point = array.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (point == null) problems.Add($"{ghost.name}: patrol point {i + 1} is empty.");
                    else points.Add(point.position);
                }
                if (points.Count == 0)
                {
                    problems.Add($"{ghost.name} has no patrol points.");
                    continue;
                }
                patrolPoints += points.Count;

                bool onMesh = true;
                for (int i = 0; i < points.Count; i++)
                {
                    if (NavMesh.SamplePosition(points[i], out NavMeshHit hit, 0.6f, NavMesh.AllAreas))
                    {
                        points[i] = hit.position;
                        continue;
                    }
                    onMesh = false;
                    problems.Add($"{ghost.name}: patrol point {i + 1} at {points[i]} is not on the NavMesh.");
                }
                if (!onMesh) continue;
                if (!NavMesh.SamplePosition(ghost.transform.position, out NavMeshHit home, 0.6f, NavMesh.AllAreas))
                {
                    problems.Add($"{ghost.name} itself is not standing on the NavMesh.");
                    continue;
                }
                homes[ghost] = home.position;

                var path = new NavMeshPath();
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 from = points[i];
                    Vector3 to = points[(i + 1) % points.Count];
                    if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    {
                        problems.Add($"{ghost.name}: no complete NavMesh path from patrol point {i + 1} to the next one.");
                        continue;
                    }
                    // The route the ghost really walks, corner to corner.
                    Vector3[] corners = path.corners;
                    for (int c = 0; c + 1 < corners.Length; c++)
                    {
                        foreach (PressurePlate plate in plates)
                        {
                            float d = DistanceToSegmentXZ(plate.transform.position, corners[c], corners[c + 1]);
                            closestPlate = Mathf.Min(closestPlate, d);
                            // Plate half-diagonal 1.4 m + ghost collider radius 0.55 m.
                            if (d < 2f) problems.Add($"{ghost.name}'s patrol passes {d:0.0} m from {plate.name}: a drifting ghost would press it.");
                        }
                    }
                }

                foreach (Checkpoint checkpoint in checkpoints)
                {
                    foreach (Vector3 point in points)
                    {
                        float d = DistanceXZ(point, checkpoint.RespawnPosition);
                        closestRespawn = Mathf.Min(closestRespawn, d);
                        if (d < GhostNoticeRange)
                        {
                            problems.Add($"{checkpoint.name}: respawn point is {d:0.0} m from a patrol point of {ghost.name} (needs {GhostNoticeRange} m).");
                        }
                    }
                }
            }

            // --- gates: wired, and each plate reachable by at least one ghost over the NavMesh
            foreach (Gate gate in gates)
            {
                IReadOnlyList<PressurePlate> gatePlates = gate.Plates;
                if (gatePlates.Count == 0) problems.Add($"{gate.name} has no plates.");
                if (gate.RequiredPlates > gatePlates.Count) problems.Add($"{gate.name} needs {gate.RequiredPlates} plates but only {gatePlates.Count} are wired.");
                foreach (PressurePlate plate in gatePlates)
                {
                    if (plate == null)
                    {
                        problems.Add($"{gate.name} has an empty plate slot.");
                        continue;
                    }
                    var reachedBy = new List<string>();
                    var path = new NavMeshPath();
                    if (NavMesh.SamplePosition(plate.transform.position, out NavMeshHit onPlate, 1f, NavMesh.AllAreas))
                    {
                        foreach (KeyValuePair<GhostAI, Vector3> ghost in homes)
                        {
                            if (NavMesh.CalculatePath(ghost.Value, onPlate.position, NavMesh.AllAreas, path)
                                && path.status == NavMeshPathStatus.PathComplete) reachedBy.Add(ghost.Key.name);
                        }
                    }
                    if (reachedBy.Count == 0) problems.Add($"{gate.name}: no ghost can reach {plate.name} over the NavMesh.");
                    report.Append($"\n  {gate.name} <- {plate.name}: reachable by {reachedBy.Count} ghost(s)");
                }
            }

            // --- respawn points on solid, baked ground
            foreach (Checkpoint checkpoint in checkpoints)
            {
                if (!NavMesh.SamplePosition(checkpoint.RespawnPosition, out _, 0.6f, NavMesh.AllAreas))
                {
                    problems.Add($"{checkpoint.name}: respawn point is not on baked ground.");
                }
            }

            // --- the walking route and the pacing estimate
            float totalSeconds = 0f;
            float totalLength = 0f;
            float sprintSeconds = 0f;
            var pacing = new StringBuilder(512);
            GameObject notes = GameObject.Find(GoldenPathRootName);
            Transform golden = notes != null ? notes.transform.Find("GoldenPath") : null;
            if (golden == null)
            {
                problems.Add($"'{GoldenPathRootName}/GoldenPath' is missing: no route check and no pacing estimate.");
            }
            else
            {
                for (int s = 0; s < sectionIds.Length; s++)
                {
                    Transform section = golden.Find(sectionIds[s]);
                    if (section == null || section.childCount < 2)
                    {
                        problems.Add($"GoldenPath/{sectionIds[s]} needs at least two points.");
                        continue;
                    }
                    var points = new Vector3[section.childCount];
                    for (int i = 0; i < points.Length; i++) points[i] = section.GetChild(i).position;

                    float length = CheckRoute(sectionIds[s], points, spec.AllowedGap[s], problems);
                    float seconds = length / PlayerWalkSpeed + spec.WaitSeconds[s];
                    totalLength += length;
                    totalSeconds += seconds;
                    sprintSeconds += length / PlayerSprintSpeed + spec.WaitSeconds[s];
                    pacing.Append($"\n  {sectionIds[s],-16} path {length,6:0.0} m / {PlayerWalkSpeed:0} m/s = {length / PlayerWalkSpeed,5:0.0} s"
                                  + $"  + wait {spec.WaitSeconds[s],3:0} s  = {seconds,5:0.0} s");
                }
                pacing.Append($"\n  TOTAL            path {totalLength,6:0.0} m, {totalSeconds:0} s = {totalSeconds / 60f:0.0} min at walking speed"
                              + $" (sprinting everywhere: {sprintSeconds / 60f:0.0} min)");
                if (totalSeconds < spec.MinSeconds || totalSeconds > spec.MaxSeconds)
                {
                    problems.Add($"Pacing estimate {totalSeconds / 60f:0.0} min is outside the {spec.MinSeconds / 60f:0.#}-{spec.MaxSeconds / 60f:0.#} minute target.");
                }
            }

            var summary = new StringBuilder(1024);
            summary.Append($"[Lantern Keeper] {spec.SceneName} validation: {problems.Count} problem(s).");
            summary.Append($"\n  {sections.Count} sections, {checkpoints.Count} checkpoints, {ghosts.Count} ghosts with {patrolPoints} patrol points (all on the NavMesh: {(homes.Count == ghosts.Count ? "yes" : "NO")}),");
            summary.Append($" {plates.Count} plates, {gates.Count} gates.");
            if (closestRespawn < float.MaxValue) summary.Append($"\n  Closest patrol point to a respawn point: {closestRespawn:0.0} m. Closest patrol route to a plate: {closestPlate:0.0} m.");
            summary.Append(report);
            summary.Append("\nPacing estimate (path length / walk speed + waiting):").Append(pacing);
            foreach (string problem in problems) summary.Append("\n  PROBLEM: ").Append(problem);

            if (problems.Count == 0) Debug.Log(summary.ToString());
            else Debug.LogWarning(summary.ToString());
            return problems.Count;
        }

        /// <summary>
        /// Walks the route: every 0.25 m there must be something to stand on (ground, plate or a
        /// spirit platform), apart from the jump gaps, and nothing taller than a jump in the way
        /// (gate doors excepted). Returns the route length.
        /// </summary>
        private static float CheckRoute(string sectionId, Vector3[] points, float allowedGap, List<string> problems)
        {
            const int mask = 1 << 0; // Default: ground, walls, spirit platforms
            const float step = 0.25f;
            float length = 0f;
            float gap = 0f;
            Vector3 gapStart = Vector3.zero;

            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                float distance = DistanceXZ(a, b);
                if (distance < 0.001f) continue;
                length += distance;
                Vector3 direction = (b - a) / distance;

                for (float d = 0f; d <= distance; d += step)
                {
                    Vector3 p = a + direction * d;
                    bool supported = Physics.Raycast(new Vector3(p.x, 1.2f, p.z), Vector3.down, 1.9f, mask, QueryTriggerInteraction.Ignore);
                    if (supported)
                    {
                        if (gap > allowedGap) problems.Add($"{sectionId}: {gap:0.0} m with nothing to stand on, starting near {gapStart} (allowed {allowedGap:0.0} m).");
                        gap = 0f;
                    }
                    else
                    {
                        if (gap <= 0f) gapStart = p;
                        gap += step;
                    }
                }

                // Chest height: crates and fences pass under it, walls and corn do not.
                var origin = new Vector3(a.x, 1.25f, a.z);
                foreach (RaycastHit hit in Physics.SphereCastAll(origin, 0.2f, direction, distance, mask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<Gate>() != null) continue; // a closed gate is meant to be there
                    problems.Add($"{sectionId}: the route between points {i} and {i + 1} is blocked by '{hit.collider.name}'.");
                }
            }
            if (gap > allowedGap) problems.Add($"{sectionId}: the route ends over a {gap:0.0} m gap.");
            return length;
        }

        private static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            var point = new Vector2(p.x, p.z);
            var start = new Vector2(a.x, a.z);
            Vector2 along = new Vector2(b.x, b.z) - start;
            float t = along.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - start, along) / along.sqrMagnitude) : 0f;
            return Vector2.Distance(point, start + along * t);
        }
    }
}
