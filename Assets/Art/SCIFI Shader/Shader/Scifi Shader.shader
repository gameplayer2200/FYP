Shader "SciFiHologram"
{
    Properties
    {
        [HideInInspector] _ShaderType ("Shader Type", Float) = 0
        
            [Header(Main)]
    _MainTex ("Texture", 2D) = "white" {}
    _Color ("Color", Color) = (0,0.5,1,0.7)
    [Enum(Opaque,0,Transparent,1)] _BlendMode ("Blend Mode", Float) = 1
    
    [Header(Hologram Base)]
    _HologramIntensity ("Hologram Intensity", Range(0, 2)) = 1
    _HologramOpacity ("Hologram Opacity", Range(0, 1)) = 0.7
    _HologramFlickerSpeed ("Flicker Speed", Range(0, 10)) = 1
    _HologramFlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0.1
    _FlickerPattern ("Flicker Pattern", Range(0, 3)) = 0
    _FlickerOffset ("Flicker Position Offset", Range(0, 1)) = 0
    
    [Header(Scan Line)]
    [Toggle(_SCANLINE_ON)] _EnableScanLine ("Enable Scan Line", Float) = 1
    _ScanLineColor ("Scan Line Color", Color) = (0,0.8,1,1)
    _ScanLineWidth ("Scan Line Width", Range(0, 1)) = 0.1
    _ScanLineSpeed ("Scan Line Speed", Range(0, 5)) = 1
    _ScanLineAmount ("Scan Line Amount", Range(1, 50)) = 20
    _ScanLineShiftSpeed ("Scan Line Shift Speed", Range(0, 10)) = 0
    _ScanLineDeform ("Scan Line Deformation", Range(0, 1)) = 0
    
    [Header(Rim Effect)]
    [Toggle(_RIM_ON)] _EnableRim ("Enable Rim Effect", Float) = 1
    _RimColor ("Rim Color", Color) = (0,0.8,1,1)
    _RimPower ("Rim Power", Range(0.5, 10)) = 3
    _RimIntensity ("Rim Intensity", Range(0, 2)) = 1
    _RimFlutter ("Rim Flutter", Range(0, 1)) = 0
    _RimFlutterSpeed ("Rim Flutter Speed", Range(0, 10)) = 1 
    
    [Header(Glitch Effect)]
    [Toggle(_GLITCH_ON)] _EnableGlitch ("Enable Glitch", Float) = 0
    _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.1
    _GlitchSpeed ("Glitch Speed", Range(0, 20)) = 5
    _GlitchColorIntensity ("Glitch Color Intensity", Range(0, 1)) = 0.3
    _GlitchFrequency ("Glitch Frequency", Range(0, 10)) = 2
    _GlitchJump ("Glitch Jump", Range(0, 1)) = 0.2
    _GlitchDistortion ("Glitch Distortion", Range(0, 1)) = 0.1
    _GlitchHorizontalIntensity ("Horizontal Intensity", Range(0, 1)) = 1
    
    [Header(Emission)]
    [Toggle(_EMISSION_ON)] _EnableEmission ("Enable Emission", Float) = 1
    _EmissionMap ("Emission Map", 2D) = "white" {}
    _EmissionColor ("Emission Color", Color) = (0,0.5,1,1)
    _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 1
    _EmissionPulse ("Emission Pulse", Range(0, 1)) = 0
    _EmissionPulseSpeed ("Pulse Speed", Range(0, 5)) = 1
    _EmissionAreaScale ("Emission Area Scale", Range(0.1, 2)) = 1
    _EmissionDetail ("Emission Detail", Range(0, 10)) = 1
    
    [Header(Fresnel)]
    [Toggle(_FRESNEL_ON)] _EnableFresnel ("Enable Fresnel", Float) = 1
    _FresnelColor ("Fresnel Color", Color) = (0,0.5,1,1)
    _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 2
    _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 1
    _FresnelExponent ("Fresnel Exponent", Range(0.1, 5)) = 1
    _FresnelSharpness ("Fresnel Sharpness", Range(0, 10)) = 1
    _FresnelColorVariation ("Color Variation", Range(0, 1)) = 0
    
    [Header(Distortion)]
    [Toggle(_DISTORT_ON)] _EnableDistortion ("Enable Distortion", Float) = 0
    _DistortionMap ("Distortion Map", 2D) = "bump" {}
    _DistortionSpeed ("Distortion Speed", Range(0, 5)) = 1
    _DistortionIntensity ("Distortion Intensity", Range(0, 1)) = 0.1
    _DistortionTiling ("Distortion Tiling", Range(0.1, 10)) = 1
    _DistortionDirectionality ("Directionality", Range(0, 1)) = 0.5
    _DistortionAnimation ("Animation Type", Range(0, 3)) = 0
    
    [Header(Hologram Lines)]
    [Toggle(_LINES_ON)] _EnableLines ("Enable Lines", Float) = 1
    _LineSpacing ("Line Spacing", Range(1, 50)) = 30
    _LineSpeed ("Line Speed", Range(-10, 10)) = 1
    _LineIntensity ("Line Intensity", Range(0, 1)) = 0.2
    _LineColor ("Line Color", Color) = (0,0.8,1,1)
    _LineWidth ("Line Width", Range(0, 1)) = 0.5
    _LineDistortion ("Line Distortion", Range(0, 1)) = 0
    _LineVariation ("Line Variation", Range(0, 1)) = 0
    _LineFadeDistance ("Line Fade Distance", Range(0, 1)) = 0
    _LineHighlightFrequency ("Highlight Frequency", Range(0, 10)) = 0

    [Header(Noise)]
    [Toggle(_NOISE_ON)] _EnableNoise ("Enable Noise", Float) = 0
    _NoiseMap ("Noise Map", 2D) = "white" {}
    _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.1
    _NoiseSpeed ("Noise Speed", Range(0, 10)) = 1
    _NoiseTiling ("Noise Tiling", Range(0.1, 10)) = 1
    _NoiseSaturation ("Noise Saturation", Range(0, 1)) = 0.5
    _NoiseContrast ("Noise Contrast", Range(0, 2)) = 1
    _NoiseMovement ("Noise Movement", Vector) = (1,1,0,0)
    
    [Header(Data Stream)]
    [Toggle(_DATASTREAM_ON)] _EnableDataStream ("Enable Data Stream", Float) = 0
    _DataStreamTex ("Data Stream Texture", 2D) = "white" {}
    _DataStreamSpeed ("Data Stream Speed", Range(0, 10)) = 1
    _DataStreamIntensity ("Data Stream Intensity", Range(0, 1)) = 0.5
    _DataStreamTiling ("Data Stream Tiling", Range(0.1, 10)) = 1
    _DataStreamColor ("Data Stream Color", Color) = (0,0.8,1,1)
    _DataStreamGlow ("Data Stream Glow", Range(0, 2)) = 1
    _DataStreamScrollDir ("Scroll Direction", Vector) = (0,1,0,0)
    _DataStreamDensity ("Stream Density", Range(0.1, 10)) = 1
    
    [Header(3D Hologram Projection)]
    [Toggle(_PROJECTION_ON)] _EnableProjection ("Enable 3D Projection", Float) = 0
    _ProjectionHeight ("Projection Height", Range(0, 3)) = 1
    _ProjectionFadeDistance ("Fade Distance", Range(0, 1)) = 0.3
    _ProjectionColor ("Projection Color", Color) = (0,0.5,1,0.3)
    _ProjectionIntensity ("Projection Intensity", Range(0, 2)) = 1
    _ProjectionFlicker ("Projection Flicker", Range(0, 1)) = 0.2
    _ProjectionSpread ("Projection Spread", Range(0, 1)) = 0.5
    _ProjectionAngleMultiplier ("Angle Multiplier", Range(0, 2)) = 1
    _ProjectionDistortion ("Distortion", Range(0, 1)) = 0
    
    [Header(Interface Elements)]
    [Toggle(_INTERFACE_ON)] _EnableInterface ("Enable Interface Elements", Float) = 0
    _InterfaceTex ("Interface Texture", 2D) = "white" {}
    _InterfaceSpeed ("Interface Animation Speed", Range(0, 5)) = 1
    _InterfaceColor ("Interface Color", Color) = (0,0.8,1,1)
    _InterfaceIntensity ("Interface Intensity", Range(0, 1)) = 0.5
    _InterfaceTiling ("Interface Tiling", Range(0.1, 10)) = 1
    _InterfaceGlow ("Interface Glow", Range(0, 2)) = 1
    _InterfaceScrollX ("Scroll X", Range(-2, 2)) = 0
    _InterfaceScrollY ("Scroll Y", Range(-2, 2)) = 0
    _InterfaceScanlines ("Scanline Effect", Range(0, 1)) = 0
    
    [Header(Edges)]
    [Toggle(_EDGES_ON)] _EnableEdges ("Enable Edge Highlight", Float) = 0
    _EdgeColor ("Edge Color", Color) = (0,0.8,1,1)
    _EdgeThickness ("Edge Thickness", Range(0, 0.1)) = 0.01
    _EdgeSharpness ("Edge Sharpness", Range(1, 20)) = 5
    _EdgePower ("Edge Power", Range(0.1, 5)) = 1
    _EdgeEmission ("Edge Emission", Range(0, 5)) = 1
    _EdgeDistortion ("Edge Distortion", Range(0, 1)) = 0
    _EdgeNoise ("Edge Noise", Range(0, 1)) = 0
    
    [Header(Advanced Hologram Effects)]
    [Toggle(_HEXGRID_ON)] _EnableHexGrid ("Enable Hexagon Grid", Float) = 0
    _HexSize ("Hexagon Size", Range(1, 50)) = 10
    _HexIntensity ("Hexagon Intensity", Range(0, 1)) = 0.5
    _HexColor ("Hexagon Color", Color) = (0,0.8,1,1)
    _HexEmission ("Hexagon Emission", Range(0, 2)) = 1
    _HexDistortion ("Hexagon Distortion", Range(0, 1)) = 0
    _HexRotation ("Hexagon Rotation", Range(0, 6.28)) = 0
    
    [Toggle(_SQUAREGRID_ON)] _EnableSquareGrid ("Enable Square Grid", Float) = 0
    _SquareSize ("Square Size", Range(1, 50)) = 15
    _SquareIntensity ("Square Intensity", Range(0, 1)) = 0.5
    _SquareColor ("Square Color", Color) = (0,0.8,1,1)
    _SquareEdgeWidth ("Square Edge Width", Range(0, 0.2)) = 0.05
    _SquareDistortion ("Square Distortion", Range(0, 1)) = 0
    
    [Toggle(_CIRCUIT_ON)] _EnableCircuit ("Enable Circuit Pattern", Float) = 0
    _CircuitTex ("Circuit Texture", 2D) = "white" {}
    _CircuitIntensity ("Circuit Intensity", Range(0, 1)) = 0.5
    _CircuitColor ("Circuit Color", Color) = (0,0.8,1,1)
    _CircuitSpeed ("Circuit Speed", Range(0, 2)) = 0.5
    _CircuitDistortion ("Circuit Distortion", Range(0, 1)) = 0
    _CircuitDetail ("Circuit Detail", Range(0, 10)) = 1
    
    [Toggle(_WIREFRAME_ON)] _EnableWireframe ("Enable Wireframe", Float) = 0
    _WireframeColor ("Wireframe Color", Color) = (0,0.8,1,1)
    _WireframeThickness ("Wireframe Thickness", Range(0, 0.1)) = 0.02
    _WireframeSmoothing ("Wireframe Smoothing", Range(0, 0.1)) = 0.01
    _WireframeDensity ("Wireframe Density", Range(1, 10)) = 1
    _WireframeGlow ("Wireframe Glow", Range(0, 2)) = 1
    
    [Header(Time_Based Effects)]
    [Toggle(_PULSE_ON)] _EnablePulse ("Enable Pulse Effect", Float) = 0
    _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1
    _PulseAmplitude ("Pulse Amplitude", Range(0, 1)) = 0.2
    _PulseColor ("Pulse Color", Color) = (0,0.8,1,1)
    _PulseCenter ("Pulse Center", Vector) = (0.5,0.5,0,0)
    _PulseDistortion ("Pulse Distortion", Range(0, 1)) = 0
    _PulseExp ("Pulse Exponent", Range(1, 5)) = 2
    
    [Toggle(_SCANNING_ON)] _EnableScanning ("Enable Scanning Effect", Float) = 0
    _ScanningSpeed ("Scanning Speed", Range(0, 5)) = 1
    _ScanningWidth ("Scanning Width", Range(0, 0.5)) = 0.1
    _ScanningColor ("Scanning Color", Color) = (0,0.8,1,1)
    _ScanningDirection ("Scanning Direction", Vector) = (0,1,0,0)
    _ScanningIntensity ("Scanning Intensity", Range(0, 2)) = 1
    _ScanningFade ("Scanning Fade", Range(0, 1)) = 0.5
    
    [Toggle(_BEAM_ON)] _EnableBeam ("Enable Beam Effect", Float) = 0
    _BeamSpeed ("Beam Speed", Range(0, 10)) = 2
    _BeamWidth ("Beam Width", Range(0, 0.2)) = 0.05
    _BeamColor ("Beam Color", Color) = (0,0.8,1,1)
    _BeamCount ("Beam Count", Range(1, 10)) = 3
    _BeamDistortion ("Beam Distortion", Range(0, 1)) = 0
    _BeamShift ("Beam Shift", Range(-1, 1)) = 0
    
    [Header(Color Effects)]
    [Toggle(_COLORSHIFT_ON)] _EnableColorShift ("Enable Color Shift", Float) = 0
    _ColorShiftSpeed ("Color Shift Speed", Range(0, 5)) = 1
    _ColorShiftIntensity ("Color Shift Intensity", Range(0, 1)) = 0.5
    _ColorShiftHue ("Hue Range", Range(0, 1)) = 1
    _ColorShiftStartHue ("Start Hue", Range(0, 1)) = 0
    
    [Toggle(_COLORBANDING_ON)] _EnableColorBanding ("Enable Color Banding", Float) = 0
    _ColorBands ("Color Bands", Range(1, 10)) = 3
    _BandingContrast ("Banding Contrast", Range(0, 2)) = 1
    _BandingSaturation ("Banding Saturation", Range(0, 2)) = 1
    _BandingBrightness ("Banding Brightness", Range(0, 2)) = 1
    _BandingDirection ("Banding Direction", Vector) = (0,1,0,0)
    
    [Toggle(_CHROMATIC_ON)] _EnableChromatic ("Enable Chromatic Aberration", Float) = 0
    _ChromaticIntensity ("Chromatic Intensity", Range(0, 0.1)) = 0.05
    _ChromaticOffset ("Chromatic Offset", Range(0, 1)) = 0.5
    _ChromaticCenter ("Chromatic Center", Vector) = (0.5,0.5,0,0)
    _ChromaticMode ("Chromatic Mode", Range(0, 2)) = 0
    
    [Toggle(_VIGNETTE_ON)] _EnableVignette ("Enable Vignette", Float) = 0
    _VignetteColor ("Vignette Color", Color) = (0,0.5,1,1)
    _VignettePower ("Vignette Power", Range(1, 5)) = 2
    _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.5
    _VignetteCenter ("Vignette Center", Vector) = (0.5,0.5,0,0)
    _VignetteSpeed ("Vignette Pulse Speed", Range(0, 5)) = 0
    
    [Header(Volumetric Effects)]
    [Toggle(_VOLUMETRIC_ON)] _EnableVolumetric ("Enable Volumetric Light", Float) = 0
    _VolumetricColor ("Volumetric Color", Color) = (0,0.5,1,0.2)
    _VolumetricIntensity ("Volumetric Intensity", Range(0, 2)) = 1
    _VolumetricNoise ("Volumetric Noise", Range(0, 1)) = 0.5
    _VolumetricSpeed ("Volumetric Speed", Range(0, 5)) = 1
    _VolumetricDistance ("Volumetric Distance", Range(0, 10)) = 5
    _VolumetricFalloff ("Volumetric Falloff", Range(0.1, 10)) = 1
    
    [Header(Depth Effects)]
    [Toggle(_DEPTH_ON)] _EnableDepth ("Enable Depth Effect", Float) = 0
    _DepthColor ("Depth Color", Color) = (0,0.5,1,1)
    _DepthDistance ("Depth Distance", Range(0, 10)) = 1
    _DepthGradient ("Depth Gradient", Range(0, 5)) = 1
    _DepthIntersectionThreshold ("Intersection Threshold", Range(0, 1)) = 0.1
    _DepthFadeWidth ("Depth Fade Width", Range(0, 1)) = 0.5
}

SubShader
{
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
    
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    
    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    TEXTURE2D(_EmissionMap);
    SAMPLER(sampler_EmissionMap);
    TEXTURE2D(_DistortionMap);
    SAMPLER(sampler_DistortionMap);
    TEXTURE2D(_NoiseMap);
    SAMPLER(sampler_NoiseMap);
    TEXTURE2D(_DataStreamTex);
    SAMPLER(sampler_DataStreamTex);
    TEXTURE2D(_InterfaceTex);
    SAMPLER(sampler_InterfaceTex);
    TEXTURE2D(_CircuitTex);
    SAMPLER(sampler_CircuitTex);
    
    float4 _Color;
    float _HologramIntensity;
    float _HologramOpacity;
    float _HologramFlickerSpeed;
    float _HologramFlickerIntensity;
    float _FlickerPattern;
    float _FlickerOffset;
    
    float4 _ScanLineColor;
    float _ScanLineWidth;
    float _ScanLineSpeed;
    float _ScanLineAmount;
    float _ScanLineShiftSpeed;
    float _ScanLineDeform;
    
    float4 _RimColor;
    float _RimPower;
    float _RimIntensity;
    float _RimFlutter;
    float _RimFlutterSpeed;
    
    float _GlitchIntensity;
    float _GlitchSpeed;
    float _GlitchColorIntensity;
    float _GlitchFrequency;
    float _GlitchJump;
    float _GlitchDistortion;
    float _GlitchHorizontalIntensity;
    
    float4 _EmissionColor;
    float _EmissionIntensity;
    float _EmissionPulse;
    float _EmissionPulseSpeed;
    float _EmissionAreaScale;
    float _EmissionDetail;
    
    float4 _FresnelColor;
    float _FresnelPower;
    float _FresnelIntensity;
    float _FresnelExponent;
    float _FresnelSharpness;
    float _FresnelColorVariation;
    
    float _DistortionSpeed;
    float _DistortionIntensity;
    float _DistortionTiling;
    float _DistortionDirectionality;
    float _DistortionAnimation;
    
    float _LineSpacing;
    float _LineSpeed;
    float _LineIntensity;
    float4 _LineColor;
    float _LineWidth;
    float _LineDistortion;
    float _LineVariation;
    float _LineFadeDistance;
    float _LineHighlightFrequency;
    
    float _NoiseIntensity;
    float _NoiseSpeed;
    float _NoiseTiling;
    float _NoiseSaturation;
    float _NoiseContrast;
    float4 _NoiseMovement;
    
    float _DataStreamSpeed;
    float _DataStreamIntensity;
    float _DataStreamTiling;
    float4 _DataStreamColor;
    float _DataStreamGlow;
    float4 _DataStreamScrollDir;
    float _DataStreamDensity;
    
    float _ProjectionHeight;
    float _ProjectionFadeDistance;
    float4 _ProjectionColor;
    float _ProjectionIntensity;
    float _ProjectionFlicker;
    float _ProjectionSpread;
    float _ProjectionAngleMultiplier;
    float _ProjectionDistortion;
    
    float _InterfaceSpeed;
    float4 _InterfaceColor;
    float _InterfaceIntensity;
    float _InterfaceTiling;
    float _InterfaceGlow;
    float _InterfaceScrollX;
    float _InterfaceScrollY;
    float _InterfaceScanlines;
    
    float4 _EdgeColor;
    float _EdgeThickness;
    float _EdgeSharpness;
    float _EdgePower;
    float _EdgeEmission;
    float _EdgeDistortion;
    float _EdgeNoise;
    
    float _HexSize;
    float _HexIntensity;
    float4 _HexColor;
    float _HexEmission;
    float _HexDistortion;
    float _HexRotation;
    
    float _SquareSize;
    float _SquareIntensity;
    float4 _SquareColor;
    float _SquareEdgeWidth;
    float _SquareDistortion;
    
    float _CircuitIntensity;
    float4 _CircuitColor;
    float _CircuitSpeed;
    float _CircuitDistortion;
    float _CircuitDetail;
    
    float4 _WireframeColor;
    float _WireframeThickness;
    float _WireframeSmoothing;
    float _WireframeDensity;
    float _WireframeGlow;
    
    float _PulseSpeed;
    float _PulseAmplitude;
    float4 _PulseColor;
    float4 _PulseCenter;
    float _PulseDistortion;
    float _PulseExp;
    
    float _ScanningSpeed;
    float _ScanningWidth;
    float4 _ScanningColor;
    float4 _ScanningDirection;
    float _ScanningIntensity;
    float _ScanningFade;
    
    float _BeamSpeed;
    float _BeamWidth;
    float4 _BeamColor;
    int _BeamCount;
    float _BeamDistortion;
    float _BeamShift;
    
    float _ColorShiftSpeed;
    float _ColorShiftIntensity;
    float _ColorShiftHue;
    float _ColorShiftStartHue;
    
    float _ColorBands;
    float _BandingContrast;
    float _BandingSaturation;
    float _BandingBrightness;
    float4 _BandingDirection;
    
    float _ChromaticIntensity;
    float _ChromaticOffset;
    float4 _ChromaticCenter;
    float _ChromaticMode;
    
    float4 _VignetteColor;
    float _VignettePower;
    float _VignetteIntensity;
    float4 _VignetteCenter;
    float _VignetteSpeed;
    
    float4 _VolumetricColor;
    float _VolumetricIntensity;
    float _VolumetricNoise;
    float _VolumetricSpeed;
    float _VolumetricDistance;
    float _VolumetricFalloff;
    
    float4 _DepthColor;
    float _DepthDistance;
    float _DepthGradient;
    float _DepthIntersectionThreshold;
    float _DepthFadeWidth;
    
    float _ShaderType;
    float _BlendMode;
    
    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 normalWS : NORMAL;
        float3 positionWS : TEXCOORD1;
        float4 tangentWS : TEXCOORD2;
        float3 bitangentWS : TEXCOORD3;
        float4 color : COLOR;
        float3 viewDirWS : TEXCOORD4;
        float4 positionNDC : TEXCOORD5;
        float3 worldPos : TEXCOORD6;
        float4 screenPos : TEXCOORD7;
    };
    
    float rand(float2 co)
    {
        co = frac(co * float2(123.4, 345.6));
        float dot_product = dot(co, float2(12.9898, 78.233));
        return frac(sin(dot_product) * 43758.5453);
    }
    
    float fbm(float2 p, int octaves, float lacunarity, float gain)
    {
        float sum = 0.0;
        float amp = 1.0;
        float freq = 1.0;
        
        for(int i = 0; i < octaves; i++)
        {
            sum += amp * (noise(p * freq) * 2.0 - 1.0);
            freq *= lacunarity;
            amp *= gain;
        }
        
        return sum * 0.5 + 0.5;
    }
    
    float noise(float2 uv)
    {
        float2 iuv = floor(uv);
        float2 fuv = frac(uv);
        
        fuv = fuv * fuv * (3.0 - 2.0 * fuv);
        
        float a = rand(iuv);
        float b = rand(iuv + float2(1.0, 0.0));
        float c = rand(iuv + float2(0.0, 1.0));
        float d = rand(iuv + float2(1.0, 1.0));
        
        return lerp(lerp(a, b, fuv.x), lerp(c, d, fuv.x), fuv.y);
    }
    
    float voronoiNoise(float2 uv, float cellDensity, float jitter)
    {
        float2 n = floor(uv * cellDensity);
        float2 f = frac(uv * cellDensity);
        
        float md = 8.0;
        float2 mr;
        
        for(int y = -1; y <= 1; y++)
        {
            for(int x = -1; x <= 1; x++)
            {
                float2 neighbor = float2(float(x), float(y));
                float2 p = rand(n + neighbor);
                
                p = 0.5 + 0.5 * sin(_Time.y * 0.5 + 6.2831 * p) * jitter;
                
                float2 r = neighbor + p - f;
                float d = dot(r, r);
                
                if(d < md)
                {
                    md = d;
                    mr = r;
                }
            }
        }
        
        return sqrt(md);
    }
    
    float2 rotateUV(float2 uv, float rotation)
    {
        float sinX = sin(rotation);
        float cosX = cos(rotation);
        float2x2 rotationMatrix = float2x2(cosX, -sinX, sinX, cosX);
        return mul(uv - 0.5, rotationMatrix) + 0.5;
    }
    
    float3 hsv2rgb(float3 c)
    {
        float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
        float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
        return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
    }
    
    float3 rgb2hsv(float3 c)
    {
        float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
        float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
        float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
        
        float d = q.x - min(q.w, q.y);
        float e = 1.0e-10;
        return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
    }
    
    float hexDist(float2 p)
    {
        p = abs(p);
        float c = dot(p, normalize(float2(1, 1.73)));
        c = max(c, p.x);
        return c;
    }
    
    float hexGrid(float2 uv, float size, float width, float2 offset, float distortion)
    {
        uv += offset;
        
        if (distortion > 0)
        {
            float2 noiseUV = uv * 3.0 + _Time.y * 0.1;
            float noiseVal = noise(noiseUV) * distortion;
            uv += float2(noiseVal, noiseVal * 0.7);
        }
        
        float2 r = float2(1, 1.73);
        float2 h = r * 0.5;
        float2 a = fmod(uv * size, r) - h;
        float2 b = fmod(uv * size - h, r) - h;
        
        float2 gv = length(a) < length(b) ? a : b;
        float2 id = uv * size - gv;
        
        float dist = hexDist(gv);
        float lineWidth = width * 0.1;
        
        float hexLine = smoothstep(size - lineWidth, size, dist);
        
        float idHash = frac(sin(dot(id, float2(12.9898, 78.233))) * 43758.5453);
        float variation = (sin(_Time.y * 0.5 + idHash * 10.0) * 0.5 + 0.5) * 0.3 + 0.7;
        hexLine *= variation;
        
        return hexLine;
    }
    
    float squareGrid(float2 uv, float size, float lineWidth, float distortion)
    {
        if (distortion > 0)
        {
            float2 noiseUV = uv * 3.0 + _Time.y * 0.1;
            float noiseVal = noise(noiseUV) * distortion;
            uv += float2(noiseVal, noiseVal * 0.7);
        }
        
        float2 grid = frac(uv * size);
        grid = min(grid, 1.0 - grid) * 2.0;
        
        float threshold = lineWidth;
        float lines = smoothstep(threshold, threshold + 0.01, min(grid.x, grid.y));
        
        return 1.0 - lines;
    }
    
    float wireframe(float2 uv, float thickness, float smoothing, float density)
    {
        uv *= density;
        float2 grid = abs(frac(uv) * 2.0 - 1.0);
        float2 fw = fwidth(uv) * 2.0;
        float2 smoothGrid = smoothstep(1.0 - thickness - smoothing * fw, 1.0 - thickness + smoothing * fw, grid);
        float wire = max(smoothGrid.x, smoothGrid.y);
        return wire;
    }
    
    float3 ComputeScanLine(float2 uv, float3 color, float time)
    {
        float2 scanUV = uv;
        
        if (_ScanLineDeform > 0)
        {
            float deform = sin(uv.x * 10.0 + time * 2.0) * _ScanLineDeform * 0.05;
            scanUV.y += deform;
        }
        
        if (_ScanLineShiftSpeed > 0)
        {
            float shift = sin(time * _ScanLineShiftSpeed) * 0.01;
            scanUV.y += shift;
        }
        
        float scanLine = step(1.0 - _ScanLineWidth, frac((scanUV.y - time * _ScanLineSpeed) * _ScanLineAmount));
        
        float intensity = 1.0;
        if (_FlickerPattern > 0)
        {
            intensity = (sin(time * 5.0) * 0.5 + 0.5) * 0.3 + 0.7;
        }
        
        return lerp(color, _ScanLineColor.rgb * _ScanLineColor.a, scanLine * intensity);
    }
    
    float3 ComputeRim(float3 normalWS, float3 viewDirWS, float3 baseColor, float time)
    {
        float rim = 1.0 - saturate(dot(normalWS, viewDirWS));
        
        float rimPower = _RimPower;
        float rimIntensity = _RimIntensity;
        
        if (_RimFlutter > 0)
        {
            float flutter = sin(time * _RimFlutterSpeed) * _RimFlutter;
            rimPower *= (1.0 + flutter * 0.5);
            rimIntensity *= (1.0 + flutter * 0.3);
        }
        
        rim = pow(rim, rimPower);
        
        return baseColor + _RimColor.rgb * rim * rimIntensity;
    }
    
    float3 ComputeGlitch(float2 uv, float3 color, float time)
    {
        float2 glitchUV = uv;
        
        float blockIntensity = _GlitchIntensity * (sin(time * _GlitchSpeed) * 0.5 + 0.5);
        blockIntensity += _GlitchIntensity * 0.5 * (sin(time * _GlitchSpeed * 1.5 + 1.3) * 0.5 + 0.5);
        
        if (_GlitchHorizontalIntensity > 0 && rand(floor(uv.yy * 100 + time * 10) / 100) < 0.1 * blockIntensity * _GlitchHorizontalIntensity)
        {
            float glitchAmount = 0.1 * blockIntensity * _GlitchJump;
            glitchUV.x = frac(glitchUV.x + sign(rand(floor(uv.y * 100 + time) / 100) - 0.5) * glitchAmount);
            color.r = color.r * (1 + blockIntensity * _GlitchColorIntensity);
        }
        
        if (rand(floor(uv.xx * 100 + time * 20) / 100) < 0.05 * blockIntensity)
        {
            float glitchAmount = 0.05 * blockIntensity * _GlitchJump;
            glitchUV.y = frac(glitchUV.y + sign(rand(floor(uv.x * 100 + time * 2) / 100) - 0.5) * glitchAmount);
            color.g = color.g * (1 + blockIntensity * _GlitchColorIntensity * 0.5);
        }
        
        float disruptThreshold = 0.003 * _GlitchIntensity;
        if (frac(time * _GlitchFrequency) < disruptThreshold)
        {
            int disruption = floor(rand(float2(time, 2345)) * 10);
            float disruptY = floor(uv.y * 20) / 20;
            if (floor(disruptY * 100) == disruption)
            {
                glitchUV.x = frac(glitchUV.x + rand(float2(time, disruptY)));
            }
        }
        
        if (_GlitchDistortion > 0)
        {
            float2 distortUV = uv + float2(sin(time * 13.0 + uv.y * 20.0), cos(time * 17.0 + uv.x * 23.0)) * _GlitchDistortion * 0.01;
            float3 distortColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortUV).rgb;
            color = lerp(color, distortColor, blockIntensity * _GlitchDistortion);
        }
        
        color += (rand(glitchUV + time) - 0.5) * _GlitchColorIntensity * blockIntensity;
        
        return color;
    }
    
    float3 ComputeFresnel(float3 normalWS, float3 viewDirWS, float3 baseColor, float time)
    {
        float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
        
        fresnel = pow(fresnel, _FresnelPower * _FresnelExponent);
        
        if (_FresnelSharpness > 1.0)
        {
            fresnel = pow(fresnel, _FresnelSharpness);
        }
        
        float4 fresnelColor = _FresnelColor;
        
        if (_FresnelColorVariation > 0)
        {
            float3 hsvColor = rgb2hsv(fresnelColor.rgb);
            hsvColor.x = frac(hsvColor.x + sin(time * 0.5) * _FresnelColorVariation * 0.2);
            fresnelColor.rgb = hsv2rgb(hsvColor);
        }
        
        return baseColor + fresnelColor.rgb * fresnel * _FresnelIntensity;
    }
    
    float3 ComputeHologramLines(float2 uv, float3 color, float time)
    {
        float2 distortedUV = uv;
        if (_LineDistortion > 0)
        {
            float2 noiseUV = uv * 5.0 + float2(time * 0.2, 0);
            float noiseValue = noise(noiseUV) * 2.0 - 1.0;
            distortedUV.x += noiseValue * _LineDistortion * 0.1;
        }
        
        float lineY = frac(distortedUV.y * _LineSpacing + time * _LineSpeed);
        
        float lineIntensityMod = _LineIntensity;
        if (_LineVariation > 0)
        {
            float variation = noise(float2(distortedUV.y * 10.0, time)) * _LineVariation;
            lineIntensityMod *= (1.0 - variation * 0.5);
        }
        
        float linePattern = step(_LineWidth, lineY);
        
        if (_LineHighlightFrequency > 0)
        {
            float highlight = pow(sin(time * _LineHighlightFrequency + distortedUV.y * 20.0) * 0.5 + 0.5, 3.0);
            linePattern *= (1.0 + highlight * 0.5);
        }
        
        if (_LineFadeDistance > 0)
        {
            float fadeDistance = abs(distortedUV.x - 0.5) * 2.0;
            float fade = 1.0 - saturate(fadeDistance / _LineFadeDistance);
            linePattern *= fade;
        }
        
        return lerp(color, _LineColor.rgb * _LineColor.a, linePattern * lineIntensityMod);
    }
    
    float3 ComputeDistortion(float2 uv, float time)
    {
        float2 distortUV = uv * _DistortionTiling;
        
        if (_DistortionAnimation == 0)
        {
            distortUV += float2(time * _DistortionSpeed, time * _DistortionSpeed * 0.5); 
        }
        else if (_DistortionAnimation == 1)
        {
            float angle = time * _DistortionSpeed;
            float s = sin(angle);
            float c = cos(angle);
            float2 dir = float2(c, s) * 0.1;
            distortUV += dir * time * _DistortionSpeed;
        }
        else if (_DistortionAnimation == 2)
        {
            float pulse = (sin(time * _DistortionSpeed * 0.5) * 0.5 + 0.5) * 0.2 + 0.8;
            distortUV *= pulse;
        }
        else
        {
            distortUV.x += sin(distortUV.y * 10.0 + time * _DistortionSpeed) * 0.1;
            distortUV.y += cos(distortUV.x * 10.0 + time * _DistortionSpeed * 0.7) * 0.1;
        }
        
        float3 distortNormal = UnpackNormal(SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, distortUV));
        
        if (_DistortionDirectionality != 0.5)
        {
            float dirFactor = (_DistortionDirectionality - 0.5) * 2.0;
            distortNormal.x *= (1.0 + dirFactor);
            distortNormal.y *= (1.0 - abs(dirFactor));
        }
        
        return distortNormal;
    }
    
    float ComputeFlicker(float time, float2 uv)
    {
        float flicker = 1.0;
        
        if (_FlickerPattern < 1)
        {
            flicker = sin(time * _HologramFlickerSpeed * 10) * 0.5 + 0.5;
            flicker *= cos(time * _HologramFlickerSpeed * 7.3) * 0.5 + 0.5;
        }
        else if (_FlickerPattern < 2)
        {
            flicker = sin(time * _HologramFlickerSpeed * 5) * 0.5 + 0.5;
            flicker = pow(flicker, 4.0);
        }
        else if (_FlickerPattern < 3)
        {
            flicker = noise(float2(time * _HologramFlickerSpeed, 0));
        }
        else
        {
            float segment = floor(uv.y * 5.0 + _FlickerOffset * 10.0) * 0.2;
            flicker = noise(float2(time * _HologramFlickerSpeed + segment, segment * 3.0));
        }
        
        flicker = 1.0 - (flicker * _HologramFlickerIntensity);
        return flicker;
    }
    
    float ComputeNoise(float2 uv, float time)
    {
        float2 noiseUV = uv * _NoiseTiling + float2(
            time * _NoiseSpeed * _NoiseMovement.x, 
            time * _NoiseSpeed * _NoiseMovement.y
        );
        
        float noiseValue = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;
        
        if (_NoiseSaturation > 0)
        {
            float3 noiseHSV = float3(noiseValue, _NoiseSaturation, noiseValue);
            float3 noiseRGB = hsv2rgb(noiseHSV);
            noiseValue = length(noiseRGB) / 1.732;
        }
        
        if (_NoiseContrast != 1.0)
        {
            noiseValue = pow(abs(noiseValue), _NoiseContrast);
        }
        
        return noiseValue * _NoiseIntensity;
    }
    
    float3 ComputeDataStream(float2 uv, float3 color, float time)
    {
        float2 dirVector = normalize(_DataStreamScrollDir.xy);
        if (length(_DataStreamScrollDir.xy) < 0.1) dirVector = float2(0, 1);
        
        float2 rotatedUV = float2(
            dot(uv - 0.5, float2(dirVector.y, -dirVector.x)),
            dot(uv - 0.5, dirVector)
        ) + 0.5;
        
        float2 dataUV = float2(
            rotatedUV.x * _DataStreamTiling, 
            rotatedUV.y * _DataStreamTiling * _DataStreamDensity
        ) + float2(0, time * _DataStreamSpeed);
        
        float4 dataColor = SAMPLE_TEXTURE2D(_DataStreamTex, sampler_DataStreamTex, dataUV);
        
        dataColor.rgb *= _DataStreamColor.rgb * _DataStreamGlow;
        
        return lerp(color, dataColor.rgb, dataColor.a * _DataStreamIntensity);
    }
    
    float3 ComputeInterfaceElements(float2 uv, float3 color, float time)
    {
        float2 interfaceUV = uv * _InterfaceTiling + float2(
            time * _InterfaceScrollX, 
            time * _InterfaceScrollY
        );
        
        float4 interfaceColor = SAMPLE_TEXTURE2D(_InterfaceTex, sampler_InterfaceTex, interfaceUV);
        
        if (_InterfaceScanlines > 0)
        {
            float scanline = step(0.5, frac(uv.y * 50.0 + time * 0.1));
            interfaceColor.a *= (1.0 - _InterfaceScanlines * 0.5 + scanline * _InterfaceScanlines);
        }
        
        interfaceColor.rgb *= _InterfaceColor.rgb * _InterfaceGlow;
        
        return lerp(color, interfaceColor.rgb, interfaceColor.a * _InterfaceIntensity);
    }
    
    float ComputeProjection(float3 positionWS, float3 basePositionWS, float3 normalWS, float3 viewDirWS, float time)
    {
        float distance = length(positionWS.y - basePositionWS.y);
        
        if (_ProjectionAngleMultiplier > 0)
        {
            float viewDot = abs(dot(normalWS, viewDirWS));
            distance *= (1.0 + (1.0 - viewDot) * _ProjectionAngleMultiplier);
        }
        
        float spread = _ProjectionSpread * 0.5 + 0.5;
        float projection = 1.0 - saturate(distance / (_ProjectionHeight * spread));
        
        float fade = smoothstep(0, _ProjectionFadeDistance, projection);
        
        if (_ProjectionDistortion > 0)
        {
            float distortion = noise(float2(positionWS.x * 2.0 + time, positionWS.z * 2.0)) * _ProjectionDistortion;
            fade *= (1.0 - distortion * 0.5);
        }
        
        if (_ProjectionFlicker > 0)
        {
            float flicker = sin(time * 3.0) * 0.5 + 0.5;
            flicker = 1.0 - (flicker * _ProjectionFlicker);
            fade *= flicker;
        }
        
        return fade * _ProjectionIntensity;
    }
    
    float ComputeEdges(float3 normalWS, float3 viewDirWS, float time)
    {
        float edge = 1.0 - saturate(dot(normalWS, viewDirWS));
        
        if (_EdgeDistortion > 0)
        {
            float distortion = noise(float2(edge * 10.0 + time, edge * 5.0 - time)) * _EdgeDistortion;
            edge *= (1.0 + distortion);
        }
        
        edge = pow(abs(edge), _EdgeSharpness) * _EdgePower;
        
        if (_EdgeNoise > 0)
        {
            float noiseVal = noise(float2(time * 2.0, edge * 20.0)) * 2.0 - 1.0;
            edge *= (1.0 + noiseVal * _EdgeNoise);
        }
        
        return saturate(edge - (1.0 - _EdgeThickness));
    }
    
    float ComputeHexagonGrid(float2 uv, float time)
    {
        float2 hexUV = uv * _HexSize;
        
        hexUV = rotateUV(hexUV, _HexRotation + time * 0.1);
        
        float2 offset = float2(0, 0);
        if (_HexDistortion > 0)
        {
            offset = float2(
                sin(time * 0.5 + uv.y * 10.0), 
                cos(time * 0.7 + uv.x * 10.0)
            ) * _HexDistortion * 0.1;
        }
        
        float hex = hexGrid(hexUV, 0.9, 0.02, offset, _HexDistortion);
        
        return hex * _HexIntensity;
    }
    
    float ComputeSquareGrid(float2 uv, float time)
    {
        float2 gridUV = uv * _SquareSize;
        
        gridUV = rotateUV(gridUV, -time * 0.05);
        
        float square = squareGrid(gridUV, 1.0, _SquareEdgeWidth, _SquareDistortion);
        
        return square * _SquareIntensity;
    }
    
    float3 ComputeCircuitPattern(float2 uv, float3 color, float time)
    {
        float2 circuitUV = uv;
        if (_CircuitDistortion > 0)
        {
            float2 noiseUV = uv * 5.0 + float2(time * 0.1, time * 0.2);
            float2 noiseOffset = float2(
                noise(noiseUV),
                noise(noiseUV + float2(100, 100))
            ) * 2.0 - 1.0;
            
            circuitUV += noiseOffset * _CircuitDistortion * 0.05;
        }
        
        circuitUV += float2(time * _CircuitSpeed, 0);
        
        float detailFactor = 1.0 + _CircuitDetail;
        float4 circuitTex = SAMPLE_TEXTURE2D(_CircuitTex, sampler_CircuitTex, circuitUV);
        
        if (_CircuitDetail > 0)
        {
            float detailTex = SAMPLE_TEXTURE2D(_CircuitTex, sampler_CircuitTex, circuitUV * detailFactor).r;
            circuitTex.r = max(circuitTex.r, detailTex * 0.5);
        }
        
        return lerp(color, _CircuitColor.rgb, circuitTex.r * _CircuitIntensity);
    }
    
    float3 ComputeWireframe(float2 uv, float3 color)
    {
        float wire = wireframe(uv, _WireframeThickness, _WireframeSmoothing, _WireframeDensity);
        float3 wireColor = _WireframeColor.rgb * (1.0 + _WireframeGlow);
        return lerp(color, wireColor, wire * _WireframeColor.a);
    }
    
    float3 ComputePulseEffect(float2 uv, float3 color, float time)
    {
        float2 center = _PulseCenter.xy;
        if (length(_PulseCenter.xy) < 0.1) center = float2(0.5, 0.5);
        float dist = length(uv - center) * 2.0;
        
        if (_PulseDistortion > 0)
        {
            float distort = sin(dist * 10.0 + time) * _PulseDistortion * 0.1;
            dist += distort;
        }
        
        float pulse = sin(time * _PulseSpeed - dist * 3.0) * 0.5 + 0.5;
        
        pulse = pow(pulse, _PulseExp);
        
        pulse = smoothstep(0.5 - _PulseAmplitude, 0.5 + _PulseAmplitude, pulse);
        return lerp(color, _PulseColor.rgb, pulse * _PulseColor.a);
    }
    
    float3 ComputeScanningEffect(float2 uv, float3 color, float time)
    {
        float scan = 0;
        float2 center = float2(0.5, 0.5);
        float2 dir = normalize(_ScanningDirection.xy);
        if (length(_ScanningDirection.xy) < 0.1) dir = float2(0, 1);
        
        float scanPos = dot(uv - center, dir) + 0.5;
        
        scanPos = frac(scanPos + time * _ScanningSpeed);
        
        scan = smoothstep(0, _ScanningWidth, scanPos) * 
               smoothstep(scanPos, scanPos - _ScanningWidth, 0);
        
        if (_ScanningFade > 0)
        {
            float distFade = 1.0 - saturate(length(uv - center) * 2.0 * _ScanningFade);
            scan *= distFade;
        }
        
        return lerp(color, _ScanningColor.rgb, scan * _ScanningColor.a * _ScanningIntensity);
    }
    
    float3 ComputeBeamEffect(float2 uv, float3 color, float time)
    {
        float beam = 0;
        
        uv.x += _BeamShift * 0.5;
        
        if (_BeamDistortion > 0)
        {
            float distort = sin(uv.y * 20.0 + time * 5.0) * _BeamDistortion * 0.05;
            uv.x += distort;
        }
        
        for(int i = 0; i < min(_BeamCount, 10); i++)
        {
            float offset = float(i) / max(_BeamCount, 1);
            float speedVariation = 1.0 + sin(offset * 6.28) * 0.2;
            float beamPos = frac(uv.x + time * _BeamSpeed * speedVariation + offset);
            
            float beamValue = smoothstep(0, _BeamWidth, beamPos) * 
                             smoothstep(beamPos, beamPos - _BeamWidth, 0);
            beam += beamValue;
        }
        
        beam = saturate(beam);
        return lerp(color, _BeamColor.rgb, beam * _BeamColor.a);
    }
    
    float3 ComputeColorShift(float3 color, float time)
    {
        float3 shiftedColor = color;
        
        float hueShift = _ColorShiftStartHue + frac(time * _ColorShiftSpeed * 0.1) * _ColorShiftHue;
        
        float3 hsv = rgb2hsv(color);
        hsv.x = frac(hsv.x + hueShift);
        shiftedColor = hsv2rgb(hsv);
        
        return lerp(color, shiftedColor, _ColorShiftIntensity);
    }
    
    float3 ComputeColorBanding(float3 color, float2 uv)
    {
        float2 dir = normalize(_BandingDirection.xy);
        if (length(_BandingDirection.xy) < 0.1) dir = float2(0, 1);
        
        float projection = dot(uv - 0.5, dir) + 0.5;
        
        float band = floor(projection * _ColorBands) / _ColorBands;
        
        float3 bandedColor;
        
        float3 hsvColor = rgb2hsv(color);
        
        hsvColor.x = frac(hsvColor.x + band * 0.1);
        hsvColor.y *= _BandingSaturation;
        hsvColor.z = pow(abs(hsvColor.z), 1.0/_BandingContrast) * _BandingBrightness;
        
        bandedColor = hsv2rgb(hsvColor);
        
        return bandedColor;
    }
    
    float3 ComputeChromaticAberration(float2 uv, float3 baseColor)
    {
        float2 center = _ChromaticCenter.xy;
        if (length(_ChromaticCenter.xy) < 0.1) center = float2(0.5, 0.5);
        
        float2 direction = uv - center;
        float distance = length(direction);
        
        if (length(direction) > 0.001)
            direction = normalize(direction);
        else
            direction = float2(0, 0);
        
        float3 color;
        
        if (_ChromaticMode < 1)
        {
            float2 redUV = uv - direction * _ChromaticIntensity * distance * (1.0 + _ChromaticOffset);
            float2 blueUV = uv + direction * _ChromaticIntensity * distance * (1.0 - _ChromaticOffset);
            
            color.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, redUV).r;
            color.g = baseColor.g;
            color.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, blueUV).b;
        }
        else if (_ChromaticMode < 2)
        {
            float2 redUV = center + rotateUV(uv - center, _ChromaticIntensity * 0.1);
            float2 blueUV = center + rotateUV(uv - center, -_ChromaticIntensity * 0.1);
            
            color.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, redUV).r;
            color.g = baseColor.g;
            color.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, blueUV).b;
        }
        else
        {
            float2 redUV = uv + float2(-_ChromaticIntensity, -_ChromaticIntensity) * distance;
            float2 blueUV = uv + float2(_ChromaticIntensity, _ChromaticIntensity) * distance;
            
            color.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, redUV).r;
            color.g = baseColor.g;
            color.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, blueUV).b;
        }
        
        return color;
    }
    
    float3 ComputeVignette(float2 uv, float3 color, float time)
    {
        float2 center = _VignetteCenter.xy;
        if (length(_VignetteCenter.xy) < 0.1) center = float2(0.5, 0.5);
        
        float vignetteStrength = _VignetteIntensity;
        
        if (_VignetteSpeed > 0)
        {
            float pulse = sin(time * _VignetteSpeed) * 0.5 + 0.5;
            vignetteStrength *= (0.8 + pulse * 0.4);
        }
        
        float vignette = 1.0 - length(uv - center) * 2.0;
        vignette = pow(abs(vignette), _VignettePower);
        
        return lerp(color, _VignetteColor.rgb, (1.0 - vignette) * vignetteStrength);
    }
    
    float3 ComputeVolumetricEffect(float3 positionWS, float3 color, float time)
    {
        float3 objectCenter = positionWS;
        
        float viewDistance = length(_WorldSpaceCameraPos - positionWS);
        
        float distanceFalloff = 1.0 - saturate(viewDistance / _VolumetricDistance);
        distanceFalloff = pow(distanceFalloff, _VolumetricFalloff);
        
        float noiseEffect = 1.0;
        if (_VolumetricNoise > 0)
        {
            float3 noisePos = positionWS * 0.1 + float3(0, time * _VolumetricSpeed * 0.1, 0);
            float noiseVal = noise(noisePos.xy) * noise(noisePos.yz) * noise(noisePos.xz);
            noiseEffect = 1.0 - (_VolumetricNoise * (1.0 - noiseVal));
        }
        
        float volumetricEffect = distanceFalloff * noiseEffect;
        
        return lerp(color, _VolumetricColor.rgb, volumetricEffect * _VolumetricIntensity * _VolumetricColor.a);
    }
    
    float4 ComputeDepthEffect(float4 screenPos, float3 color, float alpha)
    {
        float4 finalColor = float4(color, alpha);
        
        float2 screenUV = screenPos.xy / screenPos.w;
        
        float fragmentDepth = screenPos.z / screenPos.w;
        
        float sceneDepth = SampleSceneDepth(screenUV);
        
        float linearSceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
        float linearFragmentDepth = LinearEyeDepth(fragmentDepth, _ZBufferParams);
        
        float depthDifference = linearSceneDepth - linearFragmentDepth;
        
        float depthFactor = saturate(depthDifference / _DepthDistance);
        depthFactor = pow(depthFactor, _DepthGradient);
        
        float intersection = 1.0 - saturate(depthDifference / _DepthIntersectionThreshold);
        intersection = smoothstep(0, _DepthFadeWidth, intersection);
        
        finalColor.rgb = lerp(finalColor.rgb, _DepthColor.rgb, depthFactor);
        
        finalColor.rgb += _DepthColor.rgb * intersection * 2.0;
        
        finalColor.a = max(finalColor.a, intersection * _DepthColor.a);
        
        return finalColor;
    }
    ENDHLSL
    
    Pass
    {
        Name "Forward"
        Tags { "LightMode" = "UniversalForward" }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        
        #pragma shader_feature_local _SCANLINE_ON
        #pragma shader_feature_local _RIM_ON
        #pragma shader_feature_local _GLITCH_ON
        #pragma shader_feature_local _EMISSION_ON
        #pragma shader_feature_local _FRESNEL_ON
        #pragma shader_feature_local _DISTORT_ON
        #pragma shader_feature_local _LINES_ON
        #pragma shader_feature_local _NOISE_ON
        #pragma shader_feature_local _DATASTREAM_ON
        #pragma shader_feature_local _PROJECTION_ON
        #pragma shader_feature_local _INTERFACE_ON
        #pragma shader_feature_local _EDGES_ON
        #pragma shader_feature_local _HEXGRID_ON
        #pragma shader_feature_local _SQUAREGRID_ON
        #pragma shader_feature_local _CIRCUIT_ON
        #pragma shader_feature_local _WIREFRAME_ON
        #pragma shader_feature_local _PULSE_ON
        #pragma shader_feature_local _SCANNING_ON
        #pragma shader_feature_local _BEAM_ON
        #pragma shader_feature_local _COLORSHIFT_ON
        #pragma shader_feature_local _COLORBANDING_ON
        #pragma shader_feature_local _CHROMATIC_ON
        #pragma shader_feature_local _VIGNETTE_ON
        #pragma shader_feature_local _VOLUMETRIC_ON
        #pragma shader_feature_local _DEPTH_ON
        
        Varyings vert(Attributes input)
        {
            Varyings output;
            
            float3 positionOS = input.positionOS.xyz;
            
            #ifdef _PROJECTION_ON
            if (_ShaderType == 3) {
                float projectionFactor = positionOS.y / _ProjectionHeight;
                positionOS.xz *= 1.0 + projectionFactor * 0.2;
                
                if (_ProjectionDistortion > 0) {
                    float noise1 = sin(positionOS.x * 10.0 + positionOS.z * 5.0 + _Time.y);
                    float noise2 = sin(positionOS.z * 8.0 - positionOS.x * 4.0 + _Time.y * 1.3);
                    float combinedNoise = (noise1 + noise2) * 0.5 * _ProjectionDistortion * 0.1;
                    
                    positionOS.xz += combinedNoise * projectionFactor;
                }
            }
            #endif
            
            output.positionWS = TransformObjectToWorld(positionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.positionNDC = output.positionCS;
            output.positionNDC.xyz /= output.positionNDC.w;
            
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
            output.bitangentWS = cross(output.normalWS, output.tangentWS.xyz) * input.tangentOS.w;
            
            output.viewDirWS = normalize(_WorldSpaceCameraPos - output.positionWS);
            output.worldPos = output.positionWS;
            
            output.uv = input.uv;
            output.color = input.color;
            
            output.screenPos = ComputeScreenPos(output.positionCS);
            
            return output;
        }
        
        half4 frag(Varyings input) : SV_Target
        {
            float time = _Time.y;
            float2 uv = input.uv;
            
            #ifdef _DISTORT_ON
            float3 distortNormal = ComputeDistortion(uv, time);
            uv += distortNormal.xy * _DistortionIntensity;
            #endif
            
            half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            half3 color = texColor.rgb * _Color.rgb;
            
            #ifdef _CHROMATIC_ON
            color = ComputeChromaticAberration(uv, color);
            #endif
            
            float flicker = 1.0;
            if (_HologramFlickerIntensity > 0) {
                flicker = ComputeFlicker(time, uv);
            }
            
            #ifdef _SCANLINE_ON
            color = ComputeScanLine(uv, color, time);
            #endif
            
            #ifdef _LINES_ON
            color = ComputeHologramLines(uv, color, time);
            #endif
            
            #ifdef _HEXGRID_ON
            float hex = ComputeHexagonGrid(uv, time);
            color = lerp(color, _HexColor.rgb * _HexEmission, hex);
            #endif
            
            #ifdef _SQUAREGRID_ON
            float square = ComputeSquareGrid(uv, time);
            color = lerp(color, _SquareColor.rgb, square);
            #endif
            
            #ifdef _CIRCUIT_ON
            color = ComputeCircuitPattern(uv, color, time);
            #endif
            
            #ifdef _WIREFRAME_ON
            color = ComputeWireframe(uv, color);
            #endif
            
            float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
            
            #ifdef _RIM_ON
            color = ComputeRim(input.normalWS, viewDirWS, color, time);
            #endif
            
            #ifdef _FRESNEL_ON
            color = ComputeFresnel(input.normalWS, viewDirWS, color, time);
            #endif
            
            #ifdef _GLITCH_ON
            color = ComputeGlitch(uv, color, time);
            #endif
            
            #ifdef _EMISSION_ON
            half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv * _EmissionAreaScale).rgb * _EmissionColor.rgb * _EmissionIntensity;
            
            if (_EmissionPulse > 0) {
                float pulse = sin(time * _EmissionPulseSpeed) * 0.5 + 0.5;
                emission *= 1.0 + pulse * _EmissionPulse;
            }
            
            if (_EmissionDetail > 1.0) {
                float emissionDetail = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv * _EmissionAreaScale * _EmissionDetail).r;
                emission += emissionDetail * _EmissionColor.rgb * _EmissionIntensity * 0.5;
            }
            
            color += emission;
            #endif
            
            #ifdef _NOISE_ON
            float noise = ComputeNoise(uv, time);
            color += noise;
            #endif
            
            #ifdef _DATASTREAM_ON
            if (_ShaderType == 1 || _ShaderType == 2) {
                color = ComputeDataStream(uv, color, time);
            }
            #endif
            
            #ifdef _INTERFACE_ON
            if (_ShaderType == 2) {
                color = ComputeInterfaceElements(uv, color, time);
            }
            #endif
            
            #ifdef _PULSE_ON
            color = ComputePulseEffect(uv, color, time);
            #endif
            
            #ifdef _SCANNING_ON
            color = ComputeScanningEffect(uv, color, time);
            #endif
            
            #ifdef _BEAM_ON
            color = ComputeBeamEffect(uv, color, time);
            #endif
            
            #ifdef _COLORSHIFT_ON
            color = ComputeColorShift(color, time);
            #endif
            
            #ifdef _COLORBANDING_ON
            color = ComputeColorBanding(color, uv);
            #endif
            
            #ifdef _EDGES_ON
            float edge = ComputeEdges(input.normalWS, viewDirWS, time);
            float3 edgeColor = _EdgeColor.rgb * (1.0 + _EdgeEmission);
            color = lerp(color, edgeColor, edge * _EdgeColor.a);
            #endif
            
            #ifdef _VIGNETTE_ON
            color = ComputeVignette(uv, color, time);
            #endif
            
            #ifdef _VOLUMETRIC_ON
            color = ComputeVolumetricEffect(input.positionWS, color, time);
            #endif
            
            float projectionFade = 1.0;
            #ifdef _PROJECTION_ON
            if (_ShaderType == 3) {
                float3 basePositionWS = TransformObjectToWorld(float3(0, 0, 0));
                projectionFade = ComputeProjection(input.positionWS, basePositionWS, input.normalWS, viewDirWS, time);
                color = lerp(color, _ProjectionColor.rgb, (1.0 - projectionFade) * 0.5);
            }
            #endif
            
            color *= _HologramIntensity * flicker;
            
            float alpha = _Color.a * _HologramOpacity * flicker;
            
            if (_ShaderType == 3) {
                alpha *= projectionFade;
            }
            
            float4 finalColor = float4(color, alpha * texColor.a);
            
            #ifdef _DEPTH_ON
            finalColor = ComputeDepthEffect(input.screenPos, finalColor.rgb, finalColor.a);
            #endif
            
            return finalColor;
        }
        ENDHLSL
    }
}

CustomEditor "SciFiForge.HologramShaderGUI"
}
           
           