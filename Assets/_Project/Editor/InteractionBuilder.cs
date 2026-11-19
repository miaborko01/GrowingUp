using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GrowingUp.EditorTools
{
    /// <summary>
    /// Phase 2: builds the child interactions on top of the world (menu Growing Up/Build Interactions,
    /// or one station at a time). Everything goes under World/Interactions/&lt;Station&gt; and is wired to the
    /// Story object. Grabbing always uses the Meta Grab Interaction building block (copied from
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
            BuildRiverCrossing();
            BuildBird();
            BuildDandelion();
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

        [MenuItem("Growing Up/Build Interactions/3 Dandelion")]
        public static void BuildDandelion()
        {
            var root = StationRoot("Dandelion");
            var pos = WorldBuilder.OnPath(WorldBuilder.DandelionZ, 0.6f);
            var green = WorldBuilder.Mat("M_DandelionStem", new Color(0.42f, 0.62f, 0.25f), 0.3f);
            var leafM = WorldBuilder.Mat("M_DandelionLeaf", new Color(0.30f, 0.52f, 0.17f), 0.25f);

            // leaf rosette stays in the ground (environment)
            var rosette = new GameObject("Dandelion_Leaves").transform; rosette.SetParent(root, false); rosette.position = pos;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + R(-12f, 12f);
                var lf = WorldBuilder.Prim(PrimitiveType.Sphere, "Leaf_" + i, rosette, Vector3.zero, new Vector3(0.035f, 0.006f, 0.2f), leafM, false,
                    Quaternion.Euler(-8f, a, 0f), true);
                lf.transform.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.006f, 0.09f);
            }

            // the picked part: stem + seed head
            const float H = 0.52f;
            var dand = new GameObject("Dandelion (grab me)");
            dand.transform.SetParent(root, false);
            dand.transform.position = pos;
            WorldBuilder.Prim(PrimitiveType.Cylinder, "Stem", dand.transform, new Vector3(0, H * 0.5f, 0), new Vector3(0.012f, H * 0.5f, 0.012f), green, false, null, true);
            var cap = dand.AddComponent<CapsuleCollider>(); cap.radius = 0.014f; cap.height = H + 0.05f; cap.center = new Vector3(0, (H + 0.05f) * 0.5f, 0);
            var head = new GameObject("SeedHead").transform; head.SetParent(dand.transform, false); head.localPosition = new Vector3(0, H + 0.01f, 0);
            WorldBuilder.Prim(PrimitiveType.Sphere, "Receptacle", head, Vector3.zero, Vector3.one * 0.022f, WorldBuilder.Mat("M_DandelionCore", new Color(0.55f, 0.45f, 0.30f)), false, null, true);
            var puff = new GameObject("Puff").transform; puff.SetParent(head, false);
            WorldBuilder.Prim(PrimitiveType.Sphere, "PuffShell", puff, Vector3.zero, Vector3.one * 0.115f, TransparentUnlit("M_DandelionPuff", new Color(1f, 1f, 1f, 0.28f)), false, null, true);
            StaticFuzz(puff, "PuffFuzz", Vector3.zero, 0.055f, 90, 0.014f, new Color(1f, 1f, 0.95f, 0.8f));
            var item = MakeGrabbable(dand, 0.05f, true, 0.04f);

            var burst = Burst(root, "SeedBurst (glowing seeds)", new Color(1f, 0.97f, 0.85f, 1f), new Color(1f, 0.85f, 0.45f, 1f),
                110, 0.15f, 0.55f, 0.012f, 0.028f, 7f, -0.015f, true);
            var bm = burst.main; bm.duration = 0.3f;
            var bsh = burst.shape; bsh.shapeType = ParticleSystemShapeType.Cone; bsh.angle = 55f; bsh.radius = 0.04f;
            var noise = burst.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.6f; noise.scrollSpeed = 0.3f;
            var drag = burst.limitVelocityOverLifetime; drag.enabled = true; drag.limit = 0.25f; drag.dampen = 0.15f;

            var d = root.gameObject.AddComponent<Dandelion>();
            d.item = item; d.head = head; d.puff = puff.gameObject; d.seedBurst = burst;
            d.whoosh = Sfx(root, "SFX_DandelionWhoosh", ProceduralAudio.Kind.Whoosh, 0.9f, Vector3.zero, 10f);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // ---------------------------------------------------------------- 2. bird + seeds
        [MenuItem("Growing Up/Build Interactions/2 Bird")]
        public static void BuildBird()
        {
            var root = StationRoot("Bird");
            var spot = WorldBuilder.OnPath(WorldBuilder.BirdZ, 0.75f);

            var bird = new GameObject("Bird (robin, primitives)");
            bird.transform.SetParent(root, false);
            bird.transform.position = spot;
            bird.transform.rotation = Quaternion.Euler(0f, 250f, 0f); // looking towards the path
            var brown = WorldBuilder.Mat("M_Bird_Brown", new Color(0.42f, 0.30f, 0.20f), 0.2f);
            var breast = WorldBuilder.Mat("M_Bird_Breast", new Color(0.95f, 0.45f, 0.15f), 0.2f);
            var cream = WorldBuilder.Mat("M_Bird_Cream", new Color(0.92f, 0.88f, 0.78f), 0.2f);
            var beakM = WorldBuilder.Mat("M_Bird_Beak", new Color(0.95f, 0.75f, 0.2f), 0.3f);
            var eyeM = WorldBuilder.Mat("M_Bird_Eye", new Color(0.03f, 0.03f, 0.03f), 0.9f);
            var model = new GameObject("Model").transform; model.SetParent(bird.transform, false);
            GameObject P(PrimitiveType t, string n, Transform parent, Vector3 lp, Vector3 ls, Material m, Vector3? euler = null)
                => WorldBuilder.Prim(t, n, parent, lp, ls, m, false, Quaternion.Euler(euler ?? Vector3.zero), true);
            P(PrimitiveType.Sphere, "Body", model, new Vector3(0, 0.07f, 0), new Vector3(0.085f, 0.08f, 0.12f), brown, new Vector3(-15f, 0, 0));
            P(PrimitiveType.Sphere, "Breast", model, new Vector3(0, 0.065f, 0.028f), new Vector3(0.075f, 0.07f, 0.075f), breast);
            P(PrimitiveType.Sphere, "Belly", model, new Vector3(0, 0.045f, 0.0f), new Vector3(0.06f, 0.045f, 0.07f), cream);
            P(PrimitiveType.Cube, "Tail", model, new Vector3(0, 0.085f, -0.075f), new Vector3(0.04f, 0.008f, 0.06f), brown, new Vector3(-25f, 0, 0));
            P(PrimitiveType.Cylinder, "LegL", model, new Vector3(0.015f, 0.015f, 0.005f), new Vector3(0.006f, 0.015f, 0.006f), beakM);
            P(PrimitiveType.Cylinder, "LegR", model, new Vector3(-0.015f, 0.015f, 0.005f), new Vector3(0.006f, 0.015f, 0.006f), beakM);
            var head = new GameObject("Head").transform; head.SetParent(model, false); head.localPosition = new Vector3(0, 0.115f, 0.04f);
            P(PrimitiveType.Sphere, "HeadBall", head, Vector3.zero, new Vector3(0.055f, 0.052f, 0.055f), brown);
            P(PrimitiveType.Sphere, "Face", head, new Vector3(0, -0.006f, 0.012f), new Vector3(0.045f, 0.04f, 0.035f), breast);
            P(PrimitiveType.Cube, "Beak", head, new Vector3(0, -0.004f, 0.034f), new Vector3(0.008f, 0.008f, 0.022f), beakM, new Vector3(8f, 0, 45f));
            P(PrimitiveType.Sphere, "EyeL", head, new Vector3(0.02f, 0.008f, 0.016f), Vector3.one * 0.011f, eyeM);
            P(PrimitiveType.Sphere, "EyeR", head, new Vector3(-0.02f, 0.008f, 0.016f), Vector3.one * 0.011f, eyeM);
            var wl = new GameObject("WingL").transform; wl.SetParent(model, false); wl.localPosition = new Vector3(0.038f, 0.085f, -0.005f);
            P(PrimitiveType.Sphere, "Wing", wl, new Vector3(0.006f, -0.004f, -0.01f), new Vector3(0.016f, 0.05f, 0.09f), brown, new Vector3(-12f, 0, 0));
            var wr = new GameObject("WingR").transform; wr.SetParent(model, false); wr.localPosition = new Vector3(-0.038f, 0.085f, -0.005f);
            P(PrimitiveType.Sphere, "Wing", wr, new Vector3(-0.006f, -0.004f, -0.01f), new Vector3(0.016f, 0.05f, 0.09f), brown, new Vector3(-12f, 0, 0));
            var bc = bird.AddComponent<BirdController>();
            bc.model = model; bc.head = head; bc.wingL = wl; bc.wingR = wr;
            bc.chirp = Sfx(bird.transform, "SFX_BirdChirp", ProceduralAudio.Kind.Chirp, 0.8f, new Vector3(0, 0.1f, 0), 12f);

            // seeds on a big leaf by the path, a little before the bird
            var leafPos = WorldBuilder.OnPath(WorldBuilder.BirdZ - 0.9f, -0.45f);
            WorldBuilder.Prim(PrimitiveType.Sphere, "Leaf", root, leafPos + Vector3.up * 0.004f, new Vector3(0.22f, 0.006f, 0.12f),
                WorldBuilder.Mat("M_Leaf", new Color(0.35f, 0.55f, 0.18f), 0.3f), false, Quaternion.Euler(0f, 35f, 0f));
            var seedMat = WorldBuilder.Mat("M_Seed", new Color(0.30f, 0.22f, 0.14f), 0.4f);
            var stripe = WorldBuilder.Mat("M_SeedStripe", new Color(0.85f, 0.80f, 0.65f), 0.4f);
            var seeds = new GrabbableItem[5];
            for (int i = 0; i < seeds.Length; i++)
            {
                var sp = leafPos + new Vector3(R(-0.06f, 0.06f), 0.03f, R(-0.03f, 0.03f));
                var seed = WorldBuilder.Prim(PrimitiveType.Sphere, "Seed_" + i + " (grab me)", root, sp, new Vector3(0.018f, 0.012f, 0.032f), seedMat, true,
                    Quaternion.Euler(0f, R(0, 360), 0f));
                WorldBuilder.Prim(PrimitiveType.Sphere, "Stripe", seed.transform, new Vector3(0, 0.15f, 0), new Vector3(0.35f, 0.9f, 0.95f), stripe, false, null, true);
                seeds[i] = MakeGrabbable(seed, 0.02f, false, 0.035f);
            }
            bc.seeds = seeds;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // ---------------------------------------------------------------- 1. river crossing
        [MenuItem("Growing Up/Build Interactions/1 River Crossing")]
        public static void BuildRiverCrossing()
        {
            DropletMat();
            var root = StationRoot("RiverCrossing");
            float rz = WorldBuilder.RiverZ;

            // the loose rock on the near bank, next to the path (same size as the stepping stones)
            var rockPos = WorldBuilder.OnPath(rz - 1.35f, 0.45f) + Vector3.up * 0.06f;
            var rock = WorldBuilder.Place(WorldBuilder.Nature + "Rock_01.prefab", root, rockPos, 40f, WorldBuilder.StoneScale, true);
            rock.name = "LooseRock (grab me)";
            var item = MakeGrabbable(rock, 1.2f, false, 0.06f);

            var slot = GameObject.Find("World/River/SteppingStones/RockSlot_Gap").transform;
            var foam = WorldBuilder.Prim(PrimitiveType.Quad, "FoamRing_Gap", root, slot.position + Vector3.up * 0.038f, Vector3.one * 0.42f,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_FoamRing.mat"), false, Quaternion.Euler(90f, 30f, 0f));
            foam.SetActive(false);

            var splash = Burst(root, "SplashParticles", new Color(0.85f, 0.95f, 1f, 0.9f), new Color(0.6f, 0.8f, 0.9f, 0.8f),
                40, 0.6f, 1.4f, 0.008f, 0.02f, 0.7f, 1f, false);
            var splashSnd = Sfx(root, "SFX_Splash", ProceduralAudio.Kind.Splash, 0.9f, Vector3.zero, 8f);

            var rc = root.gameObject.AddComponent<RiverCrossing>();
            rc.riverZ = rz; rc.halfWidth = WorldBuilder.RiverHalfWidth;
            rc.looseRock = item; rc.gapSlot = slot; rc.gapFoamRing = foam;
            rc.respawnPoint = GameObject.Find("World/Stations/Station_River").transform;
            rc.splashSound = splashSnd; rc.splashParticles = splash;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }
    }
}
