using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GrowingUp.EditorTools
{
    /// <summary>
    /// Procedural higher-detail meshes (smooth, many vertices) for the hero objects, so they look more
    /// realistic than the low-poly packs: bark log, boulders, mushrooms, leaf-card bushes, tunnel lining.
    /// All saved as assets under Assets/_Project/Meshes.
    /// </summary>
    public static class MeshGen
    {
        const string MeshDir = "Assets/_Project/Meshes";

        // ---------------------------------------------------------------- noise
        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
            }
        }

        public static float Noise3(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float L(int a, int b, int c) => Hash(ix + a, iy + b, iz + c);
            float x00 = Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), fx), x10 = Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), fx);
            float x01 = Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), fx), x11 = Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz) * 2f - 1f;
        }

        public static float Fbm(Vector3 p, int oct = 4)
        {
            float s = 0f, a = 0.5f;
            for (int i = 0; i < oct; i++) { s += Noise3(p) * a; p = p * 2.03f + new Vector3(17.1f, 3.7f, 9.3f); a *= 0.5f; }
            return s;
        }

        // ---------------------------------------------------------------- save
        public static Mesh Save(string name, Mesh src)
        {
            string p = MeshDir + "/" + name + ".asset";
            System.IO.Directory.CreateDirectory(MeshDir);
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            if (m == null) { src.name = name; AssetDatabase.CreateAsset(src, p); return src; }
            m.Clear();
            m.indexFormat = src.indexFormat;
            m.vertices = src.vertices; m.uv = src.uv; m.normals = src.normals;
            m.subMeshCount = src.subMeshCount;
            for (int i = 0; i < src.subMeshCount; i++) m.SetTriangles(src.GetTriangles(i), i);
            m.RecalculateTangents(); m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh Build(List<Vector3> v, List<Vector2> uv, params List<int>[] subs)
        {
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetUVs(0, uv);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------------- log
        /// Fallen log along local X, centred, resting on y = 0. Submesh 0 = bark, 1 = cut ends (wood).
        public static Mesh Log(string name, float length, float radius, int seed)
        {
            const int radial = 48, along = 40;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var bark = new List<int>(); var ends = new List<int>();
            var off = new Vector3(seed * 1.7f, seed * 0.3f, 0f);
            float R(float a, float x)
            {
                Vector3 q = new Vector3(Mathf.Cos(a), Mathf.Sin(a), x * 0.6f);
                float ridges = Mathf.Abs(Mathf.Sin(a * 13f + Fbm(q * 2f + off) * 4f + x * 1.3f)) * 0.05f; // bark furrows
                float lumps = Fbm(q * 1.4f + off, 3) * 0.12f;
                float taper = 1f - 0.12f * (x / length + 0.5f);
                return radius * taper * (1f + lumps - ridges);
            }
            for (int i = 0; i <= along; i++)
            {
                float x = -length * 0.5f + length * i / along;
                float bend = Mathf.Sin((float)i / along * Mathf.PI) * radius * 0.15f;
                for (int j = 0; j <= radial; j++)
                {
                    float a = (float)j / radial * Mathf.PI * 2f;
                    float r = R(a % (Mathf.PI * 2f), x);
                    v.Add(new Vector3(x, radius + bend + Mathf.Sin(a) * r, Mathf.Cos(a) * r));
                    uv.Add(new Vector2((float)j / radial * 3f, x / 0.8f));
                    if (i > 0 && j > 0)
                    {
                        int c = i * (radial + 1) + j, b = c - 1, d = c - (radial + 1), e = d - 1;
                        bark.AddRange(new[] { e, b, d, d, b, c });
                    }
                }
            }
            // end caps with growth rings (uv = polar, the wood material shows rings via its texture/colour)
            for (int side = 0; side < 2; side++)
            {
                int ring = side == 0 ? 0 : along;
                float x = -length * 0.5f + length * ring / along;
                float bend = Mathf.Sin((float)ring / along * Mathf.PI) * radius * 0.15f;
                int centre = v.Count;
                v.Add(new Vector3(x + (side == 0 ? 0.01f : -0.01f), radius + bend, 0f)); uv.Add(new Vector2(0.5f, 0.5f));
                for (int j = 0; j <= radial; j++)
                {
                    float a = (float)j / radial * Mathf.PI * 2f;
                    float r = R(a % (Mathf.PI * 2f), x) * 0.97f;
                    v.Add(new Vector3(x, radius + bend + Mathf.Sin(a) * r, Mathf.Cos(a) * r));
                    uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                    if (j > 0)
                    {
                        if (side == 0) ends.AddRange(new[] { centre, centre + j, centre + j + 1 });
                        else ends.AddRange(new[] { centre, centre + j + 1, centre + j });
                    }
                }
            }
            return Save(name, Build(v, uv, bark, ends));
        }

        // ---------------------------------------------------------------- boulder
        /// Smooth, lumpy boulder (subdivided icosphere + fbm), flat bottom on y = 0. size = (w, h, d).
        public static Mesh Rock(string name, Vector3 size, int seed, int subdiv = 3)
        {
            Icosphere(subdiv, out var verts, out var tris);
            var off = new Vector3(seed * 3.1f, seed * 1.3f, seed * 2.2f);
            var v = new List<Vector3>(); var uv = new List<Vector2>();
            foreach (var p0 in verts)
            {
                Vector3 n = p0.normalized;
                float d = 1f + Fbm(n * 1.6f + off, 4) * 0.35f + Fbm(n * 6f + off, 2) * 0.05f;
                Vector3 p = n * d;
                // flatten into a slab-like stone: squashed bottom, slightly flat top facets
                if (p.y < -0.35f) p.y = -0.35f + (p.y + 0.35f) * 0.1f;
                if (p.y > 0.75f) p.y = 0.75f + (p.y - 0.75f) * 0.4f;
                v.Add(p);
                uv.Add(new Vector2(Mathf.Atan2(n.z, n.x) / (2f * Mathf.PI) * 2f, n.y));
            }
            float minY = float.MaxValue, maxY = float.MinValue, maxX = 0f, maxZ = 0f;
            foreach (var p in v) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); maxX = Mathf.Max(maxX, Mathf.Abs(p.x)); maxZ = Mathf.Max(maxZ, Mathf.Abs(p.z)); }
            for (int i = 0; i < v.Count; i++)
            {
                var p = v[i];
                v[i] = new Vector3(p.x / maxX * size.x * 0.5f, (p.y - minY) / (maxY - minY) * size.y, p.z / maxZ * size.z * 0.5f);
            }
            return Save(name, Build(v, uv, tris));
        }

        static void Icosphere(int subdiv, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3> {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>(); var nt = new List<int>();
                var vv = verts;
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    vv.Add(((vv[a] + vv[b]) * 0.5f).normalized); cache[key] = vv.Count - 1; return vv.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = nt;
            }
        }

        // ---------------------------------------------------------------- mushroom (lathe)
        /// Smooth mushroom, height ~h. Submesh 0 = stem + gills + spots, 1 = cap top.
        /// style 0 = red toadstool with white spots, 1 = brown dome (porcini), 2 = slender pale bell.
        public static Mesh Mushroom(string name, int style, float h, int seed)
        {
            const int seg = 32;
            var rng = new System.Random(seed);
            float Rf(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var stem = new List<int>(); var cap = new List<int>();

            float stemR = style == 1 ? 0.13f * h : style == 2 ? 0.05f * h : 0.08f * h;
            float stemH = style == 1 ? 0.55f * h : style == 2 ? 0.8f * h : 0.68f * h;
            float capR = style == 1 ? 0.42f * h : style == 2 ? 0.22f * h : 0.45f * h;
            float capH = h - stemH + (style == 2 ? 0.12f * h : 0.08f * h);
            float lean = Rf(-0.06f, 0.06f) * h;

            void Lathe(List<Vector2> prof, List<int> tri, float vScale)
            {
                int start = v.Count;
                for (int i = 0; i < prof.Count; i++)
                    for (int j = 0; j <= seg; j++)
                    {
                        float a = (float)j / seg * Mathf.PI * 2f;
                        float wob = 1f + 0.03f * Mathf.Sin(a * 3f + seed) + 0.02f * Mathf.Sin(a * 7f + seed * 2);
                        float y = prof[i].y;
                        float lx = lean * (y / h) * (y / h);
                        v.Add(new Vector3(Mathf.Cos(a) * prof[i].x * wob + lx, y, Mathf.Sin(a) * prof[i].x * wob));
                        uv.Add(new Vector2((float)j / seg, y * vScale));
                        if (i > 0 && j > 0)
                        {
                            int c = start + i * (seg + 1) + j, b = c - 1, d = c - (seg + 1), e = d - 1;
                            tri.AddRange(new[] { e, d, b, d, c, b });
                        }
                    }
            }

            // stem: slightly bulbous base, narrowing up
            var sp = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float r = stemR * (1f + (style == 1 ? 0.35f : 0.25f) * Mathf.Pow(1f - t, 3f) - 0.15f * t);
                sp.Add(new Vector2(r, t * stemH));
            }
            Lathe(sp, stem, 1f / h);

            // gills (underside of the cap): from the stem top out to the cap rim
            float rimY = stemH - (style == 2 ? 0.1f * h : 0.02f * h);
            var gp = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                gp.Add(new Vector2(Mathf.Lerp(stemR * 0.85f, capR * 0.97f, t), Mathf.Lerp(stemH + 0.02f * h, rimY, t * t)));
            }
            Lathe(gp, stem, 1f / h);

            // cap: rim -> dome top (outer surface)
            var cp = new List<Vector2>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;               // 0 = rim, 1 = top
                float ang = t * Mathf.PI * 0.5f;
                float shape = style == 2 ? Mathf.Pow(Mathf.Cos(ang), 0.8f) : Mathf.Cos(ang);
                float r = capR * shape * (1f + 0.04f * Mathf.Sin(t * Mathf.PI));
                float y = rimY + capH * Mathf.Pow(Mathf.Sin(ang), style == 1 ? 0.8f : 1f);
                if (i == 14) r = 0.0005f;
                cp.Add(new Vector2(r, y));
            }
            Lathe(cp, cap, 1f / h);

            // white spots on the red toadstool
            if (style == 0)
            {
                int spots = 9 + rng.Next(0, 5);
                for (int s = 0; s < spots; s++)
                {
                    float t = Rf(0.15f, 0.85f), a = Rf(0f, Mathf.PI * 2f);
                    float ang = t * Mathf.PI * 0.5f;
                    float r = capR * Mathf.Cos(ang), y = rimY + capH * Mathf.Sin(ang);
                    Vector3 c = new Vector3(Mathf.Cos(a) * r + lean * (y / h) * (y / h), y, Mathf.Sin(a) * r);
                    Vector3 n = new Vector3(Mathf.Cos(a) * Mathf.Cos(ang) / capR, Mathf.Sin(ang) / capH, Mathf.Sin(a) * Mathf.Cos(ang) / capR).normalized;
                    float sr = Rf(0.03f, 0.06f) * h;
                    Spot(v, uv, stem, c, n, sr);
                }
            }
            return Save(name, Build(v, uv, stem, cap));
        }

        static void Spot(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 c, Vector3 n, float r)
        {
            Vector3 t = Vector3.Cross(n, Vector3.up); if (t.sqrMagnitude < 1e-4f) t = Vector3.right; t.Normalize();
            Vector3 b = Vector3.Cross(n, t);
            int centre = v.Count;
            v.Add(c + n * r * 0.35f); uv.Add(new Vector2(0.5f, 0.5f));
            const int k = 8;
            for (int i = 0; i <= k; i++)
            {
                float a = i / (float)k * Mathf.PI * 2f;
                v.Add(c + (t * Mathf.Cos(a) + b * Mathf.Sin(a)) * r - n * r * 0.05f); uv.Add(new Vector2(0.5f, 0.5f));
                if (i > 0) tri.AddRange(new[] { centre, centre + i + 1, centre + i });
            }
        }

        // ---------------------------------------------------------------- leafy bush (leaf cards)
        static readonly Rect[] LeafRects = { new Rect(0.02f, 0.02f, 0.80f, 0.55f), new Rect(0.37f, 0.65f, 0.47f, 0.32f) };

        /// Dense bush made of crossed leaf cards (alpha-clipped branch texture) around a dark core.
        /// Submesh 0 = leaf cards (tree_branches material), 1 = core (dark leaves). Extents = radii.
        public static Mesh LeafBush(string name, Vector3 radii, int cards, int seed, bool core = true)
        {
            var rng = new System.Random(seed);
            float Rf(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var leaves = new List<int>(); var coreT = new List<int>();
            var normals = new List<Vector3>();
            for (int c = 0; c < cards; c++)
            {
                // point inside the upper ellipsoid, biased to the surface
                Vector3 d = new Vector3(Rf(-1f, 1f), Rf(-0.2f, 1f), Rf(-1f, 1f)).normalized * Mathf.Pow(Rf(0.55f, 1f), 0.5f);
                Vector3 p = new Vector3(d.x * radii.x, Mathf.Max(0.02f, (d.y * 0.85f + 0.15f) * radii.y), d.z * radii.z);
                Vector3 outward = new Vector3(p.x / radii.x, (p.y - radii.y * 0.3f) / radii.y, p.z / radii.z).normalized;
                float size = Rf(0.42f, 0.62f) * Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
                p.y = Mathf.Max(p.y, size * 0.3f);
                var rect = LeafRects[rng.Next(0, LeafRects.Length)];
                // two crossed quads per card, roughly facing outward
                var q = Quaternion.LookRotation(outward) * Quaternion.Euler(Rf(-25f, 25f), Rf(-25f, 25f), Rf(0f, 360f));
                for (int k = 0; k < 2; k++)
                {
                    var rot = q * Quaternion.Euler(0f, k * 90f, 0f);
                    Vector3 right = rot * Vector3.right * size * 0.5f * (rect.width / rect.height);
                    Vector3 up = rot * Vector3.up * size * 0.5f;
                    int s = v.Count;
                    v.Add(p - right - up); v.Add(p + right - up); v.Add(p + right + up); v.Add(p - right + up);
                    uv.Add(new Vector2(rect.xMin, rect.yMin)); uv.Add(new Vector2(rect.xMax, rect.yMin));
                    uv.Add(new Vector2(rect.xMax, rect.yMax)); uv.Add(new Vector2(rect.xMin, rect.yMax));
                    for (int i = 0; i < 4; i++) normals.Add(Vector3.Slerp(outward, Vector3.up, 0.3f).normalized); // soft foliage lighting
                    leaves.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                }
            }
            if (core)
            {
                Icosphere(2, out var cv, out var ct);
                int b = v.Count;
                foreach (var n in cv)
                {
                    float wob = 1f + Fbm(n * 2f + new Vector3(seed, 0, 0), 2) * 0.25f;
                    v.Add(new Vector3(n.x * radii.x * 0.78f * wob, Mathf.Max(0f, (n.y * 0.5f + 0.5f) * radii.y * 0.9f * wob), n.z * radii.z * 0.78f * wob));
                    uv.Add(new Vector2(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f));
                    normals.Add(n);
                }
                foreach (var i in ct) coreT.Add(b + i);
            }
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(normals);
            m.subMeshCount = 2; m.SetTriangles(leaves, 0); m.SetTriangles(coreT, 1);
            m.RecalculateTangents(); m.RecalculateBounds();
            return Save(name, m);
        }

        // ---------------------------------------------------------------- tunnel lining (one side)
        /// Inward-facing leafy half-arch along local +Z (0..length). side = -1 (left) or +1 (right).
        /// The arch: half-width w at the ground, height hgt at the centre.
        public static Mesh TunnelHalf(string name, float length, float w, float hgt, int side, int seed)
        {
            const int around = 20, along = 36;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            var off = new Vector3(seed * 2.3f, 0f, 0f);
            for (int i = 0; i <= along; i++)
            {
                float z = length * i / along;
                for (int j = 0; j <= around; j++)
                {
                    float a = (float)j / around * Mathf.PI * 0.5f;      // 0 = ground, pi/2 = top centre
                    Vector3 basePt = new Vector3(side * Mathf.Cos(a) * w, Mathf.Sin(a) * hgt, z);
                    float n = Fbm(new Vector3(a * 2f, z * 3f, 0f) + off, 4);
                    float bulge = (n * 0.22f + Mathf.Abs(Fbm(new Vector3(a * 9f, z * 9f, 1f) + off, 2)) * 0.12f) * Mathf.Sin(Mathf.Min(1f, (float)i / along * 8f) * Mathf.PI * 0.5f);
                    Vector3 inward = new Vector3(-side * Mathf.Cos(a) / w, -Mathf.Sin(a) / hgt, 0f).normalized;
                    v.Add(basePt + inward * bulge * Mathf.Min(w, hgt));
                    uv.Add(new Vector2(a * 2f, z * 2.5f));
                    if (i > 0 && j > 0)
                    {
                        int c = i * (around + 1) + j, b = c - 1, d = c - (around + 1), e = d - 1;
                        if (side > 0) tri.AddRange(new[] { e, d, b, d, c, b });
                        else tri.AddRange(new[] { e, b, d, d, b, c });
                    }
                }
            }
            return Save(name, Build(v, uv, tri));
        }

        // ---------------------------------------------------------------- textures
        /// Soft glowing ring (for the light waves of the transformation).
        public static Texture2D RingTexture(string path, int size = 128)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.07f, 2f)) + 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.22f, 2f));
                    if (r > 0.99f) a = 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// Wood end grain with growth rings (polar uv).
        public static Texture2D RingsTexture(string path, int size = 256)
        {
            var t = new Texture2D(size, size, TextureFormat.RGB24, true);
            var light = new Color(0.78f, 0.62f, 0.42f); var dark = new Color(0.52f, 0.36f, 0.2f); var bark = new Color(0.25f, 0.18f, 0.12f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) + Fbm(new Vector3(dx * 3f, dy * 3f, 0f), 3) * 0.04f;
                    float rings = Mathf.Pow(Mathf.Abs(Mathf.Sin(r * 60f)), 6f);
                    Color c = Color.Lerp(light, dark, rings * 0.8f + r * 0.2f);
                    if (r > 0.9f) c = Color.Lerp(c, bark, Mathf.Clamp01((r - 0.9f) / 0.06f));
                    float crack = Mathf.Abs(Mathf.Atan2(dy, dx) - 0.7f) < 0.015f * (1f - r) && r < 0.7f ? 0.5f : 1f;
                    t.SetPixel(x, y, c * crack);
                }
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
