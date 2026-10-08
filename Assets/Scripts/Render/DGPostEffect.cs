using UnityEngine;

namespace DivergentGenesis.Render
{
    /// <summary>
    /// The HDR path: render into a half-float target, pull the bright parts out of
    /// it into a quarter resolution buffer, blur that twice, then composite and
    /// tone-map back to the display.
    ///
    /// This is what makes lava, lamps, the Node's cream sea and the cracked sky
    /// actually glow instead of just being bright, and it is the reason a torch you
    /// place yourself is worth placing.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DGPostEffect : MonoBehaviour
    {
        [Header("Look")]
        [Range(0.4f, 2.5f)] public float Exposure = 1.08f;
        [Range(0f, 2f)] public float Bloom = 0.85f;
        [Range(0.4f, 3f)] public float Threshold = 0.86f;
        [Range(0f, 1.5f)] public float Saturation = 1.10f;
        [Range(1.6f, 2.6f)] public float Gamma = 2.2f;
        [Range(0f, 1f)] public float Vignette = 0.34f;
        [Range(0f, 1f)] public float Underwater = 0f;

        public Material PostMaterial;

        private Camera _camera;
        private bool _warned;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null) _camera.allowHDR = true;
            EnsureMaterial();
        }

        private void OnEnable()
        {
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (PostMaterial != null) return;
            var sh = Shader.Find("DG/Post");
            if (sh == null) sh = MaterialLibrary.LoadShader("DGPost");
            if (sh == null)
            {
                if (!_warned) { Debug.LogWarning("[DG] DG/Post shader missing - post processing off."); _warned = true; }
                return;
            }
            PostMaterial = new Material(sh) { name = "DG_Post" };
        }

        /// <summary>Pushed to shader globals so water and sky can react to the look.</summary>
        private void UpdateGlobals()
        {
            Shader.SetGlobalFloat("_DGExposure", Exposure);
            Shader.SetGlobalFloat("_DGBloom", Bloom);
            Shader.SetGlobalFloat("_DGUnderwater", Underwater);
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (src == null || dest == null) return;
            EnsureMaterial();

            if (PostMaterial == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            UpdateGlobals();

            int w = Mathf.Max(2, src.width / 4);
            int h = Mathf.Max(2, src.height / 4);
            var format = RenderTextureFormat.ARGBHalf;

            RenderTexture bright = RenderTexture.GetTemporary(w, h, 0, format);
            RenderTexture blur = RenderTexture.GetTemporary(w, h, 0, format);

            // 0: threshold the HDR image down to its bright parts
            PostMaterial.SetFloat("_Threshold", Threshold);
            PostMaterial.SetFloat("_Knee", Threshold * 0.5f);
            Graphics.Blit(src, bright, PostMaterial, 0);

            // 1 + 2: separable gaussian, cheap at quarter res
            PostMaterial.SetVector("_TexelSize", new Vector4(1f / w, 0f, 0f, 0f));
            Graphics.Blit(bright, blur, PostMaterial, 1);
            PostMaterial.SetVector("_TexelSize", new Vector4(0f, 1f / h, 0f, 0f));
            Graphics.Blit(blur, bright, PostMaterial, 2);

            // 3: composite, tone-map, vignette
            PostMaterial.SetTexture("_BloomTex", bright);
            PostMaterial.SetFloat("_Exposure", Exposure);
            PostMaterial.SetFloat("_Bloom", Bloom);
            PostMaterial.SetFloat("_Saturation", Saturation);
            PostMaterial.SetFloat("_Gamma", Gamma);
            PostMaterial.SetFloat("_Vignette", Vignette);
            PostMaterial.SetFloat("_Underwater", Underwater);
            Graphics.Blit(src, dest, PostMaterial, 3);

            RenderTexture.ReleaseTemporary(bright);
            RenderTexture.ReleaseTemporary(blur);
        }

        /// <summary>Re-applies the profile when the player changes quality settings.</summary>
        public void Apply(bool hdr, float bloomScale)
        {
            if (_camera != null) _camera.allowHDR = hdr;
            Bloom = Mathf.Clamp(0.85f * bloomScale, 0f, 2f);
            enabled = hdr || bloomScale > 0f;
        }
    }
}