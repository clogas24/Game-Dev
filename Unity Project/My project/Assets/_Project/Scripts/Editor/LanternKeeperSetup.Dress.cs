using System;
using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LanternKeeper.EditorTools
{
    // Step 5: "Lantern Keeper/Dress Level01", the art pass over the hand-tuned level.
    //
    // What it is allowed to touch, and nothing else:
    //   1. ONE root object, "Dressing". Every run deletes it and builds it again, so the item
    //      can be run as often as you like and always gives the same result. Everything
    //      decorative lives there: scenery, particles, the post-processing volume, ambience
    //      zones, the music director and the story notes. None of it has a collider.
    //   2. MeshRenderer.enabled on greybox blocks under "Environment" that a model replaces
    //      (gravestones, crates, the farmhouse...). The block itself, its collider, its
    //      position and its size are never changed, so the NavMesh, ghost line of sight and
    //      the jump heights are exactly what was playtested. Every run switches all of them
    //      back on first, and "Undress Level01" does only that and removes the root.
    //   3. Lighting: fog, ambient light, skybox, the Moon light's colour, strength and angle,
    //      and the camera's sky and post-processing flags.
    //   4. Audio-only fields: the three AudioClips on the scene's GameManager.
    //   5. The colours of the level's M_LK_* materials (assets, not the scene).
    // Positions are read from the scene's own objects by name wherever a block is replaced, so
    // the dressing follows the level as it is now, not as the greybox generator first made it.
    public static partial class LanternKeeperSetup
    {
        private const string DressingRootName = "Dressing";
        private const string NightSkyMaterial = "M_NightSky";
        private const float OuterReach = 70f; // how far the visual-only ground runs past the playable strip

        [MenuItem(Menu + "Dress Level01", priority = 43)]
        public static void DressLevel01MenuItem() => DressLevel01();

        [MenuItem(Menu + "Undress Level01 (remove the dressing)", priority = 44)]
        public static void UndressLevel01()
        {
            if (!OpenLevel01()) return;
            Scene scene = SceneManager.GetActiveScene();
            int restored = RestoreGreyboxRenderers(scene);
            GameObject root = FindRoot(scene, DressingRootName);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Lantern Keeper] Level01 undressed: '{DressingRootName}' removed, {restored} greybox renderer(s) switched back on. Lighting was left as it is.");
        }

        /// <summary>Builds the dressing. Also the batch-mode entry point. Returns false if the level could not be opened.</summary>
        public static bool DressLevel01()
        {
            if (!OpenLevel01()) return false;
            EnsurePolishAssets();

            Scene scene = SceneManager.GetActiveScene();
            var dresser = new Level01Dresser(scene);
            if (!dresser.Begin()) return false;

            dresser.Atmosphere();
            dresser.OuterWorld();
            dresser.Section1();
            dresser.Section2();
            dresser.Section3();
            dresser.Section4();
            dresser.Section5();
            dresser.Notes();
            dresser.AudioAndFx();
            dresser.Finish();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(dresser.Summary());
            return true;
        }

        private static bool OpenLevel01()
        {
            if (!File.Exists(Level01ScenePath))
            {
                Debug.LogWarning($"[Lantern Keeper] {Level01ScenePath} does not exist yet; run 'Create Level01 Greybox'.");
                return false;
            }
            if (SceneManager.GetActiveScene().path == Level01ScenePath) return true;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(Level01ScenePath, OpenSceneMode.Single);
            return true;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        // Every greybox renderer back on. Dressing hides some of them; nothing else in the level does.
        private static int RestoreGreyboxRenderers(Scene scene)
        {
            GameObject environment = FindRoot(scene, "Environment");
            if (environment == null) return 0;
            int count = 0;
            foreach (MeshRenderer renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.enabled) continue;
                renderer.enabled = true;
                EditorUtility.SetDirty(renderer);
                count++;
            }
            return count;
        }

        /// <summary>Everything needed while the dressing is built: the scene's groups, the models, counters.</summary>
        // Level01's scenery is below. The tutorial's and Level02's are in LanternKeeperSetup.DressLevels.cs.
        private sealed partial class Level01Dresser
        {
            private sealed class ModelPart
            {
                public Mesh Mesh;
                public Matrix4x4 Matrix;
                public Material[] Materials;
            }

            private sealed class Model
            {
                public readonly List<ModelPart> Parts = new List<ModelPart>(2);
                public Bounds Bounds;
            }

            private readonly Scene _scene;
            private readonly Dictionary<string, Model> _models = new Dictionary<string, Model>();
            private readonly Dictionary<string, Material> _natureMaterials = new Dictionary<string, Material>();
            private readonly List<GameObject> _static = new List<GameObject>(1024);
            private Transform _root, _group, _environment;
            private Material _palette, _kenney, _water, _outerGround, _pitWall;
            private Mesh _cube;
            private System.Random _random = new System.Random(1031);
            private int _hidden, _pieces, _lights, _particleSystems;

            public Level01Dresser(Scene scene) => _scene = scene;

            public string Summary() =>
                $"[Lantern Keeper] Level01 dressed: {_pieces} scenery objects, {_particleSystems} particle systems and {_lights} lights under '{DressingRootName}'; " +
                $"{_hidden} greybox renderers hidden behind models (colliders untouched). Run 'Validate Level01 (read-only)' to confirm the gameplay wiring.";

            // ------------------------------------------------------------------ set-up

            /// <param name="tuneMaterials">
            /// False leaves the M_LK_* material assets alone. The tutorial and Level02 use that:
            /// they share the materials and must not change Level01's colours.
            /// </param>
            public bool Begin(bool tuneMaterials = true)
            {
                GameObject environment = FindRoot(_scene, "Environment");
                if (environment == null)
                {
                    Debug.LogError("[Lantern Keeper] Level01 has no 'Environment' root; nothing was dressed.");
                    return false;
                }
                _environment = environment.transform;

                RestoreGreyboxRenderers(_scene);
                GameObject old = FindRoot(_scene, DressingRootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                _root = new GameObject(DressingRootName).transform;

                _palette = Mat("M_LK_Palette");
                _kenney = Mat("M_KenneyGraveyard");
                _cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (!tuneMaterials)
                {
                    _water = Mat("M_LK_Water");
                    _outerGround = Mat("M_LK_OuterGround");
                    _pitWall = Mat("M_LK_PitWall");
                    return true;
                }

                // The greybox colour code, kept but tuned for night: ground stays the darkest
                // neutral, water is blue, deadly pits glow dull red, the goal is warm orange.
                MakeMaterial("M_LK_Ground", new Color(0.15f, 0.165f, 0.14f));
                MakeMaterial("M_LK_Hazard", new Color(0.42f, 0.04f, 0.04f), false, new Color(0.3f, 0.02f, 0.02f));
                MakeMaterial("M_LK_Shallow", new Color(0.05f, 0.11f, 0.17f));
                MakeMaterial("M_LK_Corn", new Color(0.2f, 0.19f, 0.07f));
                MakeMaterial("M_LK_Porch", new Color(0.36f, 0.25f, 0.16f));
                _water = MakeMaterial("M_LK_Water", new Color(0.07f, 0.23f, 0.44f, 0.68f), true, new Color(0.012f, 0.06f, 0.13f));
                // No sky reflection and no moon glint: at a low camera angle they turn the water
                // nearly white, and it has to stay a clear, deep blue next to the cyan platforms.
                _water.SetFloat("_Smoothness", 0.2f);
                _water.SetFloat("_EnvironmentReflections", 0f);
                _water.SetFloat("_SpecularHighlights", 0f);
                _water.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                _water.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                _outerGround = MakeMaterial("M_LK_OuterGround", new Color(0.105f, 0.115f, 0.1f));
                _pitWall = MakeMaterial("M_LK_PitWall", new Color(0.11f, 0.09f, 0.1f));
                return true;
            }

            private void BeginGroup(string name, int seed)
            {
                _group = new GameObject(name).transform;
                _group.SetParent(_root, false);
                _random = new System.Random(seed);
            }

            private float Rand(float min, float max) => min + (float)_random.NextDouble() * (max - min);
            private T Pick<T>(params T[] options) => options[_random.Next(options.Length)];

            /// <summary>Marks the static scenery for batching, last of all.</summary>
            public void Finish()
            {
                foreach (GameObject go in _static)
                {
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                }
            }

            // ------------------------------------------------------------------ reading the level

            private Transform FindInLevel(string name)
            {
                foreach (GameObject root in _scene.GetRootGameObjects())
                {
                    if (root.transform == _root) continue;
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == name) return t;
                    }
                }
                return null;
            }

            /// <summary>World bounds of a greybox block, from its collider (or its renderer if it has none).</summary>
            private bool Block(string name, out Bounds bounds)
            {
                bounds = default;
                Transform t = null;
                foreach (Transform child in _environment.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != name) continue;
                    t = child;
                    break;
                }
                if (t == null)
                {
                    Debug.LogWarning($"[Lantern Keeper] Dress Level01: '{name}' is not in the level any more; its dressing was skipped.");
                    return false;
                }
                if (t.TryGetComponent(out Collider collider)) bounds = collider.bounds;
                else if (t.TryGetComponent(out Renderer renderer)) bounds = renderer.bounds;
                else return false;
                return true;
            }

            /// <summary>Stops drawing a greybox block. Its collider and everything else stay.</summary>
            private void Hide(string name)
            {
                foreach (Transform child in _environment.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != name || !child.TryGetComponent(out MeshRenderer renderer)) continue;
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                    _hidden++;
                    return;
                }
            }

            // ------------------------------------------------------------------ models

            // id: "G/pumpkin" = Kenney Graveyard Kit, "N/grass" = Kenney Nature Kit.
            private Model LoadModel(string id)
            {
                if (_models.TryGetValue(id, out Model cached)) return cached;

                bool nature = id.StartsWith("N/");
                string path = $"{ThirdPartyArtDir}/{(nature ? "KenneyNatureKit" : "KenneyGraveyardKit")}/{id.Substring(2)}.fbx";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Model model = null;
                if (prefab == null)
                {
                    Debug.LogWarning($"[Lantern Keeper] Dress Level01: model {path} is missing; that prop was skipped.");
                }
                else
                {
                    model = new Model();
                    bool first = true;
                    foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh == null) continue;
                        var part = new ModelPart { Mesh = filter.sharedMesh, Matrix = filter.transform.localToWorldMatrix };
                        Material[] source = filter.TryGetComponent(out MeshRenderer renderer) ? renderer.sharedMaterials : new Material[1];
                        part.Materials = new Material[source.Length];
                        for (int i = 0; i < source.Length; i++) part.Materials[i] = nature ? NatureMaterial(source[i]) : _kenney;
                        model.Parts.Add(part);

                        Bounds local = filter.sharedMesh.bounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3 p = local.center + Vector3.Scale(local.extents,
                                new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                            p = part.Matrix.MultiplyPoint3x4(p);
                            if (first) model.Bounds = new Bounds(p, Vector3.zero);
                            else model.Bounds.Encapsulate(p);
                            first = false;
                        }
                    }
                    if (model.Parts.Count == 0) model = null;
                    else if (model.Parts.Count > 1) Merge(model, id);
                }
                _models[id] = model;
                return model;
            }

            // A model made of several meshes (a corn plant is six) becomes one mesh asset with a
            // sub-mesh per material, so every copy placed in the level is a single object.
            private static void Merge(Model model, string id)
            {
                var materials = new List<Material>();
                var byMaterial = new Dictionary<Material, List<CombineInstance>>();
                foreach (ModelPart part in model.Parts)
                {
                    for (int i = 0; i < part.Mesh.subMeshCount; i++)
                    {
                        Material material = part.Materials[Mathf.Min(i, part.Materials.Length - 1)];
                        if (!byMaterial.TryGetValue(material, out List<CombineInstance> list))
                        {
                            list = new List<CombineInstance>();
                            byMaterial[material] = list;
                            materials.Add(material);
                        }
                        list.Add(new CombineInstance { mesh = part.Mesh, subMeshIndex = i, transform = part.Matrix });
                    }
                }

                var groups = new CombineInstance[materials.Count];
                var temporary = new List<Mesh>(materials.Count);
                for (int i = 0; i < materials.Count; i++)
                {
                    var group = new Mesh();
                    group.CombineMeshes(byMaterial[materials[i]].ToArray(), true, true);
                    temporary.Add(group);
                    groups[i] = new CombineInstance { mesh = group, transform = Matrix4x4.identity };
                }
                var merged = new Mesh();
                merged.CombineMeshes(groups, false, false);
                merged.RecalculateBounds();
                foreach (Mesh mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh);

                var name = new System.Text.StringBuilder("Kenney_");
                foreach (char c in id.Substring(2)) name.Append(char.IsLetterOrDigit(c) ? c : '_');
                Mesh asset = SaveMesh(merged, name.ToString());

                model.Parts.Clear();
                model.Parts.Add(new ModelPart { Mesh = asset, Matrix = Matrix4x4.identity, Materials = materials.ToArray() });
                model.Bounds = asset.bounds;
            }

            // The Nature Kit's models carry one small flat-colour material per colour. Each
            // becomes a shared project material (darkened for night), so models batch together.
            private Material NatureMaterial(Material source)
            {
                string key = source != null ? source.name : "default";
                if (_natureMaterials.TryGetValue(key, out Material material)) return material;

                Color color = new Color(0.5f, 0.5f, 0.5f);
                if (source != null && source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
                else if (source != null && source.HasProperty("_Color")) color = source.GetColor("_Color");
                color = new Color(color.r * 0.72f, color.g * 0.72f, color.b * 0.78f, 1f);

                var clean = new System.Text.StringBuilder(key.Length);
                foreach (char c in key) clean.Append(char.IsLetterOrDigit(c) ? c : '_');
                material = MakeMaterial("M_KN_" + clean, color);
                material.SetFloat("_Smoothness", 0.05f);
                _natureMaterials[key] = material;
                return material;
            }

            private GameObject NewPiece(string name, Mesh mesh, Material[] materials, Transform parent)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                _static.Add(go);
                _pieces++;
                return go;
            }

            // The model with its own origin at position. Single-mesh models become one object.
            private GameObject Spawn(Model model, string name, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                if (model.Parts.Count == 1)
                {
                    ModelPart part = model.Parts[0];
                    Vector3 partScale = part.Matrix.lossyScale;
                    Quaternion partRotation = part.Matrix.rotation;
                    bool uniform = Mathf.Approximately(scale.x, scale.y) && Mathf.Approximately(scale.y, scale.z);
                    if (uniform || Quaternion.Angle(partRotation, Quaternion.identity) < 0.01f)
                    {
                        GameObject single = NewPiece(name, part.Mesh, part.Materials, _group);
                        single.transform.SetPositionAndRotation(
                            position + rotation * Vector3.Scale(scale, part.Matrix.GetColumn(3)), rotation * partRotation);
                        single.transform.localScale = uniform ? partScale * scale.x : Vector3.Scale(scale, partScale);
                        return single;
                    }
                }

                var holder = new GameObject(name);
                holder.transform.SetParent(_group, false);
                holder.transform.SetPositionAndRotation(position, rotation);
                holder.transform.localScale = scale;
                for (int i = 0; i < model.Parts.Count; i++)
                {
                    ModelPart part = model.Parts[i];
                    GameObject child = NewPiece($"{name} {i + 1}", part.Mesh, part.Materials, holder.transform);
                    child.transform.localPosition = part.Matrix.GetColumn(3);
                    child.transform.localRotation = part.Matrix.rotation;
                    child.transform.localScale = part.Matrix.lossyScale;
                }
                return holder;
            }

            /// <summary>A model standing on a point: bottom centre at <paramref name="at"/>, scaled evenly to a height.</summary>
            private GameObject Prop(string id, Vector3 at, float yaw, float height, string name = null)
            {
                Model model = LoadModel(id);
                if (model == null) return null;
                float scale = height / Mathf.Max(0.001f, model.Bounds.size.y);
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                var foot = new Vector3(model.Bounds.center.x, model.Bounds.min.y, model.Bounds.center.z);
                return Spawn(model, name ?? id.Substring(2), at - rotation * (foot * scale), rotation, Vector3.one * scale);
            }

            /// <summary>
            /// A model stretched to fill a box exactly (a gravestone over its greybox block). It is
            /// turned a quarter turn when that lines its long side up with the box's long side.
            /// </summary>
            private GameObject Fit(string id, Bounds target, string name = null, bool flip = false)
            {
                Model model = LoadModel(id);
                if (model == null) return null;
                Vector3 size = model.Bounds.size;
                bool turn = (size.x >= size.z) != (target.size.x >= target.size.z);
                float yaw = (turn ? 90f : 0f) + (flip ? 180f : 0f);
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                var scale = new Vector3(
                    (turn ? target.size.z : target.size.x) / Mathf.Max(0.001f, size.x),
                    target.size.y / Mathf.Max(0.001f, size.y),
                    (turn ? target.size.x : target.size.z) / Mathf.Max(0.001f, size.z));
                var foot = new Vector3(model.Bounds.center.x, model.Bounds.min.y, model.Bounds.center.z);
                var at = new Vector3(target.center.x, target.min.y, target.center.z);
                return Spawn(model, name ?? id.Substring(2), at - rotation * Vector3.Scale(scale, foot), rotation, scale);
            }

            /// <summary>One of the generated meshes (palette material), placed as authored.</summary>
            private GameObject Piece(string mesh, Vector3 position, float yaw, Vector3 scale, string name = null)
            {
                Mesh asset = LoadMesh(mesh);
                if (asset == null) return null;
                GameObject go = NewPiece(name ?? mesh.Substring(3), asset, new[] { _palette }, _group);
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = scale;
                return go;
            }

            /// <summary>A flat-coloured box filling the given volume.</summary>
            private GameObject Box(Pal color, string name, float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
            {
                return Piece("LK_Box_" + color, new Vector3((xMin + xMax) * 0.5f, yMin, (zMin + zMax) * 0.5f), 0f,
                    new Vector3(xMax - xMin, yMax - yMin, zMax - zMin), name);
            }

            /// <summary>A plain slab with its own material (ground skirts, water).</summary>
            private GameObject Slab(string name, Material material, float xMin, float xMax, float yTop, float thickness, float zMin, float zMax)
            {
                GameObject go = NewPiece(name, _cube, new[] { material }, _group);
                go.transform.position = new Vector3((xMin + xMax) * 0.5f, yTop - thickness * 0.5f, (zMin + zMax) * 0.5f);
                go.transform.localScale = new Vector3(xMax - xMin, thickness, zMax - zMin);
                return go;
            }

            /// <summary>A jack-o'-lantern (lit face in the mesh). With a light it also flickers onto the ground.</summary>
            private void Jack(Vector3 at, float yaw, float size, bool withLight = false)
            {
                Piece("LK_JackOLantern", at, yaw, Vector3.one * size, "JackOLantern");
                if (!withLight) return;
                Light light = NewLight("JackOLantern Light", at + Vector3.up * (size * 0.5f), new Color(1f, 0.6f, 0.22f), 2.2f, 4.5f);
                light.gameObject.AddComponent<FlickerLight>();
            }

            private Light NewLight(string name, Vector3 position, Color color, float intensity, float range)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_group, false);
                go.transform.position = position;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.intensity = intensity;
                light.range = range;
                light.shadows = LightShadows.None;
                _lights++;
                return light;
            }

            // A row of 2 m panels from one point to another, stretched a little so they meet exactly.
            private void FenceRow(string mesh, Vector3 from, Vector3 to, float panelLength, float heightScale = 1f)
            {
                Vector3 along = to - from;
                float length = along.magnitude;
                if (length < 0.5f) return;
                int count = Mathf.Max(1, Mathf.RoundToInt(length / panelLength));
                float each = length / count;
                // Panels run along their local +X; LookRotation points +Z down the row, so turn a quarter back.
                float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg - 90f;
                Vector3 step = along / count;
                for (int i = 0; i < count; i++)
                {
                    Piece(mesh, from + step * (i + 0.5f), yaw, new Vector3(each / 2f, heightScale, 1f));
                }
            }

            // Models dropped at random inside a rectangle, skipping any spot the test rejects.
            private void Scatter(string[] ids, float xMin, float xMax, float zMin, float zMax, int count, float heightMin, float heightMax,
                Func<float, float, bool> allowed = null, float y = 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = Rand(xMin, xMax), z = Rand(zMin, zMax);
                    float height = Rand(heightMin, heightMax);
                    float yaw = Rand(0f, 360f);
                    string id = ids[_random.Next(ids.Length)];
                    if (allowed != null && !allowed(x, z)) continue;
                    Prop(id, new Vector3(x, y, z), yaw, height);
                }
            }

            private void ScatterPieces(string mesh, float xMin, float xMax, float zMin, float zMax, int count, float scaleMin, float scaleMax,
                Func<float, float, bool> allowed = null, float y = 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = Rand(xMin, xMax), z = Rand(zMin, zMax);
                    float scale = Rand(scaleMin, scaleMax);
                    float yaw = Rand(0f, 360f);
                    if (allowed != null && !allowed(x, z)) continue;
                    Piece(mesh, new Vector3(x, y, z), yaw, Vector3.one * scale);
                }
            }

            // ------------------------------------------------------------------ particles and sound

            private ParticleSystem Particles(string name, Vector3 centre, Vector3 boxSize, Material material, int max, float rate)
            {
                ParticleSystem particles = NewParticles(name, _group, Vector3.zero, material, true, true, max, true);
                particles.transform.position = centre;
                ParticleSystem.MainModule main = particles.main;
                main.prewarm = true;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = rate;
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = boxSize;
                _particleSystems++;
                return particles;
            }

            private void Fireflies(string name, Vector3 centre, Vector3 boxSize, int max)
            {
                ParticleSystem particles = Particles(name, centre, boxSize, Mat("M_FX_Glow"), max, max / 6.5f);
                ParticleSystem.MainModule main = particles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.4f), new Color(1f, 0.9f, 0.45f));
                ParticleSystem.NoiseModule noise = particles.noise;
                noise.enabled = true;
                noise.strength = 0.45f;
                noise.frequency = 0.3f;
                // Each firefly blinks on and off twice in its life.
                ParticleSystem.ColorOverLifetimeModule blink = particles.colorOverLifetime;
                blink.enabled = true;
                blink.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white,
                    (0f, 0f), (0.12f, 1f), (0.35f, 0.15f), (0.6f, 1f), (0.85f, 0.2f), (1f, 0f)));
            }

            private void Leaves(string name, Vector3 centre, Vector3 boxSize, int max)
            {
                ParticleSystem particles = Particles(name, centre, boxSize, Mat("M_FX_Leaf"), max, max / 9f);
                ParticleSystem.MainModule main = particles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.5f, 0.12f), new Color(0.6f, 0.22f, 0.08f));
                // A slow fall on a side wind.
                ParticleSystem.VelocityOverLifetimeModule drift = particles.velocityOverLifetime;
                drift.enabled = true;
                drift.space = ParticleSystemSimulationSpace.World;
                drift.x = new ParticleSystem.MinMaxCurve(-0.9f, -0.3f);
                drift.y = new ParticleSystem.MinMaxCurve(-0.65f, -0.4f);
                drift.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                ParticleSystem.NoiseModule noise = particles.noise;
                noise.enabled = true;
                noise.strength = 0.5f;
                noise.frequency = 0.4f;
                ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                fade.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, (0f, 0f), (0.1f, 1f), (0.85f, 1f), (1f, 0f)));
            }

            private void Mist(string name, Vector3 centre, Vector3 boxSize, int max)
            {
                ParticleSystem particles = Particles(name, centre, boxSize, Mat("M_FX_Mist"), max, max / 11f);
                ParticleSystem.MainModule main = particles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.62f, 0.6f, 0.85f, 0.12f);
                ParticleSystem.VelocityOverLifetimeModule drift = particles.velocityOverLifetime;
                drift.enabled = true;
                drift.space = ParticleSystemSimulationSpace.World;
                drift.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
                drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                drift.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                fade.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, (0f, 0f), (0.25f, 1f), (0.75f, 1f), (1f, 0f)));
            }

            // A looping 3D sound that is only heard near where it is: full volume inside min, silent past max.
            private void AmbienceZone(string name, Vector3 position, string clip, float volume, float minDistance, float maxDistance)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_group, false);
                go.transform.position = position;
                var source = go.AddComponent<AudioSource>();
                source.clip = Clip(clip);
                source.loop = true;
                source.playOnAwake = true;
                source.volume = volume;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                source.dopplerLevel = 0f;
                source.outputAudioMixerGroup = MixerGroup("Ambience");
            }

            // ------------------------------------------------------------------ atmosphere

            /// <summary>Night sky, fog, moonlight, post-processing. The only part that changes objects outside the Dressing root.</summary>
            public void Atmosphere()
            {
                BeginGroup("Atmosphere", 1);

                // Moon: cool blue-white, over the player's left shoulder. The level runs north and
                // the camera looks north, so this lights the player's back and the side of
                // everything that faces the camera, with long soft shadows falling away ahead.
                var moonRotation = Quaternion.Euler(42f, 40f, 0f);
                Transform moon = FindInLevel("Moon (Directional Light)");
                if (moon != null && moon.TryGetComponent(out Light moonLight))
                {
                    moon.rotation = moonRotation;
                    moonLight.color = new Color(0.6f, 0.7f, 1f);
                    moonLight.intensity = 0.46f;
                    moonLight.shadows = LightShadows.Soft;
                    moonLight.shadowStrength = 0.65f;
                    EditorUtility.SetDirty(moon);
                    EditorUtility.SetDirty(moonLight);
                }
                else
                {
                    Debug.LogWarning("[Lantern Keeper] Dress Level01: 'Moon (Directional Light)' not found; the moonlight was left alone.");
                }

                var horizon = new Color(0.1f, 0.07f, 0.19f);
                Shader skyShader = Shader.Find("LanternKeeper/NightSky");
                if (skyShader != null)
                {
                    string path = $"{MaterialsDir}/{NightSkyMaterial}.mat";
                    var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (sky == null)
                    {
                        sky = new Material(skyShader);
                        AssetDatabase.CreateAsset(sky, path);
                    }
                    sky.shader = skyShader;
                    sky.SetColor("_TopColor", new Color(0.012f, 0.018f, 0.06f));
                    sky.SetColor("_HorizonColor", horizon);
                    sky.SetColor("_BottomColor", new Color(0.02f, 0.015f, 0.04f));
                    sky.SetFloat("_HorizonPower", 0.55f);
                    sky.SetFloat("_StarDensity", 0.045f);
                    sky.SetFloat("_StarBrightness", 1.3f);
                    sky.SetVector("_MoonDirection", -(moonRotation * Vector3.forward));
                    sky.SetColor("_MoonColor", new Color(1.7f, 1.65f, 1.4f));
                    sky.SetFloat("_MoonSize", 0.06f);
                    sky.SetFloat("_MoonHalo", 0.5f);
                    EditorUtility.SetDirty(sky);
                    RenderSettings.skybox = sky;
                }
                else
                {
                    Debug.LogWarning("[Lantern Keeper] Shader 'LanternKeeper/NightSky' not found; the sky stays a flat colour.");
                }

                // Flat ambient keeps every surface readable without a light on it. The fog is
                // the horizon's colour and thin: at the lantern's 6 m it takes almost nothing,
                // at 40 m about half, so the path ahead fades instead of ending.
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.11f, 0.105f, 0.17f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = new Color(0.065f, 0.05f, 0.12f);
                RenderSettings.fogDensity = 0.016f;

                var volumeObject = new GameObject("PostProcess Volume (global)");
                volumeObject.transform.SetParent(_group, false);
                var volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.sharedProfile = EnsurePostProfile();

                Transform cameraTransform = FindInLevel("MainCamera");
                if (cameraTransform != null && cameraTransform.TryGetComponent(out Camera camera))
                {
                    if (skyShader != null) camera.clearFlags = CameraClearFlags.Skybox;
                    UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = true;
                    RecordOverrides(camera);
                    RecordOverrides(data);
                    EditorUtility.SetDirty(camera);
                    EditorUtility.SetDirty(data);
                }
                else
                {
                    Debug.LogWarning("[Lantern Keeper] Dress Level01: 'MainCamera' not found; tick Post Processing on the camera by hand.");
                }

                // The level is 400 m long and open, so without a limit every frame draws all of
                // it. At 190 m the fog has already taken 95% of the colour, so nothing is seen to pop.
                Transform follow = FindInLevel("PlayerFollowCamera");
                if (follow != null && follow.TryGetComponent(out CinemachineCamera virtualCamera))
                {
                    LensSettings lens = virtualCamera.Lens;
                    lens.FarClipPlane = 190f;
                    virtualCamera.Lens = lens;
                    RecordOverrides(virtualCamera);
                    EditorUtility.SetDirty(virtualCamera);
                }
            }

            // ------------------------------------------------------------------ the world outside the strip

            /// <summary>
            /// The playable strip ends at invisible walls. This gives it somewhere to be: ground
            /// running out to the fog on both sides, the creek carrying on past the banks, earth
            /// walls round the pits, a fence along the edge, and trees beyond it.
            /// </summary>
            public void OuterWorld()
            {
                BeginGroup("Outer", 2);
                const float narrow = 7f, wide = 14f, reach = OuterReach;

                // Ground skirts, a hair below the level's ground. Water stretches get water instead.
                foreach ((float zMin, float zMax) in new[] { (-48f, 52f), (58f, 64f), (80f, 106f) })
                {
                    Slab($"Outer Ground West z{zMin}", _outerGround, -reach, -narrow, -0.02f, 0.3f, zMin, zMax);
                    Slab($"Outer Ground East z{zMin}", _outerGround, narrow, reach, -0.02f, 0.3f, zMin, zMax);
                }
                foreach ((float zMin, float zMax) in new[] { (106f, 180f), (188f, 440f) })
                {
                    Slab($"Outer Ground West z{zMin}", _outerGround, -reach, -wide, -0.02f, 0.3f, zMin, zMax);
                    Slab($"Outer Ground East z{zMin}", _outerGround, wide, reach, -0.02f, 0.3f, zMin, zMax);
                }
                Slab("Outer Ground South", _outerGround, -narrow, narrow, -0.02f, 0.3f, -48f, -8f);
                Slab("Outer Ground North", _outerGround, -wide, wide, -0.02f, 0.3f, 392f, 440f);

                // Water: creek and pond (S2) and the trench (S3). The surface sits 0.55 m down, well
                // under the spirit platforms, and the bed carries on outside the strip.
                Material bed = Mat("M_LK_Shallow");
                foreach ((string name, float zMin, float zMax, float half) in new[]
                         {
                             ("Creek", 52f, 58f, narrow), ("Pond", 64f, 80f, narrow), ("Trench", 180f, 188f, wide)
                         })
                {
                    Slab($"{name} Water", _water, -reach, reach, -0.55f, 0.05f, zMin + 0.02f, zMax - 0.02f);
                    Slab($"{name} Bed West", bed, -reach, -half, -1.6f, 0.4f, zMin, zMax);
                    Slab($"{name} Bed East", bed, half, reach, -1.6f, 0.4f, zMin, zMax);
                }

                // Deadly pits: earth walls, so the drop reads as a hole in the ground and not the edge of the world.
                foreach ((string name, float zMin, float zMax, float half) in new[] { ("S2 Pit", 90f, 106f, narrow), ("S5 Pit", 356f, 372f, wide) })
                {
                    Slab($"{name} Wall West", _pitWall, -half - 0.6f, -half, -0.3f, 6.4f, zMin, zMax);
                    Slab($"{name} Wall East", _pitWall, half, half + 0.6f, -0.3f, 6.4f, zMin, zMax);
                    Slab($"{name} Wall South", _pitWall, -half, half, -2f, 4.7f, zMin - 0.6f, zMin);
                    Slab($"{name} Wall North", _pitWall, -half, half, -2f, 4.7f, zMax, zMax + 0.6f);
                }

                // The boundary, drawn: a rail fence round the patch and the creek, iron railings
                // round the graveyard, a rail fence again round the porch yards and the farm.
                const float n = narrow + 0.4f, w = wide + 0.4f;
                FenceRow("LK_FencePanel", new Vector3(-n, 0f, -8.4f), new Vector3(n, 0f, -8.4f), 3f);
                foreach (float side in new[] { -1f, 1f })
                {
                    FenceRow("LK_FencePanel", new Vector3(side * n, 0f, -8.4f), new Vector3(side * n, 0f, 52f), 3f);
                    FenceRow("LK_FencePanel", new Vector3(side * n, 0f, 58f), new Vector3(side * n, 0f, 64f), 3f);
                    FenceRow("LK_FencePanel", new Vector3(side * n, 0f, 80f), new Vector3(side * n, 0f, 89.6f), 3f);
                    FenceRow("LK_IronFencePanel", new Vector3(side * n, 0f, 106.4f), new Vector3(side * w, 0f, 106.4f), 2.4f);
                    FenceRow("LK_IronFencePanel", new Vector3(side * w, 0f, 106.4f), new Vector3(side * w, 0f, 180f), 3f);
                    FenceRow("LK_IronFencePanel", new Vector3(side * w, 0f, 188f), new Vector3(side * w, 0f, 196f), 2.7f);
                    FenceRow("LK_FencePanel", new Vector3(side * w, 0f, 196f), new Vector3(side * w, 0f, 300f), 3.2f);
                    FenceRow("LK_FencePanel", new Vector3(side * w, 0f, 372.4f), new Vector3(side * w, 0f, 392f), 3.2f);
                }

                // Beyond the fence: pines along the whole route, bare trees round the graveyard.
                string[] pines = { "G/pine", "G/pine-crooked", "G/pine-fall", "G/pine-fall-crooked" };
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float z = -40f; z < 430f; z += 9f)
                    {
                        float inner = z < 106f ? narrow : wide;
                        bool corn = z > 296f && z < 358f; // the cornfield has its own edge
                        if (corn) continue;
                        int trees = _random.Next(1, 4);
                        for (int i = 0; i < trees; i++)
                        {
                            float x = side * (inner + Rand(5f, 46f));
                            Prop(Pick(pines), new Vector3(x, 0f, z + Rand(-4f, 4f)), Rand(0f, 360f), Rand(5f, 9.5f));
                        }
                    }
                    for (float z = 110f; z < 196f; z += 13f)
                    {
                        Piece("LK_DeadTree", new Vector3(side * (wide + Rand(1.8f, 9f)), 0f, z + Rand(-4f, 4f)), Rand(0f, 360f), Vector3.one * Rand(0.9f, 1.4f));
                    }
                }
                // A tree line closing the view at each end of the level.
                for (float x = -60f; x <= 60f; x += 6.5f)
                {
                    Prop(Pick(pines), new Vector3(x + Rand(-2f, 2f), 0f, -30f + Rand(-6f, 6f)), Rand(0f, 360f), Rand(6f, 10f));
                    Prop(Pick(pines), new Vector3(x + Rand(-2f, 2f), 0f, 412f + Rand(-6f, 8f)), Rand(0f, 360f), Rand(7f, 11f));
                }
            }

            // ------------------------------------------------------------------ S1 Pumpkin Patch

            public void Section1()
            {
                BeginGroup("S1_PumpkinPatch", 11);
                const float w = 7f;

                // Crate row (jump, 0.5 m): the same footprint and height, drawn as crates.
                if (Block("S1_PumpkinCrates_Row (jump, 0.5 m)", out Bounds crates))
                {
                    Hide("S1_PumpkinCrates_Row (jump, 0.5 m)");
                    FillWithCrates(crates, Mathf.Max(1, Mathf.RoundToInt(crates.size.x)), 1);
                }

                // Fence (jump, 0.8 m): rail panels scaled to the block's height.
                if (Block("S1_Fence (jump, 0.8 m)", out Bounds fence))
                {
                    Hide("S1_Fence (jump, 0.8 m)");
                    FenceRow("LK_FencePanel", new Vector3(fence.min.x, 0f, fence.center.z), new Vector3(fence.max.x, 0f, fence.center.z), 2f, fence.size.y / 1.1f);
                }

                // The candle nook's two walls: stacked crates.
                foreach (string name in new[] { "S1_CrateStack_NookSouth", "S1_CrateStack_NookNorth" })
                {
                    if (!Block(name, out Bounds stack)) continue;
                    Hide(name);
                    FillWithCrates(stack, Mathf.Max(1, Mathf.RoundToInt(stack.size.x / 1.1f)), 2);
                }

                // The S-bend's two walls: hay bales two high, with pumpkins along the top.
                foreach (string name in new[] { "S1_PumpkinWall_A", "S1_PumpkinWall_B" })
                {
                    if (!Block(name, out Bounds wall)) continue;
                    Hide(name);
                    int columns = Mathf.Max(1, Mathf.RoundToInt(wall.size.x / 1.5f));
                    float width = wall.size.x / columns, layer = wall.size.y * 0.5f;
                    for (int row = 0; row < 2; row++)
                    {
                        for (int column = 0; column < columns; column++)
                        {
                            var cell = new Bounds(
                                new Vector3(wall.min.x + width * (column + 0.5f), wall.min.y + layer * (row + 0.5f), wall.center.z),
                                new Vector3(width, layer, wall.size.z));
                            Fit(Pick("G/hay-bale", "G/hay-bale-bundled"), cell, "HayBale", (row + column) % 2 == 0);
                        }
                    }
                    for (float x = wall.min.x + 0.8f; x < wall.max.x - 0.4f; x += Rand(1.3f, 2.1f))
                    {
                        var top = new Vector3(x, wall.max.y, wall.center.z + Rand(-0.15f, 0.15f));
                        if (_random.Next(3) == 0) Jack(top, 180f + Rand(-25f, 25f), Rand(0.5f, 0.65f));
                        else Prop(Pick("G/pumpkin", "G/pumpkin-tall"), top, Rand(0f, 360f), Rand(0.35f, 0.55f));
                    }
                }

                // Pumpkins: a few against the fence line inside the strip, the patch itself outside it.
                string[] pumpkins = { "G/pumpkin", "G/pumpkin", "G/pumpkin-tall", "G/pumpkin-carved", "G/pumpkin-tall-carved" };
                bool ClearOfDitch(float x, float z) => z < 27.2f || z > 31.8f;
                Scatter(pumpkins, -6.8f, -5.9f, -6f, 47f, 9, 0.35f, 0.7f, (x, z) => ClearOfDitch(x, z) && (z < 18f || z > 27f));
                Scatter(pumpkins, 5.9f, 6.8f, -6f, 47f, 10, 0.35f, 0.7f, ClearOfDitch);
                Scatter(pumpkins, -24f, -8.2f, -12f, 50f, 46, 0.35f, 0.9f);
                Scatter(pumpkins, 8.2f, 24f, -12f, 50f, 46, 0.35f, 0.9f);
                Scatter(new[] { "G/hay-bale", "G/hay-bale-bundled" }, -16f, -8.6f, -8f, 48f, 6, 0.8f, 1f);
                Scatter(new[] { "G/hay-bale", "G/hay-bale-bundled" }, 8.6f, 16f, -8f, 48f, 6, 0.8f, 1f);

                // Scarecrows keep watch from the patch on either side, turned toward the path.
                Piece("LK_Scarecrow", new Vector3(-9.6f, 0f, 12.5f), -90f, Vector3.one, "Scarecrow West");
                Piece("LK_Scarecrow", new Vector3(9.8f, 0f, 33.5f), 90f, Vector3.one * 1.08f, "Scarecrow East");
                Piece("LK_Scarecrow", new Vector3(-10.5f, 0f, 43f), -110f, Vector3.one * 0.95f, "Scarecrow North");

                // Grass underfoot: no colliders, so it is walked straight through.
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, -7.5f, 51.5f, 34, 0.25f, 0.5f, ClearOfDitch);

                Fireflies("Fireflies S1", new Vector3(0f, 1.3f, 22f), new Vector3(18f, 2.2f, 58f), 26);
                Leaves("Leaves S1", new Vector3(3f, 5f, 22f), new Vector3(20f, 1f, 58f), 18);
            }

            // Rows x columns of crates that together fill a block exactly.
            private void FillWithCrates(Bounds block, int columns, int rows)
            {
                float width = block.size.x / columns, layer = block.size.y / rows;
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        var at = new Vector3(block.min.x + width * (column + 0.5f), block.min.y + layer * row, block.center.z);
                        Piece("LK_Crate", at, 0f, new Vector3(width, layer, block.size.z));
                    }
                }
            }

            // ------------------------------------------------------------------ S2 Spirit Bridge

            public void Section2()
            {
                BeginGroup("S2_SpiritBridge", 12);
                const float bed = -1.6f;

                // Reeds stand in the water along the banks, clear of the bridges and the ramps out.
                foreach ((float x, float z) in new[]
                         {
                             (-3.4f, 52.7f), (3.6f, 52.8f), (-6.3f, 57.3f), (-3.2f, 57.4f), (3.3f, 57.3f), (6.2f, 57.2f),
                             (-1.4f, 64.8f), (1.6f, 64.7f), (6.4f, 69f), (-6.5f, 78.6f), (-1.5f, 79.3f), (1.3f, 79.2f), (6.5f, 76f), (-6.4f, 70.5f)
                         })
                {
                    Piece("LK_Reeds", new Vector3(x, bed, z), Rand(0f, 360f), Vector3.one * Rand(1.5f, 1.9f));
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float x = 8.5f; x < 30f; x += Rand(2.5f, 5f))
                    {
                        Piece("LK_Reeds", new Vector3(side * x, bed, Pick(52.6f, 57.4f)), Rand(0f, 360f), Vector3.one * Rand(1.5f, 2f));
                        Piece("LK_Reeds", new Vector3(side * x, bed, Pick(64.6f, 79.4f)), Rand(0f, 360f), Vector3.one * Rand(1.5f, 2f));
                    }
                }

                // Lily pads float on the pond, kept well away from the bridge so they are never mistaken for it.
                foreach ((float xMin, float xMax, float zMin, float zMax, int count) in new[]
                         {
                             (-1.2f, 2.2f, 65.2f, 68.8f, 4), (-1.8f, 2.2f, 73.8f, 79f, 5), (6f, 6.8f, 65f, 71f, 2)
                         })
                {
                    Scatter(new[] { "N/lily_large", "N/lily_small" }, xMin, xMax, zMin, zMax, count, 0.05f, 0.09f, null, -0.545f);
                }

                // Stepping stones lead the eye from one bridge to the next. They lie on solid ground.
                foreach ((float x, float z) in new[]
                         {
                             (0.1f, 49.9f), (-0.2f, 51.1f), (0f, 58.8f), (-0.9f, 60f), (-2.1f, 61.3f), (-3.3f, 62.5f), (-4f, 63.5f),
                             (4.4f, 80.7f), (4.5f, 82.1f), (4.2f, 83.8f), (3.7f, 85.6f), (3.2f, 87.4f), (3f, 89.2f)
                         })
                {
                    Piece("LK_SteppingStone", new Vector3(x, 0f, z), Rand(0f, 360f), new Vector3(Rand(0.8f, 1.05f), 0.45f, Rand(0.8f, 1.05f)));
                }

                Scatter(new[] { "N/grass", "N/grass_large" }, -6.7f, 6.7f, 58.3f, 63.7f, 7, 0.25f, 0.45f);
                Scatter(new[] { "N/grass", "N/grass_large", "N/mushroom_redGroup" }, -6.7f, 6.7f, 80.4f, 89.3f, 9, 0.2f, 0.45f);
                Scatter(new[] { "N/rock_smallA", "N/rock_smallFlatB", "N/stump_old", "N/log_stack" }, -6.8f, -5.6f, 58.5f, 63.5f, 2, 0.4f, 0.7f);
                Scatter(new[] { "N/rock_smallA", "N/rock_smallFlatB" }, 8f, 22f, 44f, 90f, 12, 0.4f, 1.1f);
                Scatter(new[] { "N/rock_smallA", "N/rock_smallFlatB" }, -22f, -8f, 44f, 90f, 12, 0.4f, 1.1f);

                Fireflies("Fireflies S2", new Vector3(0f, 0.9f, 66f), new Vector3(18f, 2.2f, 30f), 22);
                AmbienceZone("Ambience Creek", new Vector3(0f, -0.5f, 55f), "Ambience/creek_water", 0.5f, 4f, 24f);
                AmbienceZone("Ambience Pond", new Vector3(0f, -0.5f, 72f), "Ambience/creek_water", 0.42f, 6f, 28f);
            }

            // ------------------------------------------------------------------ S3 Graveyard

            public void Section3()
            {
                BeginGroup("S3_Graveyard", 13);
                const float w = 14f;

                // Gravestones: each greybox slab is cover from the ghosts, so each model is
                // stretched to its slab's exact width and height. What looks like cover is cover.
                string[] stones = { "G/gravestone-bevel", "G/gravestone-round", "G/gravestone-wide", "G/gravestone-decorative", "G/gravestone-roof" };
                for (int i = 1; i <= 15; i++)
                {
                    string name = $"S3_Gravestone_{i:00}";
                    if (!Block(name, out Bounds slab)) continue;
                    Hide(name);
                    Fit(stones[i % stones.Length], slab, name + " Model", true);
                    if (i % 3 == 0) Prop("G/candle-multiple", new Vector3(slab.center.x + 0.3f, 0f, slab.min.z - 0.3f), Rand(0f, 360f), 0.3f);
                }

                // Graveyard wall: the stone block stays; it gets railings, gate posts and lanterns.
                foreach (string name in new[] { "S3_GraveyardWall_West", "S3_GraveyardWall_East" })
                {
                    if (!Block(name, out Bounds wall)) continue;
                    FenceRow("LK_IronFencePanel", new Vector3(wall.min.x, wall.max.y, wall.center.z), new Vector3(wall.max.x, wall.max.y, wall.center.z), 2f, 0.5f);
                    // The end beside the gate opening is the one nearer x = 0.
                    float end = Mathf.Abs(wall.min.x) < Mathf.Abs(wall.max.x) ? wall.min.x : wall.max.x;
                    float inward = Mathf.Sign(wall.center.x); // from the opening into the wall
                    Box(Pal.Stone, "Gate Post", Mathf.Min(end, end + inward * 0.9f), Mathf.Max(end, end + inward * 0.9f),
                        0f, wall.max.y + 0.5f, wall.min.z - 0.15f, wall.max.z + 0.15f);
                    Jack(new Vector3(end + inward * 0.45f, wall.max.y + 0.5f, wall.center.z), 180f, 0.7f, true);
                }

                // The two crypt blocks: raised burial terraces. Pilasters and sealed doors on the
                // faces, railings round the top, and crypts and stones standing on them.
                foreach (string name in new[] { "S3_Crypt_West", "S3_Crypt_East" })
                {
                    if (!Block(name, out Bounds terrace)) continue;
                    float inward = -Mathf.Sign(terrace.center.x);                 // toward the corridor
                    float face = inward > 0f ? terrace.max.x : terrace.min.x;       // the corridor wall
                    float top = terrace.max.y;

                    for (float z = terrace.min.z + 0.6f; z < terrace.max.z; z += 3f)
                    {
                        Box(Pal.Stone, "Pilaster", Mathf.Min(face, face + inward * 0.1f), Mathf.Max(face, face + inward * 0.1f), 0f, top, z - 0.25f, z + 0.25f);
                    }
                    Box(Pal.Stone, "Coping", terrace.min.x - 0.1f, terrace.max.x + 0.1f, top, top + 0.18f, terrace.min.z - 0.1f, terrace.max.z + 0.1f);
                    // A sealed door on the face the player walks toward.
                    float doorX = terrace.center.x + inward * 2.5f;
                    Box(Pal.StoneDark, "Crypt Door Frame", doorX - 1.1f, doorX + 1.1f, 0f, 2.5f, terrace.min.z - 0.12f, terrace.min.z);
                    Box(Pal.Black, "Crypt Door", doorX - 0.8f, doorX + 0.8f, 0f, 2.1f, terrace.min.z - 0.16f, terrace.min.z - 0.1f);

                    FenceRow("LK_IronFencePanel", new Vector3(face, top + 0.18f, terrace.min.z), new Vector3(face, top + 0.18f, terrace.max.z), 2.2f, 0.55f);
                    FenceRow("LK_IronFencePanel", new Vector3(terrace.min.x, top + 0.18f, terrace.min.z), new Vector3(terrace.max.x, top + 0.18f, terrace.min.z), 2.2f, 0.55f);

                    float cx = terrace.center.x;
                    Prop("G/crypt-large", new Vector3(cx - inward * 1f, top, terrace.center.z + 1.5f), inward > 0f ? 90f : -90f, 3.4f);
                    Prop("G/crypt-small", new Vector3(cx + inward * 3.6f, top, terrace.min.z + 2.4f), 180f, 2.2f);
                    Prop("G/pillar-obelisk", new Vector3(cx - inward * 4.5f, top, terrace.min.z + 1.8f), 0f, 2.4f);
                    for (int i = 0; i < 6; i++)
                    {
                        Prop(Pick(stones), new Vector3(cx + Rand(-5f, 5f), top, Rand(terrace.min.z + 1f, terrace.max.z - 1f)), Pick(0f, 180f), Rand(0.9f, 1.4f));
                    }
                    Piece("LK_DeadTree", new Vector3(cx - inward * 3.5f, top, terrace.max.z - 2.5f), Rand(0f, 360f), Vector3.one * 0.9f);
                }

                // Bare trees stand against the railings; their trunks are thinner than the fence line they hug.
                foreach ((float x, float z) in new[] { (-13.3f, 121f), (13.2f, 128.5f), (-13.2f, 144.5f), (13.3f, 146f), (-13.3f, 169f), (13.2f, 176.5f) })
                {
                    Piece("LK_DeadTree", new Vector3(x, 0f, z), Rand(0f, 360f), Vector3.one * Rand(0.85f, 1.1f));
                }

                // Odds and ends along the wall, off the route.
                Prop("G/coffin-old", new Vector3(-12.3f, 0f, 118.6f), 20f, 0.5f);
                Prop("G/shovel-dirt", new Vector3(-11f, 0f, 119.6f), 140f, 0.7f);
                Prop("G/bench-damaged", new Vector3(12.2f, 0f, 119f), 180f, 0.8f);
                Prop("G/urn-round", new Vector3(12.9f, 0f, 165f), 0f, 0.8f);
                Prop("G/rocks-tall", new Vector3(-12.6f, 0f, 178.6f), 40f, 1.2f);
                Prop("G/rocks", new Vector3(12.4f, 0f, 166.5f), 0f, 0.6f);
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, 107f, 149.5f, 30, 0.25f, 0.5f);
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, 163.5f, 179.5f, 14, 0.25f, 0.5f);
                Scatter(new[] { "N/grass", "N/mushroom_redGroup" }, -w + 0.3f, w - 0.3f, 188.4f, 195.6f, 8, 0.2f, 0.4f);

                // More graves beyond the railings, so the yard does not stop at the fence.
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = 0; i < 16; i++)
                    {
                        Prop(Pick(stones), new Vector3(side * Rand(15.5f, 30f), 0f, Rand(110f, 178f)), Pick(0f, 180f), Rand(0.9f, 1.6f));
                    }
                    Prop("G/crypt", new Vector3(side * 21f, 0f, 133f + side * 6f), side > 0f ? -90f : 90f, 3.2f);
                    for (float x = 15.5f; x < 30f; x += Rand(3f, 5.5f))
                    {
                        Piece("LK_Reeds", new Vector3(side * x, -1.6f, Pick(180.6f, 187.4f)), Rand(0f, 360f), Vector3.one * Rand(1.5f, 2f));
                    }
                }
                foreach ((float x, float z) in new[] { (-11.5f, 187.3f), (-7f, 187.4f), (8f, 187.3f), (12.2f, 187.2f) })
                {
                    Piece("LK_Reeds", new Vector3(x, -1.6f, z), Rand(0f, 360f), Vector3.one * Rand(1.5f, 1.9f));
                }

                // Ground mist over the two yards. Few, large, faint puffs.
                Mist("Mist S3 Yard 1", new Vector3(0f, 0.5f, 133f), new Vector3(26f, 0.4f, 32f), 16);
                Mist("Mist S3 Yard 2", new Vector3(0f, 0.5f, 170f), new Vector3(26f, 0.4f, 18f), 10);
                Mist("Mist S3 Corridor", new Vector3(0f, 0.4f, 156.5f), new Vector3(3f, 0.3f, 12f), 5);

                AmbienceZone("Ambience Graveyard Choir", new Vector3(0f, 3f, 140f), "Ambience/graveyard_choir", 0.36f, 10f, 50f);
                AmbienceZone("Ambience Trench", new Vector3(0f, -0.5f, 184f), "Ambience/creek_water", 0.36f, 5f, 22f);
            }

            // ------------------------------------------------------------------ S4 Candy Porch -> Cemetery Gate

            public void Section4()
            {
                BeginGroup("S4_CandyPorch", 14);
                const float w = 14f;

                // The porch: a boarded floor edged with trim, and a house for it to belong to,
                // standing just outside the fence with its windows lit.
                if (Block("S4_Porch_Floor", out Bounds porch))
                {
                    Box(Pal.WoodDark, "Porch Trim South", porch.min.x - 0.12f, porch.max.x + 0.12f, 0f, 0.07f, porch.min.z - 0.12f, porch.min.z);
                    Box(Pal.WoodDark, "Porch Trim West", porch.min.x - 0.12f, porch.min.x, 0f, 0.07f, porch.min.z, porch.max.z);
                    Box(Pal.WoodDark, "Porch Trim East", porch.max.x, porch.max.x + 0.12f, 0f, 0.07f, porch.min.z, porch.max.z);
                    for (float z = porch.min.z + 1f; z < porch.max.z; z += 1f)
                    {
                        Box(Pal.WoodDark, "Porch Board Gap", porch.min.x, porch.max.x, 0.035f, 0.05f, z - 0.02f, z + 0.02f);
                    }
                    Jack(new Vector3(porch.min.x - 0.5f, 0f, porch.min.z + 0.3f), 200f, 0.75f, true);
                    Jack(new Vector3(porch.max.x + 0.5f, 0f, porch.min.z + 0.3f), 160f, 0.8f, true);
                }
                Piece("LK_Farmhouse", new Vector3(-25.5f, 0f, 203f), -90f, new Vector3(0.8f, 0.85f, 0.8f), "Porch House");
                Box(Pal.Soil, "House Path", -21.9f, -14.6f, 0f, 0.03f, 202.2f, 203.8f);

                // Porch rails: hedges with jack-o'-lanterns along the top, all looking back down the path.
                foreach (string name in new[] { "S4_Porch_Rail_West", "S4_Porch_Rail_East" })
                {
                    if (!Block(name, out Bounds rail)) continue;
                    Hide(name);
                    FillWithHedge(rail, Mathf.Max(1, Mathf.RoundToInt(rail.size.x / 2.9f)), 1);
                    for (float x = rail.min.x + 1.2f; x < rail.max.x - 0.5f; x += 2.6f)
                    {
                        Jack(new Vector3(x, rail.max.y, rail.center.z), 180f + Rand(-15f, 15f), Rand(0.55f, 0.72f));
                    }
                }

                // The three walls with openings: stone, railings on top, lantern posts at the gap.
                foreach (string name in new[]
                         {
                             "S4_Wall_NarrowPath_West", "S4_Wall_NarrowPath_East", "S4_Wall_GateOne_West", "S4_Wall_GateOne_East",
                             "S4_Wall_GateTwo_West", "S4_Wall_GateTwo_East"
                         })
                {
                    if (!Block(name, out Bounds wall)) continue;
                    FenceRow("LK_IronFencePanel", new Vector3(wall.min.x, wall.max.y, wall.center.z), new Vector3(wall.max.x, wall.max.y, wall.center.z), 2.2f, 0.5f);
                    if (!name.Contains("NarrowPath")) continue;
                    float end = Mathf.Abs(wall.min.x) < Mathf.Abs(wall.max.x) ? wall.min.x : wall.max.x;
                    float inward = Mathf.Sign(wall.center.x);
                    Jack(new Vector3(end + inward * 0.5f, wall.max.y, wall.center.z), 180f, 0.7f);
                }

                // The corridor behind gate one: clipped hedges.
                foreach (string name in new[] { "S4_Hedge_West", "S4_Hedge_East" })
                {
                    if (!Block(name, out Bounds hedge)) continue;
                    Hide(name);
                    FillWithHedge(hedge, 3, 2);
                }

                // Yard edges: bales, crates and pumpkins pushed up against the fence line.
                string[] clutter = { "G/hay-bale", "G/hay-bale-bundled", "G/pumpkin", "G/pumpkin-tall", "G/pumpkin-carved" };
                foreach (float side in new[] { -1f, 1f })
                {
                    float inner = side * 12.9f, outer = side * 13.7f;
                    Scatter(clutter, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 209f, 231f, 7, 0.4f, 0.9f);
                    Scatter(clutter, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 234f, 261f, 7, 0.4f, 0.9f);
                    Scatter(clutter, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 271f, 299f, 7, 0.4f, 0.9f);
                    Scatter(new[] { "G/pumpkin", "G/pumpkin-tall", "G/hay-bale" }, side > 0f ? 15f : -30f, side > 0f ? 30f : -15f, 198f, 298f, 30, 0.4f, 1f,
                        (x, z) => x > -15.5f || z < 196f || z > 210f); // not through the house's front yard
                }
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, 208.5f, 231.5f, 20, 0.25f, 0.5f);
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, 233.5f, 261.5f, 20, 0.25f, 0.5f);
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, 270.5f, 299.5f, 22, 0.25f, 0.5f);
            }

            private void FillWithHedge(Bounds block, int columns, int rows)
            {
                // Columns along the block's long side, rows along its short side.
                bool alongX = block.size.x >= block.size.z;
                float longStep = (alongX ? block.size.x : block.size.z) / columns;
                float shortStep = (alongX ? block.size.z : block.size.x) / rows;
                for (int column = 0; column < columns; column++)
                {
                    for (int row = 0; row < rows; row++)
                    {
                        float a = longStep * (column + 0.5f), b = shortStep * (row + 0.5f);
                        var at = alongX
                            ? new Vector3(block.min.x + a, block.min.y, block.min.z + b)
                            : new Vector3(block.min.x + b, block.min.y, block.min.z + a);
                        Vector3 scale = alongX ? new Vector3(longStep, block.size.y, shortStep) : new Vector3(shortStep, block.size.y, longStep);
                        Piece("LK_Hedge", at, 0f, scale * 1.02f);
                    }
                }
            }

            // ------------------------------------------------------------------ S5 Cornfield -> Farmhouse

            public void Section5()
            {
                BeginGroup("S5_Cornfield", 15);
                const float w = 14f;
                string[] corn = { "N/crops_cornStageD", "N/crops_cornStageD", "N/crops_cornStageC" };

                // Corn walls: the dark block stays as the dense heart of the row (it is what hides
                // the player from a ghost), and stalks stand along both faces and out of the top.
                foreach (string name in new[] { "S5_Corn_1", "S5_Corn_2a", "S5_Corn_2b", "S5_Corn_2c", "S5_Corn_3a", "S5_Corn_3b", "S5_Corn_3c", "S5_Corn_4a", "S5_Corn_4b", "S5_Corn_5a", "S5_Corn_5b" })
                {
                    if (!Block(name, out Bounds row)) continue;
                    foreach (float z in new[] { row.min.z + 0.12f, row.max.z - 0.12f })
                    {
                        for (float x = row.min.x + 0.35f; x < row.max.x - 0.2f; x += Rand(0.75f, 1.05f))
                        {
                            Prop(Pick(corn), new Vector3(x, 0f, z + Rand(-0.08f, 0.08f)), Rand(0f, 360f), row.size.y + Rand(0.25f, 0.9f), "Corn");
                        }
                    }
                }

                // The field carries on outside the strip: a dark mass with a fringe of stalks.
                Material mass = Mat("M_LK_Corn");
                foreach (float side in new[] { -1f, 1f })
                {
                    Slab($"Corn Field {(side < 0f ? "West" : "East")}", mass, side > 0f ? w + 1.2f : -OuterReach, side > 0f ? OuterReach : -w - 1.2f, 2.9f, 2.9f, 298f, 357f);
                    for (float z = 298.5f; z < 357f; z += Rand(0.9f, 1.3f))
                    {
                        Prop(Pick(corn), new Vector3(side * (w + Rand(0.5f, 1.1f)), 0f, z), Rand(0f, 360f), Rand(3.1f, 3.9f), "Corn");
                    }
                }
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, 301f, 355f, 26, 0.25f, 0.5f);
                Scatter(new[] { "G/pumpkin", "G/pumpkin-tall" }, -13.6f, -12.6f, 301f, 309f, 3, 0.4f, 0.7f);
                Scatter(new[] { "G/pumpkin", "G/pumpkin-tall" }, 12.6f, 13.6f, 329.5f, 337f, 3, 0.4f, 0.7f);

                // The farmhouse: the block's footprint and height, as a house with lit windows.
                if (Block("S5_Farmhouse", out Bounds house))
                {
                    Hide("S5_Farmhouse");
                    Piece("LK_Farmhouse", new Vector3(house.center.x, house.min.y, house.center.z), 0f,
                        new Vector3(house.size.x / 16f, house.size.y / 5f, house.size.z / 7.7f), "Farmhouse");

                    // A porch roof over the goal, a lit window in its gable as the beacon seen
                    // from across the pit, and a flame on a stone plinth either side of the steps.
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
                }

                // The farm yard: what a farm leaves lying about, kept to the sides.
                string[] yard = { "G/hay-bale", "G/hay-bale-bundled", "G/pumpkin", "G/pumpkin-tall", "G/pumpkin-carved" };
                Scatter(yard, -13.6f, -9.5f, 373f, 383.5f, 9, 0.45f, 1f);
                Scatter(yard, 9.5f, 13.6f, 373f, 383.5f, 9, 0.45f, 1f);
                Piece("LK_Crate", new Vector3(-10.6f, 0f, 382.6f), 12f, Vector3.one);
                Piece("LK_Crate", new Vector3(-11.7f, 0f, 382.3f), -8f, new Vector3(1f, 0.8f, 1f));
                Piece("LK_Scarecrow", new Vector3(11.4f, 0f, 376.5f), 150f, Vector3.one, "Scarecrow Farm");
                Scatter(new[] { "N/grass", "N/grass_large" }, -w + 0.3f, w - 0.3f, 372.6f, 383.8f, 14, 0.25f, 0.5f);

                Leaves("Leaves S5 Corn", new Vector3(4f, 5.5f, 328f), new Vector3(32f, 1f, 56f), 20);
                Leaves("Leaves S5 Farm", new Vector3(3f, 5.5f, 378f), new Vector3(30f, 1f, 14f), 8);
            }

            // ------------------------------------------------------------------ story notes

            /// <summary>
            /// Five notes from the farm family, beside the path and never on it. Each is a
            /// signpost with no collider at all and a small warm light so it can be found.
            /// </summary>
            public void Notes()
            {
                BeginGroup("Notes", 20);

                Note("Note1_Lanterns", new Vector3(-5.5f, 0f, 6.4f), -22f, "Tacked to the fence post",
                    "Mabel,\n\nEvery porch gets its jack-o'-lantern lit before dark tonight, same as every Halloween. Grandpa says the old " +
                    "farmhands still walk the rows after harvest, and a warm light on a porch is what keeps them easy.\n\nIf you take the " +
                    "lantern out past the patch, mind the candle. It burns quicker than you think, and the stubs I left along the way are " +
                    "all there is.\n\n- Pa");

                Note("Note2_SpiritBridge", new Vector3(5.4f, 0f, 61f), 28f, "A page from Mabel's diary",
                    "The creek bridge washed out years ago, but Grandpa crosses anyway. He says the boards never left, they only stopped " +
                    "showing off. Hold your light out and they come back, plain as moonlight.\n\nI tried it. He's right. Then I swung the " +
                    "lantern up high to see better, the way you do for a scare, and the boards were gone again quick as a blink. Too much " +
                    "light is as bad as none.");

                Note("Note3_Farmhands", new Vector3(-7.6f, 0f, 119.6f), -18f, "Chalked on a slate by the gate",
                    "Here rest the hands who worked this farm: Amos, Silas, Old Bertie and the rest.\n\nThey mean no harm. They drift " +
                    "toward a light the way a moth does, and they forget their manners when they get close. Raise your lantern high and " +
                    "bright and they'll shy off like startled hens.\n\nJust don't hold it there. A flare eats a candle whole, and they can " +
                    "see it from clear across the yard.");

                Note("Note4_Candy", new Vector3(-3.9f, 0f, 200.8f), -14f, "Ma's note by the candy bowl",
                    "Help yourself, and take a few for the walk.\n\nThe farmhands always had a sweet tooth. Bertie would down tools for a " +
                    "butterscotch, and nothing has changed. Toss one where you want them to go and they'll float right over and forget " +
                    "all about you.\n\nPa swears the cemetery gate only opens when one of them is standing on the old stone. Handy thing, " +
                    "a hungry ghost.");

                Note("Note5_Home", new Vector3(-5.2f, 0f, 376.2f), -30f, "Pinned up in the farmyard",
                    "If you are reading this, you crossed the patch, the graves and the corn with a candle still burning. Not many do.\n\n" +
                    "The farmhands were never cruel. They only wanted a light to walk home by, same as you.\n\nCome in. Set your lantern " +
                    "on the porch with the others. It will keep them calm until morning.\n\n- The Whitlows");
            }

            private void Note(string id, Vector3 at, float yaw, string title, string body)
            {
                var note = new GameObject(id);
                note.transform.SetParent(_group, false);
                note.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));

                var story = note.AddComponent<StoryNote>();
                SetString(story, "noteId", id);
                SetString(story, "title", title);
                SetString(story, "body", body);

                GameObject sign = NewPiece(id + " Sign", LoadMesh("LK_NoteSign"), new[] { _palette }, note.transform);
                sign.transform.localPosition = Vector3.zero;

                Transform group = _group;
                _group = note.transform;
                Light light = NewLight(id + " Light", note.transform.TransformPoint(new Vector3(0f, 1.5f, -0.6f)), new Color(1f, 0.78f, 0.45f), 1.6f, 3.4f);
                light.gameObject.AddComponent<FlickerLight>();
                _group = group;
            }

            // ------------------------------------------------------------------ audio and effects

            /// <summary>Music and level-wide ambience, the pickup sparkle, and the GameManager's three sounds.</summary>
            public void AudioAndFx()
            {
                BeginGroup("Audio and Fx", 21);
                CreateMusicDirector(_group);
                CreatePickupSparkles(_group);
                _particleSystems++;

                foreach (GameObject root in _scene.GetRootGameObjects())
                {
                    foreach (GameManager manager in root.GetComponentsInChildren<GameManager>(true))
                    {
                        WireGameManagerAudio(manager);
                        EditorUtility.SetDirty(manager);
                    }
                }
            }
        }
    }
}
