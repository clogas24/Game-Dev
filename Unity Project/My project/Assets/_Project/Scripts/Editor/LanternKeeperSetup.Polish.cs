using System;
using System.Collections.Generic;
using System.Reflection;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace LanternKeeper.EditorTools
{
    // Step 5 (polish): the shared art, audio and UI assets, and the helpers the prefab
    // generators use to give each prefab its final look and its sounds.
    //
    // Nothing here changes how the game plays. Prefab polish only swaps renderers, adds
    // cosmetic children and fills AudioClip fields; colliders, rigidbodies, NavMesh obstacles
    // and every tuning value are left exactly as the Step 2-4 generators made them.
    //
    // The polish is applied inside the existing "Create ... Prefab" items, just before each
    // prefab is saved, so running any of them again gives the same result and the same file
    // IDs. "Dress Level01" (LanternKeeperSetup.Dress.cs) uses the same assets for the level.
    public static partial class LanternKeeperSetup
    {
        private const string AudioDir = "Assets/_Project/Audio";
        private const string MixerPath = AudioDir + "/LanternKeeper.mixer";
        private const string ThirdPartyArtDir = ArtDir + "/ThirdParty";
        private const string FontsDir = ArtDir + "/Fonts";
        private const string TitleFontPath = FontsDir + "/LK_Title SDF.asset";
        private const string BodyFontPath = FontsDir + "/LK_Body SDF.asset";
        private const string PostProfilePath = "Assets/_Project/Settings/LK_PostProcess.asset";
        private const string GeneratedTextureVersion = "lk-texture-1";

        // UI palette: pumpkin orange, deep purple, bone white.
        private static readonly Color UiOrange = new Color(1f, 0.56f, 0.12f);
        private static readonly Color UiOrangeLight = new Color(1f, 0.8f, 0.45f);
        private static readonly Color UiPurple = new Color(0.2f, 0.12f, 0.3f);
        private static readonly Color UiPurpleDeep = new Color(0.05f, 0.025f, 0.09f);
        private static readonly Color UiBone = new Color(0.95f, 0.92f, 0.84f);
        private static readonly Color UiInk = new Color(0.2f, 0.13f, 0.08f);

        private static bool s_polishAssetsReady;

        [MenuItem(Menu + "Create Polish Assets", priority = 7)]
        public static void CreatePolishAssetsMenuItem()
        {
            s_polishAssetsReady = false;
            EnsurePolishAssets();
            Debug.Log("[Lantern Keeper] Polish assets (meshes, materials, mixer, fonts, animation) are up to date.");
        }

        /// <summary>
        /// Makes sure every shared polish asset exists and is current. Cheap to call again in
        /// the same editor session: the work is done once per script reload.
        /// </summary>
        private static void EnsurePolishAssets()
        {
            if (s_polishAssetsReady) return;
            AssetDatabase.Refresh(); // picks up files written by Tools/generate_audio.py and generate_textures.py
            EnsureFolder(MaterialsDir);
            WritePaletteTextures();
            ConfigureGeneratedTextures();
            ConfigureAudioImports();
            BuildPolishMeshes();
            BuildPolishMaterials();
            EnsureMixer();
            EnsureFonts();
            EnsureGhostAnimator();
            EnsurePlayerAnimator();
            AssetDatabase.SaveAssets();
            s_polishAssetsReady = true;
        }

        // ------------------------------------------------------------------ lookups

        private static Material Mat(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");
            if (material == null) Debug.LogWarning($"[Lantern Keeper] Material {name} is missing.");
            return material;
        }

        private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{name}.png");

        private static Sprite UiSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturesDir}/{name}.png");
            if (sprite == null) Debug.LogWarning($"[Lantern Keeper] UI sprite {name} is missing; run 'python Tools/generate_textures.py'.");
            return sprite;
        }

        /// <summary>An audio clip by its path under Assets/_Project/Audio, without the extension.</summary>
        private static AudioClip Clip(string relativePath)
        {
            foreach (string extension in new[] { ".wav", ".ogg", ".mp3" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{relativePath}{extension}");
                if (clip != null) return clip;
            }
            Debug.LogWarning($"[Lantern Keeper] Audio clip '{relativePath}' is missing under {AudioDir}; that sound stays silent.");
            return null;
        }

        private static void SetClip(UnityEngine.Object target, string property, string clip) => SetRef(target, property, Clip(clip));

        private static AudioMixerGroup MixerGroup(string name)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) return null;
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(name);
            foreach (AudioMixerGroup group in groups)
            {
                if (group.name == name) return group;
            }
            return groups.Length > 0 ? groups[0] : null;
        }

        // ------------------------------------------------------------------ import settings

        // The PNGs written by Tools/generate_textures.py: UI_* become sprites, T_* particle textures.
        private static void ConfigureGeneratedTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("T_LK_Palette")) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.userData == GeneratedTextureVersion) continue;

                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (name.StartsWith("UI_"))
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    if (name == "UI_Rounded") importer.spriteBorder = new Vector4(22f, 22f, 22f, 22f);
                    if (name == "UI_Parchment") importer.spriteBorder = new Vector4(30f, 30f, 30f, 30f);
                }
                else
                {
                    importer.textureType = TextureImporterType.Default;
                }
                importer.userData = GeneratedTextureVersion;
                importer.SaveAndReimport();
            }
        }

        // Music and ambience are long loops: keep them compressed in memory instead of unpacking them to PCM.
        private static void ConfigureAudioImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir + "/Music", AudioDir + "/Ambience" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;
                if (importer == null) continue;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (settings.loadType == AudioClipLoadType.CompressedInMemory
                    && settings.compressionFormat == AudioCompressionFormat.Vorbis
                    && Mathf.Approximately(settings.quality, 0.6f)) continue;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ materials

        private static void BuildPolishMaterials()
        {
            Material palette = MakeMaterial("M_LK_Palette", Color.white);
            palette.SetFloat("_Smoothness", 0.08f);
            palette.SetTexture("_BaseMap", Tex("T_LK_Palette"));
            palette.EnableKeyword("_EMISSION");
            palette.SetTexture("_EmissionMap", Tex("T_LK_PaletteEmission"));
            palette.SetColor("_EmissionColor", new Color(2.4f, 2.4f, 2.4f));
            palette.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // Every Kenney Graveyard Kit model shares one colour-map texture, so one material covers the kit.
            Material kenney = MakeMaterial("M_KenneyGraveyard", new Color(0.86f, 0.84f, 0.95f));
            kenney.SetFloat("_Smoothness", 0.05f);
            kenney.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ThirdPartyArtDir + "/KenneyGraveyardKit/Textures/colormap.png"));

            // Ghost sheet: see-through, with emission for GhostVisualFx to set. Front faces only:
            // drawing the inside of the sheet as well makes it sort against itself in bands.
            Material ghost = MakeMaterial("M_Ghost", new Color(0.35f, 1f, 0.45f, 0.86f), true, Color.black);
            ghost.SetFloat("_Cull", (float)CullMode.Back);
            ghost.SetFloat("_Smoothness", 0.3f);
            MakeMaterial("M_GhostFace", new Color(0.02f, 0.02f, 0.05f));

            // The player's lantern face and anything else whose glow is written from script.
            MakeMaterial("M_Lantern", new Color(1f, 0.6f, 0.15f), false, new Color(2.4f, 1.3f, 0.3f));
            MakeMaterial("M_JackFaceUnlit", new Color(0.05f, 0.03f, 0.02f));
            MakeMaterial("M_PlateRune", new Color(0.16f, 0.14f, 0.12f), false, Color.black);
            Material veil = MakeMaterial("M_GateVeil", new Color(0.06f, 0.04f, 0.1f, 0.5f), true);
            veil.SetFloat("_Cull", (float)CullMode.Off);

            // Spirit platforms: the custom rim-glow shader. SpiritPlatform.cs still fades _BaseColor.
            Material platform = Mat("M_SpiritPlatform");
            Shader platformShader = Shader.Find("LanternKeeper/SpiritPlatform");
            if (platform != null && platformShader != null)
            {
                platform.shader = platformShader;
                platform.SetColor("_BaseColor", new Color(0.3f, 0.95f, 1f, 0.85f));
                platform.SetFloat("_FillAlpha", 0.5f);
                platform.SetColor("_FillGlow", new Color(0.03f, 0.22f, 0.28f));
                platform.SetColor("_EdgeColor", new Color(0.7f, 2.6f, 3f));
                platform.SetFloat("_EdgeWidth", 0.1f);
                platform.SetFloat("_PulseSpeed", 0.7f);
                platform.SetFloat("_PulseAmount", 0.25f);
                platform.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(platform);
            }
            else if (platformShader == null)
            {
                Debug.LogWarning("[Lantern Keeper] Shader 'LanternKeeper/SpiritPlatform' not found; spirit platforms keep the plain URP Lit material.");
            }

            ParticleMaterial("M_FX_Glow", "T_SoftDot", true);
            ParticleMaterial("M_FX_Spark", "T_Spark", true);
            ParticleMaterial("M_FX_Leaf", "T_Leaf", false);
            ParticleMaterial("M_FX_Mist", "T_Mist", false);
        }

        private static Material ParticleMaterial(string name, string texture, bool additive)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
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
            material.SetTexture("_BaseMap", Tex(texture));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ audio mixer

        /// <summary>
        /// Creates LanternKeeper.mixer: Master with three children, Music, SFX and Ambience,
        /// and each child's volume exposed as MusicVolume, SfxVolume and AmbienceVolume.
        /// Unity has no public API for building a mixer, so this calls the editor's internal
        /// AudioMixerController the same way the Audio Mixer window does. If a Unity update
        /// breaks that, the error below says so and docs/ASSET_TODO.md has the two-minute manual recipe.
        /// </summary>
        private static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null)
            {
                if (MixerIsComplete(existing)) return existing;
                // A half-built mixer (an earlier attempt failed part-way): start again.
                AssetDatabase.DeleteAsset(MixerPath);
            }

            EnsureFolder(AudioDir);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                Assembly editor = typeof(Editor).Assembly;
                Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
                Type pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);

                object controller = controllerType.GetMethod("CreateMixerControllerAtPath", flags).Invoke(null, new object[] { MixerPath });
                object master = controllerType.GetProperty("masterGroup", flags).GetValue(controller);
                MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", flags);
                MethodInfo addChild = controllerType.GetMethod("AddChildToParent", flags);
                MethodInfo expose = controllerType.GetMethod("AddExposedParameter", flags);

                var exposedNames = new Dictionary<GUID, string>();
                foreach ((string groupName, string parameter) in new[]
                         {
                             ("Music", GameAudio.MusicParameter), ("SFX", GameAudio.SfxParameter), ("Ambience", GameAudio.AmbienceParameter)
                         })
                {
                    object group = createGroup.Invoke(controller, new object[] { groupName, false });
                    addChild.Invoke(controller, new[] { group, master });
                    var volume = (GUID)group.GetType().GetMethod("GetGUIDForVolume", flags).Invoke(group, null);
                    object parameterPath = Activator.CreateInstance(pathType, flags, null, new[] { group, volume }, null);
                    expose.Invoke(controller, new[] { parameterPath });
                    exposedNames[volume] = parameter;
                }

                // Exposed parameters are created as "MyExposedParam"; give each its real name.
                PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", flags);
                var exposed = (Array)exposedProperty.GetValue(controller);
                Type exposedType = exposed.GetType().GetElementType();
                FieldInfo guidField = exposedType.GetField("guid", flags);
                FieldInfo nameField = exposedType.GetField("name", flags);
                for (int i = 0; i < exposed.Length; i++)
                {
                    object entry = exposed.GetValue(i);
                    if (!exposedNames.TryGetValue((GUID)guidField.GetValue(entry), out string name)) continue;
                    nameField.SetValue(entry, name);
                    exposed.SetValue(entry, i);
                }
                exposedProperty.SetValue(controller, exposed);

                EditorUtility.SetDirty((UnityEngine.Object)controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(MixerPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[Lantern Keeper] Could not build the AudioMixer from code: " + (e.InnerException ?? e).Message
                               + "\nCreate it by hand (docs/ASSET_TODO.md, 'Audio mixer') and run the menu item again.");
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer != null) Debug.Log($"[Lantern Keeper] AudioMixer created at {MixerPath} (Master > Music, SFX, Ambience).");
            return mixer;
        }

        private static bool MixerIsComplete(AudioMixer mixer)
        {
            foreach (string group in new[] { "Music", "SFX", "Ambience" })
            {
                if (mixer.FindMatchingGroups(group).Length == 0) return false;
            }
            foreach (string parameter in new[] { GameAudio.MusicParameter, GameAudio.SfxParameter, GameAudio.AmbienceParameter })
            {
                if (!mixer.GetFloat(parameter, out _)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ fonts

        private static TMP_FontAsset TitleFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        private static TMP_FontAsset BodyFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        // TextMeshPro font assets, baked once from the OFL fonts in Art/ThirdParty/Fonts.
        private static void EnsureFonts()
        {
            EnsureFolder(FontsDir);
            EnsureFont(TitleFontPath, ThirdPartyArtDir + "/Fonts/Creepster-Regular.ttf", 64);
            EnsureFont(BodyFontPath, ThirdPartyArtDir + "/Fonts/Lato-Regular.ttf", 56);
        }

        private static void EnsureFont(string assetPath, string sourcePath, int pointSize)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning($"[Lantern Keeper] Font {sourcePath} is missing; text keeps the TextMeshPro default font.");
                return;
            }

            // One 1024 x 512 atlas: enough for the character set below, and half the asset size of a square one.
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, pointSize, 6, GlyphRenderMode.SDFAA, 1024, 512, AtlasPopulationMode.Dynamic, false);
            if (font == null)
            {
                Debug.LogWarning($"[Lantern Keeper] Could not bake {sourcePath}; text keeps the TextMeshPro default font.");
                return;
            }

            // Every printable ASCII character plus the punctuation the notes use, baked now so
            // the atlas is fixed and the asset does not change while the game runs.
            var characters = new System.Text.StringBuilder(128);
            for (char c = ' '; c <= '~'; c++) characters.Append(c);
            characters.Append("‘’“”–—…×°");
            font.TryAddCharacters(characters.ToString(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[Lantern Keeper] {System.IO.Path.GetFileName(sourcePath)} has no glyph for: {missing}");
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            font.name = name;
            AssetDatabase.CreateAsset(font, assetPath);
            font.material.name = name + " Material";
            font.atlasTexture.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ post-processing

        /// <summary>The level's URP volume profile: bloom, vignette, a light grade and a little film grain.</summary>
        private static VolumeProfile EnsurePostProfile()
        {
            EnsureFolder("Assets/_Project/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }

            // Bloom makes the lantern, the spirit platforms and every emissive face glow.
            var bloom = PostComponent<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.85f));

            var vignette = PostComponent<Vignette>(profile);
            vignette.color.Override(new Color(0.05f, 0.02f, 0.1f));
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            var tone = PostComponent<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            var grade = PostComponent<ColorAdjustments>(profile);
            grade.postExposure.Override(0.25f);
            grade.contrast.Override(8f);
            grade.saturation.Override(10f);

            // Cool purple shadows under warm orange highlights: the Halloween palette.
            var split = PostComponent<SplitToning>(profile);
            split.shadows.Override(new Color(0.42f, 0.4f, 0.62f));
            split.highlights.Override(new Color(0.62f, 0.54f, 0.42f));
            split.balance.Override(-15f);

            var grain = PostComponent<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.8f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T PostComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------ building blocks

        private static GameObject MeshChild(string name, Transform parent, string meshName, Material material,
            Vector3 localPosition, Vector3 localScale, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            Mesh mesh = LoadMesh(meshName);
            if (mesh == null) Debug.LogWarning($"[Lantern Keeper] Mesh {meshName} is missing.");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // Removes a placeholder primitive's renderer but leaves its collider and everything else.
        private static void StripRenderer(GameObject go)
        {
            if (go.TryGetComponent(out MeshRenderer renderer)) UnityEngine.Object.DestroyImmediate(renderer);
            if (go.TryGetComponent(out MeshFilter filter)) UnityEngine.Object.DestroyImmediate(filter);
        }

        private static void SwapMesh(GameObject go, string meshName, Material material)
        {
            go.GetComponent<MeshFilter>().sharedMesh = LoadMesh(meshName);
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Gradient FadeGradient(Color color, params (float time, float alpha)[] alphas)
        {
            var keys = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) keys[i] = new GradientAlphaKey(alphas[i].alpha, alphas[i].time);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) }, keys);
            return gradient;
        }

        /// <summary>A stopped particle system on a new child, ready to be configured. Shape and velocity modules are left to the caller.</summary>
        private static ParticleSystem NewParticles(string name, Transform parent, Vector3 localPosition, Material material,
            bool loop, bool playOnAwake, int maxParticles, bool worldSpace)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = loop;
            main.playOnAwake = playOnAwake;
            main.maxParticles = maxParticles;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        // ------------------------------------------------------------------ prefab polish: player

        /// <summary>
        /// The player's Step 5 additions: the jack-o'-lantern prop with its flame, the animation
        /// and sound components, the copied Animator Controller, and every audio clip.
        /// </summary>
        private static void PolishPlayer(GameObject player, Transform lanternRoot, Light lanternLight)
        {
            EnsurePolishAssets();

            RuntimeAnimatorController controller = EnsurePlayerAnimator();
            if (controller != null && player.TryGetComponent(out Animator animator)) animator.runtimeAnimatorController = controller;

            // The prop hangs just under the light, so the pumpkin sits round the point light.
            Transform old = lanternRoot.Find("LanternVisual");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var prop = new GameObject("LanternVisual").transform;
            prop.gameObject.layer = lanternRoot.gameObject.layer;
            prop.SetParent(lanternRoot, false);
            prop.localPosition = new Vector3(0f, -0.13f, 0f);

            Vector3 size = Vector3.one * 0.32f;
            MeshChild("LanternPumpkin", prop, "LK_Pumpkin", Mat("M_LK_Palette"), Vector3.zero, size);
            GameObject face = MeshChild("LanternFace", prop, "LK_JackFace", Mat("M_Lantern"), Vector3.zero, size);
            var handle = new GameObject("LanternHandle").transform;
            handle.SetParent(prop, false);
            handle.localPosition = new Vector3(0f, 0.33f, 0f);

            ParticleSystem flame = NewParticles("LanternFlame", prop, new Vector3(0f, 0.27f, 0f), Mat("M_FX_Glow"), true, true, 16, true);
            ParticleSystem.MainModule main = flame.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.25f), new Color(1f, 0.5f, 0.1f));
            ParticleSystem.EmissionModule emission = flame.emission;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = flame.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.015f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // cone points up
            ParticleSystem.ColorOverLifetimeModule fade = flame.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, (0f, 0f), (0.15f, 1f), (1f, 0f)));
            ParticleSystem.SizeOverLifetimeModule shrink = flame.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var lanternProp = GetOrAdd<LanternProp>(player);
            SetRef(lanternProp, "prop", prop);
            SetRef(lanternProp, "handle", handle);
            SetRef(lanternProp, "lanternLight", lanternLight);
            SetRef(lanternProp, "flame", flame);
            SetRefArray(lanternProp, "glowRenderers", face.GetComponent<Renderer>());

            // Footsteps go through the mixer (PlayerSounds); Starter Assets' own, unmixed copies are muted.
            if (player.TryGetComponent(out ThirdPersonController mover)) mover.FootstepAudioVolume = 0f;
            var sounds = GetOrAdd<PlayerSounds>(player);
            var steps = new UnityEngine.Object[5];
            for (int i = 0; i < steps.Length; i++) steps[i] = Clip($"ThirdParty/KenneyImpactSounds/footstep_grass_00{i}");
            SetRefArray(sounds, "footstepClips", steps);
            SetClip(sounds, "landClip", "ThirdParty/KenneyImpactSounds/impactSoft_heavy_000");
            SetClip(sounds, "flareLoopClip", "SFX/flare_loop");
            SetRef(sounds, "outputGroup", MixerGroup("SFX"));

            var candle = player.GetComponent<CandleManager>();
            SetClip(candle, "candleLowClip", "SFX/candle_low");
            SetClip(candle, "extinguishClip", "SFX/extinguish");
            var lantern = player.GetComponent<LanternController>();
            SetClip(lantern, "flareStartClip", "SFX/flare_start");
            SetClip(lantern, "flareEndClip", "SFX/flare_stop");
            SetClip(lantern, "flareDeniedClip", "ThirdParty/KenneyInterfaceSounds/error_006");
            var thrower = player.GetComponent<CandyThrower>();
            SetClip(thrower, "throwClip", "SFX/candy_throw");
            SetClip(thrower, "dropClip", "ThirdParty/KenneyImpactSounds/impactGeneric_light_002");
            SetClip(thrower, "emptyClip", "ThirdParty/KenneyInterfaceSounds/error_004");
            SetClip(player.GetComponent<PlayerKnockback>(), "hitClip", "ThirdParty/KenneyImpactSounds/impactPunch_medium_000");
        }

        // ------------------------------------------------------------------ prefab polish: items

        // Candy: the collider, rigidbody and scale stay; the sphere's renderer becomes a wrapped sweet.
        private static void PolishCandy(GameObject candy)
        {
            EnsurePolishAssets();
            StripRenderer(candy);
            MeshChild("Wrap", candy.transform, "LK_Candy", Mat("M_Candy"), Vector3.zero, Vector3.one, Quaternion.Euler(0f, 0f, 90f));
            var component = candy.GetComponent<Candy>();
            SetClip(component, "landClip", "ThirdParty/KenneyImpactSounds/impactGeneric_light_000");
            SetClip(component, "consumeClip", "SFX/candy_eaten");
        }

        // Candle stub: a wax candle with a small flame, on the same spinning Visual child.
        private static void PolishCandlePickup(GameObject pickup)
        {
            EnsurePolishAssets();
            Transform visual = pickup.transform.Find("Visual");
            StripRenderer(visual.gameObject);
            visual.localScale = Vector3.one;
            MeshChild("Wax", visual, "LK_Candle", Mat("M_CandlePickup"), new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.34f, 0.2f));
            MeshChild("Flame", visual, "LK_Flame", Mat("M_LampGlow"), new Vector3(0f, 0.19f, 0f), new Vector3(0.2f, 0.26f, 0.2f));
            SetClip(pickup.GetComponent<Pickup>(), "collectClip", "SFX/candle_pickup");
        }

        private static void PolishCandyPickup(GameObject pickup)
        {
            EnsurePolishAssets();
            Transform visual = pickup.transform.Find("Visual");
            StripRenderer(visual.gameObject);
            visual.localScale = Vector3.one;
            MeshChild("Wrap", visual, "LK_Candy", Mat("M_CandyPickup"), Vector3.zero, Vector3.one * 0.3f, Quaternion.Euler(0f, 0f, 60f));
            SetClip(pickup.GetComponent<Pickup>(), "collectClip", "ThirdParty/KenneyRPGAudio/handleCoins2");
        }

        // Spirit platform: the look is all in M_SpiritPlatform's shader; here only its sounds.
        // No child renderer is added, because SpiritPlatform takes its light sample points from the renderers' bounds.
        private static void PolishSpiritPlatform(GameObject platform)
        {
            EnsurePolishAssets();
            var component = platform.GetComponent<SpiritPlatform>();
            SetClip(component, "appearClip", "SFX/platform_appear");
            SetClip(component, "vanishClip", "SFX/platform_vanish");
            SetFloat(component, "volume", 0.4f);
        }

        private static void PolishPorchLamp(GameObject lamp)
        {
            EnsurePolishAssets();
            Transform head = lamp.transform.Find("Lamp");
            MeshChild("Cage", head, "LK_LanternCage", Mat("M_LampPost"), Vector3.zero, Vector3.one * 1.15f);
            MeshChild("Base", lamp.transform, "LK_Box_StoneDark", Mat("M_LK_Palette"), Vector3.zero, new Vector3(0.4f, 0.22f, 0.4f));
        }

        // ------------------------------------------------------------------ prefab polish: ghost

        /// <summary>
        /// Builds the ghost's visuals under <paramref name="root"/>: Visual (Animator) > Pivot
        /// (what the clips move) > Body (the sheet, tinted by GhostAI) and a face, plus the
        /// particle effects. Returns the body renderer for GhostAI's tint list.
        /// </summary>
        private static Renderer BuildGhostVisual(GameObject root, Light glowLight, out Animator animator)
        {
            EnsurePolishAssets();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = EnsureGhostAnimator();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(visual.transform, false);

            var body = new GameObject("Body");
            body.transform.SetParent(pivot, false);
            var skin = body.AddComponent<SkinnedMeshRenderer>();
            Mesh mesh = LoadMesh("LK_Ghost");
            skin.sharedMesh = mesh;
            skin.sharedMaterial = Mat("M_Ghost");
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.receiveShadows = false;
            // Room for the hem blend shapes and the squash and stretch of the clips.
            skin.localBounds = new Bounds(new Vector3(0f, 1.05f, 0f), new Vector3(1.9f, 2.3f, 1.9f));

            Material face = Mat("M_GhostFace");
            GameObject eyeL = NewVisual(PrimitiveType.Sphere, "Eye L", pivot, new Vector3(-0.14f, 1.56f, 0.33f), new Vector3(0.11f, 0.17f, 0.08f), face);
            GameObject eyeR = NewVisual(PrimitiveType.Sphere, "Eye R", pivot, new Vector3(0.14f, 1.56f, 0.33f), new Vector3(0.11f, 0.17f, 0.08f), face);
            GameObject mouth = NewVisual(PrimitiveType.Sphere, "Mouth", pivot, new Vector3(0f, 1.36f, 0.36f), new Vector3(0.1f, 0.08f, 0.07f), face);
            eyeL.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            eyeR.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            mouth.transform.localRotation = Quaternion.identity;

            Material glow = Mat("M_FX_Glow");
            ParticleSystem burst = NewParticles("RepelBurst", root.transform, new Vector3(0f, 1.1f, 0f), glow, false, false, 24, true);
            ParticleSystem.MainModule burstMain = burst.main;
            burstMain.duration = 0.5f;
            burstMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            burstMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            burstMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            burstMain.startColor = Color.white;
            ParticleSystem.EmissionModule burstEmission = burst.emission;
            burstEmission.rateOverTime = 0f;
            burstEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            ParticleSystem.ShapeModule burstShape = burst.shape;
            burstShape.shapeType = ParticleSystemShapeType.Sphere;
            burstShape.radius = 0.4f;
            ParticleSystem.ColorOverLifetimeModule burstFade = burst.colorOverLifetime;
            burstFade.enabled = true;
            burstFade.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, (0f, 1f), (1f, 0f)));

            ParticleSystem crumbs = NewParticles("Crumbs", root.transform, new Vector3(0f, 0.6f, 0.45f), glow, true, false, 16, true);
            ParticleSystem.MainModule crumbMain = crumbs.main;
            crumbMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            crumbMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            crumbMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            crumbMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.75f), new Color(1f, 0.9f, 0.95f));
            crumbMain.gravityModifier = 1.2f;
            ParticleSystem.EmissionModule crumbEmission = crumbs.emission;
            crumbEmission.rateOverTime = 12f;
            ParticleSystem.ShapeModule crumbShape = crumbs.shape;
            crumbShape.shapeType = ParticleSystemShapeType.Cone;
            crumbShape.angle = 35f;
            crumbShape.radius = 0.08f;
            crumbShape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule crumbFade = crumbs.colorOverLifetime;
            crumbFade.enabled = true;
            crumbFade.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, (0f, 1f), (0.7f, 1f), (1f, 0f)));

            var fx = root.AddComponent<GhostVisualFx>();
            SetRef(fx, "animator", animator);
            SetRefArray(fx, "glowRenderers", skin);
            SetRef(fx, "glowLight", glowLight);
            SetRef(fx, "repelBurst", burst);
            SetRef(fx, "crumbs", crumbs);
            return skin;
        }

        private static void WireGhostAudio(GhostAI ghost)
        {
            SetClip(ghost, "noticeClip", "SFX/ghost_notice");
            SetClip(ghost, "chaseClip", "SFX/ghost_chase");
            SetClip(ghost, "repelledClip", "SFX/ghost_repel");
            SetClip(ghost, "eatClip", "SFX/ghost_eat");
            SetClip(ghost, "hitClip", "SFX/ghost_hit");
            SetClip(ghost, "returnClip", "SFX/ghost_return");
        }

        // ------------------------------------------------------------------ prefab polish: world

        // Stone plate with a rune that glows while it is pressed. The Pad keeps its tint (yellow / green in the level).
        private static void PolishPressurePlate(GameObject root, GameObject pad)
        {
            EnsurePolishAssets();
            GameObject rune = MeshChild("Rune", pad.transform, "LK_Rune", Mat("M_PlateRune"), Vector3.zero, Vector3.one);
            var fx = root.AddComponent<PlateRune>();
            SetRef(fx, "rune", rune.GetComponent<Renderer>());
            var plate = root.GetComponent<PressurePlate>();
            SetClip(plate, "pressClip", "ThirdParty/KenneyImpactSounds/impactMining_001");
            SetClip(plate, "releaseClip", "ThirdParty/KenneyImpactSounds/impactGeneric_light_003");
        }

        // Iron cemetery gate between capped stone pillars. The Door keeps its collider, rigidbody and obstacle.
        private static void PolishGate(GameObject root, GameObject door, GameObject beam)
        {
            EnsurePolishAssets();
            StripRenderer(door);
            GameObject bars = MeshChild("Bars", door.transform, "LK_IronGate", Mat("M_GateDoor"), Vector3.zero, Vector3.one);
            // A dark veil behind the bars: the gate blocks a ghost's sight, so it should not look like a window.
            GameObject veil = NewVisual(PrimitiveType.Quad, "Veil", door.transform, Vector3.zero, new Vector3(0.98f, 0.98f, 1f), Mat("M_GateVeil"));
            veil.transform.localRotation = Quaternion.identity;

            // A stone cap and a jack-o'-lantern on each end of the lintel. The Beam cube is
            // scaled (3.8, 0.4, 0.5); the children undo that so they keep their own proportions.
            Material palette = Mat("M_LK_Palette");
            Vector3 s = beam.transform.localScale;
            foreach (float side in new[] { -1f, 1f })
            {
                string end = side < 0f ? "L" : "R";
                float x = side * 1.7f / s.x;
                MeshChild("Cap " + end, beam.transform, "LK_PillarCap", palette, new Vector3(x, 0.5f, 0f),
                    new Vector3(0.5f / s.x, 0.4f / s.y, 0.5f / s.z));
                MeshChild("Gate Lantern " + end, beam.transform, "LK_JackOLantern", palette, new Vector3(x, 0.5f + 0.22f / s.y, 0f),
                    new Vector3(0.4f / s.x, 0.4f / s.y, 0.4f / s.z), Quaternion.Euler(0f, 180f, 0f));
            }

            ParticleSystem dust = NewParticles("Dust", root.transform, new Vector3(0f, 0.15f, 0f), Mat("M_FX_Mist"), false, false, 16, true);
            ParticleSystem.MainModule main = dust.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.6f, 0.55f, 0.65f, 0.35f);
            ParticleSystem.EmissionModule emission = dust.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3f, 0.1f, 0.5f);
            ParticleSystem.ColorOverLifetimeModule fade = dust.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, (0f, 0f), (0.2f, 1f), (1f, 0f)));

            var fx = root.AddComponent<GateFx>();
            SetRef(fx, "bars", bars.transform);
            SetRef(fx, "dust", dust);
            var gate = root.GetComponent<Gate>();
            SetClip(gate, "openClip", "SFX/gate_open");
            SetClip(gate, "closeClip", "SFX/gate_close");
        }

        /// <summary>
        /// Porch lantern on a post: a jack-o'-lantern whose face is dark until the checkpoint is
        /// lit. "Lantern (lit)" is still the object Checkpoint switches on; it now holds the
        /// glowing face and a flame, and CheckpointGlow eases them in.
        /// </summary>
        private static void PolishCheckpoint(GameObject unlit, GameObject lit, Light lampLight)
        {
            EnsurePolishAssets();
            // The face looks back down the path (-Z): the level runs along +Z and checkpoints face the way on.
            Quaternion back = Quaternion.Euler(0f, 180f, 0f);
            Vector3 size = Vector3.one * 0.62f;
            Vector3 seat = new Vector3(0f, -0.1f, 0f);

            SwapMesh(unlit, "LK_Pumpkin", Mat("M_LK_Palette"));
            unlit.transform.localPosition = seat;
            unlit.transform.localRotation = back;
            unlit.transform.localScale = size;
            MeshChild("Face (unlit)", unlit.transform, "LK_JackFace", Mat("M_JackFaceUnlit"), Vector3.zero, Vector3.one);

            StripRenderer(lit);
            lit.transform.localPosition = Vector3.zero;
            lit.transform.localScale = Vector3.one;
            MeshChild("Face (lit)", lit.transform, "LK_JackFace", Mat("M_LampGlow"), seat + back * new Vector3(0f, 0f, 0.008f), size, back);
            GameObject flame = MeshChild("Flame", lit.transform, "LK_Flame", Mat("M_LampGlow"), new Vector3(0f, 0.46f, 0f), new Vector3(0.24f, 0.34f, 0.24f));

            var glow = lit.AddComponent<CheckpointGlow>();
            SetRef(glow, "lampLight", lampLight);
            SetRef(glow, "flame", flame.transform);
            SetFloat(glow, "litIntensity", lampLight.intensity);
        }

        // The farmhouse door: warmer and brighter, with a lit window and a knob.
        private static void PolishGoal(GameObject root, GameObject door)
        {
            EnsurePolishAssets();
            Material palette = Mat("M_LK_Palette");
            Vector3 s = door.transform.localScale;
            MeshChild("Door Window", door.transform, "LK_Box_GlowWindow", palette, new Vector3(0f, 0.22f, -0.55f),
                new Vector3(0.5f / s.x, 0.5f / s.y, 0.04f / s.z));
            MeshChild("Door Knob", door.transform, "LK_Box_GlowWarm", palette, new Vector3(0.32f, -0.08f, -0.6f),
                new Vector3(0.09f / s.x, 0.09f / s.y, 0.06f / s.z));
            SetClip(root.GetComponent<GoalTrigger>(), "reachedClip", "ThirdParty/KenneyRPGAudio/doorOpen_2");
        }

        // Candy bowl: a dish heaped with sweets. The child keeps the name "Bowl": the level tints it pink by that name.
        private static void PolishCandyBowl(GameObject root, GameObject bowl)
        {
            EnsurePolishAssets();
            SwapMesh(bowl, "LK_Bowl", null);
            bowl.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            bowl.transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);

            var heap = new GameObject("Candy Heap").transform;
            heap.SetParent(root.transform, false);
            heap.localPosition = new Vector3(0f, 0.86f, 0f);
            Material candy = Mat("M_Candy");
            Material other = Mat("M_CandyPickup");
            Vector3[] spots =
            {
                new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.02f, 0.06f), new Vector3(-0.19f, 0.03f, 0.1f),
                new Vector3(0.05f, 0.02f, -0.21f), new Vector3(-0.1f, 0f, -0.14f), new Vector3(0.12f, 0.12f, 0.14f)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                MeshChild($"Heap Candy {i + 1}", heap, "LK_Candy", i % 3 == 2 ? other : candy, spots[i], Vector3.one * 0.17f,
                    Quaternion.Euler(i * 47f, i * 83f, 90f + i * 31f));
            }
            SetClip(root.GetComponent<CandyBowl>(), "refillClip", "ThirdParty/KenneyRPGAudio/handleCoins");
        }

        /// <summary>The three game-flow sounds on a scene's GameManager (it is not a prefab).</summary>
        private static void WireGameManagerAudio(GameManager manager)
        {
            SetClip(manager, "pauseClip", "ThirdParty/KenneyInterfaceSounds/switch_002");
            SetClip(manager, "respawnClip", "SFX/respawn");
            SetClip(manager, "winClip", "SFX/win_jingle");
        }

        /// <summary>A MusicDirector object wired to the mixer and the three music loops plus the night ambience.</summary>
        private static GameObject CreateMusicDirector(Transform parent)
        {
            EnsurePolishAssets();
            var go = new GameObject("MusicDirector");
            if (parent != null) go.transform.SetParent(parent, false);
            var director = go.AddComponent<MusicDirector>();
            SetRef(director, "mixer", AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
            SetRef(director, "musicGroup", MixerGroup("Music"));
            SetRef(director, "sfxGroup", MixerGroup("SFX"));
            SetRef(director, "ambienceGroup", MixerGroup("Ambience"));
            SetClip(director, "titleMusic", "Music/music_title");
            SetClip(director, "exploreMusic", "Music/music_explore");
            SetClip(director, "chaseMusic", "Music/music_chase");
            SetClip(director, "ambienceLoop", "Ambience/night_wind_crickets");
            return go;
        }

        /// <summary>The shared sparkle burst for pickups (PickupSparkles) on a new object.</summary>
        private static GameObject CreatePickupSparkles(Transform parent)
        {
            EnsurePolishAssets();
            ParticleSystem sparkles = NewParticles("PickupSparkles", parent, Vector3.zero, Mat("M_FX_Spark"), false, false, 64, true);

            ParticleSystem.MainModule main = sparkles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.15f;
            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.enabled = false; // PickupSparkles calls Emit
            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            ParticleSystem.ColorOverLifetimeModule fade = sparkles.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, (0f, 1f), (0.6f, 1f), (1f, 0f)));
            ParticleSystem.SizeOverLifetimeModule shrink = sparkles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var component = sparkles.gameObject.AddComponent<PickupSparkles>();
            SetClip(component, "sparkleClip", "SFX/pickup_sparkle");
            return sparkles.gameObject;
        }
    }
}
