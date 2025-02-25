Shader "Unlit/TransparentMeshPrimitive"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderQueue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag


            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            StructuredBuffer<float3> _Positions;

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                float4 objPos = v.vertex + float4(_Positions[instanceID], 0);
                o.vertex = UnityObjectToClipPos(objPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
