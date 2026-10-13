using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper.EditorTools
{
    // "Lantern Keeper/Dress Tutorial" and "Dress Level02": Level01's art pass for the other two
    // scenes. The rules are the ones at the top of LanternKeeperSetup.Dress.cs: everything goes
    // under the one "Dressing" root (deleted and rebuilt on every run), greybox blocks that a
    // model replaces only have their renderer switched off, and no collider, position or size
    // is touched. So these can be run as often as you like, also after moving things by hand.
    //
    // Unlike Level01's pass, most of this one is not written block by block. It goes through
    // the scene's Environment and dresses each block by what its NAME says it is:
    //   ...Crates...      crates              ...Fence...        rail fence
    //   ...Gravestone...  a gravestone        ...Porch_Rail...   hedge with jack-o'-lanterns
    //   ...Hedge...       clipped hedge       ...Wall...         stone wall with railings on top
    //   ...Porch_Floor    boarded floor       ..._Building_...   the farmhouse model
    //   ...Ground...      grass
    // A block added by hand is dressed too if it is named that way. Each scene then gets its
    // own surroundings (ground to the fog, fence, trees, water, pit walls) and its own props.
    // The M_LK_* materials are shared with Level01 and are not changed here.
    public static partial class LanternKeeperSetup
    {
        [MenuItem(Menu + "Dress Tutorial", priority = 51)]
        public static void DressTutorialMenuItem() => DressLevel(TutorialSpec);

        [MenuItem(Menu + "Dress Level02", priority = 52)]
        public static void DressLevel02MenuItem() => DressLevel(Level02Spec);

        /// <summary>Batch-mode entry point: dresses the tutorial and Level02 (whichever exist).</summary>
        public static void DressTutorialAndLevel02()
        {
            DressLevel(TutorialSpec);
            DressLevel(Level02Spec);
        }

        private static bool DressLevel(LevelSpec spec)
        {
            if (!File.Exists(spec.ScenePath))
            {
                Debug.LogWarning($"[Lantern Keeper] {spec.ScenePath} does not exist yet; run 'Set Up Tutorial and Level02'.");
                return false;
            }
            if (SceneManager.GetActiveScene().path != spec.ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!DressOpenLevel(scene, spec)) return false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return true;
        }

        // Dresses the open scene without saving it (the level generator saves it afterwards).
        private static bool DressOpenLevel(Scene scene, LevelSpec spec)
        {
            EnsurePolishAssets();
            Physics.SyncTransforms(); // block sizes are read from the colliders
            var dresser = new Level01Dresser(scene);
            if (!dresser.Begin(false)) return false;

            dresser.Atmosphere();
            dresser.DressBlocksByName();
            if (spec == TutorialSpec) dresser.TutorialScenery();
            else dresser.Level02Scenery();
            dresser.AudioAndFx();
            dresser.Finish();
            Debug.Log(dresser.Summary().Replace("Level01 dressed", spec.SceneName + " dressed"));
            return true;
        }

        private sealed partial class Level01Dresser
        {
            private static readonly string[] Gravestones =
            {
                "G/gravestone-bevel", "G/gravestone-round", "G/gravestone-wide", "G/gravestone-decorative", "G/gravestone-roof"
            };
            private static readonly string[] Pines = { "G/pine", "G/pine-crooked", "G/pine-fall", "G/pine-fall-crooked" };
            private static readonly string[] Pumpkins = { "G/pumpkin", "G/pumpkin", "G/pumpkin-tall", "G/pumpkin-carved", "G/pumpkin-tall-carved" };
            private static readonly string[] Bales = { "G/hay-bale", "G/hay-bale-bundled" };
            private static readonly string[] FarmClutter = { "G/hay-bale", "G/hay-bale-bundled", "G/pumpkin", "G/pumpkin-tall", "G/pumpkin-carved" };
            private static readonly string[] Grass = { "N/grass", "N/grass_large" };

            // ------------------------------------------------------------------ blocks by name

            /// <summary>Every greybox block under Environment, dressed by what its name says it is.</summary>
            public void DressBlocksByName()
            {
                BeginGroup("Blocks", 31);

                var names = new List<string>();
                foreach (Transform child in _environment.GetComponentsInChildren<Transform>(true))
                {
                    if (child.GetComponent<MeshRenderer>() != null) names.Add(child.name);
                }

                int stone = 0;
                foreach (string name in names)
                {
                    if (!Block(name, out Bounds b)) continue;

                    if (name.Contains("Gravestone"))
                    {
                        Hide(name);
                        Fit(Gravestones[stone++ % Gravestones.Length], b, name + " Model", true);
                        if (stone % 3 == 0) Prop("G/candle-multiple", new Vector3(b.center.x + 0.3f, 0f, b.min.z - 0.3f), Rand(0f, 360f), 0.3f);
                    }
                    else if (name.Contains("Crates"))
                    {
                        Hide(name);
                        FillWithCrates(b, Mathf.Max(1, Mathf.RoundToInt(b.size.x / 1.05f)), b.size.y > 1.1f ? 2 : 1);
                    }
                    else if (name.Contains("Fence"))
                    {
                        Hide(name);
                        FenceRow("LK_FencePanel", new Vector3(b.min.x, 0f, b.center.z), new Vector3(b.max.x, 0f, b.center.z), 2f, b.size.y / 1.1f);
                    }
                    else if (name.Contains("Porch_Rail"))
                    {
                        Hide(name);
                        FillWithHedge(b, Mathf.Max(1, Mathf.RoundToInt(b.size.x / 2.9f)), 1);
                        for (float x = b.min.x + 1.2f; x < b.max.x - 0.5f; x += 2.6f)
                        {
                            Jack(new Vector3(x, b.max.y, b.center.z), 180f + Rand(-15f, 15f), Rand(0.55f, 0.72f));
                        }
                    }
                    else if (name.Contains("Hedge"))
                    {
                        Hide(name);
                        float longSide = Mathf.Max(b.size.x, b.size.z), shortSide = Mathf.Min(b.size.x, b.size.z);
                        FillWithHedge(b, Mathf.Max(1, Mathf.RoundToInt(longSide / 2.9f)), Mathf.Max(1, Mathf.RoundToInt(shortSide / 3.5f)));
                    }
                    else if (name.Contains("Wall"))
                    {
                        // The stone block stays; it gets railings, and a post with a lantern at the opening.
                        FenceRow("LK_IronFencePanel", new Vector3(b.min.x, b.max.y, b.center.z), new Vector3(b.max.x, b.max.y, b.center.z), 2.2f, 0.5f);
                        float end = Mathf.Abs(b.min.x) < Mathf.Abs(b.max.x) ? b.min.x : b.max.x; // the end beside the opening
                        if (Mathf.Abs(end) > 4f) continue;
                        float inward = Mathf.Sign(b.center.x); // from the opening into the wall
                        Box(Pal.Stone, "Gate Post", Mathf.Min(end, end + inward * 0.9f), Mathf.Max(end, end + inward * 0.9f),
                            0f, b.max.y + 0.5f, b.min.z - 0.15f, b.max.z + 0.15f);
                        Jack(new Vector3(end + inward * 0.45f, b.max.y + 0.5f, b.center.z), 180f, 0.7f, b.center.x > 0f);
                    }
                    else if (name.Contains("Porch_Floor"))
                    {
                        Box(Pal.WoodDark, "Porch Trim South", b.min.x - 0.12f, b.max.x + 0.12f, 0f, 0.07f, b.min.z - 0.12f, b.min.z);
                        Box(Pal.WoodDark, "Porch Trim West", b.min.x - 0.12f, b.min.x, 0f, 0.07f, b.min.z, b.max.z);
                        Box(Pal.WoodDark, "Porch Trim East", b.max.x, b.max.x + 0.12f, 0f, 0.07f, b.min.z, b.max.z);
                        for (float z = b.min.z + 1f; z < b.max.z; z += 1f)
                        {
                            Box(Pal.WoodDark, "Porch Board Gap", b.min.x, b.max.x, 0.035f, 0.05f, z - 0.02f, z + 0.02f);
                        }
                        Jack(new Vector3(b.min.x - 0.5f, 0f, b.min.z + 0.3f), 200f, 0.75f, true);
                        Jack(new Vector3(b.max.x + 0.5f, 0f, b.min.z + 0.3f), 160f, 0.8f, true);
                    }
                    // Not the section's own group object, which is called S4_Barn too: the block says "Building".
                    else if (name.Contains("_Building_"))
                    {
                        Hide(name);
                        House(b);
                    }
                    else if (name.Contains("Ground"))
                    {
                        // Grass has no collider: it is walked straight through.
                        int tufts = Mathf.Clamp(Mathf.RoundToInt(b.size.x * b.size.z / 22f), 4, 70);
                        Scatter(Grass, b.min.x + 0.3f, b.max.x - 0.3f, b.min.z + 0.4f, b.max.z - 0.4f, tufts, 0.25f, 0.5f);
                    }
                }
            }

            // The goal building: the block's footprint and height as a house with lit windows, a
            // porch roof over the goal, a lit gable window, and a flame on a plinth either side.
            private void House(Bounds house)
            {
                Piece("LK_Farmhouse", new Vector3(house.center.x, house.min.y, house.center.z), 0f,
                    new Vector3(house.size.x / 16f, house.size.y / 5f, house.size.z / 7.7f), "Farmhouse");

                float front = house.min.z;
                Piece("LK_FarmhousePorch", new Vector3(house.center.x, 0f, front - 4.1f), 0f, Vector3.one, "Farmhouse Porch Roof");
                Box(Pal.GlowWarm, "Beacon Window", house.center.x - 0.42f, house.center.x + 0.42f, 3.95f, 4.6f, front - 4.92f, front - 4.84f);
                Piece("LK_HangingLantern", new Vector3(house.center.x, 2.9f, front - 4.1f), 0f, Vector3.one * 0.9f, "Porch Lantern");
                foreach (float side in new[] { -1f, 1f })
                {
                    var basket = new Vector3(house.center.x + side * 3.9f, 0f, front - 4.6f);
                    Box(Pal.StoneDark, "Plinth", basket.x - 0.3f, basket.x + 0.3f, 0f, 0.9f, basket.z - 0.3f, basket.z + 0.3f);
                    Box(Pal.Stone, "Plinth Cap", basket.x - 0.38f, basket.x + 0.38f, 0.9f, 1f, basket.z - 0.38f, basket.z + 0.38f);
                    GameObject flame = NewPiece("Plinth Flame", LoadMesh("LK_Flame"), new[] { Mat("M_LampGlow") }, _group);
                    flame.transform.position = basket + Vector3.up * 1f;
                    flame.transform.localScale = new Vector3(0.45f, 0.7f, 0.45f);
                    NewLight("Plinth Light", basket + Vector3.up * 1.4f, new Color(1f, 0.55f, 0.18f), 3f, 7f).gameObject.AddComponent<FlickerLight>();
                    Jack(new Vector3(house.center.x + side * 2.3f, 0f, front - 4.55f), 180f + side * 12f, 0.8f);
                }

                // What a farm leaves lying about, kept to the sides of the yard.
                Scatter(FarmClutter, -13.6f, -9.5f, front - 11f, front - 1f, 9, 0.45f, 1f);
                Scatter(FarmClutter, 9.5f, 13.6f, front - 11f, front - 1f, 9, 0.45f, 1f);
                Piece("LK_Crate", new Vector3(-10.6f, 0f, front - 1.7f), 12f, Vector3.one);
                Piece("LK_Crate", new Vector3(-11.7f, 0f, front - 2f), -8f, new Vector3(1f, 0.8f, 1f));
                Piece("LK_Scarecrow", new Vector3(11.4f, 0f, front - 7.8f), 150f, Vector3.one, "Scarecrow Farm");
                Leaves("Leaves Farm", new Vector3(3f, 5.5f, front - 6f), new Vector3(30f, 1f, 14f), 8);
            }

            // ------------------------------------------------------------------ surroundings

            /// <summary>
            /// The world outside the strip. <paramref name="land"/> lists the stretches with ground
            /// beside them and the strip's half width there, south to north; stretches left out
            /// (water) get no ground. Fences follow the strip's edge along every stretch of land.
            /// </summary>
            private void Surroundings((float zMin, float zMax, float half)[] land, string fencePanel)
            {
                const float reach = OuterReach;
                float south = land[0].zMin, north = land[land.Length - 1].zMax;
                float southHalf = land[0].half, northHalf = land[land.Length - 1].half;

                for (int i = 0; i < land.Length; i++)
                {
                    float zMin = i == 0 ? south - 40f : land[i].zMin;
                    float zMax = i == land.Length - 1 ? north + 48f : land[i].zMax;
                    Slab($"Outer Ground West z{land[i].zMin}", _outerGround, -reach, -land[i].half, -0.02f, 0.3f, zMin, zMax);
                    Slab($"Outer Ground East z{land[i].zMin}", _outerGround, land[i].half, reach, -0.02f, 0.3f, zMin, zMax);
                }
                Slab("Outer Ground South", _outerGround, -southHalf, southHalf, -0.02f, 0.3f, south - 40f, south);
                Slab("Outer Ground North", _outerGround, -northHalf, northHalf, -0.02f, 0.3f, north, north + 48f);

                FenceRow(fencePanel, new Vector3(-southHalf - 0.4f, 0f, south - 0.4f), new Vector3(southHalf + 0.4f, 0f, south - 0.4f), 3f);
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = 0; i < land.Length; i++)
                    {
                        float x = side * (land[i].half + 0.4f);
                        FenceRow(fencePanel, new Vector3(x, 0f, land[i].zMin + (i == 0 ? -0.4f : 0f)), new Vector3(x, 0f, land[i].zMax), 3f);
                        // Where the strip widens, the fence steps out with it.
                        if (i > 0 && Mathf.Approximately(land[i].zMin, land[i - 1].zMax) && !Mathf.Approximately(land[i].half, land[i - 1].half))
                        {
                            FenceRow(fencePanel, new Vector3(side * (land[i - 1].half + 0.4f), 0f, land[i].zMin + 0.4f),
                                new Vector3(x, 0f, land[i].zMin + 0.4f), 2.4f);
                        }
                    }
                }

                // Pines beyond the fence along the whole route, and a tree line closing the view at each end.
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float z = south - 32f; z < north + 38f; z += 9f)
                    {
                        float inner = HalfAt(land, z);
                        int trees = _random.Next(1, 4);
                        for (int i = 0; i < trees; i++)
                        {
                            Prop(Pick(Pines), new Vector3(side * (inner + Rand(6f, 46f)), 0f, z + Rand(-4f, 4f)), Rand(0f, 360f), Rand(5f, 9.5f));
                        }
                    }
                }
                for (float x = -60f; x <= 60f; x += 6.5f)
                {
                    Prop(Pick(Pines), new Vector3(x + Rand(-2f, 2f), 0f, south - 22f + Rand(-6f, 6f)), Rand(0f, 360f), Rand(6f, 10f));
                    Prop(Pick(Pines), new Vector3(x + Rand(-2f, 2f), 0f, north + 20f + Rand(-6f, 8f)), Rand(0f, 360f), Rand(7f, 11f));
                }
            }

            private static float HalfAt((float zMin, float zMax, float half)[] land, float z)
            {
                float half = land[0].half;
                foreach ((float zMin, float _, float h) in land)
                {
                    if (z >= zMin) half = h;
                }
                return half;
            }

            /// <summary>Shallow water across the strip and on past it, with the bed carried on outside.</summary>
            private void Water(string name, float zMin, float zMax, float half)
            {
                const float reach = OuterReach;
                Material bed = Mat("M_LK_Shallow");
                Slab($"{name} Water", _water, -reach, reach, -0.55f, 0.05f, zMin + 0.02f, zMax - 0.02f);
                Slab($"{name} Bed West", bed, -reach, -half, -1.6f, 0.4f, zMin, zMax);
                Slab($"{name} Bed East", bed, half, reach, -1.6f, 0.4f, zMin, zMax);
            }

            /// <summary>Earth walls round a deadly pit, so the drop reads as a hole in the ground and not the edge of the world.</summary>
            private void PitWalls(string name, float zMin, float zMax, float half)
            {
                Slab($"{name} Wall West", _pitWall, -half - 0.6f, -half, -0.3f, 6.4f, zMin, zMax);
                Slab($"{name} Wall East", _pitWall, half, half + 0.6f, -0.3f, 6.4f, zMin, zMax);
                Slab($"{name} Wall South", _pitWall, -half, half, -2f, 4.7f, zMin - 0.6f, zMin);
                Slab($"{name} Wall North", _pitWall, -half, half, -2f, 4.7f, zMax, zMax + 0.6f);
            }

            /// <summary>Props against the fence line on both sides of the strip, inside it. The middle stays clear.</summary>
            private void EdgeClutter(string[] ids, float half, float zMin, float zMax, int perSide, float heightMin, float heightMax)
            {
                Scatter(ids, -half + 0.3f, -half + 1.1f, zMin, zMax, perSide, heightMin, heightMax);
                Scatter(ids, half - 1.1f, half - 0.3f, zMin, zMax, perSide, heightMin, heightMax);
            }

            // ------------------------------------------------------------------ the tutorial

            /// <summary>The front yard of a farm: a pumpkin patch, a creek, a few old graves, the house.</summary>
            public void TutorialScenery()
            {
                const float n = 7f, w = 14f;

                BeginGroup("Outer", 32);
                Surroundings(new[] { (-8f, 52f, n), (58f, 64f, n), (64f, 198f, w) }, "LK_FencePanel");
                Water("Creek", 52f, 58f, n);
                // The ditch is a metre deep: close its two ends, where it meets the fence line.
                Slab("Ditch End West", _pitWall, -n - 0.5f, -n, -0.02f, 1.2f, 18f, 21f);
                Slab("Ditch End East", _pitWall, n, n + 0.5f, -0.02f, 1.2f, 18f, 21f);

                BeginGroup("S1_Yard", 33);
                bool ClearOfDitch(float x, float z) => z < 17.2f || z > 21.8f;
                Scatter(Pumpkins, -6.8f, -5.9f, -6f, 47f, 9, 0.35f, 0.7f, ClearOfDitch);
                Scatter(Pumpkins, 5.9f, 6.8f, -6f, 47f, 9, 0.35f, 0.7f, ClearOfDitch);
                Scatter(Pumpkins, -24f, -8.2f, -12f, 50f, 40, 0.35f, 0.9f);
                Scatter(Pumpkins, 8.2f, 24f, -12f, 50f, 40, 0.35f, 0.9f);
                Scatter(Bales, -16f, -8.6f, -8f, 48f, 5, 0.8f, 1f);
                Scatter(Bales, 8.6f, 16f, -8f, 48f, 5, 0.8f, 1f);
                Piece("LK_Scarecrow", new Vector3(-9.6f, 0f, 10.5f), -90f, Vector3.one, "Scarecrow West");
                Piece("LK_Scarecrow", new Vector3(9.8f, 0f, 30f), 90f, Vector3.one * 1.08f, "Scarecrow East");
                Fireflies("Fireflies S1", new Vector3(0f, 1.3f, 22f), new Vector3(18f, 2.2f, 56f), 24);
                Leaves("Leaves S1", new Vector3(3f, 5f, 22f), new Vector3(20f, 1f, 56f), 16);

                BeginGroup("S2_Creek", 34);
                const float bed = -1.6f;
                // Reeds in the water along the banks, clear of the bridge and of the two ramps out.
                foreach ((float x, float z) in new[]
                         {
                             (-3.4f, 52.7f), (3.6f, 52.8f), (-6.3f, 57.3f), (-3.2f, 57.4f), (3.3f, 57.3f), (6.2f, 57.2f)
                         })
                {
                    Piece("LK_Reeds", new Vector3(x, bed, z), Rand(0f, 360f), Vector3.one * Rand(1.5f, 1.9f));
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float x = 8.5f; x < 30f; x += Rand(2.5f, 5f))
                    {
                        Piece("LK_Reeds", new Vector3(side * x, bed, Pick(52.6f, 57.4f)), Rand(0f, 360f), Vector3.one * Rand(1.5f, 2f));
                    }
                }
                Scatter(new[] { "N/lily_large", "N/lily_small" }, 2.6f, 6.4f, 53f, 57f, 4, 0.05f, 0.09f, null, -0.545f);
                Scatter(new[] { "N/lily_large", "N/lily_small" }, -6.4f, -2.6f, 55.6f, 57f, 2, 0.05f, 0.09f, null, -0.545f);
                // Stepping stones lead the eye to the bridge and on from it. They lie on solid ground.
                foreach ((float x, float z) in new[] { (0.1f, 49.9f), (-0.2f, 51.1f), (0f, 58.8f), (0.2f, 60.1f), (-0.1f, 61.4f), (0.1f, 62.8f) })
                {
                    Piece("LK_SteppingStone", new Vector3(x, 0f, z), Rand(0f, 360f), new Vector3(Rand(0.8f, 1.05f), 0.45f, Rand(0.8f, 1.05f)));
                }
                Scatter(new[] { "N/rock_smallA", "N/rock_smallFlatB", "N/stump_old", "N/log_stack" }, -6.8f, -5.6f, 58.5f, 63.5f, 2, 0.4f, 0.7f);
                Scatter(new[] { "N/rock_smallA", "N/rock_smallFlatB" }, 8f, 22f, 44f, 66f, 8, 0.25f, 0.6f);
                Scatter(new[] { "N/rock_smallA", "N/rock_smallFlatB" }, -22f, -8f, 44f, 66f, 8, 0.25f, 0.6f);
                Fireflies("Fireflies S2", new Vector3(0f, 0.9f, 55f), new Vector3(18f, 2.2f, 16f), 14);
                AmbienceZone("Ambience Creek", new Vector3(0f, -0.5f, 55f), "Ambience/creek_water", 0.5f, 4f, 24f);

                BeginGroup("S3_Graves", 35);
                // Bare trees against the fence, and more graves beyond it so the yard does not stop there.
                foreach ((float x, float z) in new[] { (-13.3f, 79f), (13.2f, 86.5f), (-13.2f, 101f), (13.3f, 108f) })
                {
                    Piece("LK_DeadTree", new Vector3(x, 0f, z), Rand(0f, 360f), Vector3.one * Rand(0.85f, 1.1f));
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = 0; i < 12; i++)
                    {
                        Prop(Pick(Gravestones), new Vector3(side * Rand(15.5f, 30f), 0f, Rand(68f, 110f)), Pick(0f, 180f), Rand(0.9f, 1.6f));
                    }
                    for (float z = 70f; z < 112f; z += 13f)
                    {
                        Piece("LK_DeadTree", new Vector3(side * (w + Rand(1.8f, 9f)), 0f, z + Rand(-4f, 4f)), Rand(0f, 360f), Vector3.one * Rand(0.9f, 1.4f));
                    }
                    Prop("G/crypt", new Vector3(side * 21f, 0f, 92f + side * 6f), side > 0f ? -90f : 90f, 3.2f);
                }
                Prop("G/coffin-old", new Vector3(-12.3f, 0f, 76.6f), 20f, 0.5f);
                Prop("G/shovel-dirt", new Vector3(-11f, 0f, 77.6f), 140f, 0.7f);
                Prop("G/bench-damaged", new Vector3(12.2f, 0f, 77f), 180f, 0.8f);
                Prop("G/urn-round", new Vector3(12.9f, 0f, 103f), 0f, 0.8f);
                Mist("Mist S3", new Vector3(0f, 0.5f, 92f), new Vector3(26f, 0.4f, 30f), 14);
                AmbienceZone("Ambience Graveyard Choir", new Vector3(0f, 3f, 92f), "Ambience/graveyard_choir", 0.3f, 10f, 40f);

                BeginGroup("S4_Gate", 36);
                EdgeClutter(FarmClutter, w, 126f, 147f, 6, 0.4f, 0.9f);
                EdgeClutter(FarmClutter, w, 150f, 177f, 7, 0.4f, 0.9f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Scatter(new[] { "G/pumpkin", "G/pumpkin-tall", "G/hay-bale" }, side > 0f ? 15f : -30f, side > 0f ? 30f : -15f, 114f, 196f, 26, 0.4f, 1f);
                }
                Fireflies("Fireflies S4", new Vector3(0f, 1.3f, 150f), new Vector3(26f, 2.2f, 60f), 22);
            }

            // ------------------------------------------------------------------ Level02: the orchard

            /// <summary>An orchard at harvest: tree rows outside the fence, crates and log piles inside it, a mill pond, a barn.</summary>
            public void Level02Scenery()
            {
                const float w = 14f;

                BeginGroup("Outer", 42);
                Surroundings(new[] { (-8f, 276f, w) }, "LK_FencePanel");
                PitWalls("S2 Pit A", 70f, 84f, w);
                PitWalls("S2 Pit B", 100f, 116f, w);
                PitWalls("S4 Pit", 240f, 256f, w);

                BeginGroup("S1_OrchardRows", 43);
                // The orchard itself is outside the fence: trees in straight rows, as planted.
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float x = 18f; x < 44f; x += 6.5f)
                    {
                        for (float z = -6f; z < 68f; z += 7f)
                        {
                            Prop(Pick("G/pine-fall", "G/pine-fall", "G/pine-fall-crooked"), new Vector3(side * (x + Rand(-0.5f, 0.5f)), 0f, z + Rand(-0.6f, 0.6f)),
                                Rand(0f, 360f), Rand(4.2f, 5.6f), "Orchard Tree");
                        }
                    }
                }
                // Harvest left along the fence: crates, bales, log piles, pumpkins.
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float z = -5f; z < 12f; z += Rand(3.5f, 5.5f))
                    {
                        Piece("LK_Crate", new Vector3(side * 13.2f, 0f, z), Rand(-12f, 12f), Vector3.one * Rand(0.8f, 1f));
                    }
                }
                EdgeClutter(new[] { "N/log_stack", "N/stump_old", "G/hay-bale", "G/pumpkin" }, w, 46f, 68f, 6, 0.5f, 0.9f);
                EdgeClutter(Pumpkins, w, -6f, 12f, 4, 0.35f, 0.7f);
                Piece("LK_Scarecrow", new Vector3(-12.4f, 0f, 8.5f), -120f, Vector3.one, "Scarecrow Orchard");
                Fireflies("Fireflies S1", new Vector3(0f, 1.3f, 30f), new Vector3(26f, 2.2f, 70f), 30);
                Leaves("Leaves S1", new Vector3(4f, 5.5f, 30f), new Vector3(32f, 1f, 70f), 26);

                BeginGroup("S2_MillPond", 44);
                // The island: rocks and stumps at its edges, bare trees outside, mist lying in the pits.
                EdgeClutter(new[] { "N/rock_smallA", "N/rock_smallFlatB", "N/stump_old", "G/rocks" }, w, 85f, 99f, 4, 0.4f, 0.9f);
                EdgeClutter(new[] { "N/rock_smallA", "N/log_stack", "G/rocks" }, w, 117f, 125f, 2, 0.4f, 0.8f);
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float z = 66f; z < 124f; z += 11f)
                    {
                        Piece("LK_DeadTree", new Vector3(side * (w + Rand(2f, 10f)), 0f, z + Rand(-3f, 3f)), Rand(0f, 360f), Vector3.one * Rand(0.9f, 1.4f));
                    }
                    Scatter(new[] { "N/rock_smallA", "N/rock_smallFlatB", "N/stump_old" }, side > 0f ? 15f : -30f, side > 0f ? 30f : -15f, 64f, 126f, 14, 0.3f, 0.7f);
                }
                foreach ((float x, float z) in new[] { (-6f, 65f), (-6.1f, 66.6f), (-5.9f, 68.2f), (0.4f, 85.2f), (8f, 117.2f), (7.6f, 119f) })
                {
                    Piece("LK_SteppingStone", new Vector3(x, 0f, z), Rand(0f, 360f), new Vector3(Rand(0.8f, 1.05f), 0.45f, Rand(0.8f, 1.05f)));
                }
                Mist("Mist S2 Pit A", new Vector3(0f, -2.5f, 77f), new Vector3(26f, 0.4f, 12f), 8);
                Mist("Mist S2 Pit B", new Vector3(0f, -2.5f, 108f), new Vector3(26f, 0.4f, 14f), 8);
                Mist("Mist S2 Island", new Vector3(0f, 0.5f, 92f), new Vector3(26f, 0.4f, 14f), 8);

                BeginGroup("S3_CiderPress", 45);
                // The press yards: stacked crates and barrels' worth of clutter along both fences.
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach ((float zMin, float zMax) in new[] { (128f, 164f), (176f, 201f) })
                    {
                        for (float z = zMin; z < zMax; z += Rand(4f, 7f))
                        {
                            float size = Rand(0.8f, 1.05f);
                            var at = new Vector3(side * 13.1f, 0f, z);
                            Piece("LK_Crate", at, Rand(-10f, 10f), Vector3.one * size);
                            if (_random.Next(2) == 0) Piece("LK_Crate", at + Vector3.up * size, Rand(-20f, 20f), Vector3.one * size * 0.85f);
                        }
                    }
                    Scatter(new[] { "G/hay-bale", "G/pumpkin", "G/pumpkin-tall", "N/log_stack" }, side > 0f ? 15f : -30f, side > 0f ? 30f : -15f, 126f, 206f, 24, 0.4f, 1f);
                }
                EdgeClutter(Pumpkins, w, 128f, 164f, 4, 0.35f, 0.7f);
                Fireflies("Fireflies S3", new Vector3(0f, 1.3f, 166f), new Vector3(26f, 2.2f, 78f), 26);

                BeginGroup("S4_Barn", 46);
                EdgeClutter(FarmClutter, w, 208f, 238f, 7, 0.4f, 0.9f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Scatter(new[] { "G/pumpkin", "G/pumpkin-tall", "G/hay-bale" }, side > 0f ? 15f : -30f, side > 0f ? 30f : -15f, 206f, 274f, 22, 0.4f, 1f);
                }
                Piece("LK_Scarecrow", new Vector3(-12.2f, 0f, 216f), -100f, Vector3.one * 1.05f, "Scarecrow Barn Approach");
                Mist("Mist S4 Pit", new Vector3(0f, -2.5f, 248f), new Vector3(26f, 0.4f, 14f), 8);
                Leaves("Leaves S4", new Vector3(4f, 5.5f, 224f), new Vector3(32f, 1f, 30f), 12);
            }
        }
    }
}
