using UnityEngine;
using UnityEngine.Rendering;

namespace CyberGoal.Unity.Environment
{
    /// <summary>
    /// Picks shaders that match whichever render pipeline is actually active.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The naive version of this asks for <c>"Universal Render Pipeline/Lit"</c>
    /// and falls back to <c>"Standard"</c> if that returns null. It has a hole:
    /// installing the URP <em>package</em> puts the URP shaders in the project, so
    /// <c>Shader.Find</c> succeeds — but URP only <em>renders</em> them if a URP
    /// asset is assigned in Graphics settings, and a fresh project has none until
    /// someone creates one. The result is a shader that is found, used, and drawn
    /// wrong: the classic all-magenta scene, with nothing null anywhere to explain
    /// it.
    /// </para>
    /// <para>
    /// <c>GraphicsSettings.currentRenderPipeline</c> is null exactly when the
    /// Built-in pipeline is in charge, which is the question actually being asked.
    /// Resolved once and cached, because <c>Shader.Find</c> is a string lookup and
    /// this is called for every material in the stadium.
    /// </para>
    /// </remarks>
    public static class ShaderLibrary
    {
        private static Shader _lit;
        private static Shader _unlit;

        /// <summary>True when a scriptable render pipeline (URP) is actually driving rendering.</summary>
        public static bool UsingScriptablePipeline => GraphicsSettings.currentRenderPipeline != null;

        public static Shader Lit
        {
            get
            {
                if (_lit != null) return _lit;
                _lit = UsingScriptablePipeline
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                // Belt and braces: if the expected one is missing, anything that
                // draws is better than a null material, which renders as magenta
                // and tells the reader nothing.
                _lit = _lit
                       ?? Shader.Find("Standard")
                       ?? Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Diffuse");
                return _lit;
            }
        }

        public static Shader Unlit
        {
            get
            {
                if (_unlit != null) return _unlit;
                _unlit = UsingScriptablePipeline
                    ? Shader.Find("Universal Render Pipeline/Unlit")
                    : Shader.Find("Unlit/Color");
                _unlit = _unlit
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default")
                         ?? Lit;
                return _unlit;
            }
        }

        /// <summary>
        /// Set smoothness on whichever shader we ended up with.
        /// </summary>
        /// <remarks>
        /// URP/Lit calls it <c>_Smoothness</c>; the Built-in Standard shader calls
        /// it <c>_Glossiness</c>. Setting the wrong one is silent — the property
        /// simply does not exist and the value is dropped — so both are tried.
        /// </remarks>
        public static void SetSmoothness(Material material, float smoothness)
        {
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        }

        public static void SetEmission(Material material, Color colour)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", colour);
        }

        /// <summary>Switch a material to alpha blending, on either pipeline.</summary>
        public static void MakeTransparent(Material material)
        {
            // URP reads _Surface; Built-in Standard reads _Mode. Neither errors on
            // the other's absence, so both are set.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
