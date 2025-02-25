// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/LineShader"
{
    Properties
    {
        _Color("Color", Color) = (0, 0, 0, 0)
        _XLimit("XLimit", Float) = 0.0
        _Border("Border", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;

            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };


            float _XLimit;
            float _Border;
            float4 _Color;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.worldPos.x > _XLimit) discard;

                half4 color = _Color * (1 - smoothstep(_XLimit - _Border, _XLimit, i.worldPos.x));

                return color;
            }
            ENDCG
        }
    }
}
