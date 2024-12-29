Shader "Unlit/KawaseBlur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Kawase Blur"
            ZWrite Off ZTest Always Cull Off
            
            HLSLPROGRAM
            #pragma vertex KawaseBlurVert
            #pragma fragment KawaseBlurFrag
            
            // properties and variables
            // ------------------------
            float4 _BlitTexture_TexelSize;
            float _KawaseBlurOffset;

            struct CustomVaryings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uvTopLeft   : TEXCOORD1;
                float2 uvTopRight  : TEXCOORD2;
                float2 uvBottomLeft : TEXCOORD3;
                float2 uvBottomRight : TEXCOORD4;
            };

            CustomVaryings KawaseBlurVert(Attributes input)
            {
                CustomVaryings output;
                output.positionHCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
                output.uv   = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                output.uvTopRight    = output.uv + float2( _KawaseBlurOffset,  _KawaseBlurOffset) * _BlitTexture_TexelSize.xy;
                output.uvTopLeft     = output.uv + float2(-_KawaseBlurOffset,  _KawaseBlurOffset) * _BlitTexture_TexelSize.xy;
                output.uvBottomRight = output.uv + float2( _KawaseBlurOffset, -_KawaseBlurOffset) * _BlitTexture_TexelSize.xy;
                output.uvBottomLeft  = output.uv + float2(-_KawaseBlurOffset, -_KawaseBlurOffset) * _BlitTexture_TexelSize.xy;
                return output;
            }
            
            float4 KawaseBlurFrag(CustomVaryings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv) * 0.2;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uvTopRight) * 0.2;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uvTopLeft) * 0.2;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uvBottomRight) * 0.2;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uvBottomLeft) * 0.2;
                return float4(color.rgb, 1.0);
            }
            
            ENDHLSL
        }
    }
}
