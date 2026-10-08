using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Decor;
using DivergentGenesis.Render;

namespace DivergentGenesis.Living
{
    public enum RigPart : byte
    {
        Body = 0, Head = 1, Snout = 2, Leg = 3, Arm = 4, Neck = 5, Tail = 6,
        Wing = 7, WingTip = 8, Extra = 9, Ear = 10, Cap = 11, Jaw = 12, Eye = 13
    }

    /// <summary>
    /// A live handle on one creature's body. Every transform in here is moved by
    /// the animator each frame - there is no rig asset and no skinning anywhere.
    /// </summary>
    public sealed class EntityRig
    {
        public GameObject Root;
        public Transform Body;
        public Transform Head;
        public Transform Snout;
        public Transform Jaw;
        public Transform Neck;          // first segment of the neck chain
        public Transform[] NeckChain;
        public Transform Tail;          // first segment of the tail chain
        public Transform[] TailChain;
        public Transform[] Legs;        // FL, FR, BL, BR
        public Transform[] Arms;
        public Transform WingL, WingR, WingTipL, WingTipR;
        public Transform Extra;

        /// <summary>Body centre height above the entity's feet.</summary>
        public float BodyY = 0.8f;
        public bool Biped;
        public bool Bird;
        public bool Flyer;

        public void SetVisible(bool v)
        {
            if (Root != null) Root.SetActive(v);
        }
    }

    /// <summary>
    /// Builds creature bodies from primitives.
    ///
    /// Meshes are cached per (mob, part, mirrored), so 40 rabbits in a field share
    /// six meshes between them and the GPU sees one vertex buffer per limb type.
    /// </summary>
    public static class EntityMeshFactory
    {
        private static readonly Dictionary<int, Mesh> Cache = new Dictionary<int, Mesh>(256);

        private static int Key(MobId mob, RigPart part, bool mirror)
        {
            return ((int)mob << 5) | ((int)part << 1) | (mirror ? 1 : 0);
        }

        private static Mesh Piece(MobId mob, RigPart part, bool mirror, System.Action<PrimitiveMesher.Builder> build)
        {
            int k = Key(mob, part, mirror);
            Mesh m;
            if (Cache.TryGetValue(k, out m) && m != null) return m;

            var b = new PrimitiveMesher.Builder();
            build(b);
            if (mirror) Mirror(b);

            // Vertex colour alpha is the emissive mask that DG/Entity reads. The
            // builder writes opaque alpha by default, which would make every
            // creature glow, so it is rewritten here: eyes burn, everything else
            // stays opaque and lit. Parts that should glow as a whole (wisp cores,
            // lanterns, breath) are drawn with GlowSub instead.
            byte emissive = (byte)(part == RigPart.Eye ? 255 : 0);
            for (int i = 0; i < b.C.Count; i++)
            {
                var c = b.C[i];
                c.a = emissive;
                b.C[i] = c;
            }

            m = b.ToMesh("Rig_" + mob + "_" + part + (mirror ? "_m" : ""));
            Cache[k] = m;
            return m;
        }

        private static void Mirror(PrimitiveMesher.Builder b)
        {
            for (int i = 0; i < b.V.Count; i++)
            {
                var v = b.V[i];
                b.V[i] = new Vector3(-v.x, v.y, v.z);
                var n = b.N[i];
                b.N[i] = new Vector3(-n.x, n.y, n.z);
            }
            for (int i = 0; i < b.T.Count; i += 3)
            {
                int t = b.T[i];
                b.T[i] = b.T[i + 2];
                b.T[i + 2] = t;
            }
        }

        private static GameObject Sub(string name, Transform parent, Mesh mesh, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Entity;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return go;
        }

        private static GameObject GlowSub(string name, Transform parent, Mesh mesh, Vector3 localPos)
        {
            var go = Sub(name, parent, mesh, localPos);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.EntityGlow;
            return go;
        }

