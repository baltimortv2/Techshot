Shader "ShadowBlit"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off
        Blend Zero SrcAlpha

        Pass
        {
            Name "ColorBlitPass"
            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #pragma vertex Vert
                #pragma fragment Frag

                float _ShadowIntensity;

                float4 Frag(Varyings input) : SV_Target0
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                    float2 uv = input.texcoord.xy;
                    half4 shadow = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
                    // src = (0,0,0,shadow.r), dst.rgb *= shadow.r за счёт Blend Zero SrcAlpha
                    return float4(0.0, 0.0, 0.0, shadow.r);
                }
            ENDHLSL
        }
    }

    Fallback off
}
