Shader "Unlit/BoundedUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0)
        _OutsideMul ("Outside Alpha Multiplier", Float) = 0.25
        _InsideMul ("Inside Alpha Multiplier", Float) = 1.0
        _Min ("Min", Vector) = (0, 0, 0, 0)
        _Max ("Max", Vector) = (0, 0, 0, 0)
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

            float4 _Min;
            float4 _Max;

            float _OutsideMul;
            float _InsideMul;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD0;
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
    
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
    
                float4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
    
                if(i.worldPos.x < _Min.x || i.worldPos.y < _Min.y || i.worldPos.z < _Min.z
                    || i.worldPos.x > _Max.x || i.worldPos.y > _Max.y || i.worldPos.z > _Max.z) {
                    col.a *= _OutsideMul;
                } else {
                    col.a *= _InsideMul;
                }
    
                return col;
            }
            ENDCG
        }
    }
}
