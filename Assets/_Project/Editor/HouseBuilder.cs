using UnityEditor;
using UnityEngine;

namespace GrowingUp.EditorTools
{
    /// <summary>
    /// Builds the old, weathered cottage from primitives (no house model in the imported packs).
    /// It looks the same for both ages: damp/mould stains, cracked grey plaster, mossy roof.
    /// Front door faces +Z at z = -1.6, opening x = [-0.55, 0.55], height 2.1 m.
    /// </summary>
    public static class HouseBuilder
    {
        const float W = 6f, H = 2.6f, T = 0.2f;
        const float Front = -1.6f, Back = -7f;
        const float DoorHalf = 0.55f, DoorH = 2.1f;

        static System.Random _rng;
        static float R(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

        public static void Build(Transform world)
        {
            _rng = new System.Random(77);
            var M = new
            {
                wall = WorldBuilder.Mat("M_House_Plaster_Weathered", new Color(0.56f, 0.53f, 0.46f)),
                timber = WorldBuilder.Mat("M_House_Timber_Old", new Color(0.30f, 0.23f, 0.17f)),
                roof = WorldBuilder.Mat("M_House_Roof_Mossy", new Color(0.29f, 0.27f, 0.23f)),
                moss = WorldBuilder.Mat("M_House_Moss", new Color(0.24f, 0.32f, 0.16f)),
                mould = WorldBuilder.Mat("M_House_Mould", new Color(0.17f, 0.21f, 0.15f)),
                damp = WorldBuilder.Mat("M_House_Damp", new Color(0.36f, 0.36f, 0.30f)),
                crack = WorldBuilder.Mat("M_House_Crack", new Color(0.12f, 0.11f, 0.10f)),
                glass = WorldBuilder.Mat("M_House_Glass_Dirty", new Color(0.16f, 0.20f, 0.20f), 0.7f),
                door = WorldBuilder.Mat("M_House_Door", new Color(0.36f, 0.26f, 0.17f)),
                iron = WorldBuilder.Mat("M_House_Iron_Rusty", new Color(0.25f, 0.18f, 0.13f), 0.3f),
                floor = WorldBuilder.Mat("M_House_Floor", new Color(0.22f, 0.17f, 0.12f)),
                stone = WorldBuilder.Mat("M_House_Stone", new Color(0.42f, 0.41f, 0.38f)),
            };

            var house = new GameObject("House").transform;
            house.SetParent(world, false);
            float cz = (Front + Back) * 0.5f, D = Front - Back;

            // --- shell
            var shell = new GameObject("Shell").transform; shell.SetParent(house, false);
            Box("Foundation", shell, new Vector3(0, 0.1f, cz), new Vector3(W + 0.2f, 0.2f, D + 0.2f), M.stone, false);
            Box("Floor", shell, new Vector3(0, 0.21f, cz), new Vector3(W - 0.3f, 0.02f, D - 0.3f), M.floor, true);
            float lw = (W * 0.5f - DoorHalf);
            Box("Wall_Front_L", shell, new Vector3(-(DoorHalf + lw * 0.5f), H * 0.5f, Front - T * 0.5f), new Vector3(lw, H, T), M.wall, true);
            Box("Wall_Front_R", shell, new Vector3(DoorHalf + lw * 0.5f, H * 0.5f, Front - T * 0.5f), new Vector3(lw, H, T), M.wall, true);
            Box("Wall_Front_Lintel", shell, new Vector3(0, (DoorH + H) * 0.5f, Front - T * 0.5f), new Vector3(DoorHalf * 2f, H - DoorH, T), M.wall, true);
            Box("Wall_Back", shell, new Vector3(0, H * 0.5f, Back + T * 0.5f), new Vector3(W, H, T), M.wall, true);
            Box("Wall_West", shell, new Vector3(-W * 0.5f + T * 0.5f, H * 0.5f, cz), new Vector3(T, H, D - 2f * T), M.wall, true);
            Box("Wall_East", shell, new Vector3(W * 0.5f - T * 0.5f, H * 0.5f, cz), new Vector3(T, H, D - 2f * T), M.wall, true);
            Box("Porch_Step", shell, new Vector3(0, 0.07f, Front + 0.4f), new Vector3(1.6f, 0.14f, 0.8f), M.stone, true);

            // Timber frame corners + door frame
            foreach (var x in new[] { -W * 0.5f, W * 0.5f })
                foreach (var z in new[] { Front, Back })
                    Box("Corner_Post", shell, new Vector3(x, H * 0.5f, z), new Vector3(0.24f, H, 0.24f), M.timber, false);
            Box("DoorFrame_L", shell, new Vector3(-DoorHalf - 0.06f, DoorH * 0.5f, Front + 0.01f), new Vector3(0.12f, DoorH, 0.06f), M.timber, false);
            Box("DoorFrame_R", shell, new Vector3(DoorHalf + 0.06f, DoorH * 0.5f, Front + 0.01f), new Vector3(0.12f, DoorH, 0.06f), M.timber, false);
            Box("DoorFrame_Top", shell, new Vector3(0, DoorH + 0.06f, Front + 0.01f), new Vector3(DoorHalf * 2f + 0.24f, 0.12f, 0.06f), M.timber, false);

            // --- roof (gable, ridge along X)
            var roof = new GameObject("Roof").transform; roof.SetParent(house, false);
            float rise = 1.6f, run = D * 0.5f, over = 0.45f;
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float slabLen = (run + over) / Mathf.Cos(ang * Mathf.Deg2Rad);
            float ridgeY = H + rise;
            foreach (var s in new[] { 1f, -1f })
            {
                var pivot = new GameObject(s > 0 ? "RoofSide_Front" : "RoofSide_Back").transform;
                pivot.SetParent(roof, false);
                float hz = (run + over) * 0.5f;
                pivot.localPosition = new Vector3(0, ridgeY - hz * Mathf.Tan(ang * Mathf.Deg2Rad) + 0.08f, cz + s * hz);
                pivot.localRotation = Quaternion.Euler(s * ang, 0, 0);
                var slab = Box("Slab", pivot, Vector3.zero, new Vector3(W + 0.7f, 0.14f, slabLen), M.roof, true, true);
                // moss patches and a few missing/darker shingles
                for (int k = 0; k < 14; k++)
                    Box("Moss", pivot, new Vector3(R(-3.1f, 3.1f), 0.075f, R(-slabLen * 0.45f, slabLen * 0.45f)),
                        new Vector3(R(0.4f, 1.4f), 0.02f, R(0.3f, 0.9f)), k % 4 == 0 ? M.mould : M.moss, false, true);
            }
            Box("Ridge_Beam", roof, new Vector3(0, ridgeY + 0.12f, cz), new Vector3(W + 0.8f, 0.16f, 0.22f), M.timber, false);
            var gable = GableMesh(run, rise, T);
            foreach (var x in new[] { -W * 0.5f + T * 0.5f, W * 0.5f - T * 0.5f })
            {
                var g = new GameObject("Gable");
                g.transform.SetParent(roof, false);
                g.transform.localPosition = new Vector3(x, H, cz);
                g.AddComponent<MeshFilter>().sharedMesh = gable;
                g.AddComponent<MeshRenderer>().sharedMaterial = M.wall;
            }
            var chimney = Box("Chimney", roof, new Vector3(1.7f, ridgeY - 0.1f, cz - 1.0f), new Vector3(0.6f, 1.8f, 0.6f), M.stone, true);
            chimney.transform.localRotation = Quaternion.Euler(0, 0, 2.5f); // slightly leaning
            Box("Chimney_Cap", roof, new Vector3(1.74f, ridgeY + 0.85f, cz - 1.0f), new Vector3(0.75f, 0.1f, 0.75f), M.stone, false);

            // --- windows
            var win = new GameObject("Windows").transform; win.SetParent(house, false);
            Window(win, new Vector3(-1.9f, 1.45f, Front + 0.005f), Quaternion.identity, M.glass, M.timber, true);
            Window(win, new Vector3(1.9f, 1.45f, Front + 0.005f), Quaternion.identity, M.glass, M.timber, false);
            Window(win, new Vector3(0f, 1.45f, Back - 0.005f), Quaternion.Euler(0, 180, 0), M.glass, M.timber, false);
            Window(win, new Vector3(-W * 0.5f - 0.005f, 1.45f, cz), Quaternion.Euler(0, -90, 0), M.glass, M.timber, false);
            Window(win, new Vector3(W * 0.5f + 0.005f, 1.45f, cz), Quaternion.Euler(0, 90, 0), M.glass, M.timber, true);

            // --- weathering: rising damp band, mould blotches, cracks (outside faces)
            var wx = new GameObject("Weathering").transform; wx.SetParent(house, false);
            void Face(Vector3 origin, Vector3 right, Vector3 normal, float width, bool skipDoor)
            {
                var rot = Quaternion.LookRotation(normal);
                // damp band along the bottom
                for (float u = -width * 0.5f; u < width * 0.5f - 0.1f; u += 0.5f)
                {
                    if (skipDoor && Mathf.Abs(u + 0.25f) < DoorHalf + 0.2f) continue;
                    float h = R(0.35f, 0.75f);
                    Box("Damp", wx, origin + right * (u + 0.25f) + Vector3.up * (h * 0.5f + 0.18f) + normal * 0.012f, new Vector3(0.52f, h, 0.01f), M.damp, false, false, rot);
                }
                for (int k = 0; k < 12; k++)
                {
                    float u = R(-width * 0.45f, width * 0.45f);
                    if (skipDoor && Mathf.Abs(u) < DoorHalf + 0.25f) continue;
                    float y = k < 8 ? R(0.2f, 1.1f) : R(1.9f, H - 0.15f); // mould low + under the eaves
                    float s = R(0.15f, 0.55f);
                    Box("Mould", wx, origin + right * u + Vector3.up * y + normal * 0.016f, new Vector3(s, s * R(0.6f, 1.4f), 0.01f), M.mould, false, false,
                        rot * Quaternion.Euler(0, 0, R(0, 90)));
                }
                for (int k = 0; k < 3; k++)
                {
                    float u = R(-width * 0.4f, width * 0.4f);
                    if (skipDoor && Mathf.Abs(u) < DoorHalf + 0.3f) continue;
                    Box("Crack", wx, origin + right * u + Vector3.up * R(0.8f, 2.2f) + normal * 0.02f, new Vector3(0.03f, R(0.4f, 0.9f), 0.01f), M.crack, false, false,
                        rot * Quaternion.Euler(0, 0, R(-35f, 35f)));
                }
            }
            Face(new Vector3(0, 0, Front), Vector3.right, Vector3.forward, W, true);
            Face(new Vector3(0, 0, Back), Vector3.left, Vector3.back, W, false);
            Face(new Vector3(-W * 0.5f, 0, cz), Vector3.forward, Vector3.left, D, false);
            Face(new Vector3(W * 0.5f, 0, cz), Vector3.back, Vector3.right, D, false);

            // A leaning old rain barrel and a broken board by the door
            var barrel = Prim(PrimitiveType.Cylinder, "RainBarrel", house, new Vector3(-2.6f, 0.45f, Front + 0.55f), new Vector3(0.6f, 0.45f, 0.6f), M.timber, true);
            barrel.transform.rotation = Quaternion.Euler(4f, 0, 3f);
            Box("BrokenBoard", house, new Vector3(2.4f, 0.6f, Front + 0.12f), new Vector3(0.12f, 1.2f, 0.03f), M.timber, false, false, Quaternion.Euler(0, 0, 14f));

            // --- door on a hinge (state set by HouseDoor at runtime; open for the child at start)
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(house, false);
            hinge.localPosition = new Vector3(-DoorHalf, 0f, Front - 0.04f);
            hinge.localRotation = Quaternion.Euler(0, 100f, 0); // swung inwards = open
            var door = Box("Door", hinge, new Vector3(DoorHalf, DoorH * 0.5f + 0.01f, 0f), new Vector3(DoorHalf * 2f - 0.02f, DoorH - 0.02f, 0.06f), M.door, true, true);
            for (int k = 0; k < 3; k++)
                Box("Door_Plank_Gap", hinge, new Vector3(0.28f + k * 0.28f, DoorH * 0.5f, 0.032f), new Vector3(0.015f, DoorH - 0.1f, 0.005f), M.crack, false, true);
            Box("Door_Mould", hinge, new Vector3(0.5f, 0.25f, 0.034f), new Vector3(0.9f, 0.35f, 0.005f), M.mould, false, true);
            var handle = Box("DoorHandle", hinge, new Vector3(DoorHalf * 2f - 0.15f, 1.0f, 0.07f), new Vector3(0.05f, 0.18f, 0.05f), M.iron, false, true);
            Box("DoorHandle_In", hinge, new Vector3(DoorHalf * 2f - 0.15f, 1.0f, -0.07f), new Vector3(0.05f, 0.18f, 0.05f), M.iron, false, true);
            handle.name = "DoorHandle";

            foreach (var t in house.GetComponentsInChildren<Transform>(true))
                if (!t.IsChildOf(hinge))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider, bool local = true, Quaternion? rot = null)
            => WorldBuilder.Prim(PrimitiveType.Cube, name, parent, pos, scale, m, collider, rot, local);

        static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider)
            => WorldBuilder.Prim(t, name, parent, pos, scale, m, collider, null, true);

