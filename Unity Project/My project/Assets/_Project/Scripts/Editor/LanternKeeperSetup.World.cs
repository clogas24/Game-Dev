using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LanternKeeper.EditorTools
{
    // Step 3 half of the "Lantern Keeper/" menu: the ghost, the world interactables, the menu
    // canvas, and the sandbox test area with its NavMesh. Same rules as the rest of the class:
    // every item overwrites its assets in place and can be run again.
    public static partial class LanternKeeperSetup
    {
        private const string GhostPrefabPath = PrefabsDir + "/Ghost.prefab";
        private const string PressurePlatePrefabPath = PrefabsDir + "/PressurePlate.prefab";
        private const string GatePrefabPath = PrefabsDir + "/Gate.prefab";
        private const string CheckpointPrefabPath = PrefabsDir + "/Checkpoint.prefab";
        private const string SectionTriggerPrefabPath = PrefabsDir + "/SectionTrigger.prefab";
        private const string TutorialTriggerPrefabPath = PrefabsDir + "/TutorialTrigger.prefab";
        private const string GoalTriggerPrefabPath = PrefabsDir + "/GoalTrigger.prefab";
        private const string CandyBowlPrefabPath = PrefabsDir + "/CandyBowl.prefab";
        private const string GameMenusPrefabPath = PrefabsDir + "/GameMenus.prefab";
        private const string SandboxNavMeshPath = ScenesDir + "/Sandbox-NavMesh.asset";

        // ------------------------------------------------------------------ menu items

        [MenuItem(Menu + "Create Ghost Prefab", priority = 4)]
        public static void CreateGhostPrefab()
        {
            int ghostLayer = EnsureTagsAndLayers();
            EnsureFolder(PrefabsDir);

            Material bodyMat = MakeMaterial("M_Ghost", new Color(0.35f, 1f, 0.45f, 0.7f), true);
            Material headMat = MakeMaterial("M_GhostHead", new Color(0.85f, 1f, 0.9f, 0.5f), true);

            var root = new GameObject("Ghost");

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.9f;
            agent.speed = 1.6f;
            agent.angularSpeed = 480f;
            agent.acceleration = 16f;
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // A moving collider needs a (kinematic) Rigidbody; the NavMeshAgent does the moving.
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // Trigger: found by the flare's overlap and by pressure plates and gates; never blocks the player.
            var trigger = root.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.55f;
            trigger.height = 1.9f;
            trigger.center = new Vector3(0f, 0.95f, 0f);

            GameObject bodyVisual = NewVisual(PrimitiveType.Capsule, "Body", root.transform,
                new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 0.8f, 0.8f), bodyMat);
            GameObject headVisual = NewVisual(PrimitiveType.Sphere, "Head", root.transform,
                new Vector3(0f, 1.75f, 0f), Vector3.one * 0.65f, headMat);

            var glow = new GameObject("Glow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var glowLight = glow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.35f, 1f, 0.45f);
            glowLight.intensity = 1.5f;
            glowLight.range = 4f;
            glowLight.shadows = LightShadows.None;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ghostLayer;

            var ghost = root.AddComponent<GhostAI>();
            SetRefArray(ghost, "tintRenderers", bodyVisual.GetComponent<Renderer>(), headVisual.GetComponent<Renderer>());
            SetRef(ghost, "glowLight", glowLight);

            SavePrefab(root, GhostPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lantern Keeper] Ghost prefab saved to {GhostPrefabPath}");
        }

        [MenuItem(Menu + "Create World Prefabs", priority = 5)]
        public static void CreateWorldPrefabs()
        {
            EnsureTagsAndLayers();
            EnsureFolder(PrefabsDir);

            Material plateBaseMat = MakeMaterial("M_PlateBase", new Color(0.22f, 0.22f, 0.25f));
            Material platePadMat = MakeMaterial("M_PlatePad", new Color(0.55f, 0.4f, 0.2f), false, new Color(0.12f, 0.09f, 0.04f));
            Material gateMat = MakeMaterial("M_GateDoor", new Color(0.3f, 0.22f, 0.4f), false, new Color(0.08f, 0.05f, 0.12f));
            Material frameMat = MakeMaterial("M_GateFrame", new Color(0.14f, 0.12f, 0.11f));
            Material doorMat = MakeMaterial("M_FarmhouseDoor", new Color(0.5f, 0.28f, 0.12f), false, new Color(0.25f, 0.13f, 0.04f));
            Material bowlMat = MakeMaterial("M_CandyBowl", new Color(1f, 0.5f, 0.1f), false, new Color(0.9f, 0.4f, 0.05f));
            Material lampPostMat = MakeMaterial("M_LampPost", new Color(0.12f, 0.1f, 0.09f));
            Material lampGlowMat = MakeMaterial("M_LampGlow", new Color(1f, 0.7f, 0.3f), false, new Color(3f, 1.8f, 0.6f));
            MakeMaterial("M_Wall", new Color(0.13f, 0.14f, 0.18f));

            // Pressure plate: thin base you can walk over, pad that sinks and changes colour.
            {
                var root = new GameObject("PressurePlate");
                NewVisual(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.03f, 0f),
                    new Vector3(2.2f, 0.06f, 2.2f), plateBaseMat, true);
                GameObject pad = NewVisual(PrimitiveType.Cube, "Pad", root.transform, new Vector3(0f, 0.1f, 0f),
                    new Vector3(2f, 0.08f, 2f), platePadMat);
                var plate = root.AddComponent<PressurePlate>();
                SetRef(plate, "pad", pad.transform);
                SetRef(plate, "padRenderer", pad.GetComponent<Renderer>());
                SavePrefab(root, PressurePlatePrefabPath);
            }

            // Gate: fixed frame + a door that slides down into the ground. 3 m wide doorway.
            {
                var root = new GameObject("Gate");
                var frame = new GameObject("Frame");
                frame.transform.SetParent(root.transform, false);
                GameObject left = NewVisual(PrimitiveType.Cube, "Pillar L", frame.transform, new Vector3(-1.7f, 1.7f, 0f),
                    new Vector3(0.4f, 3.4f, 0.5f), frameMat, true);
                GameObject right = NewVisual(PrimitiveType.Cube, "Pillar R", frame.transform, new Vector3(1.7f, 1.7f, 0f),
                    new Vector3(0.4f, 3.4f, 0.5f), frameMat, true);
                NewVisual(PrimitiveType.Cube, "Beam", frame.transform, new Vector3(0f, 3.6f, 0f),
                    new Vector3(3.8f, 0.4f, 0.5f), frameMat, true);
                AddCarvingObstacle(left, true);
                AddCarvingObstacle(right, true);

                GameObject door = NewVisual(PrimitiveType.Cube, "Door", root.transform, new Vector3(0f, 1.5f, 0f),
                    new Vector3(3f, 3f, 0.25f), gateMat, true);
                var doorBody = door.AddComponent<Rigidbody>();
                doorBody.isKinematic = true;
                doorBody.useGravity = false;
                // Blocks ghosts while shut; stops carving once the door has slid below the NavMesh.
                AddCarvingObstacle(door, false);

                var gate = root.AddComponent<Gate>();
                SetRef(gate, "door", door.transform);
                SavePrefab(root, GatePrefabPath);
            }

            // Checkpoint: an unlit porch lantern that lights up, with a respawn point beside it.
            {
                var root = new GameObject("Checkpoint");
                var trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 1.6f;
                trigger.center = new Vector3(0f, 1f, 0f);

                NewVisual(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 1.1f, 0f),
                    new Vector3(0.14f, 1.1f, 0.14f), lampPostMat, true);

                var lamp = new GameObject("Lamp");
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.75f, 0.35f);
                light.intensity = 4f;
                light.range = 7.5f;
                light.shadows = LightShadows.None;
                var reveal = lamp.AddComponent<RevealLight>();
                SetFloat(reveal, "radius", 5f);
                SetRef(reveal, "linkedLight", light);

                NewVisual(PrimitiveType.Cube, "Lantern (unlit)", lamp.transform, Vector3.zero, Vector3.one * 0.34f, lampPostMat);
                GameObject glow = NewVisual(PrimitiveType.Sphere, "Lantern (lit)", lamp.transform, Vector3.zero,
                    Vector3.one * 0.5f, lampGlowMat);

                var respawn = new GameObject("RespawnPoint");
                respawn.transform.SetParent(root.transform, false);
                respawn.transform.localPosition = new Vector3(0f, 0f, -1.5f);

                var checkpoint = root.AddComponent<Checkpoint>();
                SetRef(checkpoint, "respawnPoint", respawn.transform);
                SetRef(checkpoint, "lampLight", light);
                SetRef(checkpoint, "revealLight", reveal);
                SetRef(checkpoint, "litVisual", glow);
                SavePrefab(root, CheckpointPrefabPath);
            }

            // Invisible trigger volumes. Scale the instance to span the path.
            {
                var root = new GameObject("SectionTrigger");
                AddTriggerBox(root, new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 1f));
                root.AddComponent<SectionTrigger>();
                SavePrefab(root, SectionTriggerPrefabPath);
            }
            {
                var root = new GameObject("TutorialTrigger");
                AddTriggerBox(root, new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 1f));
                root.AddComponent<TutorialTrigger>();
                SavePrefab(root, TutorialTriggerPrefabPath);
            }

            // Goal: the farmhouse door with a trigger in front of it (the -Z side).
            {
                var root = new GameObject("GoalTrigger");
                AddTriggerBox(root, new Vector3(0f, 1.3f, -0.9f), new Vector3(2.6f, 2.6f, 1.6f));
                NewVisual(PrimitiveType.Cube, "Door", root.transform, new Vector3(0f, 1.2f, 0f),
                    new Vector3(1.4f, 2.4f, 0.2f), doorMat, true);
                NewVisual(PrimitiveType.Cube, "Frame", root.transform, new Vector3(0f, 1.3f, 0.15f),
                    new Vector3(2f, 2.8f, 0.2f), frameMat, true);
                var porch = new GameObject("PorchLight");
                porch.transform.SetParent(root.transform, false);
                porch.transform.localPosition = new Vector3(0f, 2.9f, -0.6f);
                var light = porch.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.8f, 0.45f);
                light.intensity = 3f;
                light.range = 7f;
                light.shadows = LightShadows.None;
                root.AddComponent<GoalTrigger>();
                SavePrefab(root, GoalTriggerPrefabPath);
            }

            // Candy bowl: reusable candy refill.
            {
                var root = new GameObject("CandyBowl");
                var trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 1.2f;
                trigger.center = new Vector3(0f, 0.7f, 0f);
                NewVisual(PrimitiveType.Cylinder, "Stand", root.transform, new Vector3(0f, 0.35f, 0f),
                    new Vector3(0.3f, 0.35f, 0.3f), lampPostMat, true);
                NewVisual(PrimitiveType.Sphere, "Bowl", root.transform, new Vector3(0f, 0.8f, 0f),
                    new Vector3(0.8f, 0.35f, 0.8f), bowlMat);
                root.AddComponent<CandyBowl>();
                SavePrefab(root, CandyBowlPrefabPath);
            }

            if (EnsureTmpEssentials()) CreateGameMenusPrefab();
            else Debug.LogWarning("[Lantern Keeper] GameMenus prefab skipped: TMP Essential Resources are still importing. Run this menu item again.");

            AssetDatabase.SaveAssets();
            Debug.Log("[Lantern Keeper] World prefabs created.");
        }

        // ------------------------------------------------------------------ menus (Title / Pause / Win)

        private static void CreateGameMenusPrefab()
        {
            var root = new GameObject("GameMenus", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above the HUD
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Buttons and the slider need an EventSystem; it travels with the prefab.
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(root.transform, false);

            // Title
            RectTransform title = NewPanel("TitlePanel", root.transform, new Color(0.01f, 0.012f, 0.03f, 0.88f));
            NewText(NewAnchored("GameName", title, 0.80f, new Vector2(1600f, 140f)), "LANTERN KEEPER", 110f, TextAlignmentOptions.Center)
                .color = new Color(1f, 0.62f, 0.2f);
            NewText(NewAnchored("Tagline", title, 0.69f, new Vector2(1600f, 60f)),
                "Cross the haunted patch before your candle burns out.", 34f, TextAlignmentOptions.Center);
            TextMeshProUGUI controls = NewText(NewAnchored("Controls", title, 0.42f, new Vector2(1100f, 400f)), "", 32f, TextAlignmentOptions.Center);
            NewText(NewAnchored("PressAnyKey", title, 0.12f, new Vector2(1600f, 60f)),
                "Press any key to start", 40f, TextAlignmentOptions.Center).color = new Color(1f, 0.85f, 0.5f);

            // Pause
            RectTransform pause = NewPanel("PausePanel", root.transform, new Color(0f, 0f, 0f, 0.8f));
            NewText(NewAnchored("Heading", pause, 0.78f, new Vector2(1200f, 110f)), "Paused", 84f, TextAlignmentOptions.Center);
            Button resume = NewButton("ResumeButton", pause, 0.62f, "Resume");
            Button restart = NewButton("RestartButton", pause, 0.52f, "Restart");
            Button quit = NewButton("QuitButton", pause, 0.42f, "Quit");
            NewText(NewAnchored("SensitivityLabel", pause, 0.30f, new Vector2(900f, 50f)),
                "Camera sensitivity", 30f, TextAlignmentOptions.Center);
            Slider slider = NewSlider("SensitivitySlider", pause, 0.24f);
            RectTransform valueRect = NewAnchored("SensitivityValue", pause, 0.24f, new Vector2(120f, 50f));
            valueRect.anchoredPosition = new Vector2(320f, 0f);
            TextMeshProUGUI sensitivityValue = NewText(valueRect, "1.0", 30f, TextAlignmentOptions.Left);
            NewText(NewAnchored("Hint", pause, 0.12f, new Vector2(1400f, 50f)),
                "Esc resumes  -  arrow keys and Enter work in this menu", 26f, TextAlignmentOptions.Center)
                .color = new Color(1f, 1f, 1f, 0.7f);

            // Win
            RectTransform win = NewPanel("WinPanel", root.transform, new Color(0.02f, 0.015f, 0f, 0.88f));
            NewText(NewAnchored("Heading", win, 0.76f, new Vector2(1700f, 110f)), "You reached the farmhouse!", 78f, TextAlignmentOptions.Center)
                .color = new Color(1f, 0.75f, 0.3f);
            TextMeshProUGUI stats = NewText(NewAnchored("Stats", win, 0.52f, new Vector2(1200f, 240f)), "", 42f, TextAlignmentOptions.Center);
            Button winRestart = NewButton("PlayAgainButton", win, 0.30f, "Play again");
            NewText(NewAnchored("Hint", win, 0.18f, new Vector2(1400f, 50f)),
                "Press R to play again", 30f, TextAlignmentOptions.Center).color = new Color(1f, 1f, 1f, 0.7f);

            // GameMenus shows the right panel in Start; saved hidden so nothing flashes.
            title.gameObject.SetActive(false);
            pause.gameObject.SetActive(false);
            win.gameObject.SetActive(false);

            var menus = root.AddComponent<GameMenus>();
            SetRef(menus, "titlePanel", title.gameObject);
            SetRef(menus, "pausePanel", pause.gameObject);
            SetRef(menus, "winPanel", win.gameObject);
            SetRef(menus, "controlsLabel", controls);
            SetRef(menus, "resumeButton", resume);
            SetRef(menus, "restartButton", restart);
            SetRef(menus, "quitButton", quit);
            SetRef(menus, "sensitivitySlider", slider);
            SetRef(menus, "sensitivityValueLabel", sensitivityValue);
            SetRef(menus, "winStatsLabel", stats);
            SetRef(menus, "winRestartButton", winRestart);

            SavePrefab(root, GameMenusPrefabPath);
        }

        private static RectTransform NewPanel(string name, Transform parent, Color background)
        {
            RectTransform rect = NewRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.gameObject.AddComponent<Image>().color = background;
            return rect;
        }

        // A box centred horizontally at a height given as a fraction of the screen.
        private static RectTransform NewAnchored(string name, Transform parent, float height, Vector2 size)
        {
            var anchor = new Vector2(0.5f, height);
            return NewRect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        private static Button NewButton(string name, Transform parent, float height, string label)
        {
            RectTransform rect = NewAnchored(name, parent, height, new Vector2(420f, 80f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // Strong highlight and selected tints, so the keyboard selection is easy to see.
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.22f, 0.2f, 0.28f);
            colors.highlightedColor = new Color(0.85f, 0.5f, 0.1f);
            colors.selectedColor = new Color(0.85f, 0.5f, 0.1f);
            colors.pressedColor = new Color(1f, 0.75f, 0.35f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            RectTransform textRect = NewRect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            NewText(textRect, label, 36f, TextAlignmentOptions.Center);
            return button;
        }

        private static Slider NewSlider(string name, Transform parent, float height)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var anchor = new Vector2(0.5f, height);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(480f, 30f);

            // No sprites in a code-built slider, so tell the parts apart by colour.
            TintChild(go.transform, "Background", new Color(0.22f, 0.2f, 0.28f));
            TintChild(go.transform, "Fill Area/Fill", new Color(0.85f, 0.5f, 0.1f));
            TintChild(go.transform, "Handle Slide Area/Handle", Color.white);

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0.1f;
            slider.maxValue = 3f;
            slider.value = 1f;
            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.5f);
            colors.selectedColor = new Color(1f, 0.75f, 0.35f);
            slider.colors = colors;
            return slider;
        }

        private static void TintChild(Transform root, string path, Color color)
        {
            Transform child = root.Find(path);
            if (child != null && child.TryGetComponent(out Image image)) image.color = color;
        }

        // ------------------------------------------------------------------ sandbox test area

        /// <summary>
        /// Adds the Step 3 test area to the sandbox being built: checkpoints, ghosts, candy bowls,
        /// a one-plate gate, a two-plate gate, the goal, tutorial and section triggers, the
        /// GameManager, menus and debug overlay, then bakes the NavMesh.
        /// </summary>
        private static bool BuildSandboxTestArea(Transform environment, Transform gameplay)
        {
            GameObject ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabPath);
            GameObject platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PressurePlatePrefabPath);
            GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath);
            GameObject checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
            GameObject sectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionTriggerPrefabPath);
            GameObject tutorialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialTriggerPrefabPath);
            GameObject goalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoalTriggerPrefabPath);
            GameObject bowlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyBowlPrefabPath);
            GameObject menusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenusPrefabPath);
            if (ghostPrefab == null || platePrefab == null || gatePrefab == null || checkpointPrefab == null || sectionPrefab == null
                || tutorialPrefab == null || goalPrefab == null || bowlPrefab == null || menusPrefab == null)
            {
                Debug.LogError("[Lantern Keeper] A Step 3 prefab is missing; run 'Create Ghost Prefab' and 'Create World Prefabs'.");
                return false;
            }

            // Walls with a 3 m doorway, so the gates cannot be walked around. Children of
            // Environment, so they are baked into the NavMesh as obstacles.
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/M_Wall.mat");
            const float gate1Z = 62f;
            const float gate2Z = 80f;
            foreach (float z in new[] { gate1Z, gate2Z })
            {
                MakeBox($"Wall z{z} L", environment, new Vector3(-5.95f, 1.6f, z), new Vector3(8.1f, 3.2f, 0.5f), wallMat);
                MakeBox($"Wall z{z} R", environment, new Vector3(5.95f, 1.6f, z), new Vector3(8.1f, 3.2f, 0.5f), wallMat);
            }

            var area = new GameObject("Step 3 Test Area").transform;
            area.SetParent(gameplay, false);

            // Checkpoints: one beside the spawn, one after the second spirit bridge.
            PlacePrefab(checkpointPrefab, "Checkpoint 1 (spawn)", area, new Vector3(3f, 0f, -3f));
            PlacePrefab(checkpointPrefab, "Checkpoint 2 (after bridge 2)", area, new Vector3(-6f, 0f, 47f));

            // Ghost 1: three waypoints just past the first spirit bridge.
            PlaceGhost(ghostPrefab, "Ghost 1 (bridge)", area,
                new Vector3(-5f, 0f, 17f), new Vector3(5f, 0f, 17f), new Vector3(0f, 0f, 23f));

            // Single-plate gate: lure Ghost 2 onto the plate with candy from the bowl.
            PlacePrefab(bowlPrefab, "CandyBowl 1", area, new Vector3(6f, 0f, 48f));
            PlaceGhost(ghostPrefab, "Ghost 2 (gate 1)", area,
                new Vector3(-5f, 0f, 58f), new Vector3(3f, 0f, 59f), new Vector3(0f, 0f, 54f));
            GameObject plate1 = PlacePrefab(platePrefab, "PressurePlate 1", area, new Vector3(5f, 0f, 56f));
            GameObject gate1 = PlacePrefab(gatePrefab, "Gate 1 (one plate)", area, new Vector3(0f, 0f, gate1Z));
            ConfigureGate(gate1, 1, false, plate1);

            // Two-plate gate: two ghosts, or one ghost plus the player. It latches open so the
            // player can step off their plate and walk through.
            PlacePrefab(bowlPrefab, "CandyBowl 2", area, new Vector3(0f, 0f, 64.5f));
            PlaceGhost(ghostPrefab, "Ghost 3 (gate 2 left)", area,
                new Vector3(-6f, 0f, 70f), new Vector3(-3f, 0f, 76f), new Vector3(-7f, 0f, 76f));
            PlaceGhost(ghostPrefab, "Ghost 4 (gate 2 right)", area,
                new Vector3(6f, 0f, 70f), new Vector3(3f, 0f, 76f), new Vector3(7f, 0f, 76f));
            // The plates sit clear of both patrol routes: a ghost drifting over them would open the gate by accident.
            GameObject plate2A = PlacePrefab(platePrefab, "PressurePlate 2A", area, new Vector3(-6f, 0f, 66f));
            GameObject plate2B = PlacePrefab(platePrefab, "PressurePlate 2B", area, new Vector3(6f, 0f, 66f));
            GameObject gate2 = PlacePrefab(gatePrefab, "Gate 2 (two plates, latches open)", area, new Vector3(0f, 0f, gate2Z));
            ConfigureGate(gate2, 2, true, plate2A, plate2B);

            PlacePrefab(goalPrefab, "GoalTrigger (farmhouse door)", area, new Vector3(0f, 0f, 92f));

            // Tutorials (one-shot HUD prompts) and sections (telemetry markers), spanning the 20 m path.
            PlaceTutorial(tutorialPrefab, area, "T1-Candle", -3.5f,
                "Your candle is burning down. Candle stubs refill it.");
            PlaceTutorial(tutorialPrefab, area, "T2-SpiritPlatforms", 7.5f,
                "Spirit platforms only exist in your light. Hold {flare} and they vanish.");
            PlaceTutorial(tutorialPrefab, area, "T3-Ghost", 14.6f,
                "A ghost! Hold {flare} to push it back. Flaring burns the candle 8x faster.");
            PlaceTutorial(tutorialPrefab, area, "T4-Candy", 45.5f,
                "Take candy from the bowl. {throw} lobs it, {drop} drops it at your feet. A ghost will stop to eat it.");
            PlaceTutorial(tutorialPrefab, area, "T5-TwoPlates", 63.2f,
                "This gate needs BOTH plates: two ghosts, or one ghost and you.");

            PlaceSection(sectionPrefab, area, "S1-Start", "", -4.5f);
            PlaceSection(sectionPrefab, area, "S2-SpiritBridge", "Spirit Bridge", 9.3f);
            PlaceSection(sectionPrefab, area, "S3-FirstGhost", "", 14.3f);
            PlaceSection(sectionPrefab, area, "S4-LongBridge", "", 33.5f);
            PlaceSection(sectionPrefab, area, "S5-OnePlateGate", "Cemetery Gate", 44.4f);
            PlaceSection(sectionPrefab, area, "S6-TwoPlateGate", "", 62.9f);

            // Game flow, menus, debug overlay.
            var manager = new GameObject("GameManager");
            var gameManager = manager.AddComponent<GameManager>();
            SetRef(gameManager, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            manager.AddComponent<DebugOverlay>();
            PlacePrefab(menusPrefab, "GameMenus", null, Vector3.zero);

            return BakeSandboxNavMesh(environment);
        }

        /// <summary>
        /// Bakes the NavMesh from the colliders under Environment only (grounds, pit floor,
        /// walls). Everything under Gameplay is left out on purpose: spirit platforms are NOT
        /// walkable for ghosts, and gate doors block ghosts through carving obstacles instead.
        /// </summary>
        private static bool BakeSandboxNavMesh(Transform environment)
        {
            Physics.SyncTransforms();
            var surface = environment.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << 0; // Default
            surface.BuildNavMesh();

            NavMeshData data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogError("[Lantern Keeper] NavMesh bake produced no data; ghosts will not move. Bake by hand (docs/SETUP_WORLD.md).");
                return false;
            }
            data.name = "Sandbox-NavMesh";
            AssetDatabase.CreateAsset(data, SandboxNavMeshPath);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(SandboxNavMeshPath);
            EditorUtility.SetDirty(surface);
            return true;
        }

        private static void PlaceGhost(GameObject prefab, string name, Transform parent, params Vector3[] waypoints)
        {
            var patrol = new GameObject(name + " Patrol").transform;
            patrol.SetParent(parent, false);
            var points = new Object[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                var point = new GameObject($"Waypoint {i + 1}").transform;
                point.SetParent(patrol, false);
                point.position = waypoints[i];
                points[i] = point;
            }

            GameObject ghost = PlacePrefab(prefab, name, parent, waypoints[0]);
            var ai = ghost.GetComponent<GhostAI>();
            SetRefArray(ai, "patrolPoints", points);
            RecordOverrides(ai);
        }

        private static void ConfigureGate(GameObject gateObject, int requiredPlates, bool stayOpen, params GameObject[] plates)
        {
            var gate = gateObject.GetComponent<Gate>();
            var plateComponents = new Object[plates.Length];
            for (int i = 0; i < plates.Length; i++) plateComponents[i] = plates[i].GetComponent<PressurePlate>();

            var so = new SerializedObject(gate);
            SerializedProperty array = so.FindProperty("plates");
            array.arraySize = plateComponents.Length;
            for (int i = 0; i < plateComponents.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = plateComponents[i];
            so.FindProperty("requiredPlates").intValue = requiredPlates;
            so.FindProperty("stayOpenOnceOpened").boolValue = stayOpen;
            so.ApplyModifiedPropertiesWithoutUndo();
            RecordOverrides(gate);
        }

        private static void PlaceTutorial(GameObject prefab, Transform parent, string id, float z, string text)
        {
            GameObject go = PlacePrefab(prefab, "Tutorial " + id, parent, new Vector3(0f, 0f, z));
            go.transform.localScale = new Vector3(2.5f, 1f, 1f); // 8 m box -> 20 m, the full path width
            RecordOverrides(go.transform);
            var trigger = go.GetComponent<TutorialTrigger>();
            SetString(trigger, "tutorialId", id);
            SetString(trigger, "text", text);
            RecordOverrides(trigger);
        }

        private static void PlaceSection(GameObject prefab, Transform parent, string id, string displayName, float z)
        {
            GameObject go = PlacePrefab(prefab, "Section " + id, parent, new Vector3(0f, 0f, z));
            go.transform.localScale = new Vector3(2.5f, 1f, 1f);
            RecordOverrides(go.transform);
            var trigger = go.GetComponent<SectionTrigger>();
            SetString(trigger, "sectionId", id);
            SetString(trigger, "displayName", displayName);
            RecordOverrides(trigger);
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject NewVisual(PrimitiveType shape, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void AddTriggerBox(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = center;
            box.size = size;
        }

        private static void AddCarvingObstacle(GameObject go, bool stationary)
        {
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.one; // local space; the object's scale makes it fit the cube
            obstacle.carving = true;
            obstacle.carveOnlyStationary = stationary;
        }

        private static void SetRefArray(Object target, string property, params Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, property);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
