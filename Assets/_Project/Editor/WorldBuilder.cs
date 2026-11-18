using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GrowingUp.EditorTools
{
    /// <summary>
    /// Builds the static world for "Growing Up" (Phase 1, step 1).
    /// Menu: Growing Up/Rebuild World. Everything it creates lives under the "World" root and is
    /// deterministic (fixed random seed), so it can be re-run after tweaking the numbers below.
    /// It never touches the player rig except to place it at the spawn point.
    /// </summary>
    public static class WorldBuilder
    {
        // ---------------- layout numbers (tweak and re-run) ----------------
        public const float PathEndZ = 90f;
        public const float PathWidth = 2.0f;
        public const float RiverZ = 46f;
        public const float RiverHalfWidth = 2.5f;
        public const float ClearingRadius = 9f;
        public static readonly Vector3 SpawnPos = new Vector3(0f, 0f, -0.7f);
        const int Seed = 20261118;

        const string MatDir = "Assets/_Project/Materials";
        const string MeshDir = "Assets/_Project/Meshes";
        const string Nature = "Assets/SimpleNaturePack/Prefabs/";
        const string FF = "Assets/Fantasy Forest Environment Free Sample/Meshes/Prefabs/";

        /// <summary>Path centre-line x for a given z (gentle meander, straight at the house).</summary>
        public static float PathX(float z)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(z / 10f));
            return k * (2.5f * Mathf.Sin(z * 0.07f) + 1.5f * (Mathf.Sin(z * 0.023f + 1f) - Mathf.Sin(1f)));
        }

        public static Vector3 ClearingCenter => new Vector3(PathX(PathEndZ), 0f, PathEndZ + 7f);

        static System.Random _rng;
        static float R() => (float)_rng.NextDouble();
        static float R(float a, float b) => a + (b - a) * R();

        [MenuItem("Growing Up/Rebuild World")]
        public static void Build()
        {
            _rng = new System.Random(Seed);
            System.IO.Directory.CreateDirectory(MatDir);
            System.IO.Directory.CreateDirectory(MeshDir);

            // Old prototype layout from the first commits (scattered around the origin) is replaced.
            foreach (var n in new[] { "World", "Trees1", "Trees2", "Propos" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            var world = new GameObject("World").transform;
            BuildGround(world);
            BuildPath(world);
            BuildRiver(world);
            HouseBuilder.Build(world);
            BuildClearing(world);
            BuildForest(world);
            BuildBounds(world);
            BuildAmbience(world);
            SetupLighting();
            PlaceInteractiveProps();
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

        static GameObject Place(string prefabPath, Transform parent, Vector3 pos, float yaw, float scale, bool keepColliders)
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
            m.vertices = src.vertices; m.triangles = src.triangles; m.uv = src.uv;
            m.RecalculateNormals(); m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        static List<Vector3> _pathPts;
        static float DistToPath(Vector3 p)
        {
            if (_pathPts == null)
            {
                _pathPts = new List<Vector3>();
                for (float z = -1.5f; z <= PathEndZ; z += 0.5f) _pathPts.Add(new Vector3(PathX(z), 0f, z));
            }
            float best = float.MaxValue;
            foreach (var q in _pathPts)
            {
                float d = (p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z);
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        static bool InHouseArea(Vector3 p, float margin) => p.x > -3.6f - margin && p.x < 3.6f + margin && p.z > -7.6f - margin && p.z < -0.8f + margin;
        static bool InClearing(Vector3 p, float extra) => Vector3.Distance(new Vector3(p.x, 0, p.z), ClearingCenter) < ClearingRadius + extra;
        static bool InRiver(Vector3 p, float margin) => Mathf.Abs(p.z - RiverZ) < RiverHalfWidth + margin;

        // ---------------------------------------------------------------- ground & path
        static void BuildGround(Transform world)
        {
            // Reuse the existing "Plane" (it carries the Teleport Interactable building block).
            var ground = GameObject.Find("Ground");
            if (ground == null) ground = GameObject.Find("Plane");
            if (ground == null)
            {
                Debug.LogError("[WorldBuilder] No 'Plane'/'Ground' with the Teleport Interactable found.");
                return;
            }
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, 0f, 48f);
            ground.transform.rotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(9f, 1f, 13.5f); // 90 m x 135 m
            ground.GetComponent<Renderer>().sharedMaterial = Mat("M_Ground", new Color(0.33f, 0.50f, 0.22f));
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);
        }

        static void BuildPath(Transform world)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            int i = 0;
            for (float z = -1.4f; z <= PathEndZ + 2f; z += 0.5f, i++)
            {
                float x = PathX(z);
                float dx = PathX(z + 0.25f) - PathX(z - 0.25f);
                var tangent = new Vector3(dx, 0f, 0.5f).normalized;
                var side = Vector3.Cross(Vector3.up, tangent);
                float w = PathWidth * 0.5f * (1f + 0.12f * Mathf.Sin(z * 0.9f) + 0.08f * Mathf.Sin(z * 2.3f));
                w *= Mathf.Lerp(0.1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((PathEndZ + 2f - z) / 5f))); // fade into the clearing
                var c = new Vector3(x, 0.012f, z);
                verts.Add(c - side * w); verts.Add(c + side * w);
                uvs.Add(new Vector2(0, z * 0.5f)); uvs.Add(new Vector2(1, z * 0.5f));
                if (i > 0)
                {
                    int b = i * 2;
                    tris.AddRange(new[] { b - 2, b, b - 1, b - 1, b, b + 1 });
                }
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

        // ---------------------------------------------------------------- river + log bridge
        static void BuildRiver(Transform world)
        {
            float bx = PathX(RiverZ);
            float yaw = Mathf.Atan2(PathX(RiverZ + 1f) - PathX(RiverZ - 1f), 2f) * Mathf.Rad2Deg;
            var river = new GameObject("River").transform;
            river.SetParent(world, false);
            river.position = new Vector3(bx, 0f, RiverZ);
            river.rotation = Quaternion.Euler(0f, yaw, 0f); // river runs perpendicular to the path at the crossing

            var water = Mat("M_Water", new Color(0.18f, 0.42f, 0.58f), 0.9f);
            var bank = Mat("M_RiverBank", new Color(0.36f, 0.30f, 0.22f));
            Prim(PrimitiveType.Cube, "Water", river, new Vector3(0f, 0.03f, 0f), new Vector3(110f, 0.02f, RiverHalfWidth * 2f), water, false, null, true);
            Prim(PrimitiveType.Cube, "Bank_N", river, new Vector3(0f, 0.018f, RiverHalfWidth + 0.35f), new Vector3(110f, 0.02f, 0.8f), bank, false, null, true);
            Prim(PrimitiveType.Cube, "Bank_S", river, new Vector3(0f, 0.018f, -RiverHalfWidth - 0.35f), new Vector3(110f, 0.02f, 0.8f), bank, false, null, true);

            // Invisible blockers so you cannot wade through the water (gap = bridge).
            const float gap = 0.8f; float bl = 55f;
            foreach (var s in new[] { -1f, 1f })
            {
                var b = new GameObject(s < 0 ? "RiverBlocker_W" : "RiverBlocker_E");
                b.transform.SetParent(river, false);
                b.transform.localPosition = new Vector3(s * (gap + bl * 0.5f), 0.75f, 0f);
                var bc = b.AddComponent<BoxCollider>();
                bc.size = new Vector3(bl, 1.5f, RiverHalfWidth * 2f + 0.6f);
            }

            // Log bridge: plank deck + log hand-rails on posts.
            var bridge = new GameObject("LogBridge").transform;
            bridge.SetParent(river, false);
            var plank = Mat("M_Plank", new Color(0.42f, 0.31f, 0.20f));
            var log = Mat("M_Log", new Color(0.33f, 0.24f, 0.16f));
            float len = RiverHalfWidth * 2f + 2.6f;
            int n = Mathf.CeilToInt(len / 0.5f);
            for (int k = 0; k < n; k++)
            {
                float z = -len * 0.5f + 0.25f + k * 0.5f;
                Prim(PrimitiveType.Cube, "Plank_" + k, bridge, new Vector3(R(-0.03f, 0.03f), 0.07f, z), new Vector3(1.5f, 0.08f, 0.46f), plank, true,
                    Quaternion.Euler(0f, R(-3f, 3f), 0f), true);
            }
            foreach (var s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cylinder, "Rail", bridge, new Vector3(s * 0.8f, 0.75f, 0f), new Vector3(0.14f, len * 0.5f, 0.14f), log, true,
                    Quaternion.Euler(90f, 0f, 0f), true);
                for (int k = 0; k < 4; k++)
                    Prim(PrimitiveType.Cylinder, "Post", bridge, new Vector3(s * 0.8f, 0.4f, -len * 0.5f + 0.3f + k * (len - 0.6f) / 3f),
                        new Vector3(0.12f, 0.4f, 0.12f), log, true, null, true);
            }

            // Rocks along both banks (not on the bridge)
            for (float x = -50f; x < 50f; x += R(1.5f, 3.5f))
            {
                if (Mathf.Abs(x) < 1.6f) continue;
                foreach (var s in new[] { -1f, 1f })
                {
                    if (R() < 0.35f) continue;
                    var lp = new Vector3(x, 0f, s * (RiverHalfWidth + R(0.1f, 0.6f)));
                    var wp = river.TransformPoint(lp);
                    if (Mathf.Abs(wp.x) > 44f) continue;
                    Place(Nature + (R() < 0.5f ? "Rock_01.prefab" : "Rock_04.prefab"), river, wp, R(0, 360), R(0.6f, 1.2f), true);
                }
            }

            // River sound sources (world)
            for (int k = -1; k <= 1; k++)
            {
                var a = new GameObject("RiverSound_" + (k + 1));
                a.transform.SetParent(river, false);
                a.transform.localPosition = new Vector3(k * 18f, 0.3f, 0f);
                AddAudio(a, ProceduralAudio.Kind.River, ProceduralAudio.Category.Nature, 0.8f, 10 + k, 3f, 28f, true);
            }
            SetStatic(river.gameObject);
            foreach (var s in river.GetComponentsInChildren<AudioSource>()) GameObjectUtility.SetStaticEditorFlags(s.gameObject, 0);
        }

        // ---------------------------------------------------------------- clearing
        static void BuildClearing(Transform world)
        {
            var c = ClearingCenter;
            var root = new GameObject("Clearing").transform;
            root.SetParent(world, false);
            root.position = c;
            Prim(PrimitiveType.Cylinder, "ClearingGrass", root, new Vector3(c.x, 0.008f, c.z), new Vector3(ClearingRadius * 2f, 0.004f, ClearingRadius * 2f),
                Mat("M_ClearingGrass", new Color(0.45f, 0.58f, 0.27f)), false);

            // The big old tree at the far side of the clearing (Fantasy Forest tree_1, ~16 m tall).
            var tree = Place(FF + "tree_1.prefab", root, c + new Vector3(0.8f, 0f, 6.5f), 200f, 0.8f, true);
            if (tree) tree.name = "OldTree";

            // Fairy ring of mushrooms in front of the tree + flowers
            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(a) * 2.4f, 0f, Mathf.Sin(a) * 2.4f);
                Place(Nature + (k % 3 == 0 ? "Mushroom_02.prefab" : "Mushroom_01.prefab"), root, p, R(0, 360), R(0.8f, 1.3f), false);
            }
            for (int k = 0; k < 40; k++)
            {
                float a = R(0, Mathf.PI * 2f), d = R(3.5f, ClearingRadius - 0.5f);
                var p = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (DistToPath(p) < 1.3f) continue;
                Place(Nature + (R() < 0.5f ? "Flowers_01.prefab" : "Flowers_02.prefab"), root, p, R(0, 360), R(0.8f, 1.4f), false);
            }
            // Ring of stones marking the clearing
            for (int k = 0; k < 14; k++)
            {
                float a = k / 14f * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(a) * ClearingRadius, 0f, Mathf.Sin(a) * ClearingRadius);
                if (DistToPath(p) < 1.8f) continue;
                Place(Nature + "Rock_04.prefab", root, p, R(0, 360), R(0.5f, 0.9f), true);
            }
            var marker = new GameObject("GrowUpSpot");
            marker.transform.SetParent(root, false);
            marker.transform.position = c;
            marker.AddComponent<GrowUpTrigger>().radius = 3.5f; // step 4: walking in here makes the child grow up
            SetStatic(root.gameObject);
            GameObjectUtility.SetStaticEditorFlags(marker, 0);
        }

        // ---------------------------------------------------------------- forest scatter
        static void BuildForest(Transform world)
        {
            var root = new GameObject("Forest").transform;
            root.SetParent(world, false);
            var trees = new Transform[] { Group(root, "Trees"), Group(root, "Bushes"), Group(root, "Rocks"), Group(root, "Ground Cover"), Group(root, "Logs & Stumps") };
            string[] treePrefabs = { "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05" };

            // Trees on a jittered grid
            const float cell = 4.2f;
            for (float x = -42f; x <= 42f; x += cell)
                for (float z = -18f; z <= 113f; z += cell)
                {
                    var p = new Vector3(x + R(-1.6f, 1.6f), 0f, z + R(-1.6f, 1.6f));
                    float d = DistToPath(p);
                    if (d < 3.8f || InHouseArea(p, 1.5f) || InClearing(p, 0.5f) || InRiver(p, 1.2f)) continue;
                    if (Vector3.Distance(p, ClearingCenter + new Vector3(0.8f, 0, 6.5f)) < 7f) continue; // room for the old tree
                    if (R() > (d < 25f ? 0.85f : 0.55f)) continue;
                    var t = Place(Nature + treePrefabs[_rng.Next(treePrefabs.Length)] + ".prefab", trees[0], p, R(0, 360), R(0.85f, 1.35f), true);
                    if (t) FixTreeCollider(t);
                }

            // Bushes
            for (float x = -40f; x <= 40f; x += 3f)
                for (float z = -16f; z <= 112f; z += 3f)
                {
                    var p = new Vector3(x + R(-1.2f, 1.2f), 0f, z + R(-1.2f, 1.2f));
                    float d = DistToPath(p);
                    if (d < 2.3f || d > 18f || InHouseArea(p, 0.3f) || InClearing(p, -1f) || InRiver(p, 0.8f)) continue;
                    if (R() > 0.35f) continue;
                    Place(Nature + "Bush_0" + (1 + _rng.Next(3)) + ".prefab", trees[1], p, R(0, 360), R(0.8f, 1.6f), true);
                }

            // Rocks
            for (int k = 0; k < 90; k++)
            {
                var p = new Vector3(R(-40f, 40f), 0f, R(-15f, 112f));
                if (DistToPath(p) < 2.4f || InHouseArea(p, 0.5f) || InClearing(p, -0.5f) || InRiver(p, 0.5f)) continue;
                string r = R() < 0.15f ? "Rock_05" : R() < 0.5f ? "Rock_04" : R() < 0.5f ? "Rock_01" : "Rock_03";
                Place(Nature + r + ".prefab", trees[2], p, R(0, 360), R(0.6f, 1.3f), true);
            }

            // Ground cover: grass, flowers and mushrooms concentrated along the path edges (no colliders)
            for (int k = 0; k < 900; k++)
            {
                float z = R(-1f, PathEndZ + 2f);
                float side = R() < 0.5f ? -1f : 1f;
                float off = 1.15f + Mathf.Pow(R(), 2f) * 7f;
                var p = new Vector3(PathX(z) + side * off, 0f, z + R(-0.4f, 0.4f));
                if (InHouseArea(p, 0f) || InRiver(p, 0.6f) || DistToPath(p) < 1.1f) continue;
                float roll = R();
                string pf = roll < 0.35f ? Nature + "Grass_01.prefab" : roll < 0.55f ? Nature + "Grass_02.prefab" : roll < 0.68f ? FF + "grass01.prefab"
                          : roll < 0.84f ? Nature + "Flowers_01.prefab" : roll < 0.95f ? Nature + "Flowers_02.prefab" : Nature + "Mushroom_0" + (1 + _rng.Next(2)) + ".prefab";
                Place(pf, trees[3], p, R(0, 360), R(0.8f, 1.5f), false);
            }

            // Stumps, branches and fallen logs off the path
            for (int k = 0; k < 40; k++)
            {
                var p = new Vector3(R(-30f, 30f), 0f, R(-10f, 108f));
                if (DistToPath(p) < 2.6f || InHouseArea(p, 0.5f) || InClearing(p, 0f) || InRiver(p, 0.6f)) continue;
                if (k % 3 == 0) Place(Nature + "Stump_01.prefab", trees[4], p, R(0, 360), R(0.9f, 1.4f), true);
                else if (k % 3 == 1) Place(Nature + "Branch_01.prefab", trees[4], p, R(0, 360), R(1f, 2f), false);
                else
                {
                    float L = R(2.5f, 4.5f);
                    var lg = Prim(PrimitiveType.Cylinder, "FallenLog", trees[4], p + Vector3.up * 0.22f, new Vector3(0.45f, L * 0.5f, 0.45f),
                        Mat("M_Log", new Color(0.33f, 0.24f, 0.16f)), true, Quaternion.Euler(90f, R(0, 360), R(-4f, 4f)));
                    lg.GetComponent<Collider>().enabled = true;
                }
            }
            SetStatic(root.gameObject);
        }

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform; g.SetParent(parent, false); return g;
        }

        /// The pack's tree BoxColliders wrap the whole canopy; replace with a trunk capsule so you can
        /// walk under branches but not through trunks.
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
            Wall("Bound_W", new Vector3(-44f, 2f, 48f), new Vector3(1f, 4f, 135f));
            Wall("Bound_E", new Vector3(44f, 2f, 48f), new Vector3(1f, 4f, 135f));
            Wall("Bound_S", new Vector3(0f, 2f, -19f), new Vector3(90f, 4f, 1f));
            Wall("Bound_N", new Vector3(0f, 2f, 115f), new Vector3(90f, 4f, 1f));
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
            AddAudio(wind, ProceduralAudio.Kind.Wind, ProceduralAudio.Category.Nature, 0.35f, 1, 1f, 500f, false);
            float[] zs = { 6f, 20f, 33f, 56f, 70f, 84f, 100f };
            for (int k = 0; k < zs.Length; k++)
            {
                var b = new GameObject("Birds_" + k); b.transform.SetParent(root, false);
                float side = k % 2 == 0 ? -1f : 1f;
                b.transform.position = new Vector3(PathX(Mathf.Min(zs[k], PathEndZ)) + side * R(5f, 9f), R(3.5f, 6f), zs[k]);
                AddAudio(b, ProceduralAudio.Kind.Birds, ProceduralAudio.Category.Nature, 0.7f, 100 + k, 2f, 26f, true);
            }
        }

        static void SetupLighting()
        {
            var sun = GameObject.Find("Directional Light");
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
            RenderSettings.fog = false; // fog is part of the ADULT's perception, set at runtime by the AgeController
        }

        static void PlaceInteractiveProps()
        {
            // The grabbable mushroom + rock (Grab Interaction building blocks) sit on a stump next to the door.
            var stump = GameObject.Find("Stump_01");
            if (stump != null && stump.transform.parent == null) { stump.transform.position = new Vector3(1.8f, 0f, 0.9f); stump.transform.rotation = Quaternion.Euler(0, 30, 0); }
            var mush = GameObject.Find("Mushroom_01");
            if (mush != null && mush.transform.parent == null) mush.transform.position = new Vector3(1.75f, 0.86f, 0.95f);
            var rock = GameObject.Find("Rock_02");
            if (rock != null && rock.transform.parent == null) rock.transform.position = new Vector3(1.92f, 0.86f, 0.82f);
        }

        static void PlacePlayer()
        {
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig == null) { Debug.LogError("[WorldBuilder] No OVRCameraRig in the scene."); return; }
            rig.transform.position = SpawnPos;
            rig.transform.rotation = Quaternion.identity; // facing +Z = down the path, back to the door
        }
    }
}