        static void Window(Transform parent, Vector3 pos, Quaternion rot, Material glass, Material frame, bool cracked)
        {
            var w = new GameObject("Window").transform;
            w.SetParent(parent, false); w.localPosition = pos; w.localRotation = rot;
            Box("Glass", w, Vector3.zero, new Vector3(0.8f, 0.9f, 0.02f), glass, false);
            Box("Frame_T", w, new Vector3(0, 0.48f, 0.02f), new Vector3(0.96f, 0.08f, 0.06f), frame, false);
            Box("Frame_B", w, new Vector3(0, -0.48f, 0.04f), new Vector3(1.0f, 0.08f, 0.12f), frame, false);
            Box("Frame_L", w, new Vector3(-0.44f, 0, 0.02f), new Vector3(0.08f, 0.9f, 0.06f), frame, false);
            Box("Frame_R", w, new Vector3(0.44f, 0, 0.02f), new Vector3(0.08f, 0.9f, 0.06f), frame, false);
            Box("Bar_V", w, new Vector3(0, 0, 0.02f), new Vector3(0.04f, 0.9f, 0.03f), frame, false);
            Box("Bar_H", w, new Vector3(0, 0, 0.02f), new Vector3(0.8f, 0.04f, 0.03f), frame, false);
            if (cracked)
                Box("Board", w, new Vector3(0.18f, 0.15f, 0.05f), new Vector3(0.95f, 0.14f, 0.025f), frame, false, true, Quaternion.Euler(0, 0, -22f));
        }

        /// Triangular prism (gable end) in local YZ, extruded along X by thickness.
        static Mesh GableMesh(float halfDepth, float rise, float thick)
        {
            float t = thick * 0.5f;
            Vector3 a = new Vector3(0, 0, -halfDepth), b = new Vector3(0, 0, halfDepth), c = new Vector3(0, rise, 0);
            Vector3 X = new Vector3(t, 0, 0);
            var v = new[]
            {
                a + X, c + X, b + X,           // +X face
                a - X, b - X, c - X,           // -X face
                a - X, c - X, c + X, a + X,    // back slope
                b + X, c + X, c - X, b - X,    // front slope
                a - X, a + X, b + X, b - X,    // bottom
            };
            var tri = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 6, 8, 9, 10, 11, 12, 10, 12, 13, 14, 15, 16, 14, 16, 17 };
            var m = new Mesh { vertices = v, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return WorldBuilder.SaveMesh("GableMesh", m);
        }
    }
}
