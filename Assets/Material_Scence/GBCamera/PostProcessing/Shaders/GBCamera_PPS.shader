
Shader "RogueNoodle/GBCamera_PPS"
{
	Properties
	{
		_Palette("Palette", 2D) = "white" {}
		_ResX("Res X", Float) = 160
		_ResY("Res Y", Float) = 144
		_Fade("Fade", Range( 0 , 5)) = 1

	}

	SubShader
	{
		LOD 0

		Cull Off
		ZWrite Off
		ZTest Always
		
		Pass
		{
			CGPROGRAM

			

			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 3.0

			#include "UnityCG.cginc"
			
		
			struct ASEAttributesDefault
			{
				float3 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				
			};

			struct ASEVaryingsDefault
			{
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
				float2 texcoordStereo : TEXCOORD1;
			#if STEREO_INSTANCING_ENABLED
				uint stereoTargetEyeIndex : SV_RenderTargetArrayIndex;
			#endif
				
			};

			uniform sampler2D _MainTex;
			uniform half4 _MainTex_TexelSize;
			uniform half4 _MainTex_ST;
			
			uniform sampler2D _Palette;
			uniform float _ResX;
			uniform float _ResY;
			uniform float _Fade;


			
			float2 TransformTriangleVertexToUV (float2 vertex)
			{
				float2 uv = (vertex + 1.0) * 0.5;
				return uv;
			}

			ASEVaryingsDefault Vert( ASEAttributesDefault v  )
			{
				ASEVaryingsDefault o;
				o.vertex = float4(v.vertex.xy, 0.0, 1.0);
				o.texcoord = TransformTriangleVertexToUV (v.vertex.xy);
#if UNITY_UV_STARTS_AT_TOP
				o.texcoord = o.texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
#endif
				o.texcoordStereo = TransformStereoScreenSpaceTex (o.texcoord, 1.0);

				v.texcoord = o.texcoordStereo;
				float4 ase_ppsScreenPosVertexNorm = float4(o.texcoordStereo,0,1);

				

				return o;
			}

			float4 Frag (ASEVaryingsDefault i  ) : SV_Target
			{
				float4 ase_ppsScreenPosFragNorm = float4(i.texcoordStereo,0,1);

				float2 uv_MainTex = i.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 appendResult18 = (float2(( floor( ( uv_MainTex.x * _ResX ) ) / _ResX ) , ( floor( ( uv_MainTex.y * _ResY ) ) / _ResY )));
				float lerpResult24 = lerp( tex2D( _MainTex, appendResult18 ).r , 0.0 , ( 1.0 - _Fade ));
				float2 appendResult3 = (float2(lerpResult24 , lerpResult24));
				

				float4 color = tex2Dlod( _Palette, float4( appendResult3, 0, 0.0) );
				
				return color;
			}
			ENDCG
		}
	}
	
	
}