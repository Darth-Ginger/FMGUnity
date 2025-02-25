Shader "Unlit/Lines"
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

            struct v2f
            {
                float4 vertex : SV_POSITION;

            };

            float4 _Color;
            StructuredBuffer<float3> _Positions;

            v2f vert (appdata_base v, uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                float4 objPos = float4(_Positions[instanceID * 2 + vertexID], 0);
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
