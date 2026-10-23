// 探测器·场地数字波（全屏叠加）
//
// 改编自 Assets/Art/VFX/MatrixScan/Shaders/Resources/ScreenSpaceMatrixEffect.shader（MIT，Shahriar Shahrabi）。
// 原版是「整屏铺满矩阵雨」的纯展示效果；这里把它改成一次**从玩家向外推的球面波**：
//   · 用 _CameraDepthTexture 还原每个像素的世界坐标，才知道它离玩家几米；
//   · 只让「球面壳附近」的像素数字化 —— 于是地板上是一圈扩散的环，墙面上是一道扫过的弧；
//   · 壳内侧留一点残留数字化（_WaveInner），波扫过之后场地还"没缓过来"。
//
// 与场景物体显形（FYP/ScanMatrix）的分工：
//   FYP/ScanMatrix     —— 换物体材质，让挂了 ScanTarget 的异常物显形；
//   FYP/ScanWaveOverlay—— 叠在画面上，让**周围场地本身**（墙/地/天花板/杂物）被扫过。
// 两者共用同一套全局量（球心 + 进度），所以波前永远是对齐的。
//
// 共享全局量（由 EmfDetector 每帧下发）：
//   global_font_texture   16x16 字符格图集
//   global_white_noise    512x512 动态白噪声（决定每格取哪个字母）
//
// 材质私有量（由 EmfDetector 每帧下发）：
//   _CamPos / _RayBL.._RayTR  透视射线（相机空间 z = 1），用于深度反投影
//   _WaveOrigin / _WaveRadius 球心与当前波半径
//   _WaveBand / _WaveInner    波前厚度 / 内侧残留强度
//   _WaveIntensity / _WaveColor / _WaveCharScale  观感
Shader "FYP/ScanWaveOverlay"
{
    // 这些属性全部由 ScanWaveOverlay 每帧用 SetFloat/SetVector 下发；
    // 声明出来是为了 Inspector 里能看到、也为了 Material.GetFloat 取得到（调试用）。
    Properties
    {
        _MainTex        ("Scene", 2D) = "white" {}
        _CamPos         ("Camera Position", Vector) = (0,0,0,0)
        _RayBL          ("Ray Bottom-Left", Vector) = (0,0,1,0)
        _RayBR          ("Ray Bottom-Right", Vector) = (0,0,1,0)
        _RayTL          ("Ray Top-Left", Vector) = (0,0,1,0)
        _RayTR          ("Ray Top-Right", Vector) = (0,0,1,0)
        _WaveOrigin     ("Wave Origin", Vector) = (0,0,0,0)
        _WaveRadius     ("Wave Radius", Float) = 0
        _WaveBand       ("Wave Band", Float) = 0.9
        _WaveInner      ("Inner Trail", Float) = 0.07
        _WaveIntensity  ("Intensity", Float) = 1.35
        _WaveCharScale  ("Char Scale", Float) = 0.75
        _WaveColor      ("Color", Color) = (0.47, 1, 0.76, 1)
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Overlay" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off          // 整屏替换：读回原画面，混合后直接写回

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            sampler2D global_white_noise;
            sampler2D global_font_texture;

            float3 _CamPos;
            float3 _RayBL;
            float3 _RayBR;
            float3 _RayTL;
            float3 _RayTR;

            float3 _WaveOrigin;
            float  _WaveRadius;
            float  _WaveBand;
            float  _WaveInner;
            float  _WaveIntensity;
            float  _WaveCharScale;
            float4 _WaveColor;

            #define RAIN_DROP 512.0

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            // 逐字取字符图集：block 决定取哪一格，白噪声决定那一格是 16 个字母里的哪个。
            // 与 VFX 原版逐行一致，只去掉了彩色分支。
            float text(float2 coord)
            {
                float2 uv    = frac(coord.xy / 16.);
                float2 block = floor(coord.xy / 16.);
                       uv    = uv * 0.7 + .1;

                float2 rand  = tex2D(global_white_noise, block.xy / float2(512., 512.)).xy;
                       rand  = floor(rand * 16.);
                       uv   += rand;
                       uv   *= 0.0625;
                       uv.x  = -uv.x;
                return tex2D(global_font_texture, uv).r;
            }

            // 竖向雨滴：每列有各自的起点与速度，越靠近列底越亮。
            float rainAmount(float2 fragCoord, float screenHeight)
            {
                fragCoord.x = floor(fragCoord.x / 16.);
                float offset = sin(fragCoord.x * 15.);
                float speed  = cos(fragCoord.x * 3.) * .15 + .35;
                float y      = frac((fragCoord.y / screenHeight) + _Time.y * speed + offset);
                return 1.0 / max(y * 20., 0.0001);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, i.uv);

                // ── 深度反投影：把屏幕像素还原成世界坐标 ──
                float eye = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                float3 rayB = lerp(_RayBL, _RayBR, i.uv.x);
                float3 rayT = lerp(_RayTL, _RayTR, i.uv.x);
                float3 ray  = lerp(rayB,  rayT,  i.uv.y);
                float3 worldPos = _CamPos + ray * eye;

                // ── 球面壳遮罩 ──
                float  d     = distance(worldPos, _WaveOrigin);
                float  ahead = d - _WaveRadius;                       // >0 未扫到，<0 已扫过
                float  band  = saturate(1.0 - abs(ahead) / max(_WaveBand, 0.001));
                       band  = band * band;                           // 波前最亮，两侧迅速衰减
                // 波前拖一条尾迹：波扫过的球内保留一点数字化（_WaveInner），收回时代球收缩、
                // 场地也就从外向内褪回原样 —— 这比"扫过即复原"更像一次真实的扫描。
                // 强度必须低（约 0.05~0.1）：太高就变成整屏变绿，看不出波前在推进。
                float  trail = (ahead < 0.0) ? _WaveInner : 0.0;
                float  mask  = saturate(band + trail);

                if (mask <= 0.0001) return scene;

                // ── 屏幕空间字符雨（按 1080p 归一化，换分辨率观感一致）──
                float2 px    = _ScreenParams.xy;
                float  fit   = max(px.y / 1080.0, 0.25);
                float2 coord = i.uv * px * _WaveCharScale * fit;

                float amt = text(coord) * rainAmount(coord, px.y);
                       amt = min(amt, 1.6);

                float3 ink = _WaveColor.rgb * amt * _WaveIntensity * mask;

                // 被扫到的像素压暗，再叠字符 —— 「表面正在变成字符」的观感
                float3 outc = scene.rgb * (1.0 - mask * 0.85) + ink;
                       outc = min(outc, 8.0);

                return fixed4(outc, scene.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
