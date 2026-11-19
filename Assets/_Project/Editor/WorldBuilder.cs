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
    /// Path order (z, metres from the house door): River -> Bird -> Dandelion -> Fairy ring -> Berry bush.
    /// Phase-2 interactions are added on top by InteractionBuilder (menu Growing Up/Build Interactions).
    /// </summary>
    public static class WorldBuilder
    {
        // ---------------- layout numbers (tweak and re-run) ----------------
        public const float RiverZ = 8f;
        public const float RiverHalfWidth = 0.92f;
        public const float BirdZ = 15f;
        public const float DandelionZ = 21f;
        public const float FairyRingZ = 27f;
        public const float BerryZ = 32f;
        public const float PathEndZ = 36f;
        public const float PathWidth = 1.1f;
        public static readonly Vector3 SpawnPos = new Vector3(0f, 0f, -0.25f); // just off the porch step
        // stepping stones: offsets across the river from RiverZ; index 2 is the GAP (filled by the child's rock)
        public static readonly float[] StoneOffsets = { -0.75f, -0.43f, -0.07f, 0.29f, 0.61f };
        public const int GapIndex = 2;
        public const float StoneScale = 0.23f;
        const int Seed = 20261119;

        const string MatDir = "Assets/_Project/Materials";
        const string MeshDir = "Assets/_Project/Meshes";
        public const string Nature = "Assets/SimpleNaturePack/Prefabs/";
        public const string FF = "Assets/Fantasy Forest Environment Free Sample/Meshes/Prefabs/";

        /// Path centre-line x for a given z (gentle meander, straight at the house and at the river crossing).
        public static float PathX(float z)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(z / 4f));
            float river = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(z - RiverZ) - 1.4f) / 2f));
            float x = 0.9f * Mathf.Sin(z * 0.19f) + 0.6f * (Mathf.Sin(z * 0.08f + 1f) - Mathf.Sin(1f));
            float xr = 0.9f * Mathf.Sin(RiverZ * 0.19f) + 0.6f * (Mathf.Sin(RiverZ * 0.08f + 1f) - Mathf.Sin(1f));
            return k * Mathf.Lerp(xr, x, river);
        }

        public static Vector3 OnPath(float z, float side = 0f) => new Vector3(PathX(z) + side, 0f, z);
        public static Vector3 FairyRingCenter => OnPath(FairyRingZ, 1.35f);
        public static Vector3 BerryBushPos => OnPath(BerryZ, -1.0f);
        public static Vector3 EndClearing => OnPath(PathEndZ + 2.5f);

        static System.Random _rng;
        static float R() => (float)_rng.NextDouble();
        static float R(float a, float b) => a + (b - a) * R();

        [MenuItem("Growing Up/Rebuild World")]
        public static void Build()
        {
            _rng = new System.Random(Seed);
            _pathPts = null;
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
        /// Keeps the interaction spots clear of forest scatter.
        static bool InStation(Vector3 p, float extra)
        {
            return Flat(p, OnPath(BirdZ, 0.75f)) < 1.0f + extra
                || Flat(p, OnPath(DandelionZ, 0.6f)) < 0.8f + extra
                || Flat(p, FairyRingCenter) < 1.5f + extra
                || Flat(p, BerryBushPos) < 1.2f + extra
                || Flat(p, EndClearing) < 4.0f + extra;
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
            ground.GetComponent<Renderer>().sharedMaterial = Mat("M_Ground", new Color(0.33f, 0.50f, 0.22f));
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);
        }

        static void BuildPath(Transform world)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            int i = 0;
            for (float z = -1.45f; z <= PathEndZ + 2.5f; z += 0.25f, i++)
            {
                float x = PathX(z);
                float dx = PathX(z + 0.125f) - PathX(z - 0.125f);
                var tangent = new Vector3(dx, 0f, 0.25f).normalized;
                var side = Vector3.Cross(Vector3.up, tangent);
                float w = PathWidth * 0.5f * (1f + 0.12f * Mathf.Sin(z * 1.7f) + 0.08f * Mathf.Sin(z * 4.3f));
                w *= Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((PathEndZ + 2.5f - z) / 3f)));
                var c = new Vector3(x, 0.006f, z);
                verts.Add(c - side * w); verts.Add(c + side * w);
                uvs.Add(new Vector2(0, z)); uvs.Add(new Vector2(1, z));
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
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat("M_Path", new Color(0.47f, 0.37f, 0.25f));
            SetStatic(go);
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

            // Stepping stones: one gap (index GapIndex) is left empty for the child's rock
            var stones = new GameObject("SteppingStones").transform;
            stones.SetParent(river, false);
            var foamMat = ShaderMat("M_FoamRing", "GrowingUp/FoamRing");
            for (int k = 0; k < StoneOffsets.Length; k++)
            {
                float z = RiverZ + StoneOffsets[k];
                var pos = new Vector3(PathX(z) + R(-0.03f, 0.03f), 0f, z);
                if (k == GapIndex)
                {
                    var slot = new GameObject("RockSlot_Gap");
                    slot.transform.SetParent(stones, false);
                    slot.transform.position = pos;
                    continue;
                }
                var s = Place(Nature + "Rock_01.prefab", stones, pos, R(0, 360), StoneScale * R(0.95f, 1.08f), true);
                if (s) s.name = "SteppingStone_" + k;
                var foam = Prim(PrimitiveType.Quad, "FoamRing_" + k, stones, pos + Vector3.up * 0.038f, Vector3.one * 0.42f, foamMat, false, Quaternion.Euler(90f, R(0, 360), 0f));
                foam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Rocks along both banks (not on the crossing)
            for (float x = -25f; x < 25f; x += R(0.6f, 1.6f))
            {
                if (Mathf.Abs(x - PathX(RiverZ)) < 0.9f) continue;
                foreach (var side in new[] { -1f, 1f })
                {
                    if (R() < 0.4f) continue;
                    var p = new Vector3(x, 0f, RiverZ + side * (RiverHalfWidth + R(0.0f, 0.25f)));
                    Place(Nature + (R() < 0.6f ? "Rock_01.prefab" : "Rock_04.prefab"), river, p, R(0, 360), R(0.15f, 0.45f), true);
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
            Station("Station_River", OnPath(RiverZ - 1.6f), OnPath(RiverZ));
            Station("Station_Bird", OnPath(BirdZ - 1.4f), OnPath(BirdZ, 0.75f));
            Station("Station_Dandelion", OnPath(DandelionZ - 1.2f), OnPath(DandelionZ, 0.6f));
            Station("Station_FairyRing", OnPath(FairyRingZ - 1.4f, 0.4f), FairyRingCenter);
            Station("Station_Berries", OnPath(BerryZ - 1.2f), BerryBushPos);
            Station("Station_AdultReturn", OnPath(BerryZ + 0.5f), OnPath(BerryZ - 3f));

            // Static environment at the stations (interactive parts come in Phase 2)
            var env = new GameObject("StationEnvironment").transform; env.SetParent(root, false);
            var clearing = Mat("M_ClearingGrass", new Color(0.45f, 0.58f, 0.27f));
            Prim(PrimitiveType.Cylinder, "FairyRing_Clearing", env, FairyRingCenter + Vector3.up * 0.004f, new Vector3(2.8f, 0.002f, 2.8f), clearing, false);
            Prim(PrimitiveType.Cylinder, "BirdSpot_Dirt", env, OnPath(BirdZ, 0.75f) + Vector3.up * 0.004f, new Vector3(1.2f, 0.002f, 1.2f), Mat("M_Path", new Color(0.47f, 0.37f, 0.25f)), false);
            Prim(PrimitiveType.Cylinder, "End_Clearing", env, EndClearing + Vector3.up * 0.003f, new Vector3(8f, 0.002f, 8f), clearing, false);
            var bush = Place(Nature + "Bush_01.prefab", env, BerryBushPos, 30f, 0.75f, true);
            if (bush) bush.name = "BerryBush";
            var tree = Place(FF + "tree_1.prefab", env, EndClearing + new Vector3(0.5f, 0f, 3f), 200f, 0.55f, true);
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

            const float cell = 3.4f;
            for (float x = -24f; x <= 24f; x += cell)
                for (float z = -16f; z <= 45f; z += cell)
                {
                    var p = new Vector3(x + R(-1.3f, 1.3f), 0f, z + R(-1.3f, 1.3f));
                    float d = DistToPath(p);
                    if (d < 2.6f || InHouseArea(p, 1.2f) || InRiver(p, 0.7f) || InStation(p, 1.0f)) continue;
                    if (R() > (d < 14f ? 0.85f : 0.6f)) continue;
                    var t = Place(Nature + treePrefabs[_rng.Next(treePrefabs.Length)] + ".prefab", g[0], p, R(0, 360), R(0.85f, 1.35f), true);
                    if (t) FixTreeCollider(t);
                }

            for (float x = -22f; x <= 22f; x += 2.4f)
                for (float z = -14f; z <= 44f; z += 2.4f)
                {
                    var p = new Vector3(x + R(-1f, 1f), 0f, z + R(-1f, 1f));
                    float d = DistToPath(p);
                    if (d < 1.5f || d > 12f || InHouseArea(p, 0.3f) || InRiver(p, 0.4f) || InStation(p, 0.2f)) continue;
                    if (R() > 0.35f) continue;
                    Place(Nature + "Bush_0" + (1 + _rng.Next(3)) + ".prefab", g[1], p, R(0, 360), R(0.7f, 1.5f), true);
                }

            for (int k = 0; k < 70; k++)
            {
                var p = new Vector3(R(-22f, 22f), 0f, R(-14f, 44f));
                if (DistToPath(p) < 1.2f || InHouseArea(p, 0.5f) || InRiver(p, 0.3f) || InStation(p, 0f)) continue;
                string r = R() < 0.12f ? "Rock_05" : R() < 0.5f ? "Rock_04" : R() < 0.5f ? "Rock_01" : "Rock_03";
                Place(Nature + r + ".prefab", g[2], p, R(0, 360), R(0.4f, 1.1f), true);
            }

            // Tall grass, flowers and mushrooms along the path edges: knee-high for the adult, towering over the
            // tiny child (eye ~0.35 m). No colliders, so the child walks through it.
            for (int k = 0; k < 900; k++)
            {
                float z = R(-0.8f, PathEndZ + 4f);
                float side = R() < 0.5f ? -1f : 1f;
                float off = 0.65f + Mathf.Pow(R(), 1.6f) * 5.5f;
                var p = new Vector3(PathX(Mathf.Min(z, PathEndZ)) + side * off, 0f, z + R(-0.2f, 0.2f));
                if (InHouseArea(p, 0f) || InRiver(p, 0.25f) || DistToPath(p) < 0.6f || InStation(p, -0.3f)) continue;
                float roll = R();
                if (roll < 0.34f) Place(Nature + "Grass_01.prefab", g[3], p, R(0, 360), R(1.4f, 2.8f), false);
                else if (roll < 0.52f) Place(Nature + "Grass_02.prefab", g[3], p, R(0, 360), R(1.4f, 2.6f), false);
                else if (roll < 0.72f) Place(FF + "grass01.prefab", g[3], p, R(0, 360), R(0.45f, 0.85f), false);
                else if (roll < 0.83f) Place(Nature + "Flowers_01.prefab", g[3], p, R(0, 360), R(1.2f, 2.2f), false);
                else if (roll < 0.92f) Place(Nature + "Flowers_02.prefab", g[3], p, R(0, 360), R(1.2f, 2.0f), false);
                else Place(Nature + "Mushroom_0" + (1 + _rng.Next(2)) + ".prefab", g[3], p, R(0, 360), R(1.0f, 2.4f), false);
            }

            for (int k = 0; k < 30; k++)
            {
                var p = new Vector3(R(-18f, 18f), 0f, R(-8f, 42f));
                if (DistToPath(p) < 1.8f || InHouseArea(p, 0.5f) || InRiver(p, 0.4f) || InStation(p, 0.3f)) continue;
                if (k % 3 == 0) Place(Nature + "Stump_01.prefab", g[4], p, R(0, 360), R(0.9f, 1.4f), true);
                else if (k % 3 == 1) Place(Nature + "Branch_01.prefab", g[4], p, R(0, 360), R(1f, 2f), false);
                else
                {
                    float L = R(2f, 4f);
                    Prim(PrimitiveType.Cylinder, "FallenLog", g[4], p + Vector3.up * 0.2f, new Vector3(0.4f, L * 0.5f, 0.4f),
                        Mat("M_Log", new Color(0.33f, 0.24f, 0.16f)), true, Quaternion.Euler(90f, R(0, 360), R(-4f, 4f)));
                }
            }
            SetStatic(root.gameObject);
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
