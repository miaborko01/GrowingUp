using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GrowingUp.EditorTools
{
    /// <summary>
    /// Builds the quests on top of the world (menu Growing Up/Build Interactions, or one at a time):
    /// glowing mushrooms + dandelion field (side quests), log + rock staircase, river, bush tunnel, fairy
    /// ring + berries, and the story pieces (intro, transformation, ending). Everything goes under
    /// World/Interactions/&lt;Station&gt; and is wired to the Story object. Grabbing always uses the Meta Grab Interaction building block (copied from
    /// Prefabs/GrabInteraction_Template.prefab, which was taken from the original Grab block).
    /// </summary>
    public static class InteractionBuilder
    {
        const string GrabTemplate = "Assets/_Project/Prefabs/GrabInteraction_Template.prefab";
        static System.Random _rng = new System.Random(7);
        static float R(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

        [MenuItem("Growing Up/Build Interactions (all)")]
        public static void BuildAll()
        {
            EnsureMouth();
            BuildMushroomGlow();
            BuildLog();
            BuildDandelionField();
            BuildRiverCrossing();
            BuildBushTunnel();
            BuildBerries();
            BuildAdultDusk();
            BuildStory();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // ---------------------------------------------------------------- common
        public static Transform StationRoot(string name)
        {
            var world = GameObject.Find("World").transform;
            var inter = world.Find("Interactions");
            if (inter == null) { inter = new GameObject("Interactions").transform; inter.SetParent(world, false); }
            var old = inter.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(name).transform;
            root.SetParent(inter, false);
            return root;
        }

        public static Transform StoryRoot()
        {
            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == "Story") return r.transform;
            var s = new GameObject("Story");
            s.AddComponent<StoryManager>();
            return s.transform;
        }

        public static void EnsureMouth()
        {
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            var eye = rig.centerEyeAnchor;
            if (eye.Find("MouthZone") != null) return;
            var m = new GameObject("MouthZone");
            m.transform.SetParent(eye, false);
            m.transform.localPosition = new Vector3(0f, -0.08f, 0.07f);
            m.AddComponent<MouthZone>();
        }

        /// Turns an object into a Meta grabbable: Rigidbody + (convex) colliders + Grabbable (throw on
        /// release keeps the controller velocity) + the Grab Interaction building block child.
        public static GrabbableItem MakeGrabbable(GameObject go, float mass, bool planted, float extraGrabRadius = 0f)
        {
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) mc.convex = true;
            if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<SphereCollider>();
            if (extraGrabRadius > 0f)
            {
                // a slightly bigger, invisible grab volume so tiny things are easy to pick up with tiny hands
                var s = go.AddComponent<SphereCollider>();
                s.isTrigger = true; s.radius = extraGrabRadius / Mathf.Max(0.0001f, go.transform.lossyScale.x);
            }
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = false;

            var g = go.GetComponent<Grabbable>();
            if (g == null) g = go.AddComponent<Grabbable>();
            var so = new SerializedObject(g);
            so.FindProperty("_targetTransform").objectReferenceValue = go.transform;
            so.FindProperty("_rigidbody").objectReferenceValue = rb;
            so.FindProperty("_throwWhenUnselected").boolValue = true;
            so.FindProperty("_kinematicWhileSelected").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            var tpl = AssetDatabase.LoadAssetAtPath<GameObject>(GrabTemplate);
            var block = (GameObject)PrefabUtility.InstantiatePrefab(tpl, go.transform);
            PrefabUtility.UnpackPrefabInstance(block, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            block.name = "[BuildingBlock] HandGrabInstallationRoutine";
            block.transform.localPosition = Vector3.zero; block.transform.localRotation = Quaternion.identity; block.transform.localScale = Vector3.one;
            foreach (var mb in block.GetComponents<MonoBehaviour>())
            {
                var bso = new SerializedObject(mb);
                var pe = bso.FindProperty("_pointableElement"); if (pe != null) pe.objectReferenceValue = g;
                var r = bso.FindProperty("_rigidbody"); if (r != null) r.objectReferenceValue = rb;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }

            var item = go.GetComponent<GrabbableItem>();
            if (item == null) item = go.AddComponent<GrabbableItem>();
            item.planted = planted;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            return item;
        }

        public static AudioSource Sfx(Transform parent, string name, ProceduralAudio.Kind kind, float vol, Vector3 localPos, float maxDist = 10f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return WorldBuilder.AddAudio(go, kind, ProceduralAudio.Category.Sfx, vol, kind.GetHashCode(), 0.3f, maxDist, true, false, false);
        }

        public static ParticleSystem Burst(Transform parent, string name, Color a, Color b, int count, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float life, float gravity, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity; main.maxParticles = count * 2;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.02f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = gr;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                additive ? "Assets/_Project/Materials/M_Sparkle.mat" : "Assets/_Project/Materials/M_Droplet.mat");
            return ps;
        }

        static Material DropletMat()
        {
            string p = "Assets/_Project/Materials/M_Droplet.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, p); }
            m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.renderQueue = 3000; m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- 3. dandelion
        static Material TransparentUnlit(string name, Color c)
        {
            string p = "Assets/_Project/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.renderQueue = 3000; m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        public static ParticleSystem StaticFuzz(Transform parent, string name, Vector3 localPos, float radius, int count, float size, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 100000f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
            main.startColor = color; main.maxParticles = count;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius; sh.radiusThickness = 0.15f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sparkle.mat");
            return ps;
        }

        // ---------------------------------------------------------------- side quest: glowing mushrooms
        [MenuItem("Growing Up/Build Interactions/0 Glowing Mushrooms")]
        public static void BuildMushroomGlow()
        {
            var group = GameObject.Find("World/Forest/Mushrooms (touch me)");
            if (group == null) { Debug.LogError("Rebuild the world first (no mushrooms group)."); return; }
            AddGlow(group.transform);
            EditorSceneManager.MarkSceneDirty(group.scene);
        }

        static GlowMushrooms AddGlow(Transform group)
        {
            var old = group.GetComponent<GlowMushrooms>(); if (old != null) Object.DestroyImmediate(old);
            foreach (var n in new[] { "SFX_MushroomBling", "BlingSparkle" }) { var o = group.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
            var gm = group.gameObject.AddComponent<GlowMushrooms>();
            gm.bling = Sfx(group, "SFX_MushroomBling", ProceduralAudio.Kind.Bling, 0.55f, Vector3.zero, 6f);
            var sp = Burst(group, "BlingSparkle", new Color(0.7f, 1f, 0.8f, 1f), new Color(1f, 1f, 0.7f, 1f), 18, 0.05f, 0.18f, 0.006f, 0.014f, 1.2f, -0.03f, true);
            var sh = sp.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.04f;
            foreach (var t in group.GetComponentsInChildren<Transform>(true))
                if (t == gm.bling.transform || t == sp.transform) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            // the glow uses per-renderer emission: keep mushrooms out of static batching
            foreach (var r in group.GetComponentsInChildren<Renderer>(true)) GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);
            gm.sparkle = sp;
            return gm;
        }

        // ---------------------------------------------------------------- 1. the log
        [MenuItem("Growing Up/Build Interactions/1 Log (rock staircase)")]
        public static void BuildLog()
        {
            var root = StationRoot("Log");
            float lz = WorldBuilder.LogZ, px = WorldBuilder.PathX(lz);

            // the fallen log across the path (high-detail bark mesh), top ~0.46 m: too high for the child's 0.3 m jump
            const float L = 2.6f, Rad = 0.23f;
            var log = new GameObject("FallenLog (too high to jump)");
            log.transform.SetParent(root, false);
            log.transform.SetPositionAndRotation(new Vector3(px, 0f, lz), Quaternion.Euler(0f, 4f, 0f));
            var mesh = MeshGen.Log("HD_Log_Hero", L, Rad, 99);
            log.AddComponent<MeshFilter>().sharedMesh = mesh;
            log.AddComponent<MeshRenderer>().sharedMaterials = WorldBuilder.LogMats();
            var cap = log.AddComponent<CapsuleCollider>(); cap.direction = 0; cap.radius = Rad; cap.height = L; cap.center = new Vector3(0f, Rad + 0.02f, 0f);
            // moss + a few mushrooms growing on the log
            for (int i = 0; i < 4; i++)
                WorldBuilder.PlaceHDShroom(log.transform, log.transform.TransformPoint(new Vector3(R(-1f, 1f), Rad * 2f - 0.02f, R(-0.06f, 0.06f))), R(0, 360), R(0.07f, 0.12f), 1, i, false);

            // thicket on both sides: invisible walls (whole world width) dressed with dense bushes
            var thicket = new GameObject("Thicket (can't go around)").transform; thicket.SetParent(root, false);
            void Wall(string n, float x0, float x1)
            {
                var w = new GameObject(n); w.transform.SetParent(thicket, false);
                w.transform.position = new Vector3((x0 + x1) * 0.5f, 0.9f, lz);
                w.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(x1 - x0), 1.8f, 0.9f);
            }
            Wall("Wall_Left", -24.5f, px - 0.9f);
            Wall("Wall_Right", px + 0.9f, 24.5f);
            int k = 0;
            float[] radius = { 0.6f, 0.8f, 0.5f, 0.9f }; // HD_Bush_0..3 horizontal radii
            foreach (var side in new[] { -1f, 1f })
                for (int row = 0; row < 2; row++)
                    for (float edge = 0.85f; edge < 9f;)
                    {
                        int v = edge < 2f ? (_rng.Next(2) == 0 ? 2 : 0) : _rng.Next(4);
                        float sc = edge < 2f ? R(0.8f, 1.0f) : R(1.0f, 1.5f);
                        float r = radius[v] * sc;
                        var p = new Vector3(px + side * (edge + r * 0.8f), 0f, lz + (row == 0 ? -0.25f : 0.25f) + R(-0.1f, 0.1f));
                        WorldBuilder.PlaceHDBush(thicket, "ThicketBush_" + k++, p, R(0, 360), new Vector3(sc, sc * R(0.9f, 1.3f), sc), v, false);
                        edge += r * R(1.1f, 1.5f);
                    }

            // rocks scattered along the way (pushable): low, medium, small
            var scrape = Sfx(root, "SFX_RockScrape", ProceduralAudio.Kind.Scrape, 0.5f, Vector3.zero, 6f);
            Vector3[] sizes = { new Vector3(0.34f, 0.24f, 0.3f), new Vector3(0.42f, 0.34f, 0.36f), new Vector3(0.28f, 0.16f, 0.26f) }; // tops: 0.24 / 0.34 / 0.16 m (log top ~0.48, child jump 0.3)
            Vector3[] spots = { WorldBuilder.OnPath(2.0f, 0.5f), WorldBuilder.OnPath(3.1f, -0.6f), WorldBuilder.OnPath(1.15f, -0.5f) };
            var rocks = new PushableRock[3];
            for (int i = 0; i < 3; i++)
            {
                var go = WorldBuilder.PlaceHDRock(root, "PushRock_" + i + " (push me)", spots[i], R(0, 360), sizes[i], i + 1, true, i == 1);
                var item = MakeGrabbable(go, 8f, false, 0.07f);
                var so = new SerializedObject(item.GetComponent<Grabbable>());
                so.FindProperty("_throwWhenUnselected").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                var pr = go.AddComponent<PushableRock>();
                pr.scrapeSound = scrape;
                rocks[i] = pr;
                Highlight(go.transform, new Vector3(0f, 0.5f, 0f), Mathf.Max(sizes[i].x, sizes[i].z) * 0.6f, StoryStep.Log, null);
            }
            // staircase spots for the debug skip: medium rock against the log, low rock in front of it
            var stairs = new Transform[3];
            Vector3[] st = { new Vector3(px + 0.05f, 0f, lz - 0.84f), new Vector3(px + 0.05f, 0f, lz - 0.46f), new Vector3(px + 0.5f, 0f, lz - 1.0f) };
            for (int i = 0; i < 3; i++)
            {
                var t = new GameObject("StaircaseSpot_" + i).transform; t.SetParent(root, false); t.position = st[i]; t.rotation = Quaternion.Euler(0f, 90f, 0f);
                stairs[i] = t;
            }
            var lc = root.gameObject.AddComponent<LogCrossing>();
            lc.logZ = lz; lc.logRadius = Rad; lc.rocks = rocks; lc.staircaseSpots = stairs;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // ---------------------------------------------------------------- side quest: dandelion field
        [MenuItem("Growing Up/Build Interactions/2 Dandelion Field")]
        public static void BuildDandelionField()
        {
            var root = StationRoot("DandelionField");
            var placed = new System.Collections.Generic.List<Vector3>();
            int n = 0;
            for (int tries = 0; tries < 400 && n < 14; tries++)
            {
                float z = R(WorldBuilder.DandelionZ0 + 0.2f, WorldBuilder.DandelionZ1 - 0.2f);
                var p = WorldBuilder.OnPath(z, -R(0.75f, 3.0f));
                bool ok = true;
                foreach (var q in placed) if ((q - p).sqrMagnitude < 0.38f * 0.38f) { ok = false; break; }
                if (!ok) continue;
                placed.Add(p);
                MakeDandelion(root, "Dandelion_" + n, p, R(0.42f, 0.6f));
                n++;
            }
            // short meadow grass between them
            for (int i = 0; i < 40; i++)
            {
                var p = WorldBuilder.OnPath(R(WorldBuilder.DandelionZ0, WorldBuilder.DandelionZ1), -R(0.6f, 3.2f));
                WorldBuilder.Place(WorldBuilder.FF + "grass01.prefab", root, p, R(0, 360), R(0.25f, 0.4f), false);
            }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        static Dandelion MakeDandelion(Transform root, string name, Vector3 pos, float H)
        {
            var green = WorldBuilder.Mat("M_DandelionStem", new Color(0.42f, 0.62f, 0.25f), 0.3f);
            var leafM = WorldBuilder.Mat("M_DandelionLeaf", new Color(0.30f, 0.52f, 0.17f), 0.25f);
            var holder = new GameObject(name).transform; holder.SetParent(root, false); holder.position = pos;

            // leaf rosette stays in the ground
            var rosette = new GameObject("Leaves").transform; rosette.SetParent(holder, false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + R(-12f, 12f);
                var lf = WorldBuilder.Prim(PrimitiveType.Sphere, "Leaf_" + i, rosette, Vector3.zero, new Vector3(0.035f, 0.006f, 0.2f), leafM, false,
                    Quaternion.Euler(-8f, a, 0f), true);
                lf.transform.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.006f, 0.09f);
            }

            // the picked part: stem + seed head
            var dand = new GameObject("Dandelion (grab me)");
            dand.transform.SetParent(holder, false);
            dand.transform.localRotation = Quaternion.Euler(R(-6f, 6f), R(0, 360), R(-6f, 6f));
            WorldBuilder.Prim(PrimitiveType.Cylinder, "Stem", dand.transform, new Vector3(0, H * 0.5f, 0), new Vector3(0.012f, H * 0.5f, 0.012f), green, false, null, true);
            var cap = dand.AddComponent<CapsuleCollider>(); cap.radius = 0.014f; cap.height = H + 0.05f; cap.center = new Vector3(0, (H + 0.05f) * 0.5f, 0);
            var head = new GameObject("SeedHead").transform; head.SetParent(dand.transform, false); head.localPosition = new Vector3(0, H + 0.01f, 0);
            WorldBuilder.Prim(PrimitiveType.Sphere, "Receptacle", head, Vector3.zero, Vector3.one * 0.022f, WorldBuilder.Mat("M_DandelionCore", new Color(0.55f, 0.45f, 0.30f)), false, null, true);
            var puff = new GameObject("Puff").transform; puff.SetParent(head, false);
            WorldBuilder.Prim(PrimitiveType.Sphere, "PuffShell", puff, Vector3.zero, Vector3.one * 0.115f, TransparentUnlit("M_DandelionPuff", new Color(1f, 1f, 1f, 0.28f)), false, null, true);
            StaticFuzz(puff, "PuffFuzz", Vector3.zero, 0.055f, 70, 0.014f, new Color(1f, 1f, 0.95f, 0.8f));
            var item = MakeGrabbable(dand, 0.05f, true, 0.04f);

            var burst = Burst(holder, "SeedBurst (glowing seeds)", new Color(1f, 0.97f, 0.85f, 1f), new Color(1f, 0.85f, 0.45f, 1f),
                90, 0.15f, 0.55f, 0.012f, 0.028f, 7f, -0.015f, true);
            var bm = burst.main; bm.duration = 0.3f;
            var bsh = burst.shape; bsh.shapeType = ParticleSystemShapeType.Cone; bsh.angle = 55f; bsh.radius = 0.04f;
            var noise = burst.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.6f; noise.scrollSpeed = 0.3f;
            var drag = burst.limitVelocityOverLifetime; drag.enabled = true; drag.limit = 0.25f; drag.dampen = 0.15f;

            var d = holder.gameObject.AddComponent<Dandelion>();
            d.item = item; d.head = head; d.puff = puff.gameObject; d.seedBurst = burst;
            d.whoosh = Sfx(holder, "SFX_DandelionWhoosh", ProceduralAudio.Kind.Whoosh, 0.9f, Vector3.zero, 10f);
            Highlight(head, Vector3.zero, 0.08f, null, null, d);
            return d;
        }

        // ---------------------------------------------------------------- 3. river crossing
        [MenuItem("Growing Up/Build Interactions/3 River Crossing")]
        public static void BuildRiverCrossing()
        {
            DropletMat();
            var root = StationRoot("RiverCrossing");
            var splash = Burst(root, "SplashParticles", new Color(0.85f, 0.95f, 1f, 0.9f), new Color(0.6f, 0.8f, 0.9f, 0.8f),
                40, 0.6f, 1.4f, 0.008f, 0.02f, 0.7f, 1f, false);
            var splashSnd = Sfx(root, "SFX_Splash", ProceduralAudio.Kind.Splash, 0.9f, Vector3.zero, 8f);
            var rc = root.gameObject.AddComponent<RiverCrossing>();
            rc.riverZ = WorldBuilder.RiverZ; rc.halfWidth = WorldBuilder.RiverHalfWidth;
            rc.respawnPoint = GameObject.Find("World/Stations/Station_RiverRespawn").transform;
            rc.splashSound = splashSnd; rc.splashParticles = splash;
            BuildRiverBridge(root, splash, splashSnd);
            foreach (Transform st in GameObject.Find("World/River/SteppingStones").transform)
                if (st.name.StartsWith("SteppingStone")) { var ch = Highlight(st, new Vector3(0f, 1f, 0f), 0.14f, StoryStep.River); ch.followObject = false; }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        /// ADULT way back: the river rises and runs faster (stones covered). Logs on the far bank; drop 3
        /// into the river at the crossing to build a bridge.
        static void BuildRiverBridge(Transform root, ParticleSystem splash, AudioSource splashSnd)
        {
            float rz = WorldBuilder.RiverZ, hw = WorldBuilder.RiverHalfWidth, bx = WorldBuilder.PathX(rz);
            var bridge = new GameObject("Bridge (adult)").transform; bridge.SetParent(root, false);
            const float L = 2.5f, Rad = 0.1f;
            var logMesh = MeshGen.Log("HD_BridgeLog", L, Rad, 41);
            var mats = WorldBuilder.LogMats();
            var guideMat = TransparentUnlit("M_GuideLog", new Color(0.85f, 0.85f, 0.8f, 0.18f));

            float[] offs = { 0f, -0.21f, 0.21f };
            var slots = new Transform[offs.Length]; var guides = new GameObject[offs.Length];
            for (int i = 0; i < offs.Length; i++)
            {
                var s = new GameObject("BridgeSlot_" + i).transform; s.SetParent(bridge, false);
                s.SetPositionAndRotation(new Vector3(bx + offs[i], 0f, rz), Quaternion.Euler(0f, 90f + (i - 1) * 2f, 0f));
                slots[i] = s;
                var g = new GameObject("Guide_" + i + " (where the log goes)"); g.transform.SetParent(s, false);
                g.AddComponent<MeshFilter>().sharedMesh = logMesh;
                var gr = g.AddComponent<MeshRenderer>(); gr.sharedMaterial = guideMat; gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.SetActive(false);
                guides[i] = g;
            }

            // 4 logs lying on the far bank (the adult comes from that side), one spare
            var logs = new GrabbableItem[4];
            for (int i = 0; i < logs.Length; i++)
            {
                var go = new GameObject("BridgeLog_" + i + " (adult: grab + drop in the river)");
                go.transform.SetParent(bridge, false);
                go.transform.SetPositionAndRotation(new Vector3(bx + 1.6f + R(-0.1f, 0.1f) + (i % 2) * 0.35f, 0.01f, rz + hw + 0.7f + i * 0.34f), Quaternion.Euler(0f, R(-12f, 12f), 0f));
                go.AddComponent<MeshFilter>().sharedMesh = logMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                var cap = go.AddComponent<CapsuleCollider>(); cap.direction = 0; cap.radius = Rad; cap.height = L; cap.center = new Vector3(0f, Rad + 0.01f, 0f);
                logs[i] = MakeGrabbable(go, 12f, false, 0.1f);
            }

            // the strong river: a wall along the far bank (adult only) with the crossing blocked until the bridge is built
            var walls = new GameObject("RiverTooStrong (adult)").transform; walls.SetParent(bridge, false);
            BoxCollider Wall(string n, float x0, float x1)
            {
                var w = new GameObject(n); w.transform.SetParent(walls, false);
                w.transform.position = new Vector3((x0 + x1) * 0.5f, 1.25f, rz + hw + 0.06f);
                var b = w.AddComponent<BoxCollider>(); b.size = new Vector3(Mathf.Abs(x1 - x0), 2.5f, 0.15f);
                return b;
            }
            var wl = Wall("BankWall_Left", -24.5f, bx - 0.4f);
            var wr = Wall("BankWall_Right", bx + 0.4f, 24.5f);
            var gap = Wall("Crossing_Blocker (until bridge)", bx - 0.4f, bx + 0.4f);

            var rb = bridge.gameObject.AddComponent<RiverBridge>();
            var water = GameObject.Find("World/River/Water");
            rb.water = water.transform; rb.waterRenderer = water.GetComponent<Renderer>();
            var snds = new System.Collections.Generic.List<ProceduralAudio>();
            foreach (Transform t in GameObject.Find("World/River").transform) if (t.name.StartsWith("RiverSound")) snds.Add(t.GetComponent<ProceduralAudio>());
            rb.riverSounds = snds.ToArray();
            rb.logs = logs; rb.slots = slots; rb.guides = guides;
            rb.gapBlocker = gap; rb.bankWalls = new Collider[] { wl, wr };
            rb.riverZ = rz; rb.halfWidth = hw;
            rb.splashFx = splash; rb.splashSound = splashSnd;
        }

        // ---------------------------------------------------------------- 4. bush tunnel
        [MenuItem("Growing Up/Build Interactions/4 Bush Tunnel")]
        public static void BuildBushTunnel()
        {
            var root = StationRoot("BushTunnel");
            float z0 = WorldBuilder.TunnelZ0, z1 = WorldBuilder.TunnelZ1, len = z1 - z0;
            float px = WorldBuilder.PathX((z0 + z1) * 0.5f);
            // passage fits the 60 cm child (capsule 0.6 m), not the 1.8 m adult
            const float halfW = 0.34f, ceil = 0.68f, pivotX = 1.9f;
            root.position = new Vector3(px, 0f, z0);

            var lining = WorldBuilder.Mat("M_TunnelLeaves", new Color(0.15f, 0.26f, 0.09f), 0.1f);
            lining.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WorldBuilder.FFTex + "grass01_b.tga"));
            lining.SetTextureScale("_BaseMap", new Vector2(2f, 3f));
            Transform Half(string n, int side)
            {
                var pivot = new GameObject(n).transform; pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(side * pivotX, 0f, 0f);
                var lin = new GameObject("LeafyLining"); lin.transform.SetParent(pivot, false);
                lin.transform.localPosition = new Vector3(-side * pivotX, 0f, 0f);
                lin.AddComponent<MeshFilter>().sharedMesh = MeshGen.TunnelHalf("HD_TunnelLining_" + (side < 0 ? "L" : "R"), len, halfW + 0.02f, ceil + 0.02f, side, side < 0 ? 3 : 8);
                var mr = lin.AddComponent<MeshRenderer>(); mr.sharedMaterial = lining;
                return pivot;
            }
            var left = Half("Bush_LeftHalf", -1);
            var right = Half("Bush_RightHalf", 1);

            // the big bush itself: overlapping leafy bushes on both sides and over the top
            int k = 0;
            for (float z = 0.45f; z < len - 0.3f; z += 0.55f)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    var parent = side < 0 ? left : right;
                    // side mounds stay outside the passage (HD_Bush_3 radius 0.9 * scale)
                    float sc = R(0.6f, 0.8f);
                    WorldBuilder.PlaceHDBush(parent, "Leaves_" + k++, root.TransformPoint(new Vector3(side * (halfW + 0.9f * sc * 0.9f), 0f, z + R(-0.08f, 0.08f))), R(0, 360), new Vector3(sc, sc * R(1.6f, 2.0f), sc), 3, false);
                    // the roof: sits on top of the lining
                    float tz = Mathf.Clamp(z + R(-0.1f, 0.1f), 0.5f, len - 0.5f);
                    WorldBuilder.PlaceHDBush(parent, "Leaves_" + k++, root.TransformPoint(new Vector3(side * R(0.15f, 0.35f), ceil + 0.02f, tz)), R(0, 360), new Vector3(R(0.55f, 0.65f), R(0.9f, 1.2f), R(0.55f, 0.65f)), 1, false);
                }
                // leaves hanging inside the tunnel (the child brushes through them)
                if (R(0f, 1f) < 0.7f)
                {
                    int s = R(0f, 1f) < 0.5f ? -1 : 1;
                    var hang = WorldBuilder.PlaceHDBush(s < 0 ? left : right, "HangingLeaves_" + k++, root.TransformPoint(new Vector3(s * R(0.05f, 0.18f), ceil - 0.12f, z)), R(0, 360), new Vector3(0.22f, 0.14f, 0.22f), 2, false);
                    hang.transform.rotation = Quaternion.Euler(180f, R(0, 360), 0f);
                    hang.transform.position += Vector3.up * 0.12f;
                }
            }

            // colliders: side walls (whole world width), and the low leafy ceiling that stops the ADULT
            var cols = new GameObject("Colliders").transform; cols.SetParent(root, false);
            void Box(string n, Vector3 c, Vector3 size)
            {
                var g = new GameObject(n); g.transform.SetParent(cols, false); g.transform.position = c;
                g.AddComponent<BoxCollider>().size = size;
            }
            Box("Wall_Left", new Vector3((-24.5f + px - halfW) * 0.5f, 1.25f, z0 + len * 0.5f), new Vector3(Mathf.Abs(px - halfW + 24.5f), 2.5f, len));
            Box("Wall_Right", new Vector3((24.5f + px + halfW) * 0.5f, 1.25f, z0 + len * 0.5f), new Vector3(Mathf.Abs(24.5f - px - halfW), 2.5f, len));
            var blocker = new GameObject("AdultBlocker (leafy ceiling)"); blocker.transform.SetParent(cols, false);
            blocker.transform.position = new Vector3(px, ceil + 1.1f, z0 + len * 0.5f);
            var bc = blocker.AddComponent<BoxCollider>(); bc.size = new Vector3(halfW * 2f + 0.02f, 2.2f, len);

            var bt = root.gameObject.AddComponent<BushTunnel>();
            bt.entranceZ = z0; bt.exitZ = z1; bt.pathX = px;
            bt.leftHalf = left; bt.rightHalf = right; bt.adultBlocker = bc;
            bt.rustleSound = Sfx(root, "SFX_BranchesRustle", ProceduralAudio.Kind.Rustle, 0.9f, new Vector3(0f, 0.8f, len * 0.5f), 10f);

            // child: the tunnel entrance glows so the way through is clear
            var mouth = new GameObject("TunnelEntranceGlow").transform; mouth.SetParent(root, false); mouth.localPosition = new Vector3(0f, 0f, -0.05f);
            Highlight(mouth, new Vector3(0f, ceil * 0.5f, 0f), halfW, StoryStep.BushTunnel);

            // ADULT: a grabbable branch on each half, at hand height on the side the adult comes back from
            var barkMats = WorldBuilder.LogMats();
            foreach (var side in new[] { -1, 1 })
            {
                var half = side < 0 ? left : right;
                var attach = new GameObject("BranchAttach").transform; attach.SetParent(half, false);
                attach.position = root.TransformPoint(new Vector3(side * 0.42f, 1.05f, len + 0.12f));
                attach.rotation = Quaternion.Euler(0f, side * 25f, side * -35f);
                var br = new GameObject(side < 0 ? "Branch_Left (adult: grab + pull left)" : "Branch_Right (adult: grab + pull right)");
                br.transform.SetParent(root, false);
                br.transform.SetPositionAndRotation(attach.position, attach.rotation);
                var stick = new GameObject("Stick"); stick.transform.SetParent(br.transform, false);
                stick.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                stick.transform.localScale = new Vector3(0.7f, 0.25f, 0.25f);
                stick.AddComponent<MeshFilter>().sharedMesh = WorldBuilder.HDLog(1);
                stick.AddComponent<MeshRenderer>().sharedMaterials = barkMats;
                stick.transform.localPosition = new Vector3(0f, -0.03f, 0f);
                WorldBuilder.PlaceHDBush(br.transform, "Leaves", br.transform.TransformPoint(new Vector3(0f, -0.05f, -0.3f)), 0f, new Vector3(0.35f, 0.3f, 0.35f), 2, false);
                var grabCol = br.AddComponent<CapsuleCollider>(); grabCol.direction = 2; grabCol.radius = 0.05f; grabCol.height = 0.7f;
                var item = MakeGrabbable(br, 1f, false, 0.12f);
                var so = new SerializedObject(item.GetComponent<Grabbable>());
                so.FindProperty("_throwWhenUnselected").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                var bp = br.AddComponent<BranchPull>();
                bp.tunnel = bt; bp.side = side; bp.attach = attach;
            }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // ---------------------------------------------------------------- 5. fairy ring + berries
        [MenuItem("Growing Up/Build Interactions/5 Fairy Ring + Berries")]
        public static void BuildBerries()
        {
            var root = StationRoot("FairyRingBerries");
            var c = WorldBuilder.FairyRingCenter;
            float rr = WorldBuilder.RingRadius;

            // the fairy ring: mushrooms in a circle, an opening where the path comes in
            var ring = new GameObject("FairyRing (mushrooms)").transform; ring.SetParent(root, false);
            const int count = 18;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Vector3.Dot(dir, Vector3.back) > 0.97f) continue; // entrance from the path
                var p = c + dir * (rr + R(-0.05f, 0.05f));
                WorldBuilder.PlaceHDShroom(ring, p, R(0, 360), R(0.26f, 0.42f), i % 3 == 2 ? 2 : 0, i % 2);
                if (R(0f, 1f) < 0.4f) WorldBuilder.PlaceHDShroom(ring, p + dir * R(0.08f, 0.15f) + Vector3.Cross(dir, Vector3.up) * R(-0.1f, 0.1f), R(0, 360), R(0.1f, 0.18f), 0, i % 2);
            }
            AddGlow(ring);
            var ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/LightRing.png");
            if (ringTex == null) ringTex = MeshGen.RingTexture("Assets/_Project/Textures/LightRing.png");
            var glowMat = AdditiveMat("M_FairyRingGlow", ringTex, new Color(0.85f, 1f, 0.7f, 0.22f));
            var g = WorldBuilder.Prim(PrimitiveType.Quad, "RingGlow (ground)", ring, c + Vector3.up * 0.012f, Vector3.one * (rr + 0.2f) * 2f / 0.82f, glowMat, false, Quaternion.Euler(90f, 0f, 0f));
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var motes = StaticFuzz(ring, "FairyMotes", Vector3.zero, rr, 40, 0.012f, new Color(0.85f, 1f, 0.75f, 0.9f));
            motes.transform.position = c + Vector3.up * 0.15f;
            var mm = motes.main; mm.startLifetime = 3f; mm.prewarm = true;
            var mem = motes.emission; mem.rateOverTime = 12f; mem.SetBursts(new ParticleSystem.Burst[0]);
            var msh = motes.shape; msh.shapeType = ParticleSystemShapeType.Circle; msh.radius = rr; msh.radiusThickness = 0.2f;
            motes.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var mvel = motes.velocityOverLifetime; mvel.enabled = true; mvel.x = new ParticleSystem.MinMaxCurve(0f, 0f); mvel.y = new ParticleSystem.MinMaxCurve(0f, 0f); mvel.z = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            var mcol = motes.colorOverLifetime; mcol.enabled = true;
            var mgr = new Gradient(); mgr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
            mcol.color = mgr;
            motes.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sparkle.mat");

            // the berry bush in the middle (leafy, high detail)
            var bushMesh = MeshGen.LeafBush("HD_BerryBush", new Vector3(0.36f, 0.5f, 0.36f), 120, 77);
            var bush = new GameObject("BerryBush"); bush.transform.SetParent(root, false);
            bush.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, 30f, 0f));
            bush.AddComponent<MeshFilter>().sharedMesh = bushMesh;
            bush.AddComponent<MeshRenderer>().sharedMaterials = WorldBuilder.BushMats();
            var bcap = bush.AddComponent<CapsuleCollider>(); bcap.radius = 0.16f; bcap.height = 0.7f; bcap.center = new Vector3(0f, 0.35f, 0f);

            var berryMat = WorldBuilder.EmissiveMat("M_GlowBerry", new Color(0.9f, 0.12f, 0.4f), new Color(1f, 0.3f, 0.55f) * 2.5f);
            var leafM = WorldBuilder.Mat("M_BerryLeaf", new Color(0.25f, 0.5f, 0.15f), 0.3f);
            var items = new GrabbableItem[8];
            for (int i = 0; i < items.Length; i++)
            {
                // all around the bush, a bit more on the side facing the path, at child mouth/hand height
                float yaw = 180f + (i - items.Length * 0.5f) * 38f + R(-12f, 12f);
                float h = R(0.16f, 0.42f);
                var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                float rad = 0.34f * Mathf.Sqrt(Mathf.Max(0.2f, 1f - Mathf.Pow((h - 0.25f) / 0.42f, 2)));
                var p = c + dir * rad + Vector3.up * h;
                var berry = WorldBuilder.Prim(PrimitiveType.Sphere, "GlowBerry_" + i + " (eat me)", root, p, Vector3.one * 0.044f, berryMat, true);
                WorldBuilder.Prim(PrimitiveType.Sphere, "Leaf", berry.transform, new Vector3(0.25f, 0.55f, 0f), new Vector3(0.6f, 0.12f, 0.35f), leafM, false,
                    Quaternion.Euler(0, 0, 25f), true);
                items[i] = MakeGrabbable(berry, 0.01f, true, 0.035f);
                Highlight(berry.transform, Vector3.zero, 0.035f, StoryStep.Berries, items[i]);
            }

            var glow = new GameObject("BushGlow (sparkles)"); glow.transform.SetParent(root, false);
            glow.transform.position = c + Vector3.up * 0.3f;
            var ps = glow.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f); main.maxParticles = 80;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.75f), new Color(1f, 0.9f, 0.5f));
            var em = ps.emission; em.rateOverTime = 22f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f; sh.radiusThickness = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = gr;
            glow.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sparkle.mat");

            var bb = root.gameObject.AddComponent<BerryBush>();
            bb.berries = items; bb.bushGlow = ps;
            bb.appearSound = Sfx(root, "SFX_BerriesAppear", ProceduralAudio.Kind.Chime, 0.55f, root.InverseTransformPoint(c + Vector3.up * 0.3f), 12f);
            bb.eatSound = Sfx(root, "SFX_BerryCrunch", ProceduralAudio.Kind.Crunch, 1f, Vector3.zero, 6f);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // ---------------------------------------------------------------- adult: mosquitoes + dusk
        [MenuItem("Growing Up/Build Interactions/7 Adult dusk (mosquitoes, owls, no magic)")]
        public static void BuildAdultDusk()
        {
            var root = StationRoot("AdultDusk");
            // mosquitoes between the river and the log
            var ms = root.gameObject.AddComponent<MosquitoSwarm>();
            ms.zMin = WorldBuilder.LogZ + 0.4f; ms.zMax = WorldBuilder.RiverZ - WorldBuilder.RiverHalfWidth - 0.2f;
            var dark = new Color(0.04f, 0.04f, 0.04f, 0.9f);
            var sw = new GameObject("Mosquitoes").AddComponent<ParticleSystem>(); sw.transform.SetParent(root, false);
            sw.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = sw.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.007f, 0.013f); m.startColor = dark; m.maxParticles = 70;
            var em = sw.emission; em.rateOverTime = 0f;
            var sh = sw.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.35f; sh.radiusThickness = 0.6f;
            var no = sw.noise; no.enabled = true; no.strength = 0.35f; no.frequency = 1.6f; no.scrollSpeed = 1f;
            sw.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Droplet.mat");
            var sc = Burst(root, "SwatScatter", dark, dark, 18, 1.2f, 2.6f, 0.007f, 0.012f, 0.6f, 0f, false);
            var scs = sc.shape; scs.shapeType = ParticleSystemShapeType.Sphere; scs.radius = 0.1f;
            ms.swarm = sw; ms.scatter = sc;
            var bz = new GameObject("SFX_MosquitoBuzz"); bz.transform.SetParent(root, false);
            var bsrc = WorldBuilder.AddAudio(bz, ProceduralAudio.Kind.Buzz, ProceduralAudio.Category.Sfx, 0.5f, 3, 0.1f, 3f, true, true, false);
            bz.GetComponent<ProceduralAudio>().controlVolume = false;
            ms.buzz = bsrc;

            // dusk: owls hooting (adult only) and the child's magic hidden for the adult
            var gate = root.gameObject.AddComponent<AgeGate>();
            var owls = new System.Collections.Generic.List<AudioSource>();
            float[] zs = { 2.5f, 9f, 16f, 23f };
            for (int i = 0; i < zs.Length; i++)
            {
                var o = new GameObject("SFX_Owl_" + i); o.transform.SetParent(root, false);
                o.transform.position = WorldBuilder.OnPath(zs[i], (i % 2 == 0 ? -1f : 1f) * R(4f, 7f)) + Vector3.up * R(4f, 6f);
                var src = WorldBuilder.AddAudio(o, ProceduralAudio.Kind.Owl, ProceduralAudio.Category.Sfx, 0.6f, 50 + i * 13, 2f, 30f, true, true, false);
                o.GetComponent<ProceduralAudio>().controlVolume = false;
                owls.Add(src);
            }
            gate.adultOnlySounds = owls.ToArray(); gate.adultVolume = 0.55f;
            var magic = new System.Collections.Generic.List<GameObject>();
            foreach (var path in new[] { "World/Interactions/FairyRingBerries/FairyRing (mushrooms)/RingGlow (ground)",
                                         "World/Interactions/FairyRingBerries/FairyRing (mushrooms)/FairyMotes",
                                         "World/Interactions/FairyRingBerries/BushGlow (sparkles)" })
            { var g = GameObject.Find(path); if (g != null) magic.Add(g); }
            gate.childOnly = magic.ToArray();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        /// Child-only magical glow around an interactable: a soft pulsing halo + small rising sparkles.
        public static ChildHighlight Highlight(Transform target, Vector3 localCenter, float radius, StoryStep? step = null,
            GrabbableItem item = null, Dandelion dandelion = null)
        {
            var old = target.Find("ChildGlow"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var col = new Color(1f, 0.88f, 0.55f);
            var halo = new GameObject("ChildGlow").AddComponent<ParticleSystem>();
            halo.transform.SetParent(target, false); halo.transform.localPosition = localCenter;
            halo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = halo.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.scalingMode = ParticleSystemScalingMode.Shape;
            m.startLifetime = 1.6f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(radius * 3.0f, radius * 3.6f);
            m.startColor = new Color(col.r, col.g, col.b, 0.38f); m.maxParticles = 8;
            var em = halo.emission; em.rateOverTime = 3f;
            var sh = halo.shape; sh.enabled = false;
            var c1 = halo.colorOverLifetime; c1.enabled = true;
            var g1 = new Gradient(); g1.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.5f), new GradientAlphaKey(0, 1) });
            c1.color = g1;
            var hr = halo.GetComponent<ParticleSystemRenderer>(); hr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sparkle.mat");
            hr.maxParticleSize = 5f;

            var sp = new GameObject("Sparkles").AddComponent<ParticleSystem>();
            sp.transform.SetParent(halo.transform, false);
            sp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm = sp.main; sm.loop = true; sm.playOnAwake = false; sm.simulationSpace = ParticleSystemSimulationSpace.World;
            sm.scalingMode = ParticleSystemScalingMode.Shape;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f); sm.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f); sm.startColor = new Color(1f, 0.95f, 0.7f, 1f); sm.maxParticles = 40;
            var sem = sp.emission; sem.rateOverTime = 16f;
            var ssh = sp.shape; ssh.shapeType = ParticleSystemShapeType.Sphere; ssh.radius = radius; ssh.radiusThickness = 0.2f;
            var vel = sp.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0.06f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            var c2 = sp.colorOverLifetime; c2.enabled = true; c2.color = g1;
            var spr = sp.GetComponent<ParticleSystemRenderer>(); spr.sharedMaterial = hr.sharedMaterial;

            var ch = target.gameObject.AddComponent<ChildHighlight>();
            ch.fx = halo; ch.followObject = false;
            if (step.HasValue) { ch.onlyDuringStep = true; ch.step = step.Value; }
            ch.item = item; ch.dandelion = dandelion;
            return ch;
        }

        static Material AdditiveMat(string name, Texture2D tex, Color c)
        {
            string p = "Assets/_Project/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, p); }
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); // additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0); m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_BLENDMODE_ADD");
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- story: intro, transformation, ending
        [MenuItem("Growing Up/Build Interactions/6 Story (intro, transformation, ending)")]
        public static void BuildStory()
        {
            var story = StoryRoot();
            var sm = story.GetComponent<StoryManager>();
            sm.stationsRoot = GameObject.Find("World/Stations").transform;
            var door = GameObject.Find("World/House/DoorHinge").GetComponent<HouseDoor>();
            var houseVo = GameObject.Find("World/House/VO_Mother_HaveFun").GetComponent<AudioSource>();

            // Intro
            var intro = Child(story, "Intro").GetComponent<IntroSequence>();
            if (intro == null) intro = story.Find("Intro").gameObject.AddComponent<IntroSequence>();
            intro.door = door; intro.motherVoice = houseVo;
            intro.motherLine = "Mum: \"Go have fun, don't be late!\"";

            // Player FX (part of the player): overlay on the eyes, light waves + sparkles under PlayerAge
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            var eye = rig.centerEyeAnchor;
            var ov = eye.Find("ScreenOverlay"); if (ov != null) Object.DestroyImmediate(ov.gameObject);
            var fadeMat = WorldBuilder.ShaderMat("M_ScreenFade", "GrowingUp/ScreenFade");
            var sphere = WorldBuilder.Prim(PrimitiveType.Sphere, "ScreenOverlay", eye, Vector3.zero, Vector3.one * 0.6f, fadeMat, false, null, true);
            var sr = sphere.GetComponent<MeshRenderer>(); sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
            sphere.AddComponent<ScreenOverlay>().sphere = sr;

            var perception = GameObject.Find("PlayerAge/Perception").transform;
            var oldFx = perception.Find("TransformFX"); if (oldFx != null) Object.DestroyImmediate(oldFx.gameObject);
            var fx = new GameObject("TransformFX").transform; fx.SetParent(perception, false);

            var ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/LightRing.png");
            if (ringTex == null) ringTex = MeshGen.RingTexture("Assets/_Project/Textures/LightRing.png");
            var waveMat = AdditiveMat("M_LightWave", ringTex, Color.white);
            var waves = new GameObject("LightWaves").AddComponent<ParticleSystem>(); waves.transform.SetParent(fx, false);
            waves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var wm = waves.main; wm.loop = true; wm.playOnAwake = false; wm.simulationSpace = ParticleSystemSimulationSpace.World;
            wm.scalingMode = ParticleSystemScalingMode.Shape; wm.startLifetime = 2f; wm.startSpeed = 0f; wm.startSize = 6f; wm.maxParticles = 20;
            var wem = waves.emission; wem.rateOverTime = 0f;
            var wsh = waves.shape; wsh.enabled = false;
            var wsz = waves.sizeOverLifetime; wsz.enabled = true; wsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.03f, 1f, 1f));
            var wvel = waves.velocityOverLifetime; wvel.enabled = true; wvel.space = ParticleSystemSimulationSpace.World; wvel.x = new ParticleSystem.MinMaxCurve(0f); wvel.y = new ParticleSystem.MinMaxCurve(0.35f); wvel.z = new ParticleSystem.MinMaxCurve(0f);
            var wcol = waves.colorOverLifetime; wcol.enabled = true;
            var wg = new Gradient(); wg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(0, 1) });
            wcol.color = wg;
            var wr = waves.GetComponent<ParticleSystemRenderer>(); wr.renderMode = ParticleSystemRenderMode.HorizontalBillboard; wr.sharedMaterial = waveMat;
            wr.maxParticleSize = 50f;

            var spark = new GameObject("TransformSparkles").AddComponent<ParticleSystem>(); spark.transform.SetParent(fx, false);
            spark.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm2 = spark.main; sm2.loop = true; sm2.playOnAwake = false; sm2.simulationSpace = ParticleSystemSimulationSpace.World;
            sm2.scalingMode = ParticleSystemScalingMode.Shape; sm2.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            sm2.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.4f); sm2.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.025f); sm2.maxParticles = 300;
            var sem = spark.emission; sem.rateOverTime = 0f;
            var ssh = spark.shape; ssh.shapeType = ParticleSystemShapeType.Sphere; ssh.radius = 0.7f; ssh.radiusThickness = 0.4f;
            var sno = spark.noise; sno.enabled = true; sno.strength = 0.3f; sno.frequency = 0.8f;
            var scol = spark.colorOverLifetime; scol.enabled = true; scol.color = wg;
            var spr = spark.GetComponent<ParticleSystemRenderer>(); spr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sparkle.mat");
            spr.maxParticleSize = 0.2f;

            AudioSource Voice(string n, ProceduralAudio.Kind kind, float vol, bool loop, bool controlVolume)
            {
                var go = new GameObject(n); go.transform.SetParent(fx, false);
                var src = WorldBuilder.AddAudio(go, kind, ProceduralAudio.Category.Sfx, vol, 31 + (int)kind, 1f, 10f, false, loop, false);
                go.GetComponent<ProceduralAudio>().controlVolume = controlVolume;
                return src;
            }
            var echo = new GameObject("VO_Mother_Echo"); echo.transform.SetParent(fx, false);
            var echoSrc = echo.AddComponent<AudioSource>(); echoSrc.playOnAwake = false; echoSrc.spatialBlend = 0f; echoSrc.volume = 0.8f;
            var ef = echo.AddComponent<AudioEchoFilter>(); ef.delay = 380f; ef.decayRatio = 0.5f; ef.wetMix = 0.8f; ef.dryMix = 0.7f;
            var rv = echo.AddComponent<AudioReverbFilter>(); rv.reverbPreset = AudioReverbPreset.Cave;

            // GrowingUp sequence
            var gu = Child(story, "GrowingUp").GetComponent<GrowingUpSequence>();
            if (gu == null) gu = story.Find("GrowingUp").gameObject.AddComponent<GrowingUpSequence>();
            gu.lightWaves = waves; gu.sparkles = spark;
            gu.motherEcho = echoSrc; gu.introMotherVoice = houseVo;
            gu.laughter = Voice("SFX_ChildrenLaughter", ProceduralAudio.Kind.Laughter, 0.55f, false, true);
            gu.voices = Voice("SFX_ChildrenVoices", ProceduralAudio.Kind.ChildVoices, 0.5f, true, false);
            gu.shimmer = Voice("SFX_TransformShimmer", ProceduralAudio.Kind.Shimmer, 0.6f, false, true);
            gu.sun = GameObject.Find("Sun").GetComponent<Light>();
            gu.house = GameObject.Find("World/House").transform;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.55f), 0.35f),
                                 new GradientColorKey(new Color(0.55f, 0.45f, 0.75f), 0.7f), new GradientColorKey(new Color(0.08f, 0.08f, 0.16f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            gu.fxColour = grad;
            gu.sunsetIntensity = 0.85f; gu.sunsetElevation = 9f; gu.sunsetAmbient = 0.8f; gu.sunsetColour = new Color(1f, 0.58f, 0.32f);
            gu.lateLine = "<i>\"Oh, it's getting late. I should go home.\"</i>";
            gu.houseLine = "<i>\"It's been so long. I wonder if the old house is still there.\"</i>";

            // Ending
            var en = Child(story, "Ending").GetComponent<EndingSequence>();
            if (en == null) en = story.Find("Ending").gameObject.AddComponent<EndingSequence>();
            en.door = door;
            EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
            return t;
        }
    }
}
