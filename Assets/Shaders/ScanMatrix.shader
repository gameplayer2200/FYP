// 扫描特效专用着色器：改编自 MatrixVFX 的 Unlit/TriPlanarMatrix
// 原版路径 Assets/Material/MatrixVFX-master/Assets/Shaders/Resources/TriPlanarMatrix.shader
//
// 与原版的两处区别：
//   1. 去掉 split_from_midle（屏幕空间竖向裁切）——原版那是「从左到右擦屏」的转场，
//      不是球形扫描。保留它会让显形区域变成屏幕中间一条竖带。
//   2. 球形裁切加软边（_Scan_Softness），避免出现硬邦邦的球形切面。
//
// 另外：原版依赖的 RandomLib.cginc / LabColorSpace.cginc 已内联到本文件，
// 因为 #include "xxx.cginc" 只按本 shader 所在目录与工程根解析，跨目录会报
// "Couldn't open include file"。
//
// 全局量（由 MatrixScanDemo 每帧下发）：
//   global_font_texture      字体图集（16x16 字符格）
//   global_white_noise       512x512 动态白噪声，决定每个字符格取哪个字母
//   global_colored           0=经典绿 1=彩色
//   _session_rand_seed       噪声种子
//   _Global_Transition_value 扫描进度（0~1），可视半径 = 该值 * _Scan_Radius_Scale
//   _Global_Effect_center    扫描球心（世界坐标）
Shader "FYP/ScanMatrix"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Scan_Radius_Scale ("半径系数（半径 = 进度 x 本值）", Float) = 60
        _Scan_Softness ("软边宽度（米）", Float) = 1.5
        _Scan_Intensity ("亮度", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal   : NORMAL;
            };

            sampler2D _MainTex;
            float     _Scan_Radius_Scale;
            float     _Scan_Softness;
            float     _Scan_Intensity;

            sampler2D global_white_noise;
            sampler2D global_font_texture;
            uint      global_colored;
            uint      _session_rand_seed;

            float     _Global_Transition_value;
            float3    _Global_Effect_center;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal   = UnityObjectToWorldNormal(v.normal);
                return o;
            }

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

            #define dropLength 512
            float3 rain(float2 fragCoord)
            {
                fragCoord.x = floor(fragCoord.x / 16.);
                float offset = sin(fragCoord.x * 15.);
                float speed  = cos(fragCoord.x * 3.) * .15 + .35;
                float y      = frac((fragCoord.y / dropLength) + _Time.y * speed + offset);
                return float3(.1, 1., .35) / (y * 20.);
            }

            // ---- 内联自 MatrixVFX RandomLib.cginc ----
            #define rnd(seed, constant)  wang_rnd(seed + triple32(_session_rand_seed) * constant)
            uint triple32(uint x)
            {
                x ^= x >> 17;
                x *= 0xed5ad4bbU;
                x ^= x >> 11;
                x *= 0xac4c1b51U;
                x ^= x >> 15;
                x *= 0x31848babU;
                x ^= x >> 14;
                return x;
            }
            float wang_rnd(uint seed)
            {
                uint rndint = triple32(seed);
                return ((float)rndint) / float(0xFFFFFFFF);
            }
            // ---- 内联结束 ----

            // ---- 内联自 MatrixVFX LabColorSpace.cginc（只保留 lab2rgb 用到的部分）----
            float3 rgb2xyz(float3 c)
            {
                float3 tmp;
                tmp.x = (c.r > 0.04045) ? pow((c.r + 0.055) / 1.055, 2.4) : c.r / 12.92;
                tmp.y = (c.g > 0.04045) ? pow((c.g + 0.055) / 1.055, 2.4) : c.g / 12.92;
                tmp.z = (c.b > 0.04045) ? pow((c.b + 0.055) / 1.055, 2.4) : c.b / 12.92;
                const float3x3 mat = float3x3(
                    0.4124, 0.3576, 0.1805,
                    0.2126, 0.7152, 0.0722,
                    0.0193, 0.1192, 0.9505);
                return 100.0 * mul(tmp, mat);
            }
            float3 xyz2lab(float3 c)
            {
                float3 n = c / float3(95.047, 100, 108.883);
                float3 v;
                v.x = (n.x > 0.008856) ? pow(n.x, 1.0 / 3.0) : (7.787 * n.x) + (16.0 / 116.0);
                v.y = (n.y > 0.008856) ? pow(n.y, 1.0 / 3.0) : (7.787 * n.y) + (16.0 / 116.0);
                v.z = (n.z > 0.008856) ? pow(n.z, 1.0 / 3.0) : (7.787 * n.z) + (16.0 / 116.0);
                return float3((116.0 * v.y) - 16.0, 500.0 * (v.x - v.y), 200.0 * (v.y - v.z));
            }
            float3 rgb2lab(float3 c)
            {
                float3 lab = xyz2lab(rgb2xyz(c));
                return float3(lab.x / 100.0, 0.5 + 0.5 * (lab.y / 127.0), 0.5 + 0.5 * (lab.z / 127.0));
            }
            float3 lab2xyz(float3 c)
            {
                float fy = (c.x + 16.0) / 116.0;
                float fx = c.y / 500.0 + fy;
                float fz = fy - c.z / 200.0;
                return float3(
                    95.047  * ((fx > 0.206897) ? fx * fx * fx : (fx - 16.0 / 116.0) / 7.787),
                    100.000 * ((fy > 0.206897) ? fy * fy * fy : (fy - 16.0 / 116.0) / 7.787),
                    108.883 * ((fz > 0.206897) ? fz * fz * fz : (fz - 16.0 / 116.0) / 7.787));
            }
            float3 xyz2rgb(float3 c)
            {
                const float3x3 mat = float3x3(
                     3.2406, -1.5372, -0.4986,
                    -0.9689,  1.8758,  0.0415,
                     0.0557, -0.2040,  1.0570);
                float3 v = mul(c / 100.0, mat);
                float3 r;
                r.x = (v.r > 0.0031308) ? ((1.055 * pow(v.r, (1.0 / 2.4))) - 0.055) : 12.92 * v.r;
                r.y = (v.g > 0.0031308) ? ((1.055 * pow(v.g, (1.0 / 2.4))) - 0.055) : 12.92 * v.g;
                r.z = (v.b > 0.0031308) ? ((1.055 * pow(v.b, (1.0 / 2.4))) - 0.055) : 12.92 * v.b;
                return r;
            }
            float3 lab2rgb(float3 c)
            {
                return xyz2rgb(lab2xyz(float3(100.0 * c.x, 2.0 * 127.0 * (c.y - 0.5), 2.0 * 127.0 * (c.z - 0.5))));
            }
            // ---- 内联结束 ----


            float3 rain_colored(float2 fragCoord)
            {
                fragCoord.x = floor(fragCoord.x / 16.);
                float offset = rnd(fragCoord.x * 521., 612);
                float speed  = rnd(fragCoord.x * 612., 951) * .15 + .35;
                      speed *= 0.4;
                float y = frac((fragCoord.y / dropLength) + _Time.y * speed + offset);

                int randomSeed = (fragCoord.x + floor((fragCoord.y / dropLength) + _Time.y * speed + offset)) * 51.;
                float3 col = float3(rnd(randomSeed, 21),
                                    frac(rnd(randomSeed, 712) + 0.8),
                                    frac(rnd(randomSeed, 61) + 0.2));
                       col = lab2rgb(col);
                return col / (y * 20.);
            }

            #define scale     0.2
            #define sharpness 10.
            float3 MatrixEffect(float2 coord)
            {
                float3 rain_col = rain(coord * float2(dropLength, dropLength) * scale);
                if (global_colored == 1)
                    rain_col = rain_colored(coord * float2(dropLength, dropLength) * scale);
                return text(coord * float2(dropLength, dropLength) * scale) * rain_col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col      = float4(0., 0., 0., 1.);
                float3 colFront = MatrixEffect(i.worldPos.xy + sin(i.worldPos.zz));
                float3 colSide  = MatrixEffect(i.worldPos.zy + sin(i.worldPos.xx));
                float3 colTop   = MatrixEffect(i.worldPos.xz + sin(i.worldPos.yy));

                float3 blendWeight = pow(normalize(abs(i.normal)), sharpness);
                       blendWeight /= (blendWeight.x + blendWeight.y + blendWeight.z);
                       col.xyz      = colFront * blendWeight.z +
                                      colSide  * blendWeight.x +
                                      colTop   * blendWeight.y;

                // 球形裁切（带软边）：扫描波到达前完全透明，扫过之后显形
                float dist     = distance(i.worldPos.xyz, _Global_Effect_center.xyz);
                float radius   = saturate(_Global_Transition_value) * _Scan_Radius_Scale;
                float softness = max(_Scan_Softness, 0.0001);
                float visible  = saturate((radius - dist) / softness);
                       col.xyz *= visible * _Scan_Intensity;

                col = min(1.5, col);
                return col;
            }
            ENDCG
        }
    }
}