        /// <summary>Builds the whole hierarchy for one creature.</summary>
        public static EntityRig Build(MobDef d)
        {
            var rig = new EntityRig();
            var root = new GameObject("Mob_" + d.Id);
            rig.Root = root;

            var bodyParent = new GameObject("Root").transform;
            bodyParent.SetParent(root.transform, false);
            bodyParent.localPosition = Vector3.zero;

            float s = d.Scale;
            float len = d.BodyLength * s;
            float wid = d.BodyWidth * s;
            float leg = d.LegLength * s;
            float head = d.HeadSize * s;
            Color32 pc = d.Primary, sc = d.Secondary, ac = d.Accent;

            switch (d.Body)
            {
                case EntityArchetype.Biped: BuildBiped(rig, d, bodyParent, len, wid, leg, head, pc, sc, ac); break;
                case EntityArchetype.Bird: BuildBird(rig, d, bodyParent, len, wid, leg, head, pc, sc, ac); break;
                case EntityArchetype.Blob: BuildBlob(rig, d, bodyParent, wid, head, pc, sc, ac); break;
                case EntityArchetype.Dragon: BuildDragon(rig, d, bodyParent, len, wid, leg, head, pc, sc, ac); break;
                case EntityArchetype.Wisp: BuildWisp(rig, d, bodyParent, wid, head, pc, sc, ac); break;
                default: BuildQuadruped(rig, d, bodyParent, len, wid, leg, head, pc, sc, ac); break;
            }

            return rig;
        }

        // ============================================================== quadruped
        private static void BuildQuadruped(EntityRig rig, MobDef d, Transform root,
                                            float len, float wid, float leg, float head,
                                            Color32 pc, Color32 sc, Color32 ac)
        {
            float bodyY = leg + wid * 0.5f;
            rig.BodyY = bodyY;
            rig.Flyer = d.Flying;

            var body = Sub("Body", root, Piece(d.Id, RigPart.Body, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(wid, wid, len), pc);
                b.Box(new Vector3(0f, -wid * 0.22f, len * 0.12f), new Vector3(wid * 1.02f, wid * 0.42f, len * 0.78f), sc);
            }), new Vector3(0f, bodyY, 0f));
            rig.Body = body.transform;

            var neck = Sub("Neck", body.transform, Piece(d.Id, RigPart.Neck, false, b =>
            {
                b.Box(new Vector3(0f, head * 0.35f, len * 0.34f), new Vector3(wid * 0.55f, head * 0.85f, wid * 0.55f), pc);
            }), Vector3.zero);
            rig.Neck = neck.transform;

