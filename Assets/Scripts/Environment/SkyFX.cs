using UnityEngine;
using DivergentGenesis.Core;

namespace DivergentGenesis.Environment
{
    /// <summary>
    /// The one place that knows what colour the sky is *supposed* to be.
    ///
    /// SkyController still owns the day/night cycle and passes its result in; this
    /// then blends that toward the dimension's palette and, when the ritual has
    /// been performed, toward the shattered purple of a sky with cracks in it.
    /// Everything is a smooth float, so entering the Node or summoning the
    /// Sovereign is a gradient rather than a cut.
    /// </summary>
    public static class SkyFX
    {
        /// <summary>0 = overworld sky, 1 = Node sky.</summary>
        public static float NodeBlend { get; private set; }
        /// <summary>0 = intact, 1 = torn open by the Sovereign.</summary>
        public static float Corruption { get; private set; }
        /// <summary>Animated crack field offset, so two rituals do not look alike.</summary>
        public static float CrackSeed = 11.3f;

        private static float _nodeTarget;
        private static float _corruptTarget;
        private static float _flare;

        public static bool NodeSky { get { return _nodeTarget > 0.5f; } }

        public static void SetDimension(bool node)
        {
            _nodeTarget = node ? 1f : 0f;
        }

        /// <summary>Instant version, used when loading a save or teleporting.</summary>
        public static void Snap(bool node)
        {
            _nodeTarget = node ? 1f : 0f;
            NodeBlend = _nodeTarget;
        }

        public static void SetCorruption(float amount)
        {
            _corruptTarget = Mathf.Clamp01(amount);
        }

        public static void SnapCorruption(float amount)
        {
            _corruptTarget = Mathf.Clamp01(amount);
            Corruption = _corruptTarget;
        }

        /// <summary>A one-off surge, used the instant the boss arrives.</summary>
        public static void Pulse(float strength)
        {
            _flare = Mathf.Max(_flare, strength);
        }

        public static void Tick(float dt)
        {
            NodeBlend = Mathf.MoveTowards(NodeBlend, _nodeTarget, dt * 0.85f);
            float rate = _corruptTarget > Corruption ? 0.32f : 0.55f;
            Corruption = Mathf.MoveTowards(Corruption, _corruptTarget, dt * rate);
            _flare = Mathf.MoveTowards(_flare, 0f, dt * 0.9f);
        }

        /// <summary>How much crack geometry the sky shader should draw.</summary>
        public static float CrackAmount
        {
            get
            {
                float c = Corruption;
                return Mathf.Clamp01(c * 1.15f + _flare * 0.5f);
            }
        }

        public static float Flare { get { return _flare; } }

        /// <summary>
        /// Blends the day-cycle colours toward the active dimension, then toward
        /// the corruption palette. Called once per frame by SkyController.
        /// </summary>
        public static void Apply(ref Color zenith, ref Color horizon, ref Color fog,
                                 ref Color ambient, ref Color sun, float day)
        {
            day = Mathf.Clamp01(day);
            float dayLift = Mathf.Lerp(0.24f, 1f, day);

            if (NodeBlend > 0.001f)
            {
                float b = NodeBlend;
                zenith = Color.Lerp(zenith, NodeConfig.SkyZenith * dayLift, b);
                horizon = Color.Lerp(horizon, NodeConfig.SkyHorizon * dayLift, b * 0.95f);
                fog = Color.Lerp(fog, NodeConfig.SkyFog * Mathf.Lerp(0.35f, 1f, day), b * 0.9f);
                ambient = Color.Lerp(ambient, NodeConfig.SkyAmbient * dayLift, b * 0.85f);
                sun = Color.Lerp(sun, NodeConfig.SunColor, b * 0.7f);
            }

            if (Corruption > 0.001f)
            {
                float c = Corruption;
                zenith = Color.Lerp(zenith, NodeConfig.CorruptZenith * Mathf.Lerp(0.55f, 1f, day), c);
                horizon = Color.Lerp(horizon, NodeConfig.CorruptHorizon * Mathf.Lerp(0.45f, 1f, day), c * 0.95f);
                fog = Color.Lerp(fog, NodeConfig.CorruptFog, c * 0.9f);
                ambient = Color.Lerp(ambient, new Color(0.30f, 0.14f, 0.40f) * dayLift, c * 0.85f);
                sun = Color.Lerp(sun, new Color(0.95f, 0.62f, 0.98f), c * 0.55f);
            }
        }

        /// <summary>Fog thickens as the sky comes apart - it sells the dread.</summary>
        public static float FogScale(bool node)
        {
            float scale = node ? 1.35f : 1f;
            scale *= Mathf.Lerp(1f, 1.75f, Corruption);
            scale *= 1f + Flare * 0.6f;
            return scale;
        }
    }
}