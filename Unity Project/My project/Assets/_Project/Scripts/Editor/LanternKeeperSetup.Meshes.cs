using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.EditorTools
{
    // Step 5: the low-poly meshes that are built in code instead of downloaded (the ghost, the
    // jack-o'-lantern, candles, candy, the iron gate, the farmhouse, hedges, dead trees, reeds).
    // Each one is saved as a Mesh asset under Art/Generated/Meshes and updated in place on every
    // run, so its GUID and every reference to it survive.
    //
    // Almost all of them share ONE material, M_LK_Palette: a tiny texture of flat colours, with
    // each face's UV pointing at its colour's cell (the same trick Kenney's kits use). One
    // material means the whole of the level dressing can be batched together.
    public static partial class LanternKeeperSetup
    {
        private const string ArtDir = "Assets/_Project/Art";
        private const string MeshesDir = ArtDir + "/Generated/Meshes";
        private const string TexturesDir = ArtDir + "/Generated/Textures";
        private const int PaletteColumns = 8;
        private const int PaletteRows = 4;

        /// <summary>Colours of the shared palette texture. The three Glow entries are emissive.</summary>
        private enum Pal
        {
            Wood, WoodDark, WoodLight, Iron, Stone, StoneDark, Pumpkin, PumpkinDark,
            Stem, Bone, Straw, Purple, PurpleDark, Hedge, HedgeDark, Wax,
            Parchment, CandyPink, CandyWhite, Soil, Black, Roof, Reed, ReedHead,
            GlowWarm, GlowWindow, GlowFlame, Rope
        }

        private static readonly Color[] PaletteColors =
        {
            new Color(0.36f, 0.24f, 0.15f), new Color(0.20f, 0.13f, 0.09f), new Color(0.52f, 0.38f, 0.24f), new Color(0.11f, 0.11f, 0.14f),
            new Color(0.42f, 0.40f, 0.48f), new Color(0.24f, 0.23f, 0.30f), new Color(0.95f, 0.45f, 0.08f), new Color(0.70f, 0.28f, 0.05f),
            new Color(0.30f, 0.35f, 0.12f), new Color(0.90f, 0.88f, 0.80f), new Color(0.78f, 0.62f, 0.25f), new Color(0.32f, 0.20f, 0.45f),
            new Color(0.17f, 0.11f, 0.26f), new Color(0.12f, 0.25f, 0.14f), new Color(0.07f, 0.15f, 0.10f), new Color(0.95f, 0.90f, 0.75f),
            new Color(0.90f, 0.82f, 0.62f), new Color(1.00f, 0.38f, 0.68f), new Color(1.00f, 0.92f, 0.95f), new Color(0.20f, 0.15f, 0.12f),
            new Color(0.03f, 0.03f, 0.04f), new Color(0.20f, 0.14f, 0.22f), new Color(0.25f, 0.33f, 0.15f), new Color(0.33f, 0.20f, 0.10f),
            new Color(1.00f, 0.60f, 0.15f), new Color(1.00f, 0.78f, 0.40f), new Color(1.00f, 0.85f, 0.40f), new Color(0.50f, 0.42f, 0.30f)
        };

        private static bool IsEmissive(Pal color) => color == Pal.GlowWarm || color == Pal.GlowWindow || color == Pal.GlowFlame;

        private static Vector2 PaletteUv(Pal color)
        {
            int index = (int)color;
            return new Vector2((index % PaletteColumns + 0.5f) / PaletteColumns, (index / PaletteColumns + 0.5f) / PaletteRows);
        }

        private static Mesh LoadMesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesDir}/{name}.asset");

        /// <summary>Saves the mesh as an asset, or copies it over the existing asset so the GUID stays.</summary>
        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            EnsureFolder(MeshesDir);
            mesh.name = name;
            string path = $"{MeshesDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>Writes the palette and its emission twin as 8 x 4 pixel PNGs (only when they changed).</summary>
        private static void WritePaletteTextures()
        {
            EnsureFolder(TexturesDir);
            var colors = new Color32[PaletteColumns * PaletteRows];
            var emission = new Color32[PaletteColumns * PaletteRows];
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = i < PaletteColors.Length ? PaletteColors[i] : Color.magenta;
                colors[i] = c;
                emission[i] = i < PaletteColors.Length && IsEmissive((Pal)i) ? (Color32)c : new Color32(0, 0, 0, 255);
            }
            WritePaletteTexture("T_LK_Palette", colors);
            WritePaletteTexture("T_LK_PaletteEmission", emission);
        }

        private static void WritePaletteTexture(string name, Color32[] pixels)
        {
            string path = $"{TexturesDir}/{name}.png";
            var texture = new Texture2D(PaletteColumns, PaletteRows, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            bool same = File.Exists(path);
            if (same)
            {
                byte[] old = File.ReadAllBytes(path);
                same = old.Length == png.Length;
                for (int i = 0; same && i < png.Length; i++) same = old[i] == png[i];
            }
            if (!same)
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            // Each pixel is one flat colour: no filtering, no mips, no compression, or neighbours bleed.
            if (importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.wrapMode == TextureWrapMode.Clamp) return;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ mesh builder

        /// <summary>
        /// Collects quads, boxes, tubes and surfaces of revolution into one mesh. Faces that
        /// share vertices come out smooth, faces that do not come out flat, so nothing sets a
        /// normal by hand. A triangle's front is the side its corners run clockwise on.
        /// </summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>(256);
            private readonly List<Vector2> _uvs = new List<Vector2>(256);
            private readonly List<int> _triangles = new List<int>(512);

            /// <summary>Applied to every point added from now on. Keep its scale positive.</summary>
            public Matrix4x4 Transform = Matrix4x4.identity;

            public int VertexCount => _vertices.Count;
            public List<Vector3> Vertices => _vertices;

            private int Add(Vector3 point, Vector2 uv)
            {
                _vertices.Add(Transform.MultiplyPoint3x4(point));
                _uvs.Add(uv);
                return _vertices.Count - 1;
            }

            /// <summary>Corners clockwise as seen from the front.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Pal color)
            {
                Vector2 uv = PaletteUv(color);
                int i = Add(a, uv);
                Add(b, uv);
                Add(c, uv);
                Add(d, uv);
                _triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            /// <summary>A triangle that faces along <paramref name="facing"/>, whatever order its corners are given in.</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Pal color)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f) (b, c) = (c, b);
                Vector2 uv = PaletteUv(color);
                int i = Add(a, uv);
                Add(b, uv);
                Add(c, uv);
                _triangles.AddRange(new[] { i, i + 1, i + 2 });
            }

            public void Box(Vector3 center, Vector3 size, Pal color, Quaternion? rotation = null)
            {
                Quaternion q = rotation ?? Quaternion.identity;
                Vector3 x = q * Vector3.right * (size.x * 0.5f);
                Vector3 y = q * Vector3.up * (size.y * 0.5f);
                Vector3 z = q * Vector3.forward * (size.z * 0.5f);
                Face(center + z, y, x, color);   // +Z
                Face(center - z, x, y, color);   // -Z
                Face(center + x, z, y, color);   // +X
                Face(center - x, y, z, color);   // -X
                Face(center + y, x, z, color);   // +Y
                Face(center - y, z, x, color);   // -Y
            }

            // A face centred on c whose normal is v x u.
            private void Face(Vector3 c, Vector3 u, Vector3 v, Pal color)
            {
                Quad(c - u - v, c - u + v, c + u + v, c + u - v, color);
            }

            /// <summary>
            /// A skin stretched over rings of points. point(ring, segment) gives each corner;
            /// rings run from one end to the other and segments run round, so that
            /// (next ring - this ring) x (next segment - this segment) points outward.
            /// </summary>
            public void Skin(int rings, int segments, Func<int, int, Vector3> point, Pal color, bool capStart = false, bool capEnd = false)
            {
                Vector2 uv = PaletteUv(color);
                int first = _vertices.Count;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++) Add(point(r, s), uv);
                }
                for (int r = 0; r + 1 < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int a = first + r * segments + s;
                        int b = first + (r + 1) * segments + s;
                        int c = first + r * segments + (s + 1) % segments;
                        int d = first + (r + 1) * segments + (s + 1) % segments;
                        _triangles.AddRange(new[] { a, b, c, b, d, c });
                    }
                }
                if (capStart) Cap(point, 0, segments, color, false);
                if (capEnd) Cap(point, rings - 1, segments, color, true);
            }

            private void Cap(Func<int, int, Vector3> point, int ring, int segments, Pal color, bool end)
            {
                Vector2 uv = PaletteUv(color);
                Vector3 middle = Vector3.zero;
                for (int s = 0; s < segments; s++) middle += point(ring, s);
                middle /= segments;
                int centre = Add(middle, uv);
                int first = _vertices.Count;
                for (int s = 0; s < segments; s++) Add(point(ring, s), uv);
                for (int s = 0; s < segments; s++)
                {
                    int a = first + s;
                    int b = first + (s + 1) % segments;
                    if (end) _triangles.AddRange(new[] { centre, b, a });
                    else _triangles.AddRange(new[] { centre, a, b });
                }
            }

            /// <summary>
            /// A surface of revolution round the Y axis. profile: x = radius, y = height, listed
            /// from the bottom up for an outward-facing surface. shape(angle, t) may return a
            /// radius multiplier (x) and a height offset (y), with t running 0..1 along the profile.
            /// </summary>
            public void Lathe(IList<Vector2> profile, int segments, Pal color, Func<float, float, Vector2> shape = null,
                bool capBottom = false, bool capTop = false, Vector3 offset = default, float turn = 0f)
            {
                Skin(profile.Count, segments, (r, s) =>
                {
                    float angle = turn + s * Mathf.PI * 2f / segments;
                    float radius = profile[r].x;
                    float height = profile[r].y;
                    if (shape != null)
                    {
                        Vector2 change = shape(angle, profile.Count > 1 ? r / (profile.Count - 1f) : 0f);
                        radius *= change.x;
                        height += change.y;
                    }
                    return offset + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                }, color, capBottom, capTop);
            }

            /// <summary>A tapered tube between two points, capped at both ends.</summary>
            public void Tube(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int segments, Pal color)
            {
                Vector3 axis = (to - from).normalized;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 v = Vector3.Cross(u, axis);
                Skin(2, segments, (r, s) =>
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    Vector3 around = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    return r == 0 ? from + around * radiusFrom : to + around * radiusTo;
                }, color, true, true);
            }

            /// <summary>A gable roof: ridge along X, eaves at y = 0, centred on centre.</summary>
            public void GableRoof(Vector3 center, float lengthX, float widthZ, float height, Pal roof, Pal gable)
            {
                float hx = lengthX * 0.5f, hz = widthZ * 0.5f;
                Vector3 a = center + new Vector3(-hx, 0f, -hz), b = center + new Vector3(hx, 0f, -hz);
                Vector3 c = center + new Vector3(hx, 0f, hz), d = center + new Vector3(-hx, 0f, hz);
                Vector3 r0 = center + new Vector3(-hx, height, 0f), r1 = center + new Vector3(hx, height, 0f);
                Quad(a, r0, r1, b, roof);                       // slope facing -Z
                Quad(c, r1, r0, d, roof);                       // slope facing +Z
                Tri(a, d, r0, Vector3.left, gable);
                Tri(b, c, r1, Vector3.right, gable);
                Quad(a, b, c, d, gable);                        // underside
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            public Mesh Save(string name) => SaveMesh(ToMesh(), name);
        }

        // ------------------------------------------------------------------ the meshes

        /// <summary>Builds (or rebuilds in place) every generated mesh.</summary>
        private static void BuildPolishMeshes()
        {
            BuildGhostMesh();
            BuildPumpkinMeshes();
            BuildSmallPropMeshes();
            BuildGateAndPlateMeshes();
            BuildSceneryMeshes();
            foreach (Pal color in new[]
                     {
                         Pal.Wood, Pal.WoodDark, Pal.WoodLight, Pal.Stone, Pal.StoneDark, Pal.Iron, Pal.Soil, Pal.Straw, Pal.Roof,
                         Pal.PurpleDark, Pal.Parchment, Pal.GlowWarm, Pal.GlowWindow, Pal.Hedge
                     })
            {
                var box = new MeshBuilder();
                box.Box(new Vector3(0f, 0.5f, 0f), Vector3.one, color);
                box.Save("LK_Box_" + color);
            }
        }

        // Height range (metres) of the ghost's sheet, and how far up from the hem the cloth waves.
        private const float GhostHem = 0.28f;
        private const float GhostTop = 1.97f;
        private const float GhostShoulder = 1.55f;

        /// <summary>
        /// A sheet ghost: a dome on a flared skirt with a scalloped hem, 1.7 m from hem to
        /// crown, floating 0.28 m off the ground. Three blend shapes move the hem and are what
        /// the ghost's animation clips key: WaveA and WaveB are the same ripple a quarter of a
        /// turn apart (animating them as sine and cosine sends the ripple round the hem), and
        /// Billow flares the skirt out.
        /// </summary>
        private static void BuildGhostMesh()
        {
            const int segments = 24;
            const int lowerRings = 10;
            const int domeRings = 8;
            const int lobes = 8;

            var profile = new List<Vector2>();
            for (int i = 0; i < lowerRings; i++)
            {
                float y = Mathf.Lerp(GhostHem, GhostShoulder, i / (float)lowerRings);
                float down = (GhostShoulder - y) / (GhostShoulder - GhostHem);
                profile.Add(new Vector2(0.40f + 0.13f * Mathf.Pow(down, 1.5f), y));
            }
            for (int i = 0; i <= domeRings; i++)
            {
                float t = i / (float)domeRings;
                float y = Mathf.Lerp(GhostShoulder, GhostTop, Mathf.Sin(t * Mathf.PI * 0.5f));
                float across = (y - GhostShoulder) / (GhostTop - GhostShoulder);
                profile.Add(new Vector2(0.40f * Mathf.Sqrt(Mathf.Max(0f, 1f - across * across)), y));
            }

            // 0 at the shoulder and above, 1 at the hem: how much of the cloth motion a ring gets.
            float Loose(float y) => Mathf.Pow(Mathf.Clamp01((1.0f - y) / (1.0f - GhostHem)), 1.6f);

            var builder = new MeshBuilder();
            builder.Lathe(profile, segments, Pal.Bone, (angle, t) =>
            {
                float y = profile[Mathf.RoundToInt(t * (profile.Count - 1))].y;
                float loose = Loose(y);
                float scallop = Mathf.Cos(angle * lobes);
                return new Vector2(1f + 0.09f * loose * scallop, -0.07f * loose * (0.5f + 0.5f * scallop));
            });
            Mesh mesh = builder.ToMesh();

            Vector3[] vertices = mesh.vertices;
            var waveA = new Vector3[vertices.Length];
            var waveB = new Vector3[vertices.Length];
            var billow = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float loose = Loose(p.y);
                if (loose <= 0f) continue;
                float angle = Mathf.Atan2(p.z, p.x);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float phase = angle * 3f + p.y * 5f;
                waveA[i] = (outward * 0.10f * Mathf.Sin(phase) + Vector3.up * 0.045f * Mathf.Cos(phase)) * loose;
                waveB[i] = (outward * 0.10f * Mathf.Cos(phase) - Vector3.up * 0.045f * Mathf.Sin(phase)) * loose;
                billow[i] = (outward * 0.16f + Vector3.up * 0.05f) * loose;
            }
            mesh.AddBlendShapeFrame("WaveA", 100f, waveA, null, null);
            mesh.AddBlendShapeFrame("WaveB", 100f, waveB, null, null);
            mesh.AddBlendShapeFrame("Billow", 100f, billow, null, null);
            SaveMesh(mesh, "LK_Ghost");
        }

        // A pumpkin 1 unit across and 0.8 tall, sitting on y = 0, with eight ribs and a stem.
        private static void AddPumpkin(MeshBuilder builder, Pal skin = Pal.Pumpkin)
        {
            const int rings = 9;
            var profile = new List<Vector2>(rings);
            for (int i = 0; i < rings; i++)
            {
                float latitude = Mathf.Lerp(-80f, 80f, i / (rings - 1f)) * Mathf.Deg2Rad;
                profile.Add(new Vector2(0.5f * Mathf.Pow(Mathf.Cos(latitude), 0.75f), 0.4f + 0.4f * Mathf.Sin(latitude)));
            }
            builder.Lathe(profile, 16, skin, (angle, t) =>
                new Vector2(1f - 0.07f * (0.5f - 0.5f * Mathf.Cos(angle * 8f)) * Mathf.Sin(t * Mathf.PI), 0f), true, true);
            builder.Tube(new Vector3(0f, 0.74f, 0f), new Vector3(0.06f, 0.96f, 0.02f), 0.075f, 0.045f, 6, Pal.Stem);
        }

        // Depth of the unit pumpkin's skin at a point on its front (+Z) side.
        private static Vector3 OnPumpkinFront(float x, float y, float lift)
        {
            float nx = x / 0.5f, ny = (y - 0.4f) / 0.4f;
            float z = 0.5f * Mathf.Sqrt(Mathf.Max(0.02f, 1f - nx * nx - ny * ny * 0.9f));
            return new Vector3(x, y, z * 0.95f + lift);
        }

        // Two triangle eyes, a triangle nose and a toothy grin, laid on the pumpkin's front.
        private static void AddJackFace(MeshBuilder builder, Pal color, float lift)
        {
            void Shape(params Vector2[] points)
            {
                Vector3 centre = Vector3.zero;
                foreach (Vector2 p in points) centre += OnPumpkinFront(p.x, p.y, lift);
                centre /= points.Length;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % points.Length];
                    builder.Tri(centre, OnPumpkinFront(a.x, a.y, lift), OnPumpkinFront(b.x, b.y, lift), Vector3.forward, color);
                }
            }
            Shape(new Vector2(-0.24f, 0.48f), new Vector2(-0.07f, 0.47f), new Vector2(-0.14f, 0.62f));
            Shape(new Vector2(0.24f, 0.48f), new Vector2(0.07f, 0.47f), new Vector2(0.14f, 0.62f));
            Shape(new Vector2(-0.035f, 0.38f), new Vector2(0.035f, 0.38f), new Vector2(0f, 0.45f));
            // Grin: a band with two teeth notched into its top edge and one into the bottom.
            Shape(new Vector2(-0.27f, 0.31f), new Vector2(-0.16f, 0.27f), new Vector2(-0.11f, 0.31f), new Vector2(-0.05f, 0.26f),
                new Vector2(0.05f, 0.26f), new Vector2(0.11f, 0.31f), new Vector2(0.16f, 0.27f), new Vector2(0.27f, 0.31f),
                new Vector2(0.17f, 0.17f), new Vector2(0.04f, 0.13f), new Vector2(0f, 0.18f), new Vector2(-0.04f, 0.13f),
                new Vector2(-0.17f, 0.17f));
        }

        private static void BuildPumpkinMeshes()
        {
            var plain = new MeshBuilder();
            AddPumpkin(plain);
            plain.Save("LK_Pumpkin");

            var face = new MeshBuilder();
            AddJackFace(face, Pal.GlowWarm, 0.012f);
            face.Save("LK_JackFace");

            // The dressing's jack-o'-lantern: pumpkin and lit face in one mesh, one material.
            var jack = new MeshBuilder();
            AddPumpkin(jack);
            AddJackFace(jack, Pal.GlowWarm, 0.012f);
            jack.Save("LK_JackOLantern");
        }

        private static void BuildSmallPropMeshes()
        {
            // Flame: a teardrop 1 unit tall, base at y = 0.
            var flame = new MeshBuilder();
            var flameProfile = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float y = i / 8f;
                flameProfile.Add(new Vector2(0.3f * Mathf.Sin(Mathf.Pow(y, 0.6f) * Mathf.PI) * (1f - 0.3f * y), y));
            }
            flame.Lathe(flameProfile, 10, Pal.GlowFlame);
            flame.Save("LK_Flame");

            // Candle stub: 1 unit tall, 1 across, with a lip of melted wax and a wick.
            var candle = new MeshBuilder();
            candle.Lathe(new[]
            {
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.86f), new Vector2(0.57f, 0.9f), new Vector2(0.55f, 0.97f),
                new Vector2(0.44f, 1f), new Vector2(0.3f, 0.95f), new Vector2(0f, 0.93f)
            }, 12, Pal.Wax, null, true);
            candle.Tube(new Vector3(0f, 0.93f, 0f), new Vector3(0.02f, 1.12f, 0f), 0.035f, 0.02f, 5, Pal.Black);
            candle.Save("LK_Candle");

            // Wrapped candy: a round sweet (radius 0.5, matching the candy's collider) with a
            // twist of wrapper at each end.
            var candy = new MeshBuilder();
            var body = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float latitude = Mathf.Lerp(-90f, 90f, i / 8f) * Mathf.Deg2Rad;
                body.Add(new Vector2(0.48f * Mathf.Cos(latitude), 0.42f * Mathf.Sin(latitude)));
            }
            candy.Lathe(body, 12, Pal.CandyPink);
            Vector2 Pleat(float angle, float t) => new Vector2(1f + 0.18f * t * Mathf.Cos(angle * 6f), 0f);
            candy.Lathe(new[] { new Vector2(0.1f, 0.36f), new Vector2(0.2f, 0.6f), new Vector2(0.36f, 0.86f) }, 12, Pal.CandyWhite, Pleat, false, true);
            candy.Lathe(new[] { new Vector2(0.36f, -0.86f), new Vector2(0.2f, -0.6f), new Vector2(0.1f, -0.36f) }, 12, Pal.CandyWhite,
                (angle, t) => Pleat(angle, 1f - t), true);
            candy.Save("LK_Candy");

            // Candy bowl: a dish 1 unit across with a thick rim, open at the top.
            var bowl = new MeshBuilder();
            bowl.Lathe(new[]
            {
                new Vector2(0.16f, 0f), new Vector2(0.36f, 0.05f), new Vector2(0.48f, 0.22f), new Vector2(0.5f, 0.34f),
                new Vector2(0.46f, 0.34f), new Vector2(0.42f, 0.2f), new Vector2(0.3f, 0.1f), new Vector2(0f, 0.08f)
            }, 14, Pal.Pumpkin, null, true);
            bowl.Save("LK_Bowl");

            // Lantern cage round a lamp's glow: four bars, a floor, a pointed roof and a ring.
            var cage = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.17f : 0.17f, z = i < 2 ? -0.17f : 0.17f;
                cage.Box(new Vector3(x, 0f, z), new Vector3(0.03f, 0.44f, 0.03f), Pal.Iron);
            }
            cage.Box(new Vector3(0f, -0.23f, 0f), new Vector3(0.42f, 0.04f, 0.42f), Pal.Iron);
            cage.Lathe(new[] { new Vector2(0.34f, 0.22f), new Vector2(0.05f, 0.42f), new Vector2(0f, 0.42f) }, 4, Pal.Iron,
                null, true, false, default, Mathf.PI * 0.25f);
            cage.Tube(new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0.52f, 0f), 0.03f, 0.03f, 5, Pal.Iron);
            cage.Save("LK_LanternCage");

            // Stone cap for a gate pillar: 1 unit square, a slab under a low pyramid.
            var cap = new MeshBuilder();
            cap.Box(new Vector3(0f, 0.1f, 0f), new Vector3(1.3f, 0.2f, 1.3f), Pal.Stone);
            var apex = new Vector3(0f, 0.6f, 0f);
            Vector3[] corners =
            {
                new Vector3(-0.55f, 0.2f, -0.55f), new Vector3(0.55f, 0.2f, -0.55f), new Vector3(0.55f, 0.2f, 0.55f), new Vector3(-0.55f, 0.2f, 0.55f)
            };
            for (int i = 0; i < 4; i++) cap.Tri(corners[i], apex, corners[(i + 1) % 4], Vector3.up, Pal.StoneDark);
            cap.Save("LK_PillarCap");

            // Note signpost: a post, a board and a sheet of paper pinned to its front (-Z).
            var sign = new MeshBuilder();
            sign.Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.1f), Pal.WoodDark);
            sign.Box(new Vector3(0f, 1.15f, -0.07f), new Vector3(0.78f, 0.56f, 0.06f), Pal.WoodLight);
            sign.Box(new Vector3(0f, 1.15f, -0.105f), new Vector3(0.54f, 0.4f, 0.015f), Pal.Parchment, Quaternion.Euler(0f, 0f, -4f));
            sign.Box(new Vector3(0f, 1.47f, -0.07f), new Vector3(0.9f, 0.07f, 0.12f), Pal.WoodDark);
            sign.Save("LK_NoteSign");

            // Hanging lantern for the farmhouse beacon: a glowing box in an iron frame on a chain.
            var hanging = new MeshBuilder();
            hanging.Box(new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.44f, 0.34f), Pal.GlowFlame);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.19f : 0.19f, z = i < 2 ? -0.19f : 0.19f;
                hanging.Box(new Vector3(x, 0f, z), new Vector3(0.05f, 0.5f, 0.05f), Pal.Iron);
            }
            hanging.Box(new Vector3(0f, -0.26f, 0f), new Vector3(0.46f, 0.05f, 0.46f), Pal.Iron);
            hanging.Lathe(new[] { new Vector2(0.38f, 0.25f), new Vector2(0.06f, 0.5f), new Vector2(0f, 0.5f) }, 4, Pal.Iron, null, true,
                false, default, Mathf.PI * 0.25f);
            hanging.Tube(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 1.1f, 0f), 0.02f, 0.02f, 4, Pal.Iron);
            hanging.Save("LK_HangingLantern");
        }

        private static void BuildGateAndPlateMeshes()
        {
            // Iron cemetery gate. The gate prefab's Door is a cube scaled (3, 3, 0.25); the bars
            // are a child of it, so they are authored in metres and pre-shrunk by that scale.
            var gate = new MeshBuilder { Transform = Matrix4x4.Scale(new Vector3(1f / 3f, 1f / 3f, 1f / 0.25f)) };
            const float half = 1.5f;
            for (int i = 0; i <= 10; i++)
            {
                float x = Mathf.Lerp(-half + 0.06f, half - 0.06f, i / 10f);
                bool tall = i % 2 == 0;
                float top = tall ? 1.32f : 1.12f;
                gate.Box(new Vector3(x, (top - half) * 0.5f, 0f), new Vector3(0.07f, top + half, 0.07f), Pal.Iron);
                // Spear point.
                gate.Lathe(new[] { new Vector2(0.075f, top), new Vector2(0f, top + 0.2f) }, 4, Pal.Iron, null, true, false, new Vector3(x, 0f, 0f));
            }
            foreach (float y in new[] { -1.3f, -0.35f, 0.95f })
            {
                gate.Box(new Vector3(0f, y, 0f), new Vector3(3f, 0.1f, 0.1f), Pal.Iron);
            }
            // A ring in the middle of each leaf, made of twelve short bars.
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * 30f;
                    Vector3 at = new Vector3(x, 0.3f, 0f) + Quaternion.Euler(0f, 0f, angle) * Vector3.right * 0.33f;
                    gate.Box(at, new Vector3(0.06f, 0.19f, 0.06f), Pal.Iron, Quaternion.Euler(0f, 0f, angle));
                }
            }
            gate.Save("LK_IronGate");

            // Rune for a pressure plate. The Pad is a cube scaled (2, 0.08, 2); the rune sits on
            // its top face, so it is authored in metres and pre-shrunk by that scale.
            var rune = new MeshBuilder { Transform = Matrix4x4.Scale(new Vector3(0.5f, 1f / 0.08f, 0.5f)) };
            const float y0 = 0.041f, thickness = 0.012f;
            const int ringSegments = 24;
            for (int i = 0; i < ringSegments; i++)
            {
                float a0 = i * Mathf.PI * 2f / ringSegments, a1 = (i + 1) * Mathf.PI * 2f / ringSegments;
                Vector3 Ring(float angle, float radius) => new Vector3(Mathf.Cos(angle) * radius, y0 + thickness, Mathf.Sin(angle) * radius);
                rune.Quad(Ring(a0, 0.74f), Ring(a0, 0.62f), Ring(a1, 0.62f), Ring(a1, 0.74f), Pal.GlowWarm);
            }
            // The glyph: a lantern shape (a diamond on a stem with two arms).
            void Bar(Vector2 from, Vector2 to, float width)
            {
                Vector2 along = (to - from).normalized;
                var side = new Vector3(-along.y, 0f, along.x) * (width * 0.5f);
                var a = new Vector3(from.x, y0 + thickness, from.y);
                var b = new Vector3(to.x, y0 + thickness, to.y);
                Vector3[] corners = { a - side, a + side, b + side, b - side };
                if (Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]).y < 0f) Array.Reverse(corners);
                rune.Quad(corners[0], corners[1], corners[2], corners[3], Pal.GlowWarm);
            }
            Bar(new Vector2(0f, -0.48f), new Vector2(0f, 0.05f), 0.09f);
            Bar(new Vector2(0f, 0.05f), new Vector2(-0.22f, 0.27f), 0.09f);
            Bar(new Vector2(-0.22f, 0.27f), new Vector2(0f, 0.5f), 0.09f);
            Bar(new Vector2(0f, 0.5f), new Vector2(0.22f, 0.27f), 0.09f);
            Bar(new Vector2(0.22f, 0.27f), new Vector2(0f, 0.05f), 0.09f);
            Bar(new Vector2(-0.3f, -0.2f), new Vector2(0.3f, -0.2f), 0.09f);
            rune.Save("LK_Rune");
        }

        private static void BuildSceneryMeshes()
        {
            var random = new System.Random(1031);
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);

            // Crate: 1 x 1 x 1 standing on y = 0, planks framed by darker beams.
            var crate = new MeshBuilder();
            crate.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.92f, 0.92f, 0.92f), Pal.Wood);
            foreach (float a in new[] { -0.46f, 0.46f })
            {
                foreach (float b in new[] { -0.46f, 0.46f })
                {
                    crate.Box(new Vector3(a, 0.5f, b), new Vector3(0.09f, 1f, 0.09f), Pal.WoodDark);
                    crate.Box(new Vector3(0f, 0.5f + a, b), new Vector3(1f, 0.09f, 0.09f), Pal.WoodDark);
                    crate.Box(new Vector3(a, 0.5f + b, 0f), new Vector3(0.09f, 0.09f, 1f), Pal.WoodDark);
                }
            }
            crate.Save("LK_Crate");

            // Rail fence panel: 2 m long along X, 1 m tall, one post at the -X end.
            var fence = new MeshBuilder();
            fence.Box(new Vector3(-1f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.14f), Pal.WoodDark);
            fence.Box(new Vector3(0f, 0.82f, 0f), new Vector3(2f, 0.1f, 0.06f), Pal.Wood, Quaternion.Euler(0f, 0f, 1.5f));
            fence.Box(new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.1f, 0.06f), Pal.Wood, Quaternion.Euler(0f, 0f, -1.5f));
            fence.Save("LK_FencePanel");

            // Iron fence panel: 2 m long along X, 1.6 m tall, spear-topped bars and a post.
            var iron = new MeshBuilder();
            iron.Box(new Vector3(-1f, 0.85f, 0f), new Vector3(0.12f, 1.7f, 0.12f), Pal.Iron);
            iron.Lathe(new[] { new Vector2(0.11f, 1.7f), new Vector2(0f, 1.92f) }, 4, Pal.Iron, null, true, false, new Vector3(-1f, 0f, 0f));
            for (int i = 1; i < 8; i++)
            {
                float x = -1f + i * 0.25f;
                iron.Box(new Vector3(x, 0.75f, 0f), new Vector3(0.045f, 1.5f, 0.045f), Pal.Iron);
                iron.Lathe(new[] { new Vector2(0.05f, 1.5f), new Vector2(0f, 1.66f) }, 4, Pal.Iron, null, true, false, new Vector3(x, 0f, 0f));
            }
            iron.Box(new Vector3(0f, 0.25f, 0f), new Vector3(2f, 0.06f, 0.06f), Pal.Iron);
            iron.Box(new Vector3(0f, 1.3f, 0f), new Vector3(2f, 0.06f, 0.06f), Pal.Iron);
            iron.Save("LK_IronFencePanel");

            // Hedge: a 1 x 1 x 1 block of clipped bush, lumpy at the edges.
            var hedge = new MeshBuilder();
            hedge.Box(new Vector3(0f, 0.48f, 0f), new Vector3(0.94f, 0.96f, 0.94f), Pal.Hedge);
            for (int i = 0; i < 22; i++)
            {
                var at = new Vector3(Next(-0.42f, 0.42f), Next(0.1f, 0.92f), Next(-0.42f, 0.42f));
                // Push the lump out to the nearest face so it breaks the silhouette.
                int axis = i % 3;
                if (axis == 0) at.x = at.x < 0f ? -0.44f : 0.44f;
                else if (axis == 1) at.y = 0.94f;
                else at.z = at.z < 0f ? -0.44f : 0.44f;
                hedge.Box(at, new Vector3(Next(0.14f, 0.3f), Next(0.1f, 0.2f), Next(0.14f, 0.3f)),
                    i % 2 == 0 ? Pal.HedgeDark : Pal.Hedge, Quaternion.Euler(Next(-20f, 20f), Next(0f, 90f), Next(-20f, 20f)));
            }
            hedge.Save("LK_Hedge");

            // Dead tree: about 4.6 m tall, a leaning trunk that forks into bare branches.
            var tree = new MeshBuilder();
            void Branch(Vector3 from, Vector3 direction, float length, float radius, int depth)
            {
                Vector3 to = from + direction.normalized * length;
                tree.Tube(from, to, radius, radius * 0.62f, depth == 0 ? 7 : 5, Pal.WoodDark);
                if (depth >= 3) return;
                int children = depth == 0 ? 3 : 2;
                for (int i = 0; i < children; i++)
                {
                    Vector3 spread = Quaternion.Euler(Next(-48f, 48f), Next(0f, 360f), Next(-48f, 48f)) * direction;
                    spread.y = Mathf.Max(spread.y, 0.25f);
                    Branch(Vector3.Lerp(from, to, Next(0.75f, 1f)), spread, length * Next(0.55f, 0.72f), radius * 0.6f, depth + 1);
                }
            }
            Branch(Vector3.zero, new Vector3(0.08f, 1f, 0.04f), 2.1f, 0.24f, 0);
            tree.Save("LK_DeadTree");

            // Reeds: a clump about 1 m across and up to 1.6 m tall; some stalks carry a cattail.
            var reeds = new MeshBuilder();
            for (int i = 0; i < 11; i++)
            {
                var foot = new Vector3(Next(-0.4f, 0.4f), 0f, Next(-0.4f, 0.4f));
                float height = Next(0.8f, 1.6f);
                Vector3 tip = foot + new Vector3(Next(-0.22f, 0.22f), height, Next(-0.22f, 0.22f));
                reeds.Tube(foot, tip, 0.022f, 0.01f, 4, Pal.Reed);
                if (i % 3 != 0) continue;
                Vector3 along = (tip - foot).normalized;
                reeds.Tube(tip - along * 0.36f, tip - along * 0.1f, 0.05f, 0.045f, 5, Pal.ReedHead);
            }
            reeds.Save("LK_Reeds");

            // Stepping stone: a flat, uneven slab about 1 m across.
            var stone = new MeshBuilder();
            float[] wobble = new float[9];
            for (int i = 0; i < wobble.Length; i++) wobble[i] = Next(0.82f, 1.1f);
            stone.Lathe(new[] { new Vector2(0.46f, 0f), new Vector2(0.5f, 0.05f), new Vector2(0.42f, 0.1f), new Vector2(0f, 0.11f) }, 9, Pal.Stone,
                (angle, t) => new Vector2(wobble[Mathf.RoundToInt(angle / (Mathf.PI * 2f) * 9f) % 9], 0f), true);
            stone.Save("LK_SteppingStone");

            // Scarecrow: 2.4 m, arms out, a jack-o'-lantern for a head, facing -Z.
            var scarecrow = new MeshBuilder();
            scarecrow.Box(new Vector3(0f, 1f, 0f), new Vector3(0.1f, 2f, 0.1f), Pal.WoodDark);
            scarecrow.Box(new Vector3(0f, 1.55f, 0f), new Vector3(1.9f, 0.09f, 0.09f), Pal.WoodDark);
            scarecrow.Box(new Vector3(0f, 1.3f, 0f), new Vector3(0.6f, 0.75f, 0.3f), Pal.Purple);
            scarecrow.Box(new Vector3(-0.6f, 1.52f, 0f), new Vector3(0.7f, 0.24f, 0.24f), Pal.Purple, Quaternion.Euler(0f, 0f, 6f));
            scarecrow.Box(new Vector3(0.6f, 1.52f, 0f), new Vector3(0.7f, 0.24f, 0.24f), Pal.Purple, Quaternion.Euler(0f, 0f, -6f));
            scarecrow.Box(new Vector3(0f, 0.86f, 0f), new Vector3(0.66f, 0.16f, 0.34f), Pal.Straw);
            foreach (float x in new[] { -1f, 1f })
            {
                for (int i = 0; i < 4; i++)
                {
                    scarecrow.Tube(new Vector3(x * 0.93f, 1.5f, 0f), new Vector3(x * Next(1.05f, 1.2f), Next(1.25f, 1.6f), Next(-0.1f, 0.1f)), 0.02f, 0.01f, 4, Pal.Straw);
                }
            }
            scarecrow.Transform = Matrix4x4.TRS(new Vector3(0f, 1.68f, 0f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.58f);
            AddPumpkin(scarecrow);
            AddJackFace(scarecrow, Pal.GlowWarm, 0.012f);
            scarecrow.Transform = Matrix4x4.identity;
            scarecrow.Lathe(new[] { new Vector2(0.52f, 2.1f), new Vector2(0.3f, 2.14f), new Vector2(0.2f, 2.42f), new Vector2(0f, 2.5f) }, 10, Pal.Straw, null, true);
            scarecrow.Save("LK_Scarecrow");

            // Farmhouse, designed for the level's 16 x 5 x 7.7 m block: walls on y = 0, the front
            // on -Z. The door itself is the GoalTrigger prefab; this leaves a dark doorway for it.
            var house = new MeshBuilder();
            house.Box(new Vector3(0f, 2.5f, 0f), new Vector3(16f, 5f, 7.7f), Pal.PurpleDark);
            house.Box(new Vector3(0f, 0.25f, 0f), new Vector3(16.3f, 0.5f, 8f), Pal.StoneDark);
            house.GableRoof(new Vector3(0f, 5f, 0f), 17.2f, 9.2f, 3.1f, Pal.Roof, Pal.Purple);
            house.Box(new Vector3(5.2f, 7.4f, 1.2f), new Vector3(1.1f, 2.6f, 1.1f), Pal.StoneDark);
            house.Box(new Vector3(5.2f, 8.75f, 1.2f), new Vector3(1.35f, 0.25f, 1.35f), Pal.Stone);
            foreach (float x in new[] { -6f, -3.4f, 3.4f, 6f })
            {
                // Lit windows with a cross of glazing bars and a sill.
                house.Box(new Vector3(x, 2.7f, -3.86f), new Vector3(1.3f, 1.6f, 0.06f), Pal.GlowWindow);
                house.Box(new Vector3(x, 2.7f, -3.9f), new Vector3(0.08f, 1.6f, 0.06f), Pal.WoodDark);
                house.Box(new Vector3(x, 2.7f, -3.9f), new Vector3(1.3f, 0.08f, 0.06f), Pal.WoodDark);
                house.Box(new Vector3(x, 1.82f, -3.93f), new Vector3(1.6f, 0.12f, 0.2f), Pal.Wood);
                house.Box(new Vector3(x, 3.58f, -3.93f), new Vector3(1.6f, 0.12f, 0.16f), Pal.Wood);
            }
            house.Box(new Vector3(0f, 6.2f, -3.86f), new Vector3(1.1f, 1.1f, 0.06f), Pal.GlowWindow);
            house.Box(new Vector3(0f, 6.2f, -3.9f), new Vector3(0.08f, 1.1f, 0.06f), Pal.WoodDark);
            // Corner boards.
            foreach (float x in new[] { -8f, 8f })
            {
                house.Box(new Vector3(x, 2.5f, -3.85f), new Vector3(0.3f, 5f, 0.3f), Pal.Wood);
                house.Box(new Vector3(x, 2.5f, 3.85f), new Vector3(0.3f, 5f, 0.3f), Pal.Wood);
            }
            house.Save("LK_Farmhouse");

            // Porch roof for the farmhouse door: two posts, a beam and a small gable.
            var porch = new MeshBuilder();
            foreach (float x in new[] { -2.9f, 2.9f })
            {
                porch.Box(new Vector3(x, 1.75f, 0f), new Vector3(0.24f, 3.5f, 0.24f), Pal.Wood);
                porch.Box(new Vector3(x, 0.12f, 0f), new Vector3(0.44f, 0.24f, 0.44f), Pal.StoneDark);
            }
            porch.Box(new Vector3(0f, 3.58f, 0f), new Vector3(6.4f, 0.22f, 0.3f), Pal.WoodDark);
            porch.Box(new Vector3(-2.9f, 3.58f, 1.6f), new Vector3(0.2f, 0.2f, 3.3f), Pal.WoodDark);
            porch.Box(new Vector3(2.9f, 3.58f, 1.6f), new Vector3(0.2f, 0.2f, 3.3f), Pal.WoodDark);
            porch.GableRoof(new Vector3(0f, 3.7f, 1.4f), 7f, 4.2f, 1.2f, Pal.Roof, Pal.Purple);
            porch.Save("LK_FarmhousePorch");
        }
    }
}
