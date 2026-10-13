using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper.EditorTools
{
    // The two scenes around Level01: the tutorial before it and Level02 after it. LevelFlow.cs
    // (runtime) holds the play order: Level00_Tutorial -> Level01_PumpkinPatch -> Level02_Orchard.
    //
    // Like Level01, each scene is built ONCE and then tuned by hand:
    //   - "Create ..." refuses to run if the scene file already exists.
    //   - "Rebuild ... (DESTRUCTIVE)" asks first (Editor) or needs -forceRebuild (batch mode).
    //   - "Validate ... (read-only)" only reads the scene. Run it after moving things by hand.
    // "Set Up Tutorial and Level02" is the first-time button: it regenerates the HUD and menu
    // prefabs (run clock, Continue and Skip buttons), creates whichever of the two scenes is
    // missing and puts the scenes in play order in the build list. It never touches Level01.
    //
    // Both are made from the same kit as Level01 and dressed straight away
    // (LanternKeeperSetup.DressLevels.cs), so they are saved with their scenery.
    // Layout: +Z is the way on, ground tops at y = 0, the player starts at the origin.
    public static partial class LanternKeeperSetup
    {
        private static readonly string[] TutorialSectionIds = { "S1_Yard", "S2_Creek", "S3_Graves", "S4_Gate" };
        private static readonly LevelSpec TutorialSpec = new LevelSpec
        {
            SceneName = "Level00_Tutorial",
            SectionIds = TutorialSectionIds,
            WaitSeconds = new[] { 12f, 8f, 20f, 50f },
            AllowedGap = new[] { 3.3f, 0.35f, 0.35f, 0.35f }, // S1: the sprint-jump ditch
            MinSeconds = 120f,
            MaxSeconds = 300f
        };

        private static readonly string[] Level02SectionIds = { "S1_OrchardRows", "S2_MillPond", "S3_CiderPress", "S4_Barn" };
        private static readonly LevelSpec Level02Spec = new LevelSpec
        {
            SceneName = "Level02_Orchard",
            SectionIds = Level02SectionIds,
            WaitSeconds = new[] { 50f, 45f, 80f, 45f },
            AllowedGap = new[] { 0.35f, 2.3f, 0.35f, 2.3f }, // S2 and S4: the 2 m jump gaps
            MinSeconds = 240f,
            MaxSeconds = 540f
        };

        // ------------------------------------------------------------------ menu items

        /// <summary>First-time setup, safe to run again. Also the batch-mode entry point (-executeMethod).</summary>
        [MenuItem(Menu + "Set Up Tutorial and Level02", priority = 21)]
        public static void SetUpTutorialAndLevel02()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateUiPrefabs();
            if (!File.Exists(TutorialSpec.ScenePath)) BuildLevel(TutorialSpec);
            if (!File.Exists(Level02Spec.ScenePath)) BuildLevel(Level02Spec);
            EnsureBuildSceneOrder();
            AssetDatabase.SaveAssets();
            Debug.Log("[Lantern Keeper] Tutorial and Level02 are set up. Play order in the build list: tutorial, Level01, Level02.");
        }

        [MenuItem(Menu + "Create Tutorial Level", priority = 45)]
        public static void CreateTutorialLevel() => CreateLevel(TutorialSpec);

        [MenuItem(Menu + "Create Level02", priority = 46)]
        public static void CreateLevel02() => CreateLevel(Level02Spec);

        [MenuItem(Menu + "Validate Tutorial (read-only)", priority = 47)]
        public static void ValidateTutorialMenuItem() => ValidateLevel(TutorialSpec);

        [MenuItem(Menu + "Validate Level02 (read-only)", priority = 48)]
        public static void ValidateLevel02MenuItem() => ValidateLevel(Level02Spec);

        [MenuItem(Menu + "Rebuild Tutorial Level (DESTRUCTIVE)", priority = 49)]
        public static void RebuildTutorialLevel() => RebuildLevel(TutorialSpec);

        [MenuItem(Menu + "Rebuild Level02 (DESTRUCTIVE)", priority = 50)]
        public static void RebuildLevel02() => RebuildLevel(Level02Spec);

        private static void CreateLevel(LevelSpec spec)
        {
            if (File.Exists(spec.ScenePath))
            {
                Debug.LogWarning($"[Lantern Keeper] {spec.ScenePath} already exists and was NOT touched. " +
                                 "The level is tuned by hand; use its 'Rebuild ... (DESTRUCTIVE)' item to throw it away and start again.");
                return;
            }
            BuildLevel(spec);
        }

        private static void RebuildLevel(LevelSpec spec)
        {
            if (Application.isBatchMode)
            {
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), ForceRebuildArg) < 0)
                {
                    Debug.LogError($"[Lantern Keeper] Rebuild refused: in batch mode it only runs with the {ForceRebuildArg} command-line flag. {spec.ScenePath} was NOT touched.");
                    return;
                }
            }
            else if (File.Exists(spec.ScenePath) && !EditorUtility.DisplayDialog("Rebuild " + spec.SceneName,
                         $"This DELETES everything you changed by hand in\n{spec.ScenePath}\nand generates the greybox again from code.\n\nThis cannot be undone (except through git).",
                         "Delete and rebuild", "Cancel"))
            {
                return;
            }
            BuildLevel(spec);
        }

        /// <summary>Read-only: checks the level and prints the pacing estimate. Returns the number of problems.</summary>
        private static int ValidateLevel(LevelSpec spec)
        {
            if (!File.Exists(spec.ScenePath))
            {
                Debug.LogWarning($"[Lantern Keeper] {spec.ScenePath} does not exist yet; run 'Set Up Tutorial and Level02'.");
                return 1;
            }
            if (SceneManager.GetActiveScene().path != spec.ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return 1;
                EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
            }
            return ValidateOpenLevel(spec);
        }

        // ------------------------------------------------------------------ build

        private static bool BuildLevel(LevelSpec spec)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            EnsureTagsAndLayers();
            if (!EnsureTmpEssentials())
            {
                Debug.LogError("[Lantern Keeper] TMP Essential Resources are still importing. Run 'Run All Setup' once, then create the level again.");
                return false;
            }

            // The prefabs are used as they are: regenerating them here would be a surprise.
            var kit = new LevelKit(spec.SectionIds, true);
            if (!kit.LoadPrefabs())
            {
                Debug.LogError($"[Lantern Keeper] {spec.SceneName} not built: a prefab is missing. Run 'Run All Setup' and check the Console.");
                return false;
            }
            kit.MakeMaterials();

            EnsureFolder(ScenesDir);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            kit.CreateGroups();
            if (spec == TutorialSpec) kit.BuildTutorial();
            else kit.BuildLevel02();
            kit.BuildPlayerAndSystems();
            kit.BuildGoldenPath();

            if (!BakeLevelNavMesh(kit.Environment, spec.SceneName))
            {
                Debug.LogError($"[Lantern Keeper] {spec.SceneName} not saved: the NavMesh bake failed.");
                return false;
            }
            if (!DressOpenLevel(scene, spec))
            {
                Debug.LogError($"[Lantern Keeper] {spec.SceneName} not saved: it could not be dressed.");
                return false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, spec.ScenePath))
            {
                Debug.LogError($"[Lantern Keeper] Could not save {spec.ScenePath}.");
                return false;
            }
            EnsureBuildSceneOrder();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lantern Keeper] {spec.SceneName} greybox saved to {spec.ScenePath}.");

            // Opened again from disk, for the same reason as Level01: the check should see what was saved.
            EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
            ValidateOpenLevel(spec);
            return true;
        }

        private sealed partial class LevelKit
        {
            // ================================================================== TUTORIAL
            // One lesson at a time, in the order Level01 uses them, with nothing that kills in one
            // mistake, with one exception: the creek. Water puts the lantern out, so the first
            // bridge is also the first place the light can be lost (the checkpoint is 16 m back).
            // Section names are left empty so that a section banner never replaces a lesson on the HUD.
            //
            //   S1 Yard    z  -8..44    jump, sprint-jump, the candle, checkpoints      (14 m strip)
            //   S2 Creek   z  44..64    spirit platforms                                (14 m strip)
            //   S3 Graves  z  64..112   one ghost: the flare                            (28 m strip)
            //   S4 Gate    z 112..198   candy: lure a ghost, then a ghost on a plate; the goal
            //
            // S4 is Level01's Candy Porch and one-plate yard moved 84 m south, so its distances
            // (plate to doorway, ghost route to plate) are the ones already playtested.

            public void BuildTutorial()
            {
                BuildLighting("S4_House_Beacon (goal light)", new Vector3(0f, 4.5f, 187.5f));
                BuildTutorialYard();
                BuildTutorialCreek();
                BuildTutorialGraves();
                BuildTutorialGate();

                Transform parent = Child(Environment, "Boundaries (invisible)");
                float n = StripNarrow, w = StripWide, t = 0.5f;
                Invisible("Boundary_South", parent, -n - t, n + t, -8f - t, -8f);
                Invisible("Boundary_S1S2_West", parent, -n - t, -n, -8f, 64f);
                Invisible("Boundary_S1S2_East", parent, n, n + t, -8f, 64f);
                Invisible("Boundary_Step_West", parent, -w - t, -n, 64f - t, 64f);
                Invisible("Boundary_Step_East", parent, n, w + t, 64f - t, 64f);
                Invisible("Boundary_S3S4_West", parent, -w - t, -w, 64f, 198f);
                Invisible("Boundary_S3S4_East", parent, w, w + t, 64f, 198f);
                Invisible("Boundary_North", parent, -w - t, w + t, 198f, 198f + t);
            }

            private void BuildTutorialYard()
            {
                BeginSection(_sectionIds[0]);
                float w = StripNarrow;

                Ground("S1_Ground_Start", -w, w, -8f, 18f);
                // The same ditch as Level01: 3 m wide, 1 m deep. A walking jump falls in, a
                // sprinting jump clears it, and you can jump back out.
                Slab("S1_Ditch_Floor", _env, -w, w, 18f, 21f, -1f, 1f, _ground);
                Ground("S1_Ground_ToCreek", -w, w, 21f, 52f);

                Block("S1_Crates_Row (jump, 0.5 m)", -w, w, 8f, 9f, 0.5f, _obstacle);
                AddPickup("S1_CandlePickup_Path", new Vector3(0f, 0.7f, 28f));

                // A fence with a 4 m opening, and the checkpoint in the opening: it cannot be missed.
                Block("S1_Fence_West", -w, -2f, 36f, 36.5f, 1.7f, _obstacle);
                Block("S1_Fence_East", 2f, w, 36f, 36.5f, 1.7f, _obstacle);

                AddCheckpoint("S1_Checkpoint_Start", new Vector3(3f, 0f, 1.5f), 1.6f, true);
                AddCheckpoint("S1_Checkpoint_Fence", new Vector3(1.2f, 0f, 34.5f), 3.6f, false);
                AddSection(0, "", -w, w, 1.5f);
                AddTutorial("T1_Jump", -w, w, 4.5f, "{jump} to jump. Hop over the crates.");
                AddTutorial("T2_Sprint", -w, w, 12f, "Hold Left Shift to sprint. A sprinting jump clears the ditch.");
                AddTutorial("T3_Candle", -w, w, 24f, "Your candle burns down as you go. Candle stubs refill it.");
                // Only as wide as the opening and inside the checkpoint's trigger, so it comes
                // after "Porch lantern lit" and is not replaced by it.
                AddTutorial("T4_Checkpoint", -2f, 2f, 35.6f,
                    "Porch lanterns are checkpoints. If your light goes out, you wake up at the last one you lit.");

                Route(0, V(0, 0), V(0, 8.5f), V(0, 18), V(0, 21), V(0, 28), V(0, 36.25f), V(0, 44));
            }

            private void BuildTutorialCreek()
            {
                BeginSection(_sectionIds[1]);
                float w = StripNarrow;

                // Level01's first bridge, same numbers: four platforms over the creek and a lamp
                // that shows the first one. The water puts the lantern out: the bridge is the only way.
                Slab("S2_Creek_Floor (shallow)", _env, -w, w, 52f, 58f, -ShallowDepth, 0.5f, _shallow);
                Bridge("S2_Bridge", 0f, 2f, 52f, 58f, 4, 0.12f);
                PlacePrefab(_lamp, "S2_PorchLamp_FirstPlatform", _play, new Vector3(2.6f, 0f, 50.4f));
                Ground("S2_Ground_FarBank", -w, w, 58f, 64f);

                AddSection(1, "", -w, w, 44.5f);
                AddTutorial("T5_SpiritPlatforms", -w, w, 46.5f,
                    "Spirit platforms exist only inside light. Walk up and your lantern shows the way across. Water puts your lantern out.");

                Route(1, V(0, 44), V(0, 52), V(0, 58), V(0, 64));
            }

            private void BuildTutorialGraves()
            {
                BeginSection(_sectionIds[2]);
                float w = StripWide;

                Ground("S3_Ground_Graves", -w, w, 64f, 112f);
                Block("S3_Wall_West", -w, -2f, 74f, 75f, WallHeight, _wall);
                Block("S3_Wall_East", 2f, w, 74f, 75f, WallHeight, _wall);

                // Cover on the way to the ghost; the centre line is left clear.
                Vector2[] stones = { V(-8, 84), V(-3, 84), V(4, 84), V(9, 84), V(-6, 89), V(6, 89) };
                for (int i = 0; i < stones.Length; i++) Gravestone($"S3_Gravestone_{i + 1:00}", stones[i]);

                // One ghost on a long east-west patrol, 21 m past the wall: it can be watched
                // from the opening. The yard is wide enough for a flared ghost to be pushed aside.
                AddGhost("S3_Ghost_A", 9f, V(-9, 96), V(9, 96));
                AddPickup("S3_CandlePickup_AfterGhost", new Vector3(-6f, 0.7f, 105f));

                AddCheckpoint("S3_Checkpoint_Wall", new Vector3(1.2f, 0f, 72.2f), 3.6f, false);
                AddSection(2, "", -w, w, 65.5f);
                AddTutorial("T6_Flare", -w, w, 79f,
                    "Hold {flare} to flare. Ghosts flee from it, but it burns your candle 8x faster.");

                Route(2, V(0, 64), V(0, 74.5f), V(0, 105), V(-6, 105), V(0, 112));
            }

            private void BuildTutorialGate()
            {
                BeginSection(_sectionIds[3]);
                float w = StripWide;

                Ground("S4_Ground_PorchAndLure", -w, w, 112f, 149f);
                Ground("S4_Ground_PlateYard", -w, w, 149f, 178f);
                Ground("S4_Ground_HouseYard", -w, w, 178f, 198f);

                // The porch: rails funnel the player between the porch lantern and the candy bowl.
                Decal("S4_Porch_Floor", -5f, 5f, 115f, 123f, _porch);
                Block("S4_Porch_Rail_West", -w, -2.5f, 123f, 124f, 1.7f, _obstacle);
                Block("S4_Porch_Rail_East", 2.5f, w, 123f, 124f, 1.7f, _obstacle);
                AddCheckpoint("S4_Checkpoint_Porch", new Vector3(1.4f, 0f, 121f), 4f, false);
                AddBowl("S4_CandyBowl_Porch", new Vector3(-1.5f, 0f, 121.3f));

                // Lure: a ghost paces in front of a 3.2 m opening. Candy thrown to one side pulls it away.
                Block("S4_Wall_NarrowPath_West", -w, -1.6f, 148f, 149f, WallHeight, _wall);
                Block("S4_Wall_NarrowPath_East", 1.6f, w, 148f, 149f, WallHeight, _wall);
                AddGhost("S4_Ghost_B", 9f, V(-3.5f, 143.5f), V(3.5f, 143.5f));

                // The one-plate gate. The plate is 16 m from the doorway, so standing on it
                // yourself is not enough: a ghost has to hold it down. The ghost's route stays
                // 8.5 m from the plate, and its candy sense is 11 m so candy on the plate is found.
                AddBowl("S4_CandyBowl_Gate", new Vector3(3.5f, 0f, 152.5f));
                GameObject plate = AddPlate("S4_Plate", new Vector3(8f, 0f, 162f));
                AddPickup("S4_CandlePickup_ByPlate", new Vector3(11.5f, 0.7f, 162f));
                AddGhost("S4_Ghost_C", PuzzleCandySense, V(-0.5f, 162), V(-8, 156), V(-8, 170));
                Block("S4_Wall_Gate_West", -w, -1.9f, 177.75f, 178.25f, 3.2f, _wall);
                Block("S4_Wall_Gate_East", 1.9f, w, 177.75f, 178.25f, 3.2f, _wall);
                GameObject gate = PlacePrefab(_gate, "S4_Gate", _play, new Vector3(0f, 0f, 178f));
                ConfigureGate(gate, 1, false, plate);

                // The house: a dark block with the goal door in warm orange.
                Block("S4_Building_House", -8f, 8f, 190.3f, 198f, 5f, _wall);
                Decal("S4_House_Porch (goal)", -3f, 3f, 185.5f, 190.3f, _goalMat);
                PlacePrefab(_goal, "S4_Goal_HouseDoor", _triggers, new Vector3(0f, 0f, 190f));

                AddSection(3, "", -w, w, 113f);
                AddTutorial("T7_Candy", -w, w, 119.5f,
                    "Take candy from the bowl. {throw} throws it, {drop} drops it. Ghosts stop to eat.");
                AddTutorial("T8_Plate", -w, w, 154.5f,
                    "A plate opens its gate while something stands on it. Throw candy onto the plate and a ghost will hold it down.");
                AddTutorial("T9_Goal", -w, w, 180.5f, "That is everything. Reach the door with your candle still lit.");

                Route(3, V(0, 112), V(0, 120), V(0, 123.5f), V(4, 132), V(4, 142), V(0, 148.5f), V(4.3f, 152),
                    V(8, 162), V(11.5f, 162), V(4, 170), V(0, 177), V(0, 188.6f));
            }

            // ================================================================== LEVEL02: THE ORCHARD
            // No new rules, new combinations, and much less room for error than Level01: more
            // ghosts than safe places to stand, bridges with jumps and corners over deadly pits,
            // and four checkpoints for the whole level. 28 m strip.
            //
            //   S1 Orchard Rows  z  -8..62    a hedge maze: one way through, two dead ends, three
            //                                 ghosts in it and a fourth crossing where it comes out
            //   S2 Mill Pond     z  62..126   bridge A (a jump, then a corner) to an island with two
            //                                 ghosts and no checkpoint, then bridge B (two jumps).
            //                                 Every jump is 2 m: only a sprinting jump clears it
            //   S3 Cider Press   z 126..206   a ghost on the way in; a two-plate gate with ONE ghost
            //                                 (the player holds the other plate); a one-plate gate
            //                                 with a second ghost walking past the doorway
            //   S4 Barn          z 206..276   three ghosts, a dog-leg bridge with two jumps, the goal
            //
            // The gate yards and the pair use Level01's playtested distances; what is new is how
            // they are combined. Expect to move things after the first playtest.

            public void BuildLevel02()
            {
                BuildLighting("S4_Barn_Beacon (goal light)", new Vector3(0f, 4.5f, 265.5f));
                BuildOrchardRows();
                BuildMillPond();
                BuildCiderPress();
                BuildBarn();

                Transform parent = Child(Environment, "Boundaries (invisible)");
                float w = StripWide, t = 0.5f;
                Invisible("Boundary_South", parent, -w - t, w + t, -8f - t, -8f);
                Invisible("Boundary_West", parent, -w - t, -w, -8f, 276f);
                Invisible("Boundary_East", parent, w, w + t, -8f, 276f);
                Invisible("Boundary_North", parent, -w - t, w + t, 276f, 276f + t);
            }

            private void BuildOrchardRows()
            {
                BeginSection(_sectionIds[0]);
                float w = StripWide;

                Ground("S1_Ground_Orchard", -w, w, -8f, 70f);

                // The hedge maze: six rows across the strip with 5 m corridors between them, one
                // way through, and a ghost walking three of the corridors.
                //   in at the east end of row 1, west along corridor 1 (ghost A),
                //   round the west end of row 2, east along corridor 2 to the gap at x -3..1
                //   (carry on east and it is a dead end with a candle stub),
                //   west along corridor 3 (ghost B) to the gap at the west end of row 4
                //   (the east gap of row 4 leads to a dead end with ghost C and another stub),
                //   east along corridor 4 to the gap at x -6..-2, and out through the middle of row 6.
                // A fourth ghost crosses 6 m past the way out.
                Corn("S1_Hedge_Row1", -w, 9.5f, 14f);
                Corn("S1_Hedge_Row2", -9.5f, w, 20f);
                Corn("S1_Hedge_Row3_West", -w, -3f, 26f);
                Corn("S1_Hedge_Row3_East", 1f, w, 26f);
                Corn("S1_Hedge_Row4_Mid", -10f, 5f, 32f);
                Corn("S1_Hedge_Row4_East", 9.5f, w, 32f);
                Corn("S1_Hedge_Row5_West", -w, -6f, 38f);
                Corn("S1_Hedge_Row5_East", -2f, w, 38f);
                Corn("S1_Hedge_Row6_West", -w, -2f, 43f);
                Corn("S1_Hedge_Row6_East", 2f, w, 43f);
                Block("S1_Hedge_Stub_Corridor2", 9f, 10f, 21f, 26f, WallHeight, _corn);
                Block("S1_Hedge_Stub_Corridor4", 0f, 1f, 33f, 38f, WallHeight, _corn);
                AddGhost("S1_Ghost_A", 9f, V(-11, 17.5f), V(6, 17.5f));
                AddGhost("S1_Ghost_B", 9f, V(-12, 29.5f), V(12, 29.5f));
                AddGhost("S1_Ghost_C", 9f, V(4, 35.5f), V(12, 35.5f));
                AddGhost("S1_Ghost_D", 9f, V(-9, 50), V(9, 50));
                AddPickup("S1_CandlePickup_Corridor2DeadEnd", new Vector3(7f, 0.7f, 23.5f));
                AddPickup("S1_CandlePickup_Corridor4DeadEnd", new Vector3(13f, 0.7f, 37f));
                AddPickup("S1_CandlePickup_AfterMaze", new Vector3(4f, 0.7f, 56f));

                AddCheckpoint("S1_Checkpoint_Start", new Vector3(3f, 0f, 1.5f), 1.6f, true);
                AddBowl("S1_CandyBowl_Start", new Vector3(-3f, 0f, 6f));
                AddSection(0, "Orchard Rows", -w, w, 1.5f);

                Route(0, V(0, 0), V(0, 10), V(11.75f, 12), V(11.75f, 17.5f), V(-11.75f, 17.5f), V(-11.75f, 23.5f), V(-1, 23.5f),
                    V(-1, 29.5f), V(-12, 29.5f), V(-12, 35.5f), V(-4, 35.5f), V(-4, 41), V(0, 41), V(0, 44.5f), V(4, 56), V(0, 62));
            }

            private void BuildMillPond()
            {
                BeginSection(_sectionIds[1]);
                float w = StripWide;

                // Bridge A, 14 m over a deadly pit: north, a 2 m jump (sprint: a walking jump is
                // 1.6 m) onto a 4.6 m run, a corner east, north again. The checkpoint's lamp shows
                // its first tiles and no more.
                Slab("S2_PitA_Floor (deadly)", _env, -w, w, 70f, 84f, -PitDepth, 0.5f, _hazard);
                HazardEdge("S2_PitA_Edge_South", -w, w, 69.5f, 70f);
                HazardEdge("S2_PitA_Edge_North", -w, w, 84f, 84.5f);
                Transform bridgeA = Child(_play, "S2_BridgeA (jump, then a corner)");
                Tile("S2_BridgeA_A1", bridgeA, -7f, -5f, 70.05f, 71.5f);
                Tile("S2_BridgeA_A2", bridgeA, -7f, -5f, 71.55f, 73f);
                Tile("S2_BridgeA_B1", bridgeA, -7f, -5f, 75f, 77.3f);
                Tile("S2_BridgeA_B2", bridgeA, -7f, -5f, 77.35f, 79.65f);
                Tile("S2_BridgeA_E1", bridgeA, -4.9f, -2.9f, 77.65f, 79.65f);
                Tile("S2_BridgeA_E2", bridgeA, -2.8f, -0.8f, 77.65f, 79.65f);
                Tile("S2_BridgeA_E3", bridgeA, -0.7f, 1.3f, 77.65f, 79.65f);
                Tile("S2_BridgeA_N1", bridgeA, -0.7f, 1.3f, 79.75f, 81.85f);
                Tile("S2_BridgeA_N2", bridgeA, -0.7f, 1.3f, 81.9f, 83.98f);
                AddCheckpoint("S2_Checkpoint_BridgeA", new Vector3(-8f, 0f, 67.5f), 4f, false);

                // The island has no checkpoint: falling from bridge B or losing the light here
                // means crossing bridge A again. One ghost walks in front of bridge B, 7 m from
                // the bridge head, so nobody reaches the bridge unseen; a second keeps the west
                // side and its candle stub. Candy sends a ghost away; so does a flare, but a
                // flare on a bridge erases the bridge. Ghosts cannot follow onto platforms.
                Ground("S2_Ground_Island", -w, w, 84f, 100f);
                Block("S2_Crates_IslandSouth", -1f, 1f, 89f, 90f, 1.7f, _obstacle);
                Block("S2_Crates_IslandMid", -3f, -1f, 94f, 95f, 1.7f, _obstacle);
                AddGhost("S2_Ghost_E", 9f, V(4, 93), V(11, 93));
                AddGhost("S2_Ghost_F", 9f, V(-8, 90), V(-8, 97));
                AddPickup("S2_CandlePickup_IslandCorner", new Vector3(-11f, 0.7f, 97f));

                // Bridge B: 16 m in three runs with two 2 m jumps between them.
                PlacePrefab(_lamp, "S2_PorchLamp_BridgeB", _play, new Vector3(10.2f, 0f, 98.6f));
                Slab("S2_PitB_Floor (deadly)", _env, -w, w, 100f, 116f, -PitDepth, 0.5f, _hazard);
                HazardEdge("S2_PitB_Edge_South", -w, w, 99.5f, 100f);
                HazardEdge("S2_PitB_Edge_North", -w, w, 116f, 116.5f);
                Transform bridgeB = Child(_play, "S2_BridgeB (two jumps)");
                Tile("S2_BridgeB_A1", bridgeB, 6.8f, 9.2f, 100.05f, 101.65f);
                Tile("S2_BridgeB_A2", bridgeB, 6.8f, 9.2f, 101.7f, 103.3f);
                Tile("S2_BridgeB_B1", bridgeB, 6.8f, 9.2f, 105.3f, 107.42f);
                Tile("S2_BridgeB_B2", bridgeB, 6.8f, 9.2f, 107.47f, 109.6f);
                Tile("S2_BridgeB_C1", bridgeB, 6.8f, 9.2f, 111.6f, 113.77f);
                Tile("S2_BridgeB_C2", bridgeB, 6.8f, 9.2f, 113.82f, 115.98f);

                Ground("S2_Ground_FarBank", -w, w, 116f, 126f);
                AddCheckpoint("S2_Checkpoint_FarBank", new Vector3(9.5f, 0f, 119f), 4.5f, false);

                AddSection(1, "Mill Pond", -w, w, 62.5f);

                Route(1, V(0, 62), V(-6, 69), V(-6, 78.65f), V(0.3f, 78.65f), V(0.3f, 84.5f), V(-11, 97), V(8, 99),
                    V(8, 116.5f), V(8, 126));
            }

            private void BuildCiderPress()
            {
                BeginSection(_sectionIds[2]);
                float w = StripWide;

                Ground("S3_Ground_TwoPlateYard", -w, w, 126f, 166f);
                Ground("S3_Ground_OnePlateYard", -w, w, 166f, 206f);

                // A ghost crosses the way in, 2 m from the candy bowl: the candy has to be earned.
                AddBowl("S3_CandyBowl_TwoPlate", new Vector3(-1.5f, 0f, 131f));
                AddGhost("S3_Ghost_G", 9f, V(-9, 133), V(9, 133));

                // Two plates, one ghost. The gate needs both at once and then stays open: candy
                // puts the ghost on one plate (it eats for 4 s) while the player stands on the
                // other. That ghost's route is 9 m north of both plates, out of noticing range.
                GameObject plateA = AddPlate("S3_Plate_TwoA", new Vector3(-7f, 0f, 146f));
                GameObject plateB = AddPlate("S3_Plate_TwoB", new Vector3(7f, 0f, 146f));
                AddGhost("S3_Ghost_H", PuzzleCandySense, V(-8.5f, 155), V(8.5f, 155));
                AddPickup("S3_CandlePickup_TwoPlate", new Vector3(12f, 0.7f, 140f));
                Block("S3_Wall_GateTwo_West", -w, -1.9f, 165.75f, 166.25f, 3.2f, _wall);
                Block("S3_Wall_GateTwo_East", 1.9f, w, 165.75f, 166.25f, 3.2f, _wall);
                GameObject gateTwo = PlacePrefab(_gate, "S3_Gate_TwoPlate", _play, new Vector3(0f, 0f, 166f));
                ConfigureGate(gateTwo, 2, true, plateA, plateB);

                // A corridor behind the gate. Unlike Level01 it has no checkpoint: the two-plate
                // gate stays open, so losing the light in the next yard costs the walk back.
                Block("S3_Hedge_West", -w, -2.5f, 166.25f, 174f, WallHeight, _corn);
                Block("S3_Hedge_East", 2.5f, w, 166.25f, 174f, WallHeight, _corn);

                // Level01's one-plate yard, mirrored: the plate is 16 m from the doorway and the
                // gate shuts when the plate is released, so only a ghost can hold it. A second
                // ghost walks east of the doorway and sees it from the near end of its route:
                // the doorway is only safe while that ghost is walking away.
                AddBowl("S3_CandyBowl_OnePlate", new Vector3(-3.5f, 0f, 177.5f));
                GameObject plateOne = AddPlate("S3_Plate_One", new Vector3(-8f, 0f, 187f));
                AddPickup("S3_CandlePickup_ByPlate", new Vector3(-11.5f, 0.7f, 187f));
                AddGhost("S3_Ghost_I", PuzzleCandySense, V(0.5f, 187), V(8, 181), V(8, 195));
                AddGhost("S3_Ghost_J", 9f, V(3, 197), V(10, 197));
                Block("S3_Wall_GateOne_West", -w, -1.9f, 202.75f, 203.25f, 3.2f, _wall);
                Block("S3_Wall_GateOne_East", 1.9f, w, 202.75f, 203.25f, 3.2f, _wall);
                GameObject gateOne = PlacePrefab(_gate, "S3_Gate_OnePlate", _play, new Vector3(0f, 0f, 203f));
                ConfigureGate(gateOne, 1, false, plateOne);

                AddSection(2, "Cider Press", -w, w, 127f);
                AddTutorial("T1_TwoPlates", -w, w, 136f, "This gate needs both plates held down at once. You can stand on one yourself.");

                Route(2, V(8, 126), V(0, 131), V(-5, 140), V(7, 146), V(0, 160), V(0, 166), V(0, 174), V(-4.3f, 177),
                    V(-8, 187), V(-11.5f, 187), V(-4, 195), V(0, 202), V(0, 206));
            }

            private void BuildBarn()
            {
                BeginSection(_sectionIds[3]);
                float w = StripWide;

                Ground("S4_Ground_BarnApproach", -w, w, 206f, 240f);

                // Entry glade: the last checkpoint.
                AddCheckpoint("S4_Checkpoint_Entry", new Vector3(1f, 0f, 209.5f), 4f, false);
                AddPickup("S4_CandlePickup_Entry", new Vector3(-6f, 0.7f, 210f));
                AddBowl("S4_CandyBowl_Entry", new Vector3(-3f, 0f, 213f));

                // The pair: two ghosts crossing behind a 4 m gap. One candy takes one ghost. A
                // third walks the pit's edge and reaches to 6.4 m from the bridge head.
                Corn("S4_Hedge_PairWest", -w, -2f, 220f);
                Corn("S4_Hedge_PairEast", 2f, w, 220f);
                AddGhost("S4_Ghost_K", 9f, V(-7, 226), V(7, 226));
                AddGhost("S4_Ghost_L", 9f, V(7, 231), V(-7, 231));
                AddGhost("S4_Ghost_M", 9f, V(-10, 235), V(0, 235));

                // The last pit: north with a 2 m jump, across, north again with a second 2 m jump,
                // over a deadly drop. About 6 m is lit at a time. Ghosts stop at the edge.
                PlacePrefab(_lamp, "S4_PorchLamp_LastBridge", _play, new Vector3(6.2f, 0f, 238.6f));
                Slab("S4_Pit_Floor (deadly)", _env, -w, w, 240f, 256f, -PitDepth, 0.5f, _hazard);
                HazardEdge("S4_Pit_Edge_South", -w, w, 239.5f, 240f);
                HazardEdge("S4_Pit_Edge_North", -w, w, 256f, 256.5f);
                Transform bridge = Child(_play, "S4_Bridge_Last (dogleg, two jumps)");
                Tile("S4_BridgeLast_A1", bridge, 3f, 5f, 240.05f, 242.2f);
                Tile("S4_BridgeLast_B1", bridge, 3f, 5f, 244.2f, 246.3f);
                Tile("S4_BridgeLast_B2", bridge, 3f, 5f, 246.35f, 248.35f);
                for (int i = 0; i < 4; i++) Tile($"S4_BridgeLast_W{i + 1}", bridge, 0.9f - 2.1f * i, 2.9f - 2.1f * i, 246.35f, 248.35f);
                Tile("S4_BridgeLast_C1", bridge, -5.4f, -3.4f, 248.45f, 250.3f);
                Tile("S4_BridgeLast_D1", bridge, -5.4f, -3.4f, 252.3f, 254.1f);
                Tile("S4_BridgeLast_D2", bridge, -5.4f, -3.4f, 254.15f, 255.98f);

                // The barn: a dark block with the goal door in warm orange.
                Ground("S4_Ground_BarnYard", -w, w, 256f, 276f);
                Block("S4_Building_Barn", -8f, 8f, 268.3f, 276f, 5f, _wall);
                Decal("S4_Barn_Porch (goal)", -3f, 3f, 263.5f, 268.3f, _goalMat);
                PlacePrefab(_goal, "S4_Goal_BarnDoor", _triggers, new Vector3(0f, 0f, 268f));

                AddSection(3, "The Barn", -w, w, 207.5f);

                Route(3, V(0, 206), V(0, 210), V(-6, 210), V(0, 214), V(0, 220.5f), V(0, 236), V(4, 240), V(4, 247.35f),
                    V(-4.4f, 247.35f), V(-4.4f, 256), V(0, 266.6f));
            }
        }
    }
}
