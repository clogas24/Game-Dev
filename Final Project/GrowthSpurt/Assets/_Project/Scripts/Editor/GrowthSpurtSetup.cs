using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarterAssets;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GrowthSpurt.EditorTools
{
    /// <summary>
    /// Tools > Growth Spurt > ... menu items that build Section 1's assets, so no .prefab/.unity YAML is hand-edited.
    /// Re-running is safe: size profiles, materials and the HUD prefab are only created when missing (your tuning
    /// is kept), GS_Player is updated in place, and the test scene is rebuilt from scratch.
    /// </summary>
    public static class GrowthSpurtSetup
    {
        private const string Root = "Assets/_Project";
        private const string ProfilesDir = Root + "/Data/SizeProfiles";
        private const string PrefabsDir = Root + "/Prefabs";
        private const string ScenesDir = Root + "/Scenes";
        private const string MaterialsDir = Root + "/Art/Materials";
        private const string CharactersDir = Root + "/Art/Characters";

        public const string PlayerPrefabPath = PrefabsDir + "/GS_Player.prefab";
        public const string HudPrefabPath = PrefabsDir + "/GS_HUD.prefab";
        public const string TestScenePath = ScenesDir + "/S1_SizeTest.unity";

        private const string StarterTpc = "Assets/Starter Assets/Runtime/ThirdPersonController";
        private const string ArmaturePrefabPath = StarterTpc + "/Prefabs/PlayerArmature.prefab";
        private const string MainCameraPrefabPath = StarterTpc + "/Prefabs/MainCamera.prefab";
        private const string FollowCameraPrefabPath = StarterTpc + "/Prefabs/PlayerFollowCamera.prefab";
        private const string AnimatorControllerPath = StarterTpc + "/Character/Animations/StarterAssetsThirdPerson.controller";
        private const string GridMaterialPath = "Assets/Starter Assets/Sample/Environment/Art/Materials/URP_HDRP_ShaderGraph/ShaderGraphGrid_01_Mat.mat";
        private const string SkyboxPath = "Assets/Starter Assets/Sample/Environment/Art/Skybox/SkyboxLite.mat";

        // =====================================================================
        // Menu items
        // =====================================================================

        [MenuItem("Tools/Growth Spurt/Build S1 Test Scene", priority = 0)]
        public static void BuildTestSceneMenu()
        {
            if (!EnsureTmpResources()) return;
            if (!Application.isBatchMode)
            {
                if (File.Exists(TestScenePath) &&
                    !EditorUtility.DisplayDialog("Rebuild S1 test scene?",
                        $"{TestScenePath} already exists and will be rebuilt from scratch.", "Rebuild", "Cancel"))
                    return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }
            BuildTestScene();
        }

        [MenuItem("Tools/Growth Spurt/Build GS_Player Prefab", priority = 1)]
        public static void BuildPlayerPrefabMenu()
        {
            var prefab = BuildPlayerPrefab();
            if (prefab != null) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
        }

        [MenuItem("Tools/Growth Spurt/Create Size Profiles", priority = 2)]
        public static void CreateSizeProfilesMenu()
        {
            var p = CreateSizeProfiles();
            Selection.objects = p.Cast<Object>().ToArray();
        }

        // Entry point for -executeMethod (batch mode).
        public static void BuildAllBatch()
        {
            if (!EnsureTmpResources()) { EditorApplication.Exit(2); return; }
            BuildTestScene();
        }

        // =====================================================================
        // Size profiles
        // =====================================================================

        private struct ProfileDefaults
        {
            public PlayerSize size; public float scale, move, sprint, jump, gravity; public int weight;
            public float push, step, camDistance; public Vector3 shoulder; public float anim;
        }

        // Starting values. Tune them on the assets in Assets/_Project/Data/SizeProfiles; re-running the menu
        // never overwrites an existing asset.
        private static readonly ProfileDefaults[] Defaults =
        {
            new ProfileDefaults { size = PlayerSize.Small, scale = 0.4f, move = 3.0f, sprint = 6.0f, jump = 0.7f, gravity = 1.0f,
                weight = 1, push = 15f, step = 0.1f, camDistance = 2.0f, shoulder = new Vector3(0.45f, 0.1f, 0f), anim = 2.5f },
            new ProfileDefaults { size = PlayerSize.Normal, scale = 1.0f, move = 2.0f, sprint = 5.335f, jump = 1.2f, gravity = 1.0f,
                weight = 2, push = 30f, step = 0.25f, camDistance = 4.0f, shoulder = new Vector3(1f, 0f, 0f), anim = 1.0f },
            new ProfileDefaults { size = PlayerSize.Big, scale = 2.2f, move = 2.2f, sprint = 4.0f, jump = 1.0f, gravity = 1.6f,
                weight = 3, push = 100f, step = 0.55f, camDistance = 7.0f, shoulder = new Vector3(1.5f, 0.4f, 0f), anim = 0.45f },
        };

        public static SizeProfile[] CreateSizeProfiles()
        {
            EnsureFolder(ProfilesDir);
            var result = new SizeProfile[3];
            foreach (var d in Defaults)
            {
                string path = $"{ProfilesDir}/SP_{d.size}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<SizeProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<SizeProfile>();
                    profile.size = d.size;
                    profile.scale = d.scale;
                    profile.moveSpeed = d.move;
                    profile.sprintSpeed = d.sprint;
                    profile.jumpHeight = d.jump;
                    profile.gravityMultiplier = d.gravity;
                    profile.weight = d.weight;
                    profile.pushStrength = d.push;
                    profile.stepOffset = d.step;
                    profile.cameraDistance = d.camDistance;
                    profile.cameraShoulderOffset = d.shoulder;
                    profile.animationSpeedMultiplier = d.anim;
                    AssetDatabase.CreateAsset(profile, path);
                }
                result[(int)d.size] = profile;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // =====================================================================
        // GS_Player prefab (variant of Starter Assets PlayerArmature)
        // =====================================================================

        public static GameObject BuildPlayerPrefab()
        {
            var profiles = CreateSizeProfiles();
            EnsureFolder(PrefabsDir);

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmaturePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"[Growth Spurt] Can't find {ArmaturePrefabPath}. Is Starter Assets imported?");
                return null;
            }

            string fbxPath = FindCharacterModel();
            if (fbxPath != null) EnsureHumanoid(fbxPath);

            if (!File.Exists(PlayerPrefabPath))
            {
                // Instantiate the base prefab in a preview scene and save it => a Prefab Variant.
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
                    instance.name = "GS_Player";
                    ConfigurePlayer(instance, profiles, fbxPath);
                    PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            else
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
                try
                {
                    ConfigurePlayer(contents, profiles, fbxPath);
                    PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }

            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Debug.Log($"[Growth Spurt] GS_Player prefab ready ({(fbxPath != null ? "model: " + fbxPath : "Starter Assets robot")}).", prefab);
            return prefab;
        }

        private static void ConfigurePlayer(GameObject root, SizeProfile[] profiles, string fbxPath)
        {
            var shifter = GetOrAdd<SizeShifter>(root);
            var so = new SerializedObject(shifter);
            so.FindProperty("smallProfile").objectReferenceValue = profiles[0];
            so.FindProperty("normalProfile").objectReferenceValue = profiles[1];
            so.FindProperty("bigProfile").objectReferenceValue = profiles[2];
            so.ApplyModifiedPropertiesWithoutUndo();

            GetOrAdd<SizeShiftInput>(root);

            // SizeShifter does the pushing (size-aware); keep the Starter Assets pusher off so they don't stack.
            var basicPush = root.GetComponent<BasicRigidBodyPush>();
            if (basicPush != null) basicPush.canPush = false;

            if (fbxPath != null) ApplyCharacterModel(root, fbxPath);
        }

        // ---------- Mixamo / custom character ----------

        private static string FindCharacterModel()
        {
            if (!AssetDatabase.IsValidFolder(CharactersDir)) return null;
            return AssetDatabase.FindAssets("t:Model", new[] { CharactersDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .FirstOrDefault();
        }

        private static void EnsureHumanoid(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null || importer.animationType == ModelImporterAnimationType.Human) return;
            CharacterModelPostprocessor.ConfigureHumanoid(importer);
            importer.SaveAndReimport();
            Debug.Log($"[Growth Spurt] Set {fbxPath} to Humanoid.");
        }

        private static Avatar FindHumanAvatar(string fbxPath) =>
            AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);

        /// <summary>
        /// Swaps the robot for the FBX: the robot's Geometry/Skeleton are deactivated (a variant can't delete
        /// base objects) and the FBX's children are placed directly under the player root, so the bone paths match
        /// the FBX avatar (the Animator, which ThirdPersonController drives, stays on the root).
        /// </summary>
        private static void ApplyCharacterModel(GameObject root, string fbxPath)
        {
            var avatar = FindHumanAvatar(fbxPath);
            var animator = root.GetComponent<Animator>();
            if (avatar == null)
            {
                Debug.LogWarning($"[Growth Spurt] {fbxPath} has no valid Humanoid avatar. Select it, Rig > Animation Type = Humanoid, " +
                                 "Configure... and fix any red bones, then run Build GS_Player Prefab again. Keeping the robot.");
                return;
            }
            if (animator.avatar == avatar) return; // already applied

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            foreach (string robotPart in new[] { "Geometry", "Skeleton" })
            {
                var t = root.transform.Find(robotPart);
                if (t != null) t.gameObject.SetActive(false);
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (var child in model.transform.Cast<Transform>().ToList())
                child.SetParent(root.transform, true);
            Object.DestroyImmediate(model);

            animator.avatar = avatar;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            animator.applyRootMotion = false;
        }

        // =====================================================================
        // HUD prefab
        // =====================================================================

        public static GameObject BuildHudPrefab()
        {
            EnsureFolder(PrefabsDir);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (existing != null) return existing;

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var root = new GameObject("GS_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                // Pip row, bottom centre
                var row = NewUI("SizePips", root.transform);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
                row.pivot = new Vector2(0.5f, 0f);
                row.anchoredPosition = new Vector2(0f, 48f);
                row.sizeDelta = new Vector2(320f, 110f);
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 30f;
                layout.childControlWidth = layout.childControlHeight = false;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;

                string[] letters = { "S", "N", "B" };
                float[] sizes = { 46f, 64f, 88f }; // the pips themselves are small / medium / big
                var pips = new Image[3];
                var labels = new TMP_Text[3];
                for (int i = 0; i < 3; i++)
                {
                    var pipRt = NewUI($"Pip_{(PlayerSize)i}", row);
                    pipRt.sizeDelta = new Vector2(sizes[i], sizes[i]);
                    var img = pipRt.gameObject.AddComponent<Image>();
                    img.sprite = knob;
                    img.raycastTarget = false;
                    pips[i] = img;

                    var labelRt = NewUI("Label", pipRt);
                    Stretch(labelRt);
                    var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
                    tmp.text = letters[i];
                    tmp.fontSize = sizes[i] * 0.5f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                    labels[i] = tmp;
                }

                var hintRt = NewUI("Hint", root.transform);
                hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0f);
                hintRt.pivot = new Vector2(0.5f, 0f);
                hintRt.anchoredPosition = new Vector2(0f, 14f);
                hintRt.sizeDelta = new Vector2(600f, 30f);
                var hint = hintRt.gameObject.AddComponent<TextMeshProUGUI>();
                hint.text = "Q  shrink        E  grow";
                hint.fontSize = 20f;
                hint.alignment = TextAlignmentOptions.Center;
                hint.color = new Color(1f, 1f, 1f, 0.7f);
                hint.raycastTarget = false;

                var debugRt = NewUI("DebugReadout", root.transform);
                debugRt.anchorMin = debugRt.anchorMax = new Vector2(0f, 1f);
                debugRt.pivot = new Vector2(0f, 1f);
                debugRt.anchoredPosition = new Vector2(24f, -20f);
                debugRt.sizeDelta = new Vector2(900f, 80f);
                var debugText = debugRt.gameObject.AddComponent<TextMeshProUGUI>();
                debugText.fontSize = 22f;
                debugText.alignment = TextAlignmentOptions.TopLeft;
                debugText.color = new Color(1f, 1f, 1f, 0.85f);
                debugText.raycastTarget = false;

                var hud = root.AddComponent<SizeHUD>();
                var so = new SerializedObject(hud);
                SetArray(so.FindProperty("pips"), pips);
                SetArray(so.FindProperty("pipLabels"), labels);
                so.FindProperty("pipRow").objectReferenceValue = row;
                so.FindProperty("debugText").objectReferenceValue = debugText;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // =====================================================================
        // Materials
        // =====================================================================

        private static Material Mat(string name, Color color)
        {
            EnsureFolder(MaterialsDir);
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // =====================================================================
        // Test scene
        // =====================================================================

        private class SceneMats
        {
            public Material ground, block, step, ceiling, tunnel, platform, ledge, lightCrate, heavyCrate;
        }

        public static void BuildTestScene()
        {
            var playerPrefab = BuildPlayerPrefab();
            var hudPrefab = BuildHudPrefab();
            if (playerPrefab == null || hudPrefab == null) return;

            var mats = new SceneMats
            {
                ground = AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath) ?? Mat("M_GS_Ground", new Color(0.55f, 0.57f, 0.6f)),
                block = Mat("M_GS_Block", new Color(0.42f, 0.47f, 0.55f)),
                step = Mat("M_GS_Step", new Color(0.95f, 0.6f, 0.25f)),
                ceiling = Mat("M_GS_Ceiling", new Color(0.85f, 0.32f, 0.3f)),
                tunnel = Mat("M_GS_Tunnel", new Color(0.25f, 0.65f, 0.65f)),
                platform = Mat("M_GS_Platform", new Color(0.3f, 0.48f, 0.85f)),
                ledge = Mat("M_GS_Ledge", new Color(0.55f, 0.4f, 0.75f)),
                lightCrate = Mat("M_GS_CrateLight", new Color(0.86f, 0.72f, 0.42f)),
                heavyCrate = Mat("M_GS_CrateHeavy", new Color(0.22f, 0.24f, 0.28f)),
            };

            EnsureFolder(ScenesDir);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- lighting ----------
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.47f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.23f);

            var level = new GameObject("Level").transform;
            Box("Ground", level, new Vector3(0f, -0.5f, 5f), new Vector3(90f, 1f, 90f), mats.ground);

            // The player spawns at (0, 0, 2) facing +Z. Zones are in a row ahead (z 10..35);
            // crates and the open area are behind the spawn.
            BuildTunnel(level, mats, new Vector3(-26f, 0f, 13f));
            BuildLowCeiling(level, mats, new Vector3(-14f, 0f, 13f));
            BuildSteps(level, mats, new Vector3(0f, 0f, 12f));
            BuildGaps(level, mats, 14f);
            BuildTallLedge(level, mats, 26f);
            BuildCrates(level, mats);
            BuildOpenArea(level);

            // ---------- player, cameras, HUD ----------
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2f), Quaternion.identity);
            var shifter = player.GetComponent<SizeShifter>();

            var mainCam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MainCameraPrefabPath), scene);
            mainCam.transform.SetPositionAndRotation(new Vector3(0.5f, 1.4f, -2f), Quaternion.identity);

            var followCam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefabPath), scene);
            var cmCam = followCam.GetComponent<CinemachineCamera>();
            var cameraRoot = player.transform.Find("PlayerCameraRoot");
            cmCam.Target.TrackingTarget = cameraRoot;
            var zoom = GetOrAdd<SizeCameraZoom>(followCam);
            var zso = new SerializedObject(zoom);
            zso.FindProperty("shifter").objectReferenceValue = shifter;
            zso.FindProperty("follow").objectReferenceValue = followCam.GetComponent<CinemachineThirdPersonFollow>();
            zso.ApplyModifiedPropertiesWithoutUndo();

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            var hso = new SerializedObject(hud.GetComponent<SizeHUD>());
            hso.FindProperty("shifter").objectReferenceValue = shifter;
            hso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TestScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Growth Spurt] Built {TestScenePath}. Press Play to test.");
        }

        // ---------- zones ----------

        // A 10 m wide, 4 m tall, 6 m deep block with a 1.2 m wide x 1.0 m tall tunnel through the middle.
        private static void BuildTunnel(Transform level, SceneMats m, Vector3 c)
        {
            var z = new GameObject("Zone_Tunnel").transform;
            z.SetParent(level);
            const float w = 1.2f, h = 1.0f, wall = 4.4f, depth = 6f, tall = 4f;
            Box("Tunnel_Left", z, c + new Vector3(-(w / 2 + wall / 2), tall / 2, 0), new Vector3(wall, tall, depth), m.block);
            Box("Tunnel_Right", z, c + new Vector3(w / 2 + wall / 2, tall / 2, 0), new Vector3(wall, tall, depth), m.block);
            Box("Tunnel_Roof", z, c + new Vector3(0, h + (tall - h) / 2, 0), new Vector3(w, tall - h, depth), m.tunnel);
            Label(z, "LOW TUNNEL  (1.0 m tall, 1.2 m wide)\nonly Small fits through", c + new Vector3(0, tall + 1.0f, -depth / 2 - 0.1f), 5f);
            Label(z, "made it! (growing inside the tunnel is blocked)", c + new Vector3(0, 1.8f, depth / 2 + 0.6f), 3f, 180f);
        }

        // A 6 x 6 roof at 3.0 m on four pillars. Normal (1.8 m) fits under it, Big (~4 m) doesn't.
        private static void BuildLowCeiling(Transform level, SceneMats m, Vector3 c)
        {
            var z = new GameObject("Zone_LowCeiling").transform;
            z.SetParent(level);
            const float size = 6f, ceiling = 3.0f, thick = 0.3f, pillar = 0.4f;
            Box("Ceiling_Roof", z, c + new Vector3(0, ceiling + thick / 2, 0), new Vector3(size, thick, size), m.ceiling);
            float o = size / 2 - pillar / 2;
            int i = 0;
            foreach (var p in new[] { new Vector2(-o, -o), new Vector2(o, -o), new Vector2(-o, o), new Vector2(o, o) })
                Box($"Ceiling_Pillar{i++}", z, c + new Vector3(p.x, ceiling / 2, p.y), new Vector3(pillar, ceiling, pillar), m.block);
            Label(z, "LOW CEILING  (3.0 m)\nwalk under as Normal, press E: Big is blocked", c + new Vector3(0, ceiling + 1.3f, -size / 2 - 0.1f), 5f);
        }

        // Blocks of increasing height. Walk into them without jumping.
        // Step offsets: Small 0.10, Normal 0.25, Big 0.55.
        private static void BuildSteps(Transform level, SceneMats m, Vector3 c)
        {
            var z = new GameObject("Zone_Steps").transform;
            z.SetParent(level);
            var steps = new (float h, string who)[]
            {
                (0.08f, "S N B"), (0.20f, "N B"), (0.35f, "B"), (0.45f, "B"), (0.80f, "jump: N B"),
            };
            const float width = 1.8f, gap = 0.6f, depth = 2f;
            float x0 = c.x - (steps.Length - 1) * (width + gap) / 2f;
            for (int i = 0; i < steps.Length; i++)
            {
                float x = x0 + i * (width + gap);
                var (h, who) = steps[i];
                Box($"Step_{h:0.00}m", z, new Vector3(x, h / 2, c.z), new Vector3(width, h, depth), m.step);
                Label(z, $"{h:0.00} m\n<size=75%>{who}</size>", new Vector3(x, h + 1.0f, c.z - depth / 2 - 0.05f), 3.5f);
            }
            Label(z, "STEPS  (walk into them, no jumping)", new Vector3(c.x, 3.0f, c.z - depth / 2 - 0.1f), 5f);
        }

        // A 1 m high walkway along +Z with gaps of 1.5 / 2.5 / 3.5 / 4.5 m. Falling drops you to the ground.
        private static void BuildGaps(Transform level, SceneMats m, float x)
        {
            var z = new GameObject("Zone_Gaps").transform;
            z.SetParent(level);
            const float height = 1f, width = 3f, segment = 3f;
            Ramp("Gaps_Ramp", z, new Vector3(x, 0f, 1f), new Vector3(x, height, 8f), width, m.platform);

            float[] gaps = { 1.5f, 2.5f, 3.5f, 4.5f };
            float start = 8f;
            for (int i = 0; i <= gaps.Length; i++)
            {
                Box($"Gaps_Platform{i}", z, new Vector3(x, height / 2, start + segment / 2), new Vector3(width, height, segment), m.platform);
                if (i == gaps.Length) break;
                float gapStart = start + segment;
                Label(z, $"{gaps[i]:0.0} m gap", new Vector3(x, height + 2.2f, gapStart + gaps[i] / 2), 4.5f);
                start = gapStart + gaps[i];
            }
            Label(z, "GAPS  (sprint + jump)", new Vector3(x, height + 3.2f, 7.9f), 5f);
        }

        // A 4 m tower with a ramp. Jump off it, grow to Big mid-air: slam.
        private static void BuildTallLedge(Transform level, SceneMats m, float x)
        {
            var z = new GameObject("Zone_TallLedge").transform;
            z.SetParent(level);
            const float height = 4f, top = 5f;
            Box("Ledge_Tower", z, new Vector3(x, height / 2, 20.5f), new Vector3(top, height, top), m.ledge);
            Ramp("Ledge_Ramp", z, new Vector3(x, 0f, 5f), new Vector3(x, height, 18f), 3f, m.ledge);
            Label(z, "TALL LEDGE  (4 m)\njump off, press E mid-air: Big slam", new Vector3(x, height + 2.0f, 18f - 0.1f), 5f);
        }

        // A light crate any size can push and a heavy one only Big can push (push strength: S 15, N 30, B 100 kg).
        private static void BuildCrates(Transform level, SceneMats m)
        {
            var z = new GameObject("Zone_Crates").transform;
            z.SetParent(level);
            Crate("Crate_Light_10kg", z, new Vector3(-3f, 0.5f, -8f), 1f, 10f, m.lightCrate);
            Crate("Crate_Heavy_60kg", z, new Vector3(3f, 0.8f, -8f), 1.6f, 60f, m.heavyCrate);
            Label(z, "LIGHT CRATE  10 kg\nany size pushes", new Vector3(-3f, 2.3f, -8f), 3f, 180f);
            Label(z, "HEAVY CRATE  60 kg\nonly Big pushes", new Vector3(3f, 2.9f, -8f), 3f, 180f);
        }

        private static void BuildOpenArea(Transform level)
        {
            var z = new GameObject("Zone_OpenArea").transform;
            z.SetParent(level);
            Label(z, "OPEN AREA\nfeel testing: run, jump, grow & shrink", new Vector3(0f, 3.5f, -16f), 5f, 180f);
            // Flat on the floor in front of the spawn, so it reads from the start position.
            Label(z, "BEHIND YOU: crates + open area", new Vector3(0f, 0.02f, 4.6f), 3f, 0f, 90f);
        }

        // ---------- primitives ----------

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        // A slab whose top surface runs from bottom (at ground) to top. Width along X.
        private static void Ramp(string name, Transform parent, Vector3 bottom, Vector3 top, float width, Material mat)
        {
            const float thick = 0.3f;
            Vector3 along = top - bottom;
            float length = along.magnitude;
            Quaternion rot = Quaternion.LookRotation(along.normalized, Vector3.up);
            Vector3 normal = rot * Vector3.up;
            Vector3 center = (bottom + top) * 0.5f - normal * (thick * 0.5f);
            var go = Box(name, parent, center, new Vector3(width, thick, length + 0.2f), mat);
            go.transform.rotation = rot;
        }

        private static void Crate(string name, Transform parent, Vector3 center, float size, float mass, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // slide, don't tumble
            rb.linearDamping = 0.5f;
        }

        // World-space TextMeshPro label. yaw 0 = readable from the spawn (looking +Z); 180 = readable looking -Z.
        // pitch 90 = lying flat on the floor.
        private static void Label(Transform parent, string text, Vector3 position, float size, float yaw = 0f, float pitch = 0f)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(20, 20, 30, 255);
            tmp.rectTransform.sizeDelta = new Vector2(size * 8f, size * 1.5f);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static bool EnsureTmpResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;
            const string msg = "TextMeshPro Essential Resources are not in the project yet.\n\n" +
                               "Window > TextMeshPro > Import TMP Essential Resources, then run Tools > Growth Spurt > Build S1 Test Scene again.";
            if (Application.isBatchMode) Debug.LogError("[Growth Spurt] " + msg);
            else EditorUtility.DisplayDialog("Import TMP Essential Resources", msg, "OK");
            return false;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetArray<T>(SerializedProperty prop, IList<T> items) where T : Object
        {
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
