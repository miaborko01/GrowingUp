using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GrowingUp.EditorTools
{
    /// <summary>
    /// Builds the static world for "Growing Up" (Phase 1). Menu: Growing Up/Rebuild World.
    /// Everything lives under the "World" root and is deterministic (fixed seed), so it can be re-run
    /// after tweaking the layout numbers below. The world is the SAME for both ages; it is sized so the
    /// tiny child (eye ~0.35 m) has tall grass towering over them, and the adult sees an ordinary meadow.
    ///
    /// Path order (z, metres from the house door): glowing mushrooms all along -> fallen LOG (rock
    /// staircase) -> DANDELION field (left) -> RIVER (stepping stones a bit to the right) -> BUSH TUNNEL
    /// -> FAIRY RING with the glowing berry bush in the middle (end of the path).
    /// The quests are added on top by InteractionBuilder (menu Growing Up/Build Interactions).
    /// </summary>
    public static class WorldBuilder
    {
        // ---------------- layout numbers (tweak and re-run) ----------------
        public const float LogZ = 4.6f;
        public const float DandelionZ0 = 6.6f, DandelionZ1 = 10.2f;
        public const float RiverZ = 13f;
        public const float RiverHalfWidth = 0.92f;
        // The CHILD is 60 cm tall: every mushroom is smaller (max ~0.45 m); bushes, the log and grass are big.
        // stepping stones are this far to the RIGHT (+x) of the path
        public const float StonesSide = 2.3f;
        public const float TunnelZ0 = 18.5f, TunnelZ1 = 21.5f;
        public const float FairyRingZ = 26f;
        public const float RingRadius = 1.15f;
        public const float PathEndZ = FairyRingZ - RingRadius + 0.2f;
        public const float PathWidth = 1.1f;
        public static readonly Vector3 SpawnPos = new Vector3(0f, 0f, -0.25f); // just off the porch step
        // stepping stones: offsets across the river from RiverZ (complete row, ~0.13 m gaps)
        public static readonly float[] StoneOffsets = { -0.72f, -0.38f, -0.04f, 0.30f, 0.62f };
        const int Seed = 20261119;

        const string MatDir = "Assets/_Project/Materials";
        const string MeshDir = "Assets/_Project/Meshes";
        public const string Nature = "Assets/SimpleNaturePack/Prefabs/";
        public const string FF = "Assets/Fantasy Forest Environment Free Sample/Meshes/Prefabs/";
        public const string FFTex = "Assets/Fantasy Forest Environment Free Sample/Textures/";

        static float Meander(float z) => 0.9f * Mathf.Sin(z * 0.19f) + 0.6f * (Mathf.Sin(z * 0.08f + 1f) - Mathf.Sin(1f));
        // straight sections: (centre z, half length) - log, river, tunnel, fairy ring
        static readonly Vector2[] Straight = { new Vector2(LogZ, 0.8f), new Vector2(RiverZ, 1.4f),
            new Vector2((TunnelZ0 + TunnelZ1) * 0.5f, (TunnelZ1 - TunnelZ0) * 0.5f + 0.6f), new Vector2(FairyRingZ, 2.2f) };

        /// Path centre-line x for a given z (gentle meander, straight at the house and at every quest).
        public static float PathX(float z)
        {
            float x = Meander(z);
            foreach (var st in Straight)
            {
                float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(z - st.x) - st.y) / 2f));
                x = Mathf.Lerp(Meander(st.x), x, w);
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(z / 3f));
            return k * x;
        }

        public static Vector3 OnPath(float z, float side = 0f) => new Vector3(PathX(z) + side, 0f, z);
        public static Vector3 FairyRingCenter => OnPath(FairyRingZ);
        public static Vector3 BerryBushPos => FairyRingCenter;
        public static Vector3 EndClearing => FairyRingCenter;
        public static float StonesX => PathX(RiverZ) + StonesSide;

        static System.Random _rng;
        static float R() => (float)_rng.NextDouble();
        static float R(float a, float b) => a + (b - a) * R();

        [MenuItem("Growing Up/Rebuild World")]
        public static void Build()
        {
            _rng = new System.Random(Seed);
            _pathPts = null; _rocks = null; _rockCols = null; _logs = null; _shrooms = null; _bushes = null;
            System.IO.Directory.CreateDirectory(MatDir);
            System.IO.Directory.CreateDirectory(MeshDir);

            // Replace the previous world + the old prototype props (stump/mushroom/rock from the first assignment).
            var oldNames = new HashSet<string> { "World", "Trees1", "Trees2", "Propos", "Stump_01", "Mushroom_01", "Rock_02" };
            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (oldNames.Contains(r.name)) Object.DestroyImmediate(r);
            var oldText = GameObject.Find("InstructionText");
            if (oldText != null) Object.DestroyImmediate(oldText);

            var world = new GameObject("World").transform;
            BuildGround(world);
            BuildPath(world);
            BuildRiver(world);
            HouseBuilder.Build(world);
            BuildStations(world);
            BuildForest(world);
            BuildBounds(world);
            BuildAmbience(world);
            SetupLighting();
            PlacePlayer();

            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            Debug.Log("[WorldBuilder] World rebuilt.");
        }

        // ---------------------------------------------------------------- helpers
        public static Material Mat(string name, Color c, float smooth = 0.05f)
        {
            string p = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, p);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material EmissiveMat(string name, Color c, Color emission)
        {
            var m = Mat(name, c, 0.3f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material ShaderMat(string name, string shader)
        {
            string p = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogError("Shader not found: " + shader); sh = Shader.Find("Universal Render Pipeline/Unlit"); }
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            else m.shader = sh;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m,
            bool collider = true, Quaternion? rot = null, bool local = false)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (local) { go.transform.localPosition = pos; go.transform.localRotation = rot ?? Quaternion.identity; }
            else { go.transform.position = pos; go.transform.rotation = rot ?? Quaternion.identity; }
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Place(string prefabPath, Transform parent, Vector3 pos, float yaw, float scale, bool keepColliders)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning("Missing prefab " + prefabPath); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            if (!keepColliders)
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return go;
        }

        public static Mesh SaveMesh(string name, Mesh src)
        {
            string p = MeshDir + "/" + name + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            if (m == null) { src.name = name; AssetDatabase.CreateAsset(src, p); return src; }
            m.Clear();
            m.indexFormat = src.indexFormat;
            m.vertices = src.vertices; m.triangles = src.triangles; m.uv = src.uv;
            m.RecalculateNormals(); m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void SetStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        static List<Vector3> _pathPts;
        public static float DistToPath(Vector3 p)
        {
            if (_pathPts == null)
            {
                _pathPts = new List<Vector3>();
                for (float z = -1.5f; z <= PathEndZ; z += 0.25f) _pathPts.Add(new Vector3(PathX(z), 0f, z));
            }
            float best = float.MaxValue;
            foreach (var q in _pathPts)
            {
                float d = (p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z);
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        static bool InHouseArea(Vector3 p, float margin) => p.x > -3.6f - margin && p.x < 3.6f + margin && p.z > -7.6f - margin && p.z < -0.8f + margin;
        static bool InRiver(Vector3 p, float margin) => Mathf.Abs(p.z - RiverZ) < RiverHalfWidth + margin;
        /// Keeps the quest areas clear of forest scatter.
        public static bool InStation(Vector3 p, float extra)
        {
            float px = PathX(p.z);
            return (Mathf.Abs(p.z - LogZ) < 1.0f + extra && Mathf.Abs(p.x - px) < 7.5f)                       // log + thicket
                || (p.z > DandelionZ0 - extra && p.z < DandelionZ1 + extra && p.x < px - 0.55f + extra && p.x > px - 3.4f - extra) // dandelion field
                || (Mathf.Abs(p.z - RiverZ) < RiverHalfWidth + 0.8f + extra && p.x > px - 0.7f - extra && p.x < StonesX + 0.9f + extra) // bank trail
                || (p.z > TunnelZ0 - 0.8f - extra && p.z < TunnelZ1 + 0.8f + extra && Mathf.Abs(p.x - px) < 2.4f + extra) // bush tunnel
                || Flat(p, FairyRingCenter) < RingRadius + 1.6f + extra;
        }

        // ---------------------------------------------------------------- ground & path
        static void BuildGround(Transform world)
        {
            // Reuse the existing "Plane"/"Ground" (it carries the Teleport Interactable building block).
            var ground = GameObject.Find("Ground");
            if (ground == null) ground = GameObject.Find("Plane");
            if (ground == null) { Debug.LogError("[WorldBuilder] No 'Ground' with the Teleport Interactable found."); return; }
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, 0f, 14f);
            ground.transform.rotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(5f, 1f, 6.4f); // 50 m x 64 m
            var gm = Mat("M_Ground", new Color(0.62f, 0.78f, 0.5f), 0.08f);
            gm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FFTex + "grass01.tga"));
            gm.SetTextureScale("_BaseMap", new Vector2(34f, 44f));
            ground.GetComponent<Renderer>().sharedMaterial = gm;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);
        }

        static void BuildPath(Transform world)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            int i = 0;
            for (float z = -1.45f; z <= PathEndZ + 0.6f; z += 0.25f, i++)
            {
                float x = PathX(z);
                float dx = PathX(z + 0.125f) - PathX(z - 0.125f);
                var tangent = new Vector3(dx, 0f, 0.25f).normalized;
                var side = Vector3.Cross(Vector3.up, tangent);
                float w = PathWidth * 0.5f * (1f + 0.12f * Mathf.Sin(z * 1.7f) + 0.08f * Mathf.Sin(z * 4.3f));
                w *= Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((PathEndZ + 0.6f - z) / 1.2f)));
                var c = new Vector3(x, 0.006f, z);
                verts.Add(c - side * w); verts.Add(c + side * w);
                uvs.Add(new Vector2(0, z * 0.7f)); uvs.Add(new Vector2(1, z * 0.7f));
                // no path inside the river: it ends at the near bank and starts again at the far bank
                bool wet = Mathf.Abs(z - RiverZ) < RiverHalfWidth + 0.05f || Mathf.Abs(z - 0.25f - RiverZ) < RiverHalfWidth + 0.05f;
                if (i > 0 && !wet) { int b = i * 2; tris.AddRange(new[] { b - 2, b, b - 1, b - 1, b, b + 1 }); }
            }
            var mesh = new Mesh { vertices = verts.ToArray(), triangles = tris.ToArray(), uv = uvs.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh = SaveMesh("PathMesh", mesh);
            var go = new GameObject("Path");
            go.transform.SetParent(world, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = PathMat();
            SetStatic(go);
        }

        public static Material PathMat()
        {
            var m = Mat("M_Path", new Color(1f, 0.97f, 0.92f), 0.05f); // dirt01 is already dark brown soil
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FFTex + "dirt01.tga"));
            m.SetTextureScale("_BaseMap", new Vector2(1.5f, 1.2f));
            return m;
        }

        // ---------------------------------------------------------------- HD props (procedural, smooth)
        static Mesh[] _rocks, _rockCols, _logs; static Mesh[][] _shrooms; static Mesh[] _bushes;
        public static Mesh HDRock(int i) { if (_rocks == null) { _rocks = new Mesh[5]; for (int k = 0; k < 5; k++) _rocks[k] = MeshGen.Rock("HD_Rock_" + k, new Vector3(1f, 0.55f + 0.1f * k, 0.85f), 11 + k * 7); } return _rocks[i % 5]; }
        /// Low-res version of the same rock (same noise) for the convex collider (PhysX hull limit 256 polys).
        public static Mesh HDRockCol(int i) { if (_rockCols == null) { _rockCols = new Mesh[5]; for (int k = 0; k < 5; k++) _rockCols[k] = MeshGen.Rock("HD_Rock_" + k + "_Collider", new Vector3(1f, 0.55f + 0.1f * k, 0.85f), 11 + k * 7, 1); } return _rockCols[i % 5]; }
        public static Material RockMat() => Mat("M_RockHD", new Color(0.47f, 0.46f, 0.43f), 0.18f);
        public static Material MossRockMat() => Mat("M_RockMossy", new Color(0.36f, 0.40f, 0.30f), 0.12f);

        public static GameObject PlaceHDRock(Transform parent, string name, Vector3 pos, float yaw, Vector3 size, int variant, bool collider, bool mossy = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var mesh = HDRock(variant);
            var ms = mesh.bounds.size; // size = final world size
            go.transform.localScale = new Vector3(size.x / ms.x, size.y / ms.y, size.z / ms.z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mossy ? MossRockMat() : RockMat();
            if (collider) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = HDRockCol(variant); mc.convex = true; }
            return go;
        }

        public static Mesh HDLog(int i) { if (_logs == null) { _logs = new Mesh[3]; for (int k = 0; k < 3; k++) _logs[k] = MeshGen.Log("HD_Log_" + k, 1f, 0.12f, 5 + k); } return _logs[i % 3]; }
        public static Material[] LogMats()
        {
            var bark = Mat("M_BarkHD", new Color(0.72f, 0.64f, 0.56f), 0.08f);
            bark.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FFTex + "bark01_bottom.tga"));
            var wood = Mat("M_LogEnd", Color.white, 0.1f);
            var rt = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/WoodRings.png");
            if (rt == null) rt = MeshGen.RingsTexture("Assets/_Project/Textures/WoodRings.png");
            wood.SetTexture("_BaseMap", rt);
            return new[] { bark, wood };
        }

        /// style 0 red toadstool, 1 brown, 2 pale slender. Materials have emission enabled (glow side quest).
        public static Mesh HDShroom(int style, int v)
        {
            if (_shrooms == null)
            {
                _shrooms = new Mesh[3][];
                for (int s2 = 0; s2 < 3; s2++) { _shrooms[s2] = new Mesh[2]; for (int k = 0; k < 2; k++) _shrooms[s2][k] = MeshGen.Mushroom("HD_Mushroom_" + s2 + "_" + k, s2, 1f, 3 + s2 * 13 + k * 5); }
            }
            return _shrooms[style % 3][v % 2];
        }
        public static Material[] ShroomMats(int style)
        {
            Material Glow(Material m) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(m); return m; }
            var stem = Glow(Mat("M_MushStem", new Color(0.93f, 0.89f, 0.80f), 0.25f));
            Material cap = style == 0 ? Mat("M_MushCapRed", new Color(0.78f, 0.12f, 0.08f), 0.55f)
                         : style == 1 ? Mat("M_MushCapBrown", new Color(0.45f, 0.27f, 0.14f), 0.4f)
                         : Mat("M_MushCapPale", new Color(0.86f, 0.80f, 0.66f), 0.35f);
            return new[] { stem, Glow(cap) };
        }

        public static GameObject PlaceHDShroom(Transform parent, Vector3 pos, float yaw, float height, int style, int variant, bool collider = true)
        {
            var go = new GameObject("Mushroom_" + (style == 0 ? "Red" : style == 1 ? "Brown" : "Pale"));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * height;
            go.AddComponent<MeshFilter>().sharedMesh = HDShroom(style, variant);
            go.AddComponent<MeshRenderer>().sharedMaterials = ShroomMats(style);
            if (collider)
            {
                // solid stem (to the tiny child mushrooms are like trees)
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 1; cap.radius = style == 1 ? 0.15f : style == 2 ? 0.07f : 0.1f; cap.height = 1f; cap.center = new Vector3(0f, 0.5f, 0f);
            }
            return go;
        }

        public static Mesh HDBush(int i)
        {
            if (_bushes == null)
            {
                _bushes = new Mesh[4];
                _bushes[0] = MeshGen.LeafBush("HD_Bush_0", new Vector3(0.6f, 0.75f, 0.6f), 130, 21);
                _bushes[1] = MeshGen.LeafBush("HD_Bush_1", new Vector3(0.8f, 0.6f, 0.7f), 150, 33);
                _bushes[2] = MeshGen.LeafBush("HD_Bush_2", new Vector3(0.5f, 0.5f, 0.5f), 90, 47);
                _bushes[3] = MeshGen.LeafBush("HD_Bush_3", new Vector3(0.9f, 0.9f, 0.9f), 190, 59);
            }
            return _bushes[i % 4];
        }
        public static Material[] BushMats()
        {
            var leaves = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fantasy Forest Environment Free Sample/Materials/tree_branches.mat");
            return new[] { leaves, Mat("M_BushCore", new Color(0.13f, 0.22f, 0.08f), 0.05f) };
        }

        public static GameObject PlaceHDBush(Transform parent, string name, Vector3 pos, float yaw, Vector3 scale, int variant, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = scale;
            var mesh = HDBush(variant);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = BushMats();
            if (collider)
            {
                var b = mesh.bounds;
                var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = new Vector3(b.size.x * 0.7f, b.size.y * 0.85f, b.size.z * 0.7f);
            }
            return go;
        }

        // ---------------------------------------------------------------- river with stepping stones
        static void BuildRiver(Transform world)
        {
            var river = new GameObject("River").transform;
            river.SetParent(world, false);
            river.position = new Vector3(0f, 0f, RiverZ);

            // Subdivided water strip (for the swell), uv.y = 0..1 bank to bank
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            const float half = 26f, step = 0.5f; const int across = 6;
            int cols = Mathf.RoundToInt(half * 2f / step) + 1;
            for (int ix = 0; ix < cols; ix++)
                for (int iz = 0; iz <= across; iz++)
                {
                    float x = -half + ix * step, v = (float)iz / across;
                    float bankWobble = 0.05f * Mathf.Sin(x * 1.3f) + 0.03f * Mathf.Sin(x * 3.1f);
                    float z = Mathf.Lerp(-RiverHalfWidth - bankWobble, RiverHalfWidth + bankWobble, v);
                    verts.Add(new Vector3(x, 0.035f, z)); uvs.Add(new Vector2(x, v));
                    if (ix > 0 && iz > 0)
                    {
                        int a = ix * (across + 1) + iz, b = a - 1, c = a - (across + 1), d = c - 1;
                        tris.AddRange(new[] { d, c, b, c, a, b }); // clockwise seen from above
                    }
                }
            var wm = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = verts.ToArray(), uv = uvs.ToArray(), triangles = tris.ToArray() };
            wm.RecalculateNormals(); wm.RecalculateBounds();
            wm = SaveMesh("RiverWaterMesh", wm);
            var water = new GameObject("Water");
            water.transform.SetParent(river, false);
            water.AddComponent<MeshFilter>().sharedMesh = wm;
            var wr = water.AddComponent<MeshRenderer>();
            wr.sharedMaterial = ShaderMat("M_RiverWater", "GrowingUp/RiverWater");
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Riverbed + muddy banks (no colliders: the ground is the riverbed)
            Prim(PrimitiveType.Cube, "RiverBed", river, new Vector3(0f, 0.003f, 0f), new Vector3(52f, 0.004f, RiverHalfWidth * 2f + 0.1f),
                Mat("M_RiverBed", new Color(0.13f, 0.17f, 0.13f)), false, null, true);
            var mud = Mat("M_RiverBank", new Color(0.33f, 0.27f, 0.19f));
            Prim(PrimitiveType.Cube, "Bank_Near", river, new Vector3(0f, 0.005f, -RiverHalfWidth - 0.12f), new Vector3(52f, 0.006f, 0.35f), mud, false, null, true);
            Prim(PrimitiveType.Cube, "Bank_Far", river, new Vector3(0f, 0.005f, RiverHalfWidth + 0.12f), new Vector3(52f, 0.006f, 0.35f), mud, false, null, true);

            // Stepping stones: a complete row a bit to the RIGHT of where the path meets the river
            var stones = new GameObject("SteppingStones").transform;
            stones.SetParent(river, false);
            var foamMat = ShaderMat("M_FoamRing", "GrowingUp/FoamRing");
            for (int k = 0; k < StoneOffsets.Length; k++)
            {
                float z = RiverZ + StoneOffsets[k];
                var pos = new Vector3(StonesX + R(-0.04f, 0.04f), 0f, z);
                PlaceHDRock(stones, "SteppingStone_" + k, pos, R(0, 360), new Vector3(R(0.24f, 0.27f), R(0.11f, 0.13f), R(0.22f, 0.25f)), k, true, true);
                var foam = Prim(PrimitiveType.Quad, "FoamRing_" + k, stones, pos + Vector3.up * 0.038f, Vector3.one * 0.42f, foamMat, false, Quaternion.Euler(90f, R(0, 360), 0f));
                foam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Little trails along both banks: from the path to the stones and back to the path
            var pm = PathMat();
            foreach (var side in new[] { -1f, 1f })
            {
                float z = RiverZ + side * (RiverHalfWidth + 0.42f);
                float x0 = PathX(RiverZ), x1 = StonesX + 0.25f;
                var t = Prim(PrimitiveType.Cube, side < 0 ? "BankTrail_Near" : "BankTrail_Far", river, new Vector3((x0 + x1) * 0.5f, 0.007f, z),
                    new Vector3(x1 - x0 + 0.4f, 0.002f, 0.5f), pm, false);
                t.transform.localScale = new Vector3(x1 - x0 + 0.4f, 0.002f, 0.5f);
                Prim(PrimitiveType.Cylinder, "TrailEnd", river, new Vector3(StonesX, 0.0065f, RiverZ + side * (RiverHalfWidth + 0.2f)), new Vector3(0.6f, 0.001f, 0.6f), pm, false);
            }

            // Rocks along both banks (not on the crossing, the trails or where the path ends)
            for (float x = -25f; x < 25f; x += R(0.6f, 1.6f))
            {
                if (x > PathX(RiverZ) - 0.9f && x < StonesX + 0.9f) continue;
                foreach (var side in new[] { -1f, 1f })
                {
                    if (R() < 0.4f) continue;
                    var p = new Vector3(x, -0.02f, RiverZ + side * (RiverHalfWidth + R(0.0f, 0.25f)));
                    float sz = R(0.15f, 0.45f);
                    PlaceHDRock(river, "BankRock", p, R(0, 360), new Vector3(sz, sz * R(0.4f, 0.7f), sz * R(0.8f, 1.1f)), _rng.Next(5), true, R() < 0.5f);
                }
            }

            // Flowing water sound (world)
            for (int k = -1; k <= 1; k++)
            {
                var a = new GameObject("RiverSound_" + (k + 1));
                a.transform.SetParent(river, false);
                a.transform.localPosition = new Vector3(PathX(RiverZ) + k * 9f, 0.1f, 0f);
                AddAudio(a, ProceduralAudio.Kind.River, ProceduralAudio.Category.Nature, 0.8f, 10 + k, 1.5f, 16f, true);
            }
            SetStatic(river.gameObject);
            foreach (var s in river.GetComponentsInChildren<AudioSource>()) GameObjectUtility.SetStaticEditorFlags(s.gameObject, 0);
        }

        // ---------------------------------------------------------------- stations (space + spawn points for each step)
        static void BuildStations(Transform world)
        {
            var root = new GameObject("Stations").transform;
            root.SetParent(world, false);
            Transform Station(string name, Vector3 spawn, Vector3 lookAt)
            {
                var s = new GameObject(name).transform; s.SetParent(root, false);
                s.position = spawn;
                var d = lookAt - spawn; d.y = 0f;
                s.rotation = Quaternion.LookRotation(d.sqrMagnitude > 0.001f ? d : Vector3.forward);
                return s;
            }
            Station("Station_Intro", SpawnPos, SpawnPos + Vector3.forward);
            Station("Station_Log", OnPath(LogZ - 1.7f), OnPath(LogZ));
            Station("Station_River", OnPath(RiverZ - RiverHalfWidth - 0.6f), OnPath(RiverZ));
            Station("Station_RiverRespawn", new Vector3(StonesX, 0f, RiverZ - RiverHalfWidth - 0.4f), new Vector3(StonesX, 0f, RiverZ));
            Station("Station_BushTunnel", OnPath(TunnelZ0 - 1.2f), OnPath(TunnelZ0));
            Station("Station_Berries", OnPath(FairyRingZ - RingRadius - 0.45f), FairyRingCenter);
            Station("Station_AdultReturn", OnPath(FairyRingZ - 0.75f), OnPath(FairyRingZ - 5f));

            // Static environment at the stations
            var env = new GameObject("StationEnvironment").transform; env.SetParent(root, false);
            var clearing = Mat("M_ClearingGrass", new Color(0.5f, 0.62f, 0.36f), 0.05f);
            clearing.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FFTex + "grass01.tga"));
            clearing.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            Prim(PrimitiveType.Cylinder, "FairyRing_Clearing", env, FairyRingCenter + Vector3.up * 0.004f, new Vector3((RingRadius + 1.3f) * 2f, 0.002f, (RingRadius + 1.3f) * 2f), clearing, false);
            var tree = Place(FF + "tree_1.prefab", env, FairyRingCenter + new Vector3(0.8f, 0f, 3.6f), 200f, 0.55f, true);
            if (tree) tree.name = "OldTree";
            SetStatic(env.gameObject);
        }

        // ---------------------------------------------------------------- forest scatter
        static void BuildForest(Transform world)
        {
            var root = new GameObject("Forest").transform;
            root.SetParent(world, false);
            var g = new[] { Group(root, "Trees"), Group(root, "Bushes"), Group(root, "Rocks"), Group(root, "Tall Grass & Flowers"), Group(root, "Logs & Stumps") };
            string[] treePrefabs = { "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05" };

            const float cell = 4.0f;
            for (float x = -24f; x <= 24f; x += cell)
                for (float z = -16f; z <= 45f; z += cell)
                {
                    var p = new Vector3(x + R(-1.3f, 1.3f), 0f, z + R(-1.3f, 1.3f));
                    float d = DistToPath(p);
                    if (d < 2.6f || InHouseArea(p, 1.2f) || InRiver(p, 0.7f) || InStation(p, 1.0f)) continue;
                    if (R() > (d < 14f ? 0.85f : 0.6f)) continue;
                    // realistic textured tree (Fantasy Forest, 3 LODs, own trunk capsule collider)
                    var t = Place(FF + "tree_1.prefab", g[0], p, R(0, 360), R(0.32f, 0.55f), true);
                    if (t == null) { t = Place(Nature + treePrefabs[_rng.Next(treePrefabs.Length)] + ".prefab", g[0], p, R(0, 360), R(0.85f, 1.35f), true); if (t) FixTreeCollider(t); }
                }

            for (float x = -22f; x <= 22f; x += 2.4f)
                for (float z = -14f; z <= 44f; z += 2.4f)
                {
                    var p = new Vector3(x + R(-1f, 1f), 0f, z + R(-1f, 1f));
                    float d = DistToPath(p);
                    if (d < 1.5f || d > 12f || InHouseArea(p, 0.3f) || InRiver(p, 0.4f) || InStation(p, 0.2f)) continue;
                    if (R() > 0.35f) continue;
                    float bs = R(0.7f, 1.4f);
                    PlaceHDBush(g[1], "Bush", p, R(0, 360), new Vector3(bs, bs * R(0.8f, 1.15f), bs), _rng.Next(4), true);
                }

            for (int k = 0; k < 70; k++)
            {
                var p = new Vector3(R(-22f, 22f), 0f, R(-14f, 44f));
                if (DistToPath(p) < 1.2f || InHouseArea(p, 0.5f) || InRiver(p, 0.3f) || InStation(p, 0f)) continue;
                float rs = R(0.25f, 0.9f);
                PlaceHDRock(g[2], "Rock", p + Vector3.down * 0.03f, R(0, 360), new Vector3(rs, rs * R(0.45f, 0.8f), rs * R(0.75f, 1.1f)), _rng.Next(5), true, R() < 0.6f);
            }

            // Mushrooms get their own group: the glow side quest (GlowMushrooms) lights the ones the child touches
            var shrooms = Group(root, "Mushrooms (touch me)");
            // a row of mushrooms right at the path edges, easy to touch
            for (float z = 0.6f; z < PathEndZ - 0.5f; z += R(0.45f, 1.1f))
            {
                float side = R() < 0.5f ? -1f : 1f;
                var p = OnPath(z, side * R(0.62f, 1.05f));
                if (InHouseArea(p, 0f) || InRiver(p, 0.35f) || InStation(p, 0.1f)) continue;
                int st = R() < 0.45f ? 0 : R() < 0.55f ? 1 : 2;
                PlaceHDShroom(shrooms, p, R(0, 360), R(0.18f, 0.42f), st, _rng.Next(2));
                if (R() < 0.45f) PlaceHDShroom(shrooms, p + new Vector3(R(-0.12f, 0.12f), 0f, R(-0.12f, 0.12f)), R(0, 360), R(0.1f, 0.2f), st, _rng.Next(2));
            }

            // Tall grass, flowers and mushrooms along the path edges: knee-high for the adult, towering over the
            // tiny child (eye ~0.35 m). No colliders on grass/flowers, so the child walks through them.
            for (int k = 0; k < 900; k++)
            {
                float z = R(-0.8f, PathEndZ + 4f);
                float side = R() < 0.5f ? -1f : 1f;
                float off = 0.65f + Mathf.Pow(R(), 1.6f) * 5.5f;
                var p = new Vector3(PathX(Mathf.Min(z, PathEndZ)) + side * off, 0f, z + R(-0.2f, 0.2f));
                if (InHouseArea(p, 0f) || InRiver(p, 0.25f) || DistToPath(p) < 0.6f || InStation(p, -0.3f)) continue;
                float roll = R();
                if (roll < 0.68f) Place(FF + "grass01.prefab", g[3], p, R(0, 360), R(0.5f, 0.95f), false);
                else if (roll < 0.80f) Place(Nature + "Flowers_01.prefab", g[3], p, R(0, 360), R(1.2f, 2.2f), false);
                else if (roll < 0.90f) Place(Nature + "Flowers_02.prefab", g[3], p, R(0, 360), R(1.2f, 2.0f), false);
                else
                {
                    // Mushrooms are SOLID (to the tiny child they are as big as trees): stem capsule collider
                    int st = R() < 0.4f ? 0 : R() < 0.5f ? 1 : 2;
                    PlaceHDShroom(shrooms, p, R(0, 360), R(0.15f, 0.42f), st, _rng.Next(2));
                }
            }

            for (int k = 0; k < 30; k++)
            {
                var p = new Vector3(R(-18f, 18f), 0f, R(-8f, 42f));
                if (DistToPath(p) < 1.8f || InHouseArea(p, 0.5f) || InRiver(p, 0.4f) || InStation(p, 0.3f)) continue;
                if (k % 3 == 0) Place(Nature + "Stump_01.prefab", g[4], p, R(0, 360), R(0.9f, 1.4f), true);
                else if (k % 3 == 1) Place(Nature + "Branch_01.prefab", g[4], p, R(0, 360), R(1f, 2f), false);
                else
                {
                    float L = R(2f, 4f), rad = R(0.16f, 0.24f);
                    var log = new GameObject("FallenLog"); log.transform.SetParent(g[4], false);
                    log.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, R(0, 360), 0f));
                    log.transform.localScale = new Vector3(L, rad / 0.12f, rad / 0.12f);
                    var lm = HDLog(_rng.Next(3));
                    log.AddComponent<MeshFilter>().sharedMesh = lm;
                    log.AddComponent<MeshRenderer>().sharedMaterials = LogMats();
                    var lc = log.AddComponent<CapsuleCollider>(); lc.direction = 0; lc.radius = 0.12f; lc.height = 1f + 0.24f / L; lc.center = new Vector3(0f, 0.12f, 0f);
                }
            }
            SetStatic(root.gameObject);
        }

        /// Solid stem for a decorative mushroom (cap stays walk-under for nobody: the capsule covers stem + cap height).
        public static void AddStemCollider(GameObject mushroom)
        {
            var mf = mushroom.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var b = mf.sharedMesh.bounds;
            var cap = mf.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = Mathf.Max(0.02f, Mathf.Min(b.size.x, b.size.z) * 0.3f);
            cap.height = b.size.y;
            cap.center = new Vector3(b.center.x, b.center.y, b.center.z);
        }

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform; g.SetParent(parent, false); return g;
        }

        /// The pack's tree BoxColliders wrap the whole canopy; use a trunk capsule instead.
        static void FixTreeCollider(GameObject tree)
        {
            foreach (var bc in tree.GetComponentsInChildren<BoxCollider>()) Object.DestroyImmediate(bc);
            var lod0 = tree.GetComponentInChildren<MeshFilter>();
            var host = lod0 != null ? lod0.gameObject : tree;
            var cap = host.AddComponent<CapsuleCollider>();
            cap.radius = 0.3f; cap.height = 3f; cap.center = new Vector3(0f, 1.5f, 0f);
        }

        // ---------------------------------------------------------------- bounds, ambience, light
        static void BuildBounds(Transform world)
        {
            var root = new GameObject("WorldBounds").transform; root.SetParent(world, false);
            void Wall(string n, Vector3 c, Vector3 s)
            {
                var w = new GameObject(n); w.transform.SetParent(root, false); w.transform.position = c;
                w.AddComponent<BoxCollider>().size = s;
            }
            Wall("Bound_W", new Vector3(-24.5f, 2f, 14f), new Vector3(1f, 4f, 64f));
            Wall("Bound_E", new Vector3(24.5f, 2f, 14f), new Vector3(1f, 4f, 64f));
            Wall("Bound_S", new Vector3(0f, 2f, -17.5f), new Vector3(50f, 4f, 1f));
            Wall("Bound_N", new Vector3(0f, 2f, 45.5f), new Vector3(50f, 4f, 1f));
        }

        public static AudioSource AddAudio(GameObject go, ProceduralAudio.Kind kind, ProceduralAudio.Category cat, float vol, int seed,
            float minD, float maxD, bool spatial, bool loop = true, bool playOnStart = true)
        {
            var src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();
            src.loop = loop; src.playOnAwake = false;
            src.spatialBlend = spatial ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minD; src.maxDistance = maxD;
            src.dopplerLevel = 0f;
            var pa = go.GetComponent<ProceduralAudio>();
            if (pa == null) pa = go.AddComponent<ProceduralAudio>();
            pa.kind = kind; pa.category = cat; pa.baseVolume = vol; pa.seed = seed; pa.playOnStart = playOnStart;
            return src;
        }

        static void BuildAmbience(Transform world)
        {
            var root = new GameObject("Ambience").transform; root.SetParent(world, false);
            var wind = new GameObject("Wind"); wind.transform.SetParent(root, false);
            AddAudio(wind, ProceduralAudio.Kind.Wind, ProceduralAudio.Category.Nature, 0.3f, 1, 1f, 500f, false);
            float[] zs = { 3f, 12f, 19f, 25f, 31f, 38f };
            for (int k = 0; k < zs.Length; k++)
            {
                var b = new GameObject("Birds_" + k); b.transform.SetParent(root, false);
                float side = k % 2 == 0 ? -1f : 1f;
                b.transform.position = new Vector3(PathX(Mathf.Min(zs[k], PathEndZ)) + side * R(3f, 6f), R(2.5f, 4.5f), zs[k]);
                AddAudio(b, ProceduralAudio.Kind.Birds, ProceduralAudio.Category.Nature, 0.7f, 100 + k, 1.5f, 16f, true);
            }
        }

        static void SetupLighting()
        {
            var sun = GameObject.Find("Sun");
            if (sun == null) sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                sun.name = "Sun";
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                var l = sun.GetComponent<Light>();
                l.intensity = 1.15f; l.color = new Color(1f, 0.96f, 0.89f);
                l.shadows = LightShadows.Soft; l.shadowStrength = 0.75f;
            }
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fantasy Forest Environment Free Sample/Materials/skyMaterial.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false; // fog is part of the ADULT's perception (AgeController)
        }

        static void PlacePlayer()
        {
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            if (rig == null) { Debug.LogError("[WorldBuilder] No OVRCameraRig in the scene."); return; }
            rig.transform.position = SpawnPos;
            rig.transform.rotation = Quaternion.identity; // facing +Z = down the path, back to the house
        }
    }
}
