using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LanternKeeper.EditorTools
{
    /// <summary>
    /// "Lantern Keeper/" menu: builds the Player prefab, the placeholder prefabs and materials,
    /// and the Sandbox scene from code, so no .prefab or .unity YAML is written by hand.
    /// Every item is safe to run again: assets are overwritten in place (their GUIDs are kept,
    /// so existing references survive) and the Sandbox scene is rebuilt from scratch.
    /// docs/SETUP_PLAYER.md describes the same setup for doing it by hand.
    /// </summary>
    public static class LanternKeeperSetup
    {
        private const string Menu = "Lantern Keeper/";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string MaterialsDir = "Assets/_Project/Materials";
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string InputActionsPath = "Assets/_Project/Settings/LanternKeeper.inputactions";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string CandyPrefabPath = PrefabsDir + "/Candy.prefab";
        private const string CandlePickupPrefabPath = PrefabsDir + "/CandlePickup.prefab";
        private const string CandyPickupPrefabPath = PrefabsDir + "/CandyPickup.prefab";
        private const string SpiritPlatformPrefabPath = PrefabsDir + "/SpiritPlatform.prefab";
        private const string PorchLampPrefabPath = PrefabsDir + "/PorchLamp.prefab";
        private const string HudPrefabPath = PrefabsDir + "/HUD.prefab";
        private const string SandboxScenePath = ScenesDir + "/Sandbox.unity";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        private const string PlayerLayerName = "Player";
        private const string GhostLayerName = "Ghost";
        private const string CameraTargetTag = "CinemachineTarget";

        private const string SandboxPrompt =
            "Hold {flare} to flare  -  {throw} to throw candy  -  {jump} to jump  -  arrow keys turn the camera";

        // ------------------------------------------------------------------ menu items

        [MenuItem(Menu + "Create Player Prefab", priority = 1)]
        public static void CreatePlayerPrefab()
        {
            int ghostLayer = EnsureTagsAndLayers();
            EnsureFolder(PrefabsDir);

            GameObject source = FindStarterPrefab("PlayerArmature");
            if (source == null)
            {
                Debug.LogError("[Lantern Keeper] Starter Assets 'PlayerArmature' prefab not found. Import 'Starter Assets - Third Person' first.");
                return;
            }
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                Debug.LogError($"[Lantern Keeper] Input actions asset missing at {InputActionsPath}.");
                return;
            }

            // An instance of the Starter Assets prefab saved as a new asset becomes a prefab variant.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                player.name = "Player";
                player.tag = "Player";

                var playerInput = player.GetComponent<PlayerInput>();
                playerInput.actions = actions;
                playerInput.defaultActionMap = "Player";
                playerInput.defaultControlScheme = "KeyboardMouse";
                playerInput.neverAutoSwitchControlSchemes = false;

                GetOrAdd<CandleManager>(player);
                var lantern = GetOrAdd<LanternController>(player);
                var thrower = GetOrAdd<CandyThrower>(player);
                GetOrAdd<PlayerKnockback>(player);
                GetOrAdd<PlayerStateTracker>(player);
                GetOrAdd<KeyboardCameraLook>(player);

                // Lantern: hip/hand height, slightly in front of the right hand.
                Transform lanternRoot = GetOrCreateChild(player.transform, "Lantern", new Vector3(0.3f, 1.0f, 0.35f));
                var light = GetOrAdd<Light>(lanternRoot.gameObject);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.62f, 0.25f);
                light.intensity = 4f;
                light.range = 9f;
                light.shadows = LightShadows.None;
                var reveal = GetOrAdd<RevealLight>(lanternRoot.gameObject);
                SetFloat(reveal, "radius", 6f);
                SetRef(reveal, "linkedLight", light);

                Transform visual = lanternRoot.Find("LanternVisual");
                if (visual == null)
                {
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = "LanternVisual";
                    Object.DestroyImmediate(sphere.GetComponent<Collider>());
                    visual = sphere.transform;
                    visual.SetParent(lanternRoot, false);
                }
                visual.localScale = Vector3.one * 0.22f;
                var visualRenderer = visual.GetComponent<MeshRenderer>();
                visualRenderer.sharedMaterial = MakeMaterial("M_Lantern", new Color(1f, 0.55f, 0.1f), false, new Color(2.5f, 1.2f, 0.25f));
                visualRenderer.shadowCastingMode = ShadowCastingMode.Off;

                Transform throwOrigin = GetOrCreateChild(player.transform, "ThrowOrigin", new Vector3(0.3f, 1.3f, 0.5f));

                SetRef(lantern, "lanternLight", light);
                SetRef(lantern, "revealLight", reveal);
                SetLayerMask(lantern, "ghostLayers", 1 << ghostLayer);

                SetRef(thrower, "throwOrigin", throwOrigin);
                var candyPrefab = AssetDatabase.LoadAssetAtPath<Candy>(CandyPrefabPath);
                if (candyPrefab != null) SetRef(thrower, "candyPrefab", candyPrefab);
                else Debug.LogWarning("[Lantern Keeper] Candy prefab not found yet; run 'Create Placeholder Assets' and it will be wired into the Player prefab.");

                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath, out bool saved);
                Debug.Log(saved
                    ? $"[Lantern Keeper] Player prefab saved to {PlayerPrefabPath}"
                    : "[Lantern Keeper] FAILED to save the Player prefab.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem(Menu + "Create Placeholder Assets", priority = 2)]
        public static void CreatePlaceholderAssets()
        {
            int ghostLayer = EnsureTagsAndLayers();
            EnsureFolder(PrefabsDir);
            EnsureFolder(MaterialsDir);

            Material candyMat = MakeMaterial("M_Candy", new Color(1f, 0.35f, 0.65f), false, new Color(0.6f, 0.15f, 0.35f));
            Material candleMat = MakeMaterial("M_CandlePickup", new Color(1f, 0.9f, 0.6f), false, new Color(1.2f, 0.9f, 0.4f));
            Material candyPickupMat = MakeMaterial("M_CandyPickup", new Color(0.65f, 0.3f, 1f), false, new Color(0.5f, 0.2f, 0.9f));
            Material platformMat = MakeMaterial("M_SpiritPlatform", new Color(0.3f, 0.95f, 1f, 0.85f), true, new Color(0.1f, 0.5f, 0.6f));
            Material lampPostMat = MakeMaterial("M_LampPost", new Color(0.12f, 0.1f, 0.09f));
            Material lampGlowMat = MakeMaterial("M_LampGlow", new Color(1f, 0.7f, 0.3f), false, new Color(3f, 1.8f, 0.6f));
            MakeMaterial("M_Ground", new Color(0.16f, 0.2f, 0.13f));
            MakeMaterial("M_Pit", new Color(0.05f, 0.06f, 0.09f));
            MakeMaterial("M_TestDummy", new Color(0.3f, 0.9f, 0.4f));

            // Candy: small sphere + Rigidbody + Candy.
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Candy";
                go.transform.localScale = Vector3.one * 0.2f;
                go.GetComponent<MeshRenderer>().sharedMaterial = candyMat;
                var body = go.AddComponent<Rigidbody>();
                body.mass = 0.1f;
                body.linearDamping = 0.1f;
                body.angularDamping = 2f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                go.AddComponent<Candy>();
                SavePrefab(go, CandyPrefabPath);
            }

            // Pickups: still trigger on the root, spinning mesh on a child.
            {
                GameObject go = NewPickupRoot<CandlePickup>("CandlePickup", PrimitiveType.Cylinder,
                    new Vector3(0.18f, 0.22f, 0.18f), candleMat);
                SavePrefab(go, CandlePickupPrefabPath);
            }
            {
                GameObject go = NewPickupRoot<CandyPickup>("CandyPickup", PrimitiveType.Cube,
                    new Vector3(0.3f, 0.3f, 0.3f), candyPickupMat);
                SavePrefab(go, CandyPickupPrefabPath);
            }

            // Spirit platform: cube, transparent cyan URP Lit, on Default so the player treats it as ground.
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "SpiritPlatform";
                go.transform.localScale = new Vector3(2f, 0.3f, 2f);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = platformMat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                go.AddComponent<SpiritPlatform>();
                SavePrefab(go, SpiritPlatformPrefabPath);
            }

            // Porch lamp: post + glowing head with a point light and a RevealLight.
            {
                var go = new GameObject("PorchLamp");
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Post";
                post.transform.SetParent(go.transform, false);
                post.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                post.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
                post.GetComponent<MeshRenderer>().sharedMaterial = lampPostMat;

                var head = new GameObject("Lamp");
                head.transform.SetParent(go.transform, false);
                head.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                var light = head.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.7f, 0.35f);
                light.intensity = 3f;
                light.range = 6f;
                light.shadows = LightShadows.None;
                var reveal = head.AddComponent<RevealLight>();
                SetFloat(reveal, "radius", 4f);
                SetRef(reveal, "linkedLight", light);

                GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = "Glow";
                Object.DestroyImmediate(glow.GetComponent<Collider>());
                glow.transform.SetParent(head.transform, false);
                glow.transform.localScale = Vector3.one * 0.35f;
                var glowRenderer = glow.GetComponent<MeshRenderer>();
                glowRenderer.sharedMaterial = lampGlowMat;
                glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                SavePrefab(go, PorchLampPrefabPath);
            }

            if (EnsureTmpEssentials()) CreateHudPrefab();
            else Debug.LogWarning("[Lantern Keeper] HUD prefab skipped: TMP Essential Resources are still importing. Run this menu item again.");

            // The Player prefab needs the Candy prefab; (re)wire it if the player already exists.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var candy = AssetDatabase.LoadAssetAtPath<Candy>(CandyPrefabPath);
            if (playerPrefab != null && candy != null && playerPrefab.TryGetComponent(out CandyThrower thrower))
            {
                SetRef(thrower, "candyPrefab", candy);
                SetLayerMask(playerPrefab.GetComponent<LanternController>(), "ghostLayers", 1 << ghostLayer);
                PrefabUtility.SavePrefabAsset(playerPrefab);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Lantern Keeper] Placeholder prefabs and materials created.");
        }

        [MenuItem(Menu + "Build Sandbox Scene", priority = 3)]
        public static void BuildSandboxScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int ghostLayer = EnsureTagsAndLayers();
            if (!EnsureTmpEssentials())
            {
                // The package import finishes after this call returns, so pick the build up again when it lands.
                Debug.Log("[Lantern Keeper] Importing TMP Essential Resources first; the sandbox build continues automatically when that finishes.");
                AssetDatabase.importPackageCompleted += ContinueSandboxAfterTmpImport;
                return;
            }

            // Always regenerate the prefabs so the scene matches the current scripts.
            CreatePlaceholderAssets();
            CreatePlayerPrefab();

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpiritPlatformPrefabPath);
            GameObject lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PorchLampPrefabPath);
            GameObject candlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandlePickupPrefabPath);
            GameObject candyPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyPickupPrefabPath);
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            GameObject mainCameraPrefab = FindStarterPrefab("MainCamera");
            GameObject followCameraPrefab = FindStarterPrefab("PlayerFollowCamera");
            if (playerPrefab == null || platformPrefab == null || lampPrefab == null || candlePrefab == null
                || candyPickupPrefab == null || hudPrefab == null || mainCameraPrefab == null || followCameraPrefab == null)
            {
                Debug.LogError("[Lantern Keeper] Sandbox not built: a required prefab is missing (see earlier messages).");
                return;
            }

            EnsureFolder(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Night lighting: flat, very dark ambient and a dim blue "moon".
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.045f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.05f);
            RenderSettings.fogDensity = 0.02f;

            var moon = new GameObject("Moon (Directional Light)");
            moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.55f, 0.65f, 1f);
            moonLight.intensity = 0.12f;
            moonLight.shadows = LightShadows.Soft;

            // Layout along +Z. Ground tops sit at y = 0; the pit floor is 1 m down so you can jump back out.
            //   Ground A  z -10..10   spawn, pickups, porch lamp
            //   Gap 1     z  10..14   4 m, three spirit platforms, lamp reveals the first
            //   Ground B  z  14..34   pickups, test dummy
            //   Gap 2     z  34..44   10 m, six spirit platforms: flare here and they vanish
            //   Ground C  z  44..56
            var environment = new GameObject("Environment").transform;
            Material groundMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/M_Ground.mat");
            Material pitMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/M_Pit.mat");
            MakeBox("Ground A", environment, new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), groundMat);
            MakeBox("Ground B", environment, new Vector3(0f, -0.5f, 24f), new Vector3(20f, 1f, 20f), groundMat);
            MakeBox("Ground C", environment, new Vector3(0f, -0.5f, 50f), new Vector3(20f, 1f, 12f), groundMat);
            MakeBox("Pit Floor", environment, new Vector3(0f, -1.5f, 23f), new Vector3(20f, 1f, 66f), pitMat);

            var gameplay = new GameObject("Gameplay").transform;

            var bridge1 = new GameObject("Spirit Bridge 1 (4 m gap, lamp shows first platform)").transform;
            bridge1.SetParent(gameplay, false);
            float[] bridge1Z = { 10.75f, 12f, 13.25f };
            for (int i = 0; i < bridge1Z.Length; i++)
            {
                GameObject platform = PlacePrefab(platformPrefab, $"SpiritPlatform 1-{i + 1}", bridge1, new Vector3(0f, -0.15f, bridge1Z[i]));
                platform.transform.localScale = new Vector3(2f, 0.3f, 1f);
                RecordOverrides(platform.transform);
            }
            // Placed so its 4 m reveal radius reaches a corner of platform 1 but none of platform 2.
            PlacePrefab(lampPrefab, "PorchLamp", gameplay, new Vector3(3.6f, 0f, 9f));

            var bridge2 = new GameObject("Spirit Bridge 2 (10 m gap, stop flaring mid-bridge)").transform;
            bridge2.SetParent(gameplay, false);
            const int bridge2Count = 6;
            const float bridge2Length = 1.5f;
            float bridge2Spacing = (10f - bridge2Count * bridge2Length) / (bridge2Count + 1);
            for (int i = 0; i < bridge2Count; i++)
            {
                float z = 34f + bridge2Spacing * (i + 1) + bridge2Length * (i + 0.5f);
                GameObject platform = PlacePrefab(platformPrefab, $"SpiritPlatform 2-{i + 1}", bridge2, new Vector3(0f, -0.15f, z));
                platform.transform.localScale = new Vector3(2f, 0.3f, bridge2Length);
                RecordOverrides(platform.transform);
            }

            PlacePrefab(candlePrefab, "CandlePickup 1", gameplay, new Vector3(-3f, 0.7f, 2f));
            PlacePrefab(candlePrefab, "CandlePickup 2", gameplay, new Vector3(-3f, 0.7f, 22f));
            PlacePrefab(candyPickupPrefab, "CandyPickup 1", gameplay, new Vector3(3f, 0.7f, 2f));
            PlacePrefab(candyPickupPrefab, "CandyPickup 2", gameplay, new Vector3(3f, 0.7f, 18f));

            // Stand-in ghost: on the Ghost layer so the flare's overlap finds it.
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "TestDummy";
            dummy.layer = ghostLayer;
            dummy.transform.SetParent(gameplay, false);
            dummy.transform.position = new Vector3(4f, 1f, 26f);
            dummy.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/M_TestDummy.mat");
            dummy.AddComponent<TestDummy>();

            // Player + Starter Assets camera rig.
            GameObject player = PlacePrefab(playerPrefab, "Player", null, new Vector3(0f, 0f, -6f));
            GameObject mainCamera = PlacePrefab(mainCameraPrefab, "MainCamera", null, new Vector3(0f, 2f, -10f));
            if (mainCamera.TryGetComponent(out Camera camera))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.01f, 0.012f, 0.03f);
                RecordOverrides(camera);
            }
            GameObject followCamera = PlacePrefab(followCameraPrefab, "PlayerFollowCamera", null, new Vector3(0.5f, 1.375f, -10f));
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

            GameObject hud = PlacePrefab(hudPrefab, "HUD", null, Vector3.zero);
            if (hud.TryGetComponent(out HUD hudComponent))
            {
                SetString(hudComponent, "startPrompt", SandboxPrompt);
                RecordOverrides(hudComponent);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SandboxScenePath);
            AddSceneToBuildSettings(SandboxScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lantern Keeper] Sandbox scene saved to {SandboxScenePath} and added to Build Settings.");
        }

        /// <summary>Runs all three steps. Also the entry point for batch mode (-executeMethod).</summary>
        [MenuItem(Menu + "Run All Setup", priority = 20)]
        public static void RunAll()
        {
            // Building the sandbox regenerates the placeholder assets and the Player prefab first.
            BuildSandboxScene();
        }

        private static void ContinueSandboxAfterTmpImport(string packageName)
        {
            AssetDatabase.importPackageCompleted -= ContinueSandboxAfterTmpImport;
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath) != null) BuildSandboxScene();
            else Debug.LogError("[Lantern Keeper] TMP Essential Resources did not import. Use Window > TextMeshPro > Import TMP Essential Resources, then run 'Build Sandbox Scene' again.");
        }

        /// <summary>
        /// Batch-mode entry point (-executeMethod, WITHOUT -quit): imports TMP Essential Resources
        /// and exits the editor once the import has finished.
        /// </summary>
        public static void ImportTmpEssentials()
        {
            if (EnsureTmpEssentials())
            {
                Debug.Log("[Lantern Keeper] TMP Essential Resources present.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            AssetDatabase.importPackageCompleted += _ =>
            {
                AssetDatabase.Refresh();
                Debug.Log("[Lantern Keeper] TMP Essential Resources imported.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            };
        }

        // ------------------------------------------------------------------ HUD

        private static void CreateHudPrefab()
        {
            var root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Candle bar, bottom-left.
            RectTransform bar = NewRect("CandleBar", root.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(40f, 40f), new Vector2(420f, 40f));
            bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            RectTransform fill = NewRect("Fill", bar, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(1f, 0.6f, 0.1f);
            fillImage.raycastTarget = false;

            RectTransform candleLabelRect = NewRect("CandleLabel", bar, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI candleLabel = NewText(candleLabelRect, "Candle 100%", 24f, TextAlignmentOptions.Center);

            RectTransform candyRect = NewRect("CandyLabel", root.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(40f, 90f), new Vector2(420f, 40f));
            TextMeshProUGUI candyLabel = NewText(candyRect, "Candy 0/5", 28f, TextAlignmentOptions.Left);

            RectTransform promptRect = NewRect("Prompt", root.transform, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 140f));
            var promptGroup = promptRect.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            promptGroup.interactable = false;
            promptGroup.blocksRaycasts = false;
            TextMeshProUGUI promptLabel = NewText(promptRect, "", 36f, TextAlignmentOptions.Center);

            var hud = root.AddComponent<HUD>();
            SetRef(hud, "candleFill", fill);
            SetRef(hud, "candleFillImage", fillImage);
            SetRef(hud, "candleLabel", candleLabel);
            SetRef(hud, "candyLabel", candyLabel);
            SetRef(hud, "promptLabel", promptLabel);
            SetRef(hud, "promptGroup", promptGroup);

            SavePrefab(root, HudPrefabPath);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static TextMeshProUGUI NewText(RectTransform rect, string text, float size, TextAlignmentOptions alignment)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        // ------------------------------------------------------------------ project settings

        /// <summary>Makes sure the tag and layers exist. Returns the Ghost layer index.</summary>
        private static int EnsureTagsAndLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[Lantern Keeper] Could not open ProjectSettings/TagManager.asset; add the layers by hand (see docs/SETUP_PLAYER.md).");
                return 9;
            }
            var tagManager = new SerializedObject(assets[0]);

            SerializedProperty tags = tagManager.FindProperty("tags");
            bool hasTag = false;
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == CameraTargetTag) hasTag = true;
            }
            if (!hasTag)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = CameraTargetTag;
            }

            SerializedProperty layers = tagManager.FindProperty("layers");
            // Starter Assets puts PlayerArmature on layer 8 without naming it.
            EnsureLayer(layers, PlayerLayerName, 8);
            int ghostLayer = EnsureLayer(layers, GhostLayerName, 9);

            if (tagManager.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssets();
            return ghostLayer;
        }

        private static int EnsureLayer(SerializedProperty layers, string name, int preferredIndex)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
            }
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(preferredIndex).stringValue))
            {
                layers.GetArrayElementAtIndex(preferredIndex).stringValue = name;
                return preferredIndex;
            }
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = name;
                return i;
            }
            Debug.LogError($"[Lantern Keeper] No free layer slot for '{name}'.");
            return preferredIndex;
        }

        /// <summary>True if TMP Essential Resources are in the project. Starts the import and returns false if not.</summary>
        private static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath) != null) return true;
            UnityEditor.AssetPackage.Package.Import(TmpEssentialsPackage, false);
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath) != null;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != scenePath) continue;
                if (scenes[i].enabled) return;
                scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ asset helpers

        private static GameObject FindStarterPrefab(string prefabName)
        {
            foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/ThirdPersonController/Prefabs/" + prefabName + ".prefab"))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
            return null;
        }

        /// <summary>Creates the material, or updates it in place if it already exists.</summary>
        private static Material MakeMaterial(string name, Color color, bool transparent = false, Color? emission = null)
        {
            EnsureFolder(MaterialsDir);
            string path = $"{MaterialsDir}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Lantern Keeper] URP Lit shader not found; falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);

            if (transparent)
            {
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f);   // Alpha
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.SetShaderPassEnabled("DepthOnly", false);
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject NewPickupRoot<T>(string name, PrimitiveType shape, Vector3 visualScale, Material material)
            where T : Pickup
        {
            var root = new GameObject(name);
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;

            GameObject visual = GameObject.CreatePrimitive(shape);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = visualScale;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            T pickup = root.AddComponent<T>();
            SetRef(pickup, "visual", visual.transform);
            return root;
        }

        private static void SavePrefab(GameObject temp, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(temp, path, out bool saved);
            if (!saved) Debug.LogError($"[Lantern Keeper] FAILED to save prefab {path}");
            Object.DestroyImmediate(temp);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        // ------------------------------------------------------------------ scene helpers

        private static void MakeBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            if (material != null) box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static GameObject PlacePrefab(GameObject prefab, string name, Transform parent, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            if (parent != null) instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            RecordOverrides(instance);
            RecordOverrides(instance.transform);
            return instance;
        }

        private static void RecordOverrides(Object target) => PrefabUtility.RecordPrefabInstancePropertyModifications(target);

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        // ------------------------------------------------------------------ component helpers

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
        }

        private static Transform GetOrCreateChild(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            return child;
        }

        private static SerializedProperty Find(SerializedObject so, Object target, string property)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null) Debug.LogError($"[Lantern Keeper] {target.GetType().Name} has no serialized field '{property}'; wire it by hand.", target);
            return p;
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, property);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, property);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, property);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerMask(Object target, string property, int mask)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, property);
            if (p == null) return;
            p.intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
