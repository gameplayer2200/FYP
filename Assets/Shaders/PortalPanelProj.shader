// 传送门黑板投影着色器：不依赖网格 UV（黑板自带 UV 是图集子区域，且 FBX 未开 Read/Write），
// 而是把每个像素的世界坐标投影到门洞四角定义的平面上，直接算出 0~1 的采样坐标。
// 采样轴必须与取景相机的 basis 对齐，画面方向才一致：U 轴 = 相机右方向、V 轴 = 相机 up 的反向。
// 注意 U 轴**不是** c0→c1：取景相机的 up 取的是门洞 V 轴方向（见 PortalWindow.UpdatePortalCamera，
// 相机是倒架的），其右方向恰好与 c0→c1 相反。搞反就是画面左右镜像（门把手那侧的东西会跑到另一边）。
// 门洞几何由 PortalWindow 在运行时通过 _Origin/_UDir/_VDir 传入（门是静态的，设一次即可）。
Shader "Custom/PortalPanelProj"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        // 采样轴必须正式声明：只写裸 uniform 时，这些值在编辑器刷新 / 着色器重载后会被清零
        //（控制台同时刷 "doesn't have a color property '_Origin'" 一类报错），门内板 UV
        // 全部塌到 (0,0)——整块面板只采样 RT 的一个像素，看起来就是门洞里一片纯色。
        _Origin ("Sample Origin (world xyz)", Vector) = (0,0,0,0)
        _UDir ("Sample U Axis (unit xyz + len w)", Vector) = (1,0,0,0)
        _VDir ("Sample V Axis (unit xyz + len w)", Vector) = (0,1,0,0)
        _MirrorU ("Mirror U", Float) = 0
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
            float4 _Origin;  // xyz = u=0 的那个角（世界坐标）；本约定取门局部 +X 侧（见 U 轴说明）
            float4 _UDir;    // xyz = 采样水平轴（指向 u 增大的方向），w = 轴长
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
