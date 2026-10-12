// 传送门黑板投影着色器：不依赖网格 UV（黑板自带 UV 是图集子区域，且 FBX 未开 Read/Write），
// 而是把每个像素的世界坐标投影到门洞四角定义的平面上，直接算出 0~1 的采样坐标——
// 与取景相机的偏心视锥同一套数学，画面方向天然一致。
// 门洞几何由 PortalWindow 在运行时通过 _Origin/_UDir/_VDir 传入（门是静态的，设一次即可）。
Shader "Custom/PortalPanelProj"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Origin;  // xyz = 门洞左下角（世界坐标）
            float4 _UDir;    // xyz = 门洞水平轴（单位向量），w = 轴长
            float4 _VDir;    // xyz = 门洞竖直轴（单位向量），w = 轴长
            float  _MirrorU; // 1 = 画面左右镜像

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                float3 d = wp - _Origin.xyz;
                float u = dot(d, _UDir.xyz) / max(0.01, _UDir.w);
                float vv = dot(d, _VDir.xyz) / max(0.01, _VDir.w);
                u = saturate(u);
                if (_MirrorU > 0.5) u = 1.0 - u;
                o.uv = float2(u, saturate(vv));
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
