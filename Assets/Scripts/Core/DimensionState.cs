using UnityEngine;

namespace DivergentGenesis.Core
{
    /// <summary>Which plane of existence the player is standing in.</summary>
    public enum DimensionId : byte
    {
        Overworld = 0,
        Node = 1
    }

    /// <summary>
    /// Read by the terrain generator, the biome table and the mesher.
    ///
    /// Deliberately a plain static flag rather than an injected service: chunk
    /// generation runs on worker threads, and a bool field read is the only thing
    /// that is trivially safe there. Everything else about a dimension (palette,
    /// seed, entity rules) hangs off this one switch.
    /// </summary>
    public static class DimensionState
    {
        /// <summary>True while the Node dimension is the active plane.</summary>
        public static volatile bool NodeActive;

        public static DimensionId Active
        {
            get { return NodeActive ? DimensionId.Node : DimensionId.Overworld; }
        }

        public static bool IsNode { get { return NodeActive; } }

        public static string DisplayName
        {
            get { return NodeActive ? "the Node" : "the Overworld"; }
        }

        /// <summary>Fallback spawn Y so the player never falls through a half-streamed world.</summary>
        public static float SafeY { get { return NodeActive ? 88f : 92f; } }
    }

    /// <summary>
    /// The Node's physical constants. It is a small, soft, sugary place: lower
    /// gravity-feeling terrain, shallow seas of cream, and far more colour.
    /// </summary>
    public static class NodeConfig
    {
        public const float BaseHeight = 76f;
        public const float MaxHeight = 132f;
        public const float CreamSeaLevel = 60f;

        // palette: what the sky does here
        public static readonly Color SkyZenith = new Color(0.10f, 0.74f, 0.88f);
        public static readonly Color SkyHorizon = new Color(0.62f, 0.96f, 1.00f);
        public static readonly Color SkyAmbient = new Color(0.58f, 0.72f, 0.80f);
        public static readonly Color SkyFog = new Color(0.66f, 0.94f, 0.98f);
        public static readonly Color SunColor = new Color(1.00f, 0.96f, 0.86f);

        // palette: what the sky does once the Sovereign tears it open
        public static readonly Color CorruptZenith = new Color(0.22f, 0.03f, 0.34f);
        public static readonly Color CorruptHorizon = new Color(0.52f, 0.08f, 0.62f);
        public static readonly Color CorruptFog = new Color(0.34f, 0.06f, 0.44f);
        public static readonly Color CrackColor = new Color(1.00f, 0.42f, 0.95f);
    }
}