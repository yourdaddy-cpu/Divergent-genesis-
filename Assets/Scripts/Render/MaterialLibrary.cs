using UnityEngine;

namespace DivergentGenesis.Render
{
    /// <summary>
    /// Owns every runtime material. Shaders live in Assets/Resources/Shaders so
    /// Unity can never strip them out of the build - that is the one thing that
    /// silently breaks procedural projects.
    /// </summary>
    public static class MaterialLibrary
    {
        public static Material Terrain { get; private set; }
        public static Material Water { get; private set; }
        public static Material Foliage { get; private set; }
        public static Material Highlight { get; private set; }
        public static Material BorderWall { get; private set; }

        private static bool _ready;

        public static void Ensure()
        {
            if (_ready) return;
            _ready = true;

            Terrain = Make("DGTerrain");
            Water = Make("DGWater");
            Foliage = Make("DGFoliage");
            Highlight = MakeHighlight();
            BorderWall = MakeBorderWall();
        }

        public static Shader LoadShader(string file)
        {
            Shader s = Resources.Load<Shader>("Shaders/" + file);
            if (s == null) s = Shader.Find("DG/" + file);
            if (s == null) s = Shader.Find("Mobile/Diffuse");
            return s;
        }

        public static Material Make(string file)
        {
            var m = new Material(LoadShader(file));
            m.name = "DG_" + file;
            m.enableInstancing = true;
            return m;
        }

        private static Material MakeHighlight()
        {
            var sh = Shader.Find("Hidden/Internal-Colored") ?? LoadShader("DGTerrain");
            var m = new Material(sh) { name = "DG_Highlight" };
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(1f, 1f, 1f, 0.35f));
            return m;
        }

        private static Material MakeBorderWall()
        {
            var m = Make("DGTerrain");
            m.name = "DG_BorderWall";
            if (m.HasProperty("_SunColor")) m.SetColor("_SunColor", new Color(0.9f, 0.45f, 0.55f));
            if (m.HasProperty("_Ambient")) m.SetColor("_Ambient", new Color(0.5f, 0.25f, 0.35f));
            return m;
        }

        /// <summary>Pushes the current sky state into every shader that needs it.</summary>
        public static void ApplyLighting(Vector3 sunDir, Color sunColor, Color ambient, Color fog, float fogDensity)
        {
            if (Terrain != null) SetLighting(Terrain, sunDir, sunColor, ambient, fog, fogDensity);
            if (Water != null) SetLighting(Water, sunDir, sunColor, ambient, fog, fogDensity);
            if (Foliage != null) SetLighting(Foliage, sunDir, sunColor, ambient, fog, fogDensity);
            if (BorderWall != null) SetLighting(BorderWall, sunDir, sunColor, ambient, fog, fogDensity);
        }

        private static void SetLighting(Material m, Vector3 sunDir, Color sun, Color amb, Color fog, float density)
        {
            if (m == null) return;
            if (m.HasProperty("_SunDir")) m.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
            if (m.HasProperty("_SunColor")) m.SetColor("_SunColor", sun);
            if (m.HasProperty("_Ambient")) m.SetColor("_Ambient", amb);
            if (m.HasProperty("_FogColor")) m.SetColor("_FogColor", fog);
            if (m.HasProperty("_FogDensity")) m.SetFloat("_FogDensity", density);
        }
    }
}
