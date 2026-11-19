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
