Shader "Elex/FogShader"
{
    Properties
    {
        _Fog1 ("_Fog1", 2D) = "white" {}
        _Fog2 ("_Fog2", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.5, 0.5, 0.5, 1)
        _FogDensity ("Fog 边缘过渡", Range(0, 5)) = 2
        _FogDepthLerp ("_FogDepthLerp", Range(0, 3)) = 0.1
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline"="UniversalRenderPipeline"
        }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #pragma enable_d3d11_debug_symbols

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;

                float viewZ : TEXCOORD2;  // 添加view space depth
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogDensity;
                float _FogDepthLerp;
            CBUFFER_END
            TEXTURE2D(_Fog1);            SAMPLER(sampler_Fog1);
            TEXTURE2D(_Fog2);            SAMPLER(sampler_Fog2);
            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 计算world space位置，然后转换到view space获取z分量
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                output.viewZ = positionVS.z;  // view space的z分量（通常是负值）
                
                output.color = input.color;
                output.positionWS = positionWS;
                output.uv = TransformObjectToWorld(input.positionOS.xyz).xz /100.0f;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float4 _13 = input.uv.xyxy;
                float2  _34;
                float4 _21, _20;
                _20 = _Time.yyyy/10.0f * float4(0.9, 0.9, 0.7, 0.7);
                //_20 *= _Time.zwzw;

                float2 _72 = (_13.zw * float2(1.1, 1.1)) - _20.xy;
                _20      = float4(_72.x,_72.y, _20.z, _20.w);

                float2 _33 = (_13.zw * float2(1.3, 1.3)) + _20.zw;
                _34.x = SAMPLE_TEXTURE2D(_Fog1, sampler_Fog1, _33).x;
                _21.x = SAMPLE_TEXTURE2D(_Fog1, sampler_Fog1, _20.xy).x;
                
               // return half4(_34.x,0,0,1);

                // 边缘效果生成
                float4 _22;
                float2 _30;
                float  _31;
                _22.x = _34.x + _21.x;
                _22.x *= 0.5;
    
                _20.x = input.color.x;
                _20.x = clamp(_20.x, 0.0, 1.0);
                _20.x = log2(_20.x);
                _30.x = _20.x * 3.0f;
                _20.x *= 0.1;
                _20.x = exp2(_20.x);
                _30.x = exp2(_30.x);
                _31 = min(_30.x, 1.0);
                _31 = log2(_31);
                _22.x = _31 * _22.x;
                _22.x = exp2(_22.x);
                _20.x *= _22.x;
              

                // 深度相关
                float2 screenUV = input.positionCS.xy / _ScreenParams;
                float depth = SampleSceneDepth(screenUV); //采样深度
                float depthValue = LinearEyeDepth(depth, _ZBufferParams); //转到LinearEyeDepth
                depthValue += -abs(input.viewZ);
                _30.x = depthValue;
                // 视角相关：
      float3 _25;          
    //_25 = _16.xyz + (-_37._m1);
    _25 = input.positionWS + (-_WorldSpaceCameraPos);
    _33.x = max(0.0001f, dot(_25, _25));
    _33.x = 1/sqrt(_33.x);
    _25 = _33.xxx * _25;
    _33.x = max(abs(_25.y), 1.0);

    half2 _226 = _25.xz * half2(0.605f,0.605f);
    _25 = half3(_226.x, _226.y, _25.z);
    _33 = _25.xy / _33.xx;

                //..................

//return half4(depthValue,0,0,1);
                //depthValue = clamp(depthValue, 0.0, 1.0);
                //depthValue *= depthValue;

//...............第二层迷雾
                half _27;
                half2 _32;
    _27 = SAMPLE_TEXTURE2D(_Fog2, sampler_Fog2, _13.xy + _Time.yy /10).x;
    _32 = SAMPLE_TEXTURE2D(_Fog2, sampler_Fog2, _13.zw+ _Time.yy /20).xw;
    //_22.x = _32.y * _37._m4;
    _22.x = _32.y * 0.594;
    _25.x = ((-_27) * _22.x) + 1.0;
    _33 = (_33 * _25.xx) + _13.zw;
    _34 = SAMPLE_TEXTURE2D(_Fog2, sampler_Fog2, _33 + _Time.yy /30).xw;
    //_22.x = _34.y * _37._m4;
    _22.x = _34.y * 0.594f;
    _31 = _32.x * _34.x;


// 还差15没解决，以及看看_13是什么
                
// 最后部分:                
     //_33.x = _22.x * _37._m6;
     _33.x = _22.x * 0.1f;
    _30.x = abs(_30.x) * _33.x;
    _30.x = clamp(_30.x, 0.0, 1.0);
    _30.x *= _30.x;
    _22.x = min(_20.x, _30.x);
    _21.w = _22.x * input.color.y;
    //half3 _328 = _37._m9.xyz + (-vec3(_37._m10.x, _37._m10.y, _37._m10.z));
    half3 _328 = half3(1.05496, 0.54219, 0.2732)+ 0;
    _22 = half4(_328.x, _22.y, _328.y, _328.z);
    half3 _343 = (half3(_31.x,_31.x,_31.x) * _22.xzw) + half3(0.0,0,0);
    _21 = half4(_343.x, _343.y, _343.z, (_21.w));
    return input.color.y;
          }
            ENDHLSL
        }
    }
}