            var headGo = Sub("Head", neck.transform, Piece(d.Id, RigPart.Head, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(head, head * 0.92f, head * 1.15f), pc);
            }), new Vector3(0f, head * 0.78f, len * 0.34f));
            rig.Head = headGo.transform;

            Sub("Snout", rig.Head, Piece(d.Id, RigPart.Snout, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(head * 0.48f, head * 0.40f, head * 0.55f), sc);
            }), new Vector3(0f, -head * 0.12f, head * 0.72f));

            if (d.Health >= 8f)
            {
                Sub("EarL", rig.Head, Piece(d.Id, RigPart.Ear, false, b =>
                    b.Box(Vector3.zero, new Vector3(head * 0.18f, head * 0.52f, head * 0.10f), ac)),
                    new Vector3(-head * 0.34f, head * 0.60f, -head * 0.10f));
                Sub("EarR", rig.Head, Piece(d.Id, RigPart.Ear, true, b =>
                    b.Box(Vector3.zero, new Vector3(head * 0.18f, head * 0.52f, head * 0.10f), ac)),
                    new Vector3(head * 0.34f, head * 0.60f, -head * 0.10f));
            }

            var legs = new Transform[4];
            float lx = wid * 0.62f, lz = len * 0.32f;
            float[] xs = { -lx, lx, -lx, lx };
            float[] zs = { lz, lz, -lz, -lz };
            string[] names = { "LegFL", "LegFR", "LegBL", "LegBR" };
            for (int i = 0; i < 4; i++)
            {
                var l = Sub(names[i], root, Piece(d.Id, RigPart.Leg, i == 1 || i == 3, b =>
                {
                    b.Box(Vector3.zero, new Vector3(wid * 0.26f, leg, wid * 0.26f), sc);
                    b.Box(new Vector3(0f, -leg * 0.44f, wid * 0.10f), new Vector3(wid * 0.30f, leg * 0.14f, wid * 0.42f), ac);
                }), new Vector3(xs[i], leg, zs[i]));
                legs[i] = l.transform;
            }
            rig.Legs = legs;

            var tail = Sub("Tail", body.transform, Piece(d.Id, RigPart.Tail, false, b =>
            {
                b.Box(new Vector3(0f, wid * 0.10f, -len * 0.30f), new Vector3(wid * 0.30f, wid * 0.30f, len * 0.34f), sc);
                b.Box(new Vector3(0f, wid * 0.28f, -len * 0.48f), new Vector3(wid * 0.34f, wid * 0.34f, wid * 0.30f), ac);
            }), Vector3.zero);
            rig.Tail = tail.transform;
            rig.TailChain = new[] { tail.transform };
        }

        // ================================================================== biped
        private static void BuildBiped(EntityRig rig, MobDef d, Transform root,
                                       float len, float wid, float leg, float head,
                                       Color32 pc, Color32 sc, Color32 ac)
        {
            float torso = wid * 1.5f;
            float bodyY = leg + torso * 0.5f;
            rig.BodyY = bodyY;
            rig.Biped = true;

            var body = Sub("Body", root, Piece(d.Id, RigPart.Body, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(wid, torso, wid * 0.62f), sc);
                b.Box(new Vector3(0f, -torso * 0.42f, 0f), new Vector3(wid * 1.02f, torso * 0.24f, wid * 0.66f), ac);
            }), new Vector3(0f, bodyY, 0f));
            rig.Body = body.transform;

            var headGo = Sub("Head", body.transform, Piece(d.Id, RigPart.Head, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(head, head, head * 0.95f), pc);
            }), new Vector3(0f, torso * 0.5f + head * 0.5f, 0f));
            rig.Head = headGo.transform;

            // nose, so you can tell which way it is facing from any angle
            Sub("Snout", rig.Head, Piece(d.Id, RigPart.Snout, false, b =>
                b.Box(Vector3.zero, new Vector3(head * 0.34f, head * 0.30f, head * 0.34f), ac)),
                new Vector3(0f, -head * 0.10f, head * 0.62f));

            Sub("ArmL", body.transform, Piece(d.Id, RigPart.Arm, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(wid * 0.24f, torso * 0.92f, wid * 0.24f), pc);
                b.Box(new Vector3(0f, -torso * 0.44f, 0f), new Vector3(wid * 0.28f, wid * 0.28f, wid * 0.28f), ac);
            }), new Vector3(-wid * 0.62f, torso * 0.34f, 0f));
            Sub("ArmR", body.transform, Piece(d.Id, RigPart.Arm, true, b =>
            {
                b.Box(Vector3.zero, new Vector3(wid * 0.24f, torso * 0.92f, wid * 0.24f), pc);
                b.Box(new Vector3(0f, -torso * 0.44f, 0f), new Vector3(wid * 0.28f, wid * 0.28f, wid * 0.28f), ac);
            }), new Vector3(wid * 0.62f, torso * 0.34f, 0f));

            rig.Arms = new Transform[2];
            rig.Arms[0] = body.transform.Find("ArmL");
            rig.Arms[1] = body.transform.Find("ArmR");

            var legs = new Transform[2];
            legs[0] = Sub("LegL", root, Piece(d.Id, RigPart.Leg, false, b =>
                b.Box(Vector3.zero, new Vector3(wid * 0.30f, leg, wid * 0.30f), sc)),
                new Vector3(-wid * 0.28f, leg, 0f)).transform;
            legs[1] = Sub("LegR", root, Piece(d.Id, RigPart.Leg, true, b =>
                b.Box(Vector3.zero, new Vector3(wid * 0.30f, leg, wid * 0.30f), sc)),
                new Vector3(wid * 0.28f, leg, 0f)).transform;
            rig.Legs = legs;
        }

        // =================================================================== bird
        private static void BuildBird(EntityRig rig, MobDef d, Transform root,
                                      float len, float wid, float leg, float head,
                                      Color32 pc, Color32 sc, Color32 ac)
        {
            float bodyY = leg + wid * 0.6f;
            rig.BodyY = bodyY;
            rig.Bird = true;
            rig.Flyer = d.Flying;

            var body = Sub("Body", root, Piece(d.Id, RigPart.Body, false, b =>
            {
                b.Sphere(Vector3.zero, wid * 0.95f, 8, 6, pc, 1.25f);
                b.Sphere(new Vector3(0f, -wid * 0.15f, -len * 0.25f), wid * 0.62f, 6, 5, sc, 1.0f);
            }), new Vector3(0f, bodyY, 0f));
            rig.Body = body.transform;

            var headGo = Sub("Head", body.transform, Piece(d.Id, RigPart.Head, false, b =>
            {
                b.Sphere(Vector3.zero, head * 0.85f, 7, 6, pc);
            }), new Vector3(0f, wid * 0.85f, len * 0.22f));
            rig.Head = headGo.transform;

            Sub("Beak", rig.Head, Piece(d.Id, RigPart.Snout, false, b =>
                b.Box(Vector3.zero, new Vector3(head * 0.34f, head * 0.26f, head * 0.82f), ac)),
                new Vector3(0f, -head * 0.06f, head * 0.72f));

            Sub("Comb", rig.Head, Piece(d.Id, RigPart.Cap, false, b =>
                b.Box(Vector3.zero, new Vector3(head * 0.14f, head * 0.44f, head * 0.62f), ac)),
                new Vector3(0f, head * 0.72f, 0f));

            var legs = new Transform[2];
            legs[0] = Sub("LegL", root, Piece(d.Id, RigPart.Leg, false, b =>
                b.Box(Vector3.zero, new Vector3(wid * 0.16f, leg, wid * 0.16f), ac)),
                new Vector3(-wid * 0.34f, leg, 0f)).transform;
            legs[1] = Sub("LegR", root, Piece(d.Id, RigPart.Leg, true, b =>
                b.Box(Vector3.zero, new Vector3(wid * 0.16f, leg, wid * 0.16f), ac)),
                new Vector3(wid * 0.34f, leg, 0f)).transform;
            rig.Legs = legs;

            var wingL = Sub("WingL", body.transform, Piece(d.Id, RigPart.Wing, false, b =>
                b.Box(Vector3.zero, new Vector3(wid * 1.15f, wid * 0.14f, wid * 0.85f), sc)), Vector3.zero);
            var wingR = Sub("WingR", body.transform, Piece(d.Id, RigPart.Wing, true, b =>
                b.Box(Vector3.zero, new Vector3(wid * 1.15f, wid * 0.14f, wid * 0.85f), sc)), Vector3.zero);
            rig.WingL = wingL.transform;
            rig.WingR = wingR.transform;
        }

        // =================================================================== blob
        private static void BuildBlob(EntityRig rig, MobDef d, Transform root,
                                      float wid, float head, Color32 pc, Color32 sc, Color32 ac)
        {
            float bodyY = wid * 1.05f;
            rig.BodyY = bodyY;
            rig.Flyer = d.Flying;

            var body = Sub("Body", root, Piece(d.Id, RigPart.Body, false, b =>
            {
                b.Sphere(Vector3.zero, wid, 10, 8, pc, 0.92f);
                b.Sphere(new Vector3(0f, -wid * 0.30f, 0f), wid * 0.86f, 8, 6, sc, 0.42f);
            }), new Vector3(0f, bodyY, 0f));
            rig.Body = body.transform;

            // two eyes and a tiny mouth - this is the entire face budget
            Sub("EyeL", body.transform, Piece(d.Id, RigPart.Eye, false, b =>
                b.Sphere(Vector3.zero, head * 0.44f, 6, 5, ac)), new Vector3(-wid * 0.34f, wid * 0.24f, wid * 0.78f));
            Sub("EyeR", body.transform, Piece(d.Id, RigPart.Eye, true, b =>
                b.Sphere(Vector3.zero, head * 0.44f, 6, 5, ac)), new Vector3(wid * 0.34f, wid * 0.24f, wid * 0.78f));
            Sub("Mouth", body.transform, Piece(d.Id, RigPart.Snout, false, b =>
                b.Box(Vector3.zero, new Vector3(wid * 0.34f, wid * 0.12f, wid * 0.12f), sc)),
                new Vector3(0f, -wid * 0.22f, wid * 0.88f));

            rig.Head = body.transform;
            var tail = Sub("Tail", body.transform, Piece(d.Id, RigPart.Tail, false, b =>
                b.Box(Vector3.zero, new Vector3(wid * 0.20f, wid * 0.20f, wid * 0.9f), sc)),
                new Vector3(0f, 0f, -wid * 0.9f));
            rig.Tail = tail.transform;
            rig.TailChain = new[] { tail.transform };
        }

        // ================================================================== wisp
        private static void BuildWisp(EntityRig rig, MobDef d, Transform root,
                                      float wid, float head, Color32 pc, Color32 sc, Color32 ac)
        {
            rig.BodyY = wid * 2.2f;
            rig.Flyer = true;
            rig.Bird = true;

            var body = GlowSub("Body", root, Piece(d.Id, RigPart.Body, false, b =>
            {
                b.Sphere(Vector3.zero, wid, 8, 7, pc);
                b.Sphere(new Vector3(0f, 0f, 0f), wid * 1.4f, 8, 6, sc);
            }), new Vector3(0f, rig.BodyY, 0f));
            rig.Body = body.transform;

            Sub("Head", body.transform, Piece(d.Id, RigPart.Head, false, b =>
                b.Sphere(Vector3.zero, head * 0.9f, 6, 5, ac)), new Vector3(0f, 0f, wid * 0.9f));
            rig.Head = body.transform.Find("Head");

            var ring = new GameObject("Extra");
            ring.transform.SetParent(body.transform, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                GlowSub("Spark" + i, ring.transform, Piece(d.Id, RigPart.Extra, false, b =>
                    b.Sphere(Vector3.zero, wid * 0.28f, 5, 4, ac)),
                    new Vector3(Mathf.Cos(a) * wid * 2.1f, Mathf.Sin(a * 2f) * wid * 0.5f, Mathf.Sin(a) * wid * 2.1f));
            }
            rig.Extra = ring.transform;

            var wings = new GameObject("Wings");
            wings.transform.SetParent(body.transform, false);
            var wl = Sub("WingL", wings.transform, Piece(d.Id, RigPart.Wing, false, b =>
                b.Box(Vector3.zero, new Vector3(wid * 1.6f, wid * 0.10f, wid * 1.0f), sc)),
                new Vector3(-wid * 0.9f, 0f, 0f));
            var wr = Sub("WingR", wings.transform, Piece(d.Id, RigPart.Wing, true, b =>
                b.Box(Vector3.zero, new Vector3(wid * 1.6f, wid * 0.10f, wid * 1.0f), sc)),
                new Vector3(wid * 0.9f, 0f, 0f));
            rig.WingL = wl.transform;
            rig.WingR = wr.transform;
        }

        // ================================================================= dragon
        private static void BuildDragon(EntityRig rig, MobDef d, Transform root,
                                        float len, float wid, float leg, float head,
                                        Color32 pc, Color32 sc, Color32 ac)
        {
            float bodyY = leg + wid * 0.85f;
            rig.BodyY = bodyY;
            rig.Flyer = true;

            var body = Sub("Body", root, Piece(d.Id, RigPart.Body, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(wid, wid * 1.05f, len * 0.55f), pc);
                b.Box(new Vector3(0f, wid * 0.45f, -len * 0.06f), new Vector3(wid * 0.7f, wid * 0.35f, len * 0.4f), sc);
            }), new Vector3(0f, bodyY, 0f));
            rig.Body = body.transform;

            // ---- neck chain: five nested segments so the head leads the body
            var chain = new Transform[5];
            Transform parent = body.transform;
            var pos = new Vector3(0f, wid * 0.30f, len * 0.28f);
            for (int i = 0; i < 5; i++)
            {
                float size = wid * (0.62f - i * 0.055f);
                var seg = Sub("Neck" + i, parent, Piece(d.Id, RigPart.Neck, false, b =>
                {
                    b.Box(Vector3.zero, new Vector3(size, size, len * 0.20f), i % 2 == 0 ? pc : sc);
                    b.Box(new Vector3(0f, size * 0.55f, 0f), new Vector3(size * 0.34f, size * 0.55f, size * 0.34f), ac);
                }), pos);
                chain[i] = seg.transform;
                parent = seg.transform;
                pos = new Vector3(0f, 0f, len * 0.19f);
            }
            rig.Neck = chain[0];
            rig.NeckChain = chain;

            var headGo = Sub("Head", chain[4], Piece(d.Id, RigPart.Head, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(head, head * 0.85f, head * 1.35f), pc);
                b.Box(new Vector3(0f, head * 0.55f, -head * 0.15f), new Vector3(head * 0.36f, head * 0.75f, head * 0.36f), ac);
                b.Box(new Vector3(0f, head * 0.62f, -head * 0.95f), new Vector3(head * 0.26f, head * 0.5f, head * 0.26f), ac);
            }), new Vector3(0f, 0f, len * 0.19f + head * 0.5f));
            rig.Head = headGo.transform;

            var jaw = Sub("Jaw", headGo.transform, Piece(d.Id, RigPart.Jaw, false, b =>
                b.Box(Vector3.zero, new Vector3(head * 0.72f, head * 0.22f, head * 1.1f), sc)),
                new Vector3(0f, -head * 0.42f, head * 0.22f));
            rig.Jaw = jaw.transform;

            Sub("EyeL", rig.Head, Piece(d.Id, RigPart.Eye, false, b =>
                b.Sphere(Vector3.zero, head * 0.17f, 5, 4, ac)), new Vector3(-head * 0.42f, head * 0.22f, head * 0.52f));
            Sub("EyeR", rig.Head, Piece(d.Id, RigPart.Eye, true, b =>
                b.Sphere(Vector3.zero, head * 0.17f, 5, 4, ac)), new Vector3(head * 0.42f, head * 0.22f, head * 0.52f));

            // ---- tail chain
            var tail = new Transform[5];
            parent = body.transform;
            pos = new Vector3(0f, wid * 0.14f, -len * 0.26f);
            for (int i = 0; i < 5; i++)
            {
                float size = wid * (0.60f - i * 0.095f);
                var seg = Sub("Tail" + i, parent, Piece(d.Id, RigPart.Tail, false, b =>
                {
                    b.Box(Vector3.zero, new Vector3(size, size, len * 0.18f), i % 2 == 0 ? pc : sc);
                    b.Box(new Vector3(0f, size * 0.5f, 0f), new Vector3(size * 0.3f, size * 0.5f, size * 0.3f), ac);
                }), pos);
                tail[i] = seg.transform;
                parent = seg.transform;
                pos = new Vector3(0f, 0f, -len * 0.17f);
            }
            rig.Tail = tail[0];
            rig.TailChain = tail;

            // ---- legs (front pair small, rear pair weight-bearing)
            var legs = new Transform[4];
            float lx = wid * 0.66f;
            for (int i = 0; i < 4; i++)
            {
                float lh = leg * (i < 2 ? 0.82f : 1.0f);
                float lz = i < 2 ? len * 0.16f : -len * 0.16f;
                string nm = i == 0 ? "LegFL" : i == 1 ? "LegFR" : i == 2 ? "LegBL" : "LegBR";
                var l = Sub(nm, root, Piece(d.Id, RigPart.Leg, i == 1 || i == 3, b =>
                {
                    b.Box(Vector3.zero, new Vector3(wid * 0.30f, lh, wid * 0.30f), sc);
                    b.Box(new Vector3(0f, -lh * 0.44f, wid * 0.18f), new Vector3(wid * 0.40f, lh * 0.16f, wid * 0.62f), ac);
                }), new Vector3(i % 2 == 0 ? -lx : lx, lh, lz));
                legs[i] = l.transform;
            }
            rig.Legs = legs;

            // ---- wings: shoulder + tip, so they fold as well as flap
            var wingL = Sub("WingL", body.transform, Piece(d.Id, RigPart.Wing, false, b =>
            {
                b.Box(Vector3.zero, new Vector3(len * 0.62f, wid * 0.16f, len * 0.34f), sc);
                b.Box(new Vector3(-len * 0.62f, 0f, -len * 0.10f), new Vector3(len * 0.30f, wid * 0.10f, len * 0.20f), ac);
            }), new Vector3(-wid * 0.5f, wid * 0.42f, len * 0.02f));

            var tipL = Sub("WingTipL", wingL.transform, Piece(d.Id, RigPart.WingTip, false, b =>
            {
                b.Box(new Vector3(-len * 0.34f, 0f, -len * 0.12f), new Vector3(len * 0.72f, wid * 0.10f, len * 0.30f), pc);
            }), Vector3.zero);

            var wingR = Sub("WingR", body.transform, Piece(d.Id, RigPart.Wing, true, b =>
            {
                b.Box(Vector3.zero, new Vector3(len * 0.62f, wid * 0.16f, len * 0.34f), sc);
                b.Box(new Vector3(-len * 0.62f, 0f, -len * 0.10f), new Vector3(len * 0.30f, wid * 0.10f, len * 0.20f), ac);
            }), new Vector3(wid * 0.5f, wid * 0.42f, len * 0.02f));

            var tipR = Sub("WingTipR", wingR.transform, Piece(d.Id, RigPart.WingTip, true, b =>
            {
                b.Box(new Vector3(-len * 0.34f, 0f, -len * 0.12f), new Vector3(len * 0.72f, wid * 0.10f, len * 0.30f), pc);
            }), Vector3.zero);

            rig.WingL = wingL.transform;
            rig.WingR = wingR.transform;
            rig.WingTipL = tipL.transform;
            rig.WingTipR = tipR.transform;
        }

        /// <summary>Frees every cached rig mesh. Only used on a hard world reset.</summary>
        public static void ClearCache()
        {
            foreach (var kv in Cache) if (kv.Value != null) Object.Destroy(kv.Value);
            Cache.Clear();
        }
    }
}