// Vertex-coloured character shader.
//
// WHY THIS EXISTS
//
// HumanoidMeshBuilder bakes the whole kit into the mesh's vertex-colour channel:
// shirt, shorts, socks, boots, skin, hair, eyes, lips, neon trim. That is what
// keeps a character to ONE mesh and ONE draw call, which §19 of the art
// direction requires for mobile.
//
// Neither URP/Lit nor the Built-in Standard shader reads vertex colour. Assigning
// one of those produces a figure in a single flat colour, with every hem and
// facial feature silently discarded and nothing in the C# to suggest why.
//
// Two SubShaders so it works on either pipeline; Unity picks the first whose
// tags match. Deliberately simple lighting — these are 1.8 m figures under one
// key light at gameplay distance, and a full PBR loop would buy nothing visible.

Shader "CyberGoal/CharacterKit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Smoothness ("Smoothness", Range(0,1)) = 0.25
    }

    // ── Universal Render Pipeline ────────────────────────────────────────────
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float  _Smoothness;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // The kit lives here. Tint is a per-character multiplier, so one
                // material can still serve both sides without touching the mesh.
                half3 albedo = input.color.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(input.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Half-lambert rather than clamped lambert: a fully shadowed side
                // goes to ambient rather than to black, which matters on a night
                // pitch where half of every figure faces away from the one key
                // light and would otherwise be a silhouette.
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half wrapped = ndotl * 0.5h + 0.5h;
                half3 direct = mainLight.color * wrapped * mainLight.shadowAttenuation;

                half3 ambient = SampleSH(normalWS);

                // A tight rim keeps the figure separated from a dark background.
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(dot(normalWS, viewDir)), 4.0h) * _Smoothness;

                half3 colour = albedo * (direct + ambient) + _EmissionColor.rgb + rim * 0.35h;
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }

        // Shadows come from URP's own caster, so nothing here has to be
        // maintained against changes in the shadow pipeline.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    // ── Built-in pipeline fallback ───────────────────────────────────────────
    //
    // Reached when no render pipeline asset is assigned, which is the state of a
    // fresh project until someone creates one. Without this the character is
    // magenta on first open and the cause is not obvious.
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        struct Input
        {
            float4 vertexColour;
        };

        fixed4 _BaseColor;
        fixed4 _EmissionColor;

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertexColour = v.color;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.vertexColour.rgb * _BaseColor.rgb;
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }

    Fallback "Universal Render Pipeline/Lit"
}
