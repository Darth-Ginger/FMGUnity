Shader "Unlit/MinRing"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0)
        _RingThickness ("Thickness", Float) = 0.1
        _RingCenter ("Center", Vector) = (0, 0, 0, 0)
        _RingRadius ("Radius", Float) = 1.0
        _Frequency ("Frequency", Float) = 3.0
        _Speed ("Speed", Float) = 2.0
        _Divisions ("Divisions", Float) = 2.0
        _Strength ("Strength", Float) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
    
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
    
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
    
                return o;
            }

            float _RingThickness;
            float _RingRadius;
            float _Frequency;
            float _Speed;
            float _Divisions;
            float _Strength;
            float4 _RingCenter;

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                float dist = length(_RingCenter.xyz - i.worldPos.xyz);
                if(dist < _RingRadius - _RingThickness) discard;
                if(dist > _RingRadius) discard;
    
                float distFromRadius = (_RingRadius - dist);
                float distFromRadiusPercent = (_RingRadius - dist) / _RingThickness;
                distFromRadiusPercent = fmod(distFromRadiusPercent, 1.0f / _Divisions) * _Divisions;
    
                float4 instancedCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 col = instancedCol;
                float f = smoothstep(0, 1, 1.0 - distFromRadiusPercent) * smoothstep(0, 1, distFromRadiusPercent);
                col.a *= pow(f, 0.5) * 2;
                col += abs(sin((i.worldPos.x + i.worldPos.y + i.worldPos.z) * _Frequency + _Time.y * _Speed)) * instancedCol * _Strength;
                col += abs(sin((i.worldPos.x + i.worldPos.y - i.worldPos.z) * _Frequency - _Time.y * _Speed)) * instancedCol * _Strength;
    
                return col;
            }
            ENDCG
        }
    }
}
