using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SciFiForge
{
    public class HologramShaderGUI : ShaderGUI
    {
        private bool showMainOptions = true;
        private bool showHologramOptions = true;
        private bool showScanLineOptions = true;
        private bool showRimOptions = true;
        private bool showGlitchOptions = false;
        private bool showEmissionOptions = false;
        private bool showFresnelOptions = true;
        private bool showDistortionOptions = false;
        private bool showLinesOptions = true;
        private bool showNoiseOptions = false;
        private bool showDataStreamOptions = false;
        private bool showProjectionOptions = false;
        private bool showInterfaceOptions = false;
        private bool showEdgesOptions = false;
        private bool showHexGridOptions = false;
        private bool showSquareGridOptions = false;
        private bool showCircuitOptions = false;
        private bool showWireframeOptions = false;
        private bool showPulseOptions = false;
        private bool showScanningOptions = false;
        private bool showBeamOptions = false;
        private bool showColorShiftOptions = false;
        private bool showColorBandingOptions = false;
        private bool showChromaticOptions = false;
        private bool showVignetteOptions = false;
        private bool showVolumetricOptions = false;
        private bool showDepthOptions = false;
        private bool showPresets = true;
        
        private GUIContent[] shaderTypeNames = new GUIContent[] {
            new GUIContent("Standard Hologram"),
            new GUIContent("Data Stream Hologram"),
            new GUIContent("Interface Hologram"),
            new GUIContent("3D Projection")
        };
        
        private GUIContent[] blendModeNames = new GUIContent[] {
            new GUIContent("Opaque"),
            new GUIContent("Transparent")
        };
        
        private string[] standardPresets = new string[] {
            "None",
            "Classic Hologram",
            "Damaged Hologram",
            "Alien Technology",
            "Energy Field",
            "Cyber Identity",
            "Ghostly Apparition",
            "Neon Pulse",
            "Quantum Matrix",
            "Holo Blueprint",
            "Digital Mirage",
            "Starfield Hologram",
            "Glitch Core",
            "Vaporwave Hologram"
        };
        
        private string[] dataStreamPresets = new string[] {
            "None",
            "Data Stream",
            "Matrix Code",
            "Neural Network",
            "Digital Virus",
            "Binary Flow",
            "Crypto Stream",
            "Code Cascade",
            "Signal Pulse",
            "Data Vortex",
            "Quantum Data",
            "Info Wave",
            "Encrypted Flow"
        };
        
        private string[] interfacePresets = new string[] {
            "None",
            "Advanced Interface",
            "Tactical Display",
            "Medical Scan",
            "Space Navigation",
            "Cyber Console",
            "Holo Dashboard",
            "Augmented Reality",
            "Control Panel",
            "Data Grid",
            "Tech HUD",
            "Virtual Terminal",
            "SciFi Interface"
        };
        
        private string[] projectionPresets = new string[] {
            "None",
            "3D Projection",
            "Star Map",
            "Molecular Structure",
            "Character Projection",
            "Planet Hologram",
            "Ship Blueprint",
            "Orbital Scan",
            "Holo Globe",
            "Cosmic Projection",
            "Galactic Map",
            "Structure Scan",
            "Holo Avatar",
            "Quantum Core"
        };
        
        private int selectedStandardPreset = 0;
        private int selectedDataStreamPreset = 0;
        private int selectedInterfacePreset = 0;
        private int selectedProjectionPreset = 0;
        
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material targetMat = materialEditor.target as Material;
            
            EditorGUILayout.LabelField("SciFi Hologram Shader", EditorStyles.boldLabel);
            
            // Shader Type Selection
            MaterialProperty shaderTypeProp = FindProperty("_ShaderType", properties);
            EditorGUI.BeginChangeCheck();
            int shaderType = (int)shaderTypeProp.floatValue;
            shaderType = EditorGUILayout.Popup(new GUIContent("Hologram Type"), shaderType, shaderTypeNames);
            if (EditorGUI.EndChangeCheck())
            {
                shaderTypeProp.floatValue = shaderType;
                SetupShaderKeywords(targetMat, shaderType);
                
                // Reset preset selection when changing shader type
                selectedStandardPreset = 0;
                selectedDataStreamPreset = 0;
                selectedInterfacePreset = 0;
                selectedProjectionPreset = 0;
            }
            
            EditorGUILayout.Space();
            
            // Presets Section
            showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
            if (showPresets)
            {
                EditorGUI.indentLevel++;
                
                if (shaderType == 0)
                {
                    EditorGUI.BeginChangeCheck();
                    selectedStandardPreset = EditorGUILayout.Popup("Standard Presets", selectedStandardPreset, standardPresets);
                    if (EditorGUI.EndChangeCheck() && selectedStandardPreset > 0)
                    {
                        ApplyStandardPreset(targetMat, selectedStandardPreset);
                    }
                }
                else if (shaderType == 1)
                {
                    EditorGUI.BeginChangeCheck();
                    selectedDataStreamPreset = EditorGUILayout.Popup("Data Stream Presets", selectedDataStreamPreset, dataStreamPresets);
                    if (EditorGUI.EndChangeCheck() && selectedDataStreamPreset > 0)
                    {
                        ApplyDataStreamPreset(targetMat, selectedDataStreamPreset);
                    }
                }
                else if (shaderType == 2)
                {
                    EditorGUI.BeginChangeCheck();
                    selectedInterfacePreset = EditorGUILayout.Popup("Interface Presets", selectedInterfacePreset, interfacePresets);
                    if (EditorGUI.EndChangeCheck() && selectedInterfacePreset > 0)
                    {
                        ApplyInterfacePreset(targetMat, selectedInterfacePreset);
                    }
                }
                else if (shaderType == 3)
                {
                    EditorGUI.BeginChangeCheck();
                    selectedProjectionPreset = EditorGUILayout.Popup("3D Projection Presets", selectedProjectionPreset, projectionPresets);
                    if (EditorGUI.EndChangeCheck() && selectedProjectionPreset > 0)
                    {
                        ApplyProjectionPreset(targetMat, selectedProjectionPreset);
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Main Settings
            showMainOptions = EditorGUILayout.Foldout(showMainOptions, "Main Settings", true);
            if (showMainOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty mainTex = FindProperty("_MainTex", properties);
                materialEditor.TexturePropertySingleLine(new GUIContent("Main Texture"), mainTex, FindProperty("_Color", properties));
                materialEditor.TextureScaleOffsetProperty(mainTex);
                
                MaterialProperty blendModeProp = FindProperty("_BlendMode", properties);
                EditorGUI.BeginChangeCheck();
                int blendMode = (int)blendModeProp.floatValue;
                blendMode = EditorGUILayout.Popup(new GUIContent("Blend Mode"), blendMode, blendModeNames);
                if (EditorGUI.EndChangeCheck())
                {
                    blendModeProp.floatValue = blendMode;
                    SetupBlendMode(targetMat, blendMode);
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Hologram Base Options
            showHologramOptions = EditorGUILayout.Foldout(showHologramOptions, "Hologram Base Options", true);
            if (showHologramOptions)
            {
                EditorGUI.indentLevel++;
                materialEditor.RangeProperty(FindProperty("_HologramIntensity", properties), "Hologram Intensity");
                materialEditor.RangeProperty(FindProperty("_HologramOpacity", properties), "Hologram Opacity");
                materialEditor.RangeProperty(FindProperty("_HologramFlickerSpeed", properties), "Flicker Speed");
                materialEditor.RangeProperty(FindProperty("_HologramFlickerIntensity", properties), "Flicker Intensity");
                materialEditor.RangeProperty(FindProperty("_FlickerPattern", properties), "Flicker Pattern");
                materialEditor.RangeProperty(FindProperty("_FlickerOffset", properties), "Flicker Offset");
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Scan Line Options
            showScanLineOptions = EditorGUILayout.Foldout(showScanLineOptions, "Scan Line Options", true);
            if (showScanLineOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableScanLine = FindProperty("_EnableScanLine", properties);
                materialEditor.ShaderProperty(enableScanLine, "Enable Scan Line");
                
                bool scanLineEnabled = targetMat.IsKeywordEnabled("_SCANLINE_ON");
                EditorGUI.BeginDisabledGroup(!scanLineEnabled);
                materialEditor.ColorProperty(FindProperty("_ScanLineColor", properties), "Scan Line Color");
                materialEditor.RangeProperty(FindProperty("_ScanLineWidth", properties), "Scan Line Width");
                materialEditor.RangeProperty(FindProperty("_ScanLineSpeed", properties), "Scan Line Speed");
                materialEditor.RangeProperty(FindProperty("_ScanLineAmount", properties), "Scan Line Amount");
                materialEditor.RangeProperty(FindProperty("_ScanLineShiftSpeed", properties), "Scan Line Shift Speed");
                materialEditor.RangeProperty(FindProperty("_ScanLineDeform", properties), "Scan Line Deformation");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Rim Effect Options
            showRimOptions = EditorGUILayout.Foldout(showRimOptions, "Rim Effect Options", true);
            if (showRimOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableRim = FindProperty("_EnableRim", properties);
                materialEditor.ShaderProperty(enableRim, "Enable Rim Effect");
                
                bool rimEnabled = targetMat.IsKeywordEnabled("_RIM_ON");
                EditorGUI.BeginDisabledGroup(!rimEnabled);
                materialEditor.ColorProperty(FindProperty("_RimColor", properties), "Rim Color");
                materialEditor.RangeProperty(FindProperty("_RimPower", properties), "Rim Power");
                materialEditor.RangeProperty(FindProperty("_RimIntensity", properties), "Rim Intensity");
                materialEditor.RangeProperty(FindProperty("_RimFlutter", properties), "Rim Flutter");
                materialEditor.RangeProperty(FindProperty("_RimFlutterSpeed", properties), "Rim Flutter Speed");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Glitch Effect Options
            showGlitchOptions = EditorGUILayout.Foldout(showGlitchOptions, "Glitch Effect Options", true);
            if (showGlitchOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableGlitch = FindProperty("_EnableGlitch", properties);
                materialEditor.ShaderProperty(enableGlitch, "Enable Glitch");
                
                bool glitchEnabled = targetMat.IsKeywordEnabled("_GLITCH_ON");
                EditorGUI.BeginDisabledGroup(!glitchEnabled);
                materialEditor.RangeProperty(FindProperty("_GlitchIntensity", properties), "Glitch Intensity");
                materialEditor.RangeProperty(FindProperty("_GlitchSpeed", properties), "Glitch Speed");
                materialEditor.RangeProperty(FindProperty("_GlitchColorIntensity", properties), "Glitch Color Intensity");
                materialEditor.RangeProperty(FindProperty("_GlitchFrequency", properties), "Glitch Frequency");
                materialEditor.RangeProperty(FindProperty("_GlitchJump", properties), "Glitch Jump");
                materialEditor.RangeProperty(FindProperty("_GlitchDistortion", properties), "Glitch Distortion");
                materialEditor.RangeProperty(FindProperty("_GlitchHorizontalIntensity", properties), "Horizontal Intensity");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Emission Options
            showEmissionOptions = EditorGUILayout.Foldout(showEmissionOptions, "Emission Options", true);
            if (showEmissionOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableEmission = FindProperty("_EnableEmission", properties);
                materialEditor.ShaderProperty(enableEmission, "Enable Emission");
                
                bool emissionEnabled = targetMat.IsKeywordEnabled("_EMISSION_ON");
                EditorGUI.BeginDisabledGroup(!emissionEnabled);
                MaterialProperty emissionMap = FindProperty("_EmissionMap", properties);
                materialEditor.TexturePropertySingleLine(new GUIContent("Emission Map"), emissionMap, FindProperty("_EmissionColor", properties));
                materialEditor.RangeProperty(FindProperty("_EmissionIntensity", properties), "Emission Intensity");
                materialEditor.RangeProperty(FindProperty("_EmissionPulse", properties), "Emission Pulse");
                materialEditor.RangeProperty(FindProperty("_EmissionPulseSpeed", properties), "Pulse Speed");
                materialEditor.RangeProperty(FindProperty("_EmissionAreaScale", properties), "Emission Area Scale");
                materialEditor.RangeProperty(FindProperty("_EmissionDetail", properties), "Emission Detail");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Fresnel Options
            showFresnelOptions = EditorGUILayout.Foldout(showFresnelOptions, "Fresnel Options", true);
            if (showFresnelOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableFresnel = FindProperty("_EnableFresnel", properties);
                materialEditor.ShaderProperty(enableFresnel, "Enable Fresnel");
                
                bool fresnelEnabled = targetMat.IsKeywordEnabled("_FRESNEL_ON");
                EditorGUI.BeginDisabledGroup(!fresnelEnabled);
                materialEditor.ColorProperty(FindProperty("_FresnelColor", properties), "Fresnel Color");
                materialEditor.RangeProperty(FindProperty("_FresnelPower", properties), "Fresnel Power");
                materialEditor.RangeProperty(FindProperty("_FresnelIntensity", properties), "Fresnel Intensity");
                materialEditor.RangeProperty(FindProperty("_FresnelExponent", properties), "Fresnel Exponent");
                materialEditor.RangeProperty(FindProperty("_FresnelSharpness", properties), "Fresnel Sharpness");
                materialEditor.RangeProperty(FindProperty("_FresnelColorVariation", properties), "Color Variation");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Distortion Options
            showDistortionOptions = EditorGUILayout.Foldout(showDistortionOptions, "Distortion Options", true);
            if (showDistortionOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableDistortion = FindProperty("_EnableDistortion", properties);
                materialEditor.ShaderProperty(enableDistortion, "Enable Distortion");
                
                bool distortionEnabled = targetMat.IsKeywordEnabled("_DISTORT_ON");
                EditorGUI.BeginDisabledGroup(!distortionEnabled);
                MaterialProperty distortionMap = FindProperty("_DistortionMap", properties);
                materialEditor.TexturePropertySingleLine(new GUIContent("Distortion Map"), distortionMap);
                materialEditor.RangeProperty(FindProperty("_DistortionSpeed", properties), "Distortion Speed");
                materialEditor.RangeProperty(FindProperty("_DistortionIntensity", properties), "Distortion Intensity");
                materialEditor.RangeProperty(FindProperty("_DistortionTiling", properties), "Distortion Tiling");
                materialEditor.RangeProperty(FindProperty("_DistortionDirectionality", properties), "Directionality");
                materialEditor.RangeProperty(FindProperty("_DistortionAnimation", properties), "Animation Type");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Hologram Lines Options
            showLinesOptions = EditorGUILayout.Foldout(showLinesOptions, "Hologram Lines Options", true);
            if (showLinesOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableLines = FindProperty("_EnableLines", properties);
                materialEditor.ShaderProperty(enableLines, "Enable Lines");
                
                bool linesEnabled = targetMat.IsKeywordEnabled("_LINES_ON");
                EditorGUI.BeginDisabledGroup(!linesEnabled);
                materialEditor.RangeProperty(FindProperty("_LineSpacing", properties), "Line Spacing");
                materialEditor.RangeProperty(FindProperty("_LineSpeed", properties), "Line Speed");
                materialEditor.RangeProperty(FindProperty("_LineIntensity", properties), "Line Intensity");
                materialEditor.ColorProperty(FindProperty("_LineColor", properties), "Line Color");
                materialEditor.RangeProperty(FindProperty("_LineWidth", properties), "Line Width");
                materialEditor.RangeProperty(FindProperty("_LineDistortion", properties), "Line Distortion");
                materialEditor.RangeProperty(FindProperty("_LineVariation", properties), "Line Variation");
                materialEditor.RangeProperty(FindProperty("_LineFadeDistance", properties), "Line Fade Distance");
                materialEditor.RangeProperty(FindProperty("_LineHighlightFrequency", properties), "Highlight Frequency");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Noise Options
            showNoiseOptions = EditorGUILayout.Foldout(showNoiseOptions, "Noise Options", true);
            if (showNoiseOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableNoise = FindProperty("_EnableNoise", properties);
                materialEditor.ShaderProperty(enableNoise, "Enable Noise");
                
                bool noiseEnabled = targetMat.IsKeywordEnabled("_NOISE_ON");
                EditorGUI.BeginDisabledGroup(!noiseEnabled);
                MaterialProperty noiseMap = FindProperty("_NoiseMap", properties);
                materialEditor.TexturePropertySingleLine(new GUIContent("Noise Map"), noiseMap);
                materialEditor.RangeProperty(FindProperty("_NoiseIntensity", properties), "Noise Intensity");
                materialEditor.RangeProperty(FindProperty("_NoiseSpeed", properties), "Noise Speed");
                materialEditor.RangeProperty(FindProperty("_NoiseTiling", properties), "Noise Tiling");
                materialEditor.RangeProperty(FindProperty("_NoiseSaturation", properties), "Noise Saturation");
                materialEditor.RangeProperty(FindProperty("_NoiseContrast", properties), "Noise Contrast");
                materialEditor.VectorProperty(FindProperty("_NoiseMovement", properties), "Noise Movement");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Data Stream Options
            if (shaderType == 1 || shaderType == 2) // Only show for Data Stream and Interface types
            {
                showDataStreamOptions = EditorGUILayout.Foldout(showDataStreamOptions, "Data Stream Options", true);
                if (showDataStreamOptions)
                {
                    EditorGUI.indentLevel++;
                    MaterialProperty enableDataStream = FindProperty("_EnableDataStream", properties);
                    materialEditor.ShaderProperty(enableDataStream, "Enable Data Stream");
                    
                    bool dataStreamEnabled = targetMat.IsKeywordEnabled("_DATASTREAM_ON");
                    EditorGUI.BeginDisabledGroup(!dataStreamEnabled);
                    MaterialProperty dataStreamTex = FindProperty("_DataStreamTex", properties);
                    materialEditor.TexturePropertySingleLine(new GUIContent("Data Stream Texture"), dataStreamTex);
                    materialEditor.RangeProperty(FindProperty("_DataStreamSpeed", properties), "Data Stream Speed");
                    materialEditor.RangeProperty(FindProperty("_DataStreamIntensity", properties), "Data Stream Intensity");
                    materialEditor.RangeProperty(FindProperty("_DataStreamTiling", properties), "Data Stream Tiling");
                    materialEditor.ColorProperty(FindProperty("_DataStreamColor", properties), "Data Stream Color");
                    materialEditor.RangeProperty(FindProperty("_DataStreamGlow", properties), "Data Stream Glow");
                    materialEditor.VectorProperty(FindProperty("_DataStreamScrollDir", properties), "Scroll Direction");
                    materialEditor.RangeProperty(FindProperty("_DataStreamDensity", properties), "Stream Density");
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space();
            }
            
            // 3D Projection Options
            if (shaderType == 3) // Only show for Projection type
            {
                showProjectionOptions = EditorGUILayout.Foldout(showProjectionOptions, "3D Projection Options", true);
                if (showProjectionOptions)
                {
                    EditorGUI.indentLevel++;
                    MaterialProperty enableProjection = FindProperty("_EnableProjection", properties);
                    materialEditor.ShaderProperty(enableProjection, "Enable 3D Projection");
                    
                    bool projectionEnabled = targetMat.IsKeywordEnabled("_PROJECTION_ON");
                    EditorGUI.BeginDisabledGroup(!projectionEnabled);
                    materialEditor.RangeProperty(FindProperty("_ProjectionHeight", properties), "Projection Height");
                    materialEditor.RangeProperty(FindProperty("_ProjectionFadeDistance", properties), "Fade Distance");
                    materialEditor.ColorProperty(FindProperty("_ProjectionColor", properties), "Projection Color");
                    materialEditor.RangeProperty(FindProperty("_ProjectionIntensity", properties), "Projection Intensity");
                    materialEditor.RangeProperty(FindProperty("_ProjectionFlicker", properties), "Projection Flicker");
                    materialEditor.RangeProperty(FindProperty("_ProjectionSpread", properties), "Projection Spread");
                    materialEditor.RangeProperty(FindProperty("_ProjectionAngleMultiplier", properties), "Angle Multiplier");
                    materialEditor.RangeProperty(FindProperty("_ProjectionDistortion", properties), "Distortion");
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space();
            }
            
            // Interface Options
            if (shaderType == 2) // Only show for Interface type
            {
                showInterfaceOptions = EditorGUILayout.Foldout(showInterfaceOptions, "Interface Options", true);
                if (showInterfaceOptions)
                {
                    EditorGUI.indentLevel++;
                    MaterialProperty enableInterface = FindProperty("_EnableInterface", properties);
                    materialEditor.ShaderProperty(enableInterface, "Enable Interface Elements");
                    
                    bool interfaceEnabled = targetMat.IsKeywordEnabled("_INTERFACE_ON");
                    EditorGUI.BeginDisabledGroup(!interfaceEnabled);
                    MaterialProperty interfaceTex = FindProperty("_InterfaceTex", properties);
                    materialEditor.TexturePropertySingleLine(new GUIContent("Interface Texture"), interfaceTex);
                    materialEditor.RangeProperty(FindProperty("_InterfaceSpeed", properties), "Interface Speed");
                    materialEditor.ColorProperty(FindProperty("_InterfaceColor", properties), "Interface Color");
                    materialEditor.RangeProperty(FindProperty("_InterfaceIntensity", properties), "Interface Intensity");
                    materialEditor.RangeProperty(FindProperty("_InterfaceTiling", properties), "Interface Tiling");
                    materialEditor.RangeProperty(FindProperty("_InterfaceGlow", properties), "Interface Glow");
                    materialEditor.RangeProperty(FindProperty("_InterfaceScrollX", properties), "Scroll X");
                    materialEditor.RangeProperty(FindProperty("_InterfaceScrollY", properties), "Scroll Y");
                    materialEditor.RangeProperty(FindProperty("_InterfaceScanlines", properties), "Scanline Effect");
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space();
            }
            
            // Edges Options
            showEdgesOptions = EditorGUILayout.Foldout(showEdgesOptions, "Edges Options", true);
            if (showEdgesOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableEdges = FindProperty("_EnableEdges", properties);
                materialEditor.ShaderProperty(enableEdges, "Enable Edge Highlight");
                
                bool edgesEnabled = targetMat.IsKeywordEnabled("_EDGES_ON");
                EditorGUI.BeginDisabledGroup(!edgesEnabled);
                materialEditor.ColorProperty(FindProperty("_EdgeColor", properties), "Edge Color");
                materialEditor.RangeProperty(FindProperty("_EdgeThickness", properties), "Edge Thickness");
                materialEditor.RangeProperty(FindProperty("_EdgeSharpness", properties), "Edge Sharpness");
                materialEditor.RangeProperty(FindProperty("_EdgePower", properties), "Edge Power");
                materialEditor.RangeProperty(FindProperty("_EdgeEmission", properties), "Edge Emission");
                materialEditor.RangeProperty(FindProperty("_EdgeDistortion", properties), "Edge Distortion");
                materialEditor.RangeProperty(FindProperty("_EdgeNoise", properties), "Edge Noise");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Hex Grid Options
            showHexGridOptions = EditorGUILayout.Foldout(showHexGridOptions, "Hexagon Grid Options", true);
            if (showHexGridOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableHexGrid = FindProperty("_EnableHexGrid", properties);
                materialEditor.ShaderProperty(enableHexGrid, "Enable Hexagon Grid");
                
                bool hexGridEnabled = targetMat.IsKeywordEnabled("_HEXGRID_ON");
                EditorGUI.BeginDisabledGroup(!hexGridEnabled);
                materialEditor.RangeProperty(FindProperty("_HexSize", properties), "Hexagon Size");
                materialEditor.RangeProperty(FindProperty("_HexIntensity", properties), "Hexagon Intensity");
                materialEditor.ColorProperty(FindProperty("_HexColor", properties), "Hexagon Color");
                materialEditor.RangeProperty(FindProperty("_HexEmission", properties), "Hexagon Emission");
                materialEditor.RangeProperty(FindProperty("_HexDistortion", properties), "Hexagon Distortion");
                materialEditor.RangeProperty(FindProperty("_HexRotation", properties), "Hexagon Rotation");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Square Grid Options
            showSquareGridOptions = EditorGUILayout.Foldout(showSquareGridOptions, "Square Grid Options", true);
            if (showSquareGridOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableSquareGrid = FindProperty("_EnableSquareGrid", properties);
                materialEditor.ShaderProperty(enableSquareGrid, "Enable Square Grid");
                
                bool squareGridEnabled = targetMat.IsKeywordEnabled("_SQUAREGRID_ON");
                EditorGUI.BeginDisabledGroup(!squareGridEnabled);
                materialEditor.RangeProperty(FindProperty("_SquareSize", properties), "Square Size");
                materialEditor.RangeProperty(FindProperty("_SquareIntensity", properties), "Square Intensity");
                materialEditor.ColorProperty(FindProperty("_SquareColor", properties), "Square Color");
                materialEditor.RangeProperty(FindProperty("_SquareEdgeWidth", properties), "Square Edge Width");
                materialEditor.RangeProperty(FindProperty("_SquareDistortion", properties), "Square Distortion");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Circuit Options
            showCircuitOptions = EditorGUILayout.Foldout(showCircuitOptions, "Circuit Pattern Options", true);
            if (showCircuitOptions)
            {
                EditorGUI.indentLevel++;
                MaterialProperty enableCircuit = FindProperty("_EnableCircuit", properties);
                materialEditor.ShaderProperty(enableCircuit, "Enable Circuit Pattern");
                
                bool circuitEnabled = targetMat.IsKeywordEnabled("_CIRCUIT_ON");
                EditorGUI.BeginDisabledGroup(!circuitEnabled);
                MaterialProperty circuitTex = FindProperty("_CircuitTex", properties);
                materialEditor.TexturePropertySingleLine(new GUIContent("Circuit Texture"), circuitTex);
                materialEditor.RangeProperty(FindProperty("_CircuitIntensity", properties), "Circuit Intensity");
               materialEditor.ColorProperty(FindProperty("_CircuitColor", properties), "Circuit Color");
               materialEditor.RangeProperty(FindProperty("_CircuitSpeed", properties), "Circuit Speed");
               materialEditor.RangeProperty(FindProperty("_CircuitDistortion", properties), "Circuit Distortion");
               materialEditor.RangeProperty(FindProperty("_CircuitDetail", properties), "Circuit Detail");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Wireframe Options
           showWireframeOptions = EditorGUILayout.Foldout(showWireframeOptions, "Wireframe Options", true);
           if (showWireframeOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableWireframe = FindProperty("_EnableWireframe", properties);
               materialEditor.ShaderProperty(enableWireframe, "Enable Wireframe");
               
               bool wireframeEnabled = targetMat.IsKeywordEnabled("_WIREFRAME_ON");
               EditorGUI.BeginDisabledGroup(!wireframeEnabled);
               materialEditor.ColorProperty(FindProperty("_WireframeColor", properties), "Wireframe Color");
               materialEditor.RangeProperty(FindProperty("_WireframeThickness", properties), "Wireframe Thickness");
               materialEditor.RangeProperty(FindProperty("_WireframeSmoothing", properties), "Wireframe Smoothing");
               materialEditor.RangeProperty(FindProperty("_WireframeDensity", properties), "Wireframe Density");
               materialEditor.RangeProperty(FindProperty("_WireframeGlow", properties), "Wireframe Glow");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Pulse Options
           showPulseOptions = EditorGUILayout.Foldout(showPulseOptions, "Pulse Effect Options", true);
           if (showPulseOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enablePulse = FindProperty("_EnablePulse", properties);
               materialEditor.ShaderProperty(enablePulse, "Enable Pulse Effect");
               
               bool pulseEnabled = targetMat.IsKeywordEnabled("_PULSE_ON");
               EditorGUI.BeginDisabledGroup(!pulseEnabled);
               materialEditor.RangeProperty(FindProperty("_PulseSpeed", properties), "Pulse Speed");
               materialEditor.RangeProperty(FindProperty("_PulseAmplitude", properties), "Pulse Amplitude");
               materialEditor.ColorProperty(FindProperty("_PulseColor", properties), "Pulse Color");
               materialEditor.VectorProperty(FindProperty("_PulseCenter", properties), "Pulse Center");
               materialEditor.RangeProperty(FindProperty("_PulseDistortion", properties), "Pulse Distortion");
               materialEditor.RangeProperty(FindProperty("_PulseExp", properties), "Pulse Exponent");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Scanning Options
           showScanningOptions = EditorGUILayout.Foldout(showScanningOptions, "Scanning Effect Options", true);
           if (showScanningOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableScanning = FindProperty("_EnableScanning", properties);
               materialEditor.ShaderProperty(enableScanning, "Enable Scanning Effect");
               
               bool scanningEnabled = targetMat.IsKeywordEnabled("_SCANNING_ON");
               EditorGUI.BeginDisabledGroup(!scanningEnabled);
               materialEditor.RangeProperty(FindProperty("_ScanningSpeed", properties), "Scanning Speed");
               materialEditor.RangeProperty(FindProperty("_ScanningWidth", properties), "Scanning Width");
               materialEditor.ColorProperty(FindProperty("_ScanningColor", properties), "Scanning Color");
               materialEditor.VectorProperty(FindProperty("_ScanningDirection", properties), "Scanning Direction");
               materialEditor.RangeProperty(FindProperty("_ScanningIntensity", properties), "Scanning Intensity");
               materialEditor.RangeProperty(FindProperty("_ScanningFade", properties), "Scanning Fade");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Beam Options
           showBeamOptions = EditorGUILayout.Foldout(showBeamOptions, "Beam Effect Options", true);
           if (showBeamOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableBeam = FindProperty("_EnableBeam", properties);
               materialEditor.ShaderProperty(enableBeam, "Enable Beam Effect");
               
               bool beamEnabled = targetMat.IsKeywordEnabled("_BEAM_ON");
               EditorGUI.BeginDisabledGroup(!beamEnabled);
               materialEditor.RangeProperty(FindProperty("_BeamSpeed", properties), "Beam Speed");
               materialEditor.RangeProperty(FindProperty("_BeamWidth", properties), "Beam Width");
               materialEditor.ColorProperty(FindProperty("_BeamColor", properties), "Beam Color");
               materialEditor.FloatProperty(FindProperty("_BeamCount", properties), "Beam Count");
               materialEditor.RangeProperty(FindProperty("_BeamDistortion", properties), "Beam Distortion");
               materialEditor.RangeProperty(FindProperty("_BeamShift", properties), "Beam Shift");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Color Shift Options
           showColorShiftOptions = EditorGUILayout.Foldout(showColorShiftOptions, "Color Shift Options", true);
           if (showColorShiftOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableColorShift = FindProperty("_EnableColorShift", properties);
               materialEditor.ShaderProperty(enableColorShift, "Enable Color Shift");
               
               bool colorShiftEnabled = targetMat.IsKeywordEnabled("_COLORSHIFT_ON");
               EditorGUI.BeginDisabledGroup(!colorShiftEnabled);
               materialEditor.RangeProperty(FindProperty("_ColorShiftSpeed", properties), "Color Shift Speed");
               materialEditor.RangeProperty(FindProperty("_ColorShiftIntensity", properties), "Color Shift Intensity");
               materialEditor.RangeProperty(FindProperty("_ColorShiftHue", properties), "Hue Range");
               materialEditor.RangeProperty(FindProperty("_ColorShiftStartHue", properties), "Start Hue");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Color Banding Options
           showColorBandingOptions = EditorGUILayout.Foldout(showColorBandingOptions, "Color Banding Options", true);
           if (showColorBandingOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableColorBanding = FindProperty("_EnableColorBanding", properties);
               materialEditor.ShaderProperty(enableColorBanding, "Enable Color Banding");
               
               bool colorBandingEnabled = targetMat.IsKeywordEnabled("_COLORBANDING_ON");
               EditorGUI.BeginDisabledGroup(!colorBandingEnabled);
               materialEditor.RangeProperty(FindProperty("_ColorBands", properties), "Color Bands");
               materialEditor.RangeProperty(FindProperty("_BandingContrast", properties), "Banding Contrast");
               materialEditor.RangeProperty(FindProperty("_BandingSaturation", properties), "Banding Saturation");
               materialEditor.RangeProperty(FindProperty("_BandingBrightness", properties), "Banding Brightness");
               materialEditor.VectorProperty(FindProperty("_BandingDirection", properties), "Banding Direction");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Chromatic Aberration Options
           showChromaticOptions = EditorGUILayout.Foldout(showChromaticOptions, "Chromatic Aberration Options", true);
           if (showChromaticOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableChromatic = FindProperty("_EnableChromatic", properties);
               materialEditor.ShaderProperty(enableChromatic, "Enable Chromatic Aberration");
               
               bool chromaticEnabled = targetMat.IsKeywordEnabled("_CHROMATIC_ON");
               EditorGUI.BeginDisabledGroup(!chromaticEnabled);
               materialEditor.RangeProperty(FindProperty("_ChromaticIntensity", properties), "Chromatic Intensity");
               materialEditor.RangeProperty(FindProperty("_ChromaticOffset", properties), "Chromatic Offset");
               materialEditor.VectorProperty(FindProperty("_ChromaticCenter", properties), "Chromatic Center");
               materialEditor.RangeProperty(FindProperty("_ChromaticMode", properties), "Chromatic Mode");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Vignette Options
           showVignetteOptions = EditorGUILayout.Foldout(showVignetteOptions, "Vignette Options", true);
           if (showVignetteOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableVignette = FindProperty("_EnableVignette", properties);
               materialEditor.ShaderProperty(enableVignette, "Enable Vignette");
               
               bool vignetteEnabled = targetMat.IsKeywordEnabled("_VIGNETTE_ON");
               EditorGUI.BeginDisabledGroup(!vignetteEnabled);
               materialEditor.ColorProperty(FindProperty("_VignetteColor", properties), "Vignette Color");
               materialEditor.RangeProperty(FindProperty("_VignettePower", properties), "Vignette Power");
               materialEditor.RangeProperty(FindProperty("_VignetteIntensity", properties), "Vignette Intensity");
               materialEditor.VectorProperty(FindProperty("_VignetteCenter", properties), "Vignette Center");
               materialEditor.RangeProperty(FindProperty("_VignetteSpeed", properties), "Vignette Pulse Speed");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Volumetric Options
           showVolumetricOptions = EditorGUILayout.Foldout(showVolumetricOptions, "Volumetric Light Options", true);
           if (showVolumetricOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableVolumetric = FindProperty("_EnableVolumetric", properties);
               materialEditor.ShaderProperty(enableVolumetric, "Enable Volumetric Light");
               
               bool volumetricEnabled = targetMat.IsKeywordEnabled("_VOLUMETRIC_ON");
               EditorGUI.BeginDisabledGroup(!volumetricEnabled);
               materialEditor.ColorProperty(FindProperty("_VolumetricColor", properties), "Volumetric Color");
               materialEditor.RangeProperty(FindProperty("_VolumetricIntensity", properties), "Volumetric Intensity");
               materialEditor.RangeProperty(FindProperty("_VolumetricNoise", properties), "Volumetric Noise");
               materialEditor.RangeProperty(FindProperty("_VolumetricSpeed", properties), "Volumetric Speed");
               materialEditor.RangeProperty(FindProperty("_VolumetricDistance", properties), "Volumetric Distance");
               materialEditor.RangeProperty(FindProperty("_VolumetricFalloff", properties), "Volumetric Falloff");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           
           // Depth Options
           showDepthOptions = EditorGUILayout.Foldout(showDepthOptions, "Depth Effect Options", true);
           if (showDepthOptions)
           {
               EditorGUI.indentLevel++;
               MaterialProperty enableDepth = FindProperty("_EnableDepth", properties);
               materialEditor.ShaderProperty(enableDepth, "Enable Depth Effect");
               
               bool depthEnabled = targetMat.IsKeywordEnabled("_DEPTH_ON");
               EditorGUI.BeginDisabledGroup(!depthEnabled);
               materialEditor.ColorProperty(FindProperty("_DepthColor", properties), "Depth Color");
               materialEditor.RangeProperty(FindProperty("_DepthDistance", properties), "Depth Distance");
               materialEditor.RangeProperty(FindProperty("_DepthGradient", properties), "Depth Gradient");
               materialEditor.RangeProperty(FindProperty("_DepthIntersectionThreshold", properties), "Intersection Threshold");
               materialEditor.RangeProperty(FindProperty("_DepthFadeWidth", properties), "Depth Fade Width");
               EditorGUI.EndDisabledGroup();
               EditorGUI.indentLevel--;
           }
           
           EditorGUILayout.Space();
           materialEditor.RenderQueueField();
       }
       
       private void ApplyStandardPreset(Material material, int presetIndex)
{
    switch (presetIndex)
    {
        case 1: // Classic Hologram
            material.SetColor("_Color", new Color(0, 0.6f, 1f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 2.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.12f);
            material.SetFloat("_FlickerPattern", 0f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.1f, 0.8f, 1f, 1f));
            material.SetFloat("_ScanLineWidth", 0.05f);
            material.SetFloat("_ScanLineSpeed", 0.8f);
            material.SetFloat("_ScanLineAmount", 25f);
            material.SetFloat("_ScanLineDeform", 0.05f);
            material.SetFloat("_EnableLines", 1f);
            material.SetFloat("_LineSpacing", 35f);
            material.SetFloat("_LineSpeed", 0.5f);
            material.SetFloat("_LineIntensity", 0.15f);
            material.SetColor("_LineColor", new Color(0.2f, 0.85f, 1f, 1f));
            material.SetFloat("_LineWidth", 0.6f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0f, 0.7f, 1f, 1f));
            material.SetFloat("_RimPower", 2.8f);
            material.SetFloat("_RimIntensity", 1.0f);
            material.SetFloat("_RimFlutter", 0.05f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0f, 0.5f, 1f, 1f));
            material.SetFloat("_FresnelPower", 1.5f);
            material.SetFloat("_FresnelIntensity", 0.8f);
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_FRESNEL_ON");
            material.EnableKeyword("_LINES_ON");
            material.DisableKeyword("_GLITCH_ON");
            material.DisableKeyword("_EMISSION_ON");
            material.DisableKeyword("_DISTORT_ON");
            material.DisableKeyword("_HEXGRID_ON");
            material.DisableKeyword("_SQUAREGRID_ON");
            break;
                
        case 2: // Damaged Hologram
            material.SetColor("_Color", new Color(0.1f, 0.8f, 0.7f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 4.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.25f);
            material.SetFloat("_FlickerPattern", 1f);
            material.SetFloat("_EnableGlitch", 1f);
            material.SetFloat("_GlitchIntensity", 0.2f);
            material.SetFloat("_GlitchSpeed", 8.0f);
            material.SetFloat("_GlitchColorIntensity", 0.3f);
            material.SetFloat("_GlitchFrequency", 5.0f);
            material.SetFloat("_GlitchJump", 0.15f);
            material.SetFloat("_GlitchDistortion", 0.15f);
            material.SetFloat("_EnableDistortion", 1f);
            material.SetFloat("_DistortionSpeed", 1.5f);
            material.SetFloat("_DistortionIntensity", 0.08f);
            material.SetFloat("_DistortionTiling", 2.0f);
            material.SetFloat("_DistortionAnimation", 2f);
            material.SetFloat("_EnableNoise", 1f);
            material.SetFloat("_NoiseIntensity", 0.1f);
            material.SetFloat("_NoiseSpeed", 2.0f);
            material.SetFloat("_NoiseTiling", 3.0f);
            material.SetFloat("_NoiseContrast", 1.2f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.2f, 0.9f, 0.8f, 1f));
            material.SetFloat("_ScanLineWidth", 0.1f);
            material.SetFloat("_ScanLineSpeed", 2.0f);
            material.SetFloat("_ScanLineAmount", 20f);
            material.SetFloat("_ScanLineDeform", 0.2f);
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_GLITCH_ON");
            material.EnableKeyword("_DISTORT_ON");
            material.EnableKeyword("_NOISE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 3: // Alien Technology
            material.SetColor("_Color", new Color(0.3f, 0.9f, 0.5f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.4f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 1.5f);
            material.SetFloat("_HologramFlickerIntensity", 0.1f);
            material.SetFloat("_FlickerPattern", 2f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 20f);
            material.SetFloat("_HexIntensity", 0.3f);
            material.SetColor("_HexColor", new Color(0.2f, 0.8f, 0.4f, 1f));
            material.SetFloat("_HexEmission", 1.5f);
            material.SetFloat("_EnableEmission", 1f);
            material.SetColor("_EmissionColor", new Color(0.1f, 0.7f, 0.3f, 1f));
            material.SetFloat("_EmissionIntensity", 1.2f);
            material.SetFloat("_EmissionPulse", 0.3f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.3f, 0.9f, 0.5f, 1f));
            material.SetFloat("_ScanLineWidth", 0.08f);
            material.SetFloat("_ScanLineSpeed", 1.2f);
            material.SetFloat("_ScanLineAmount", 30f);
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_HEXGRID_ON");
            material.EnableKeyword("_EMISSION_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 4: // Energy Field
            material.SetColor("_Color", new Color(0.8f, 0.2f, 0.9f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.5f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 3.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.15f);
            material.SetFloat("_FlickerPattern", 0f);
            material.SetFloat("_EnablePulse", 1f);
            material.SetFloat("_PulseSpeed", 1.5f);
            material.SetFloat("_PulseAmplitude", 0.3f);
            material.SetColor("_PulseColor", new Color(0.7f, 0.1f, 0.8f, 1f));
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.9f, 0.3f, 1f, 1f));
            material.SetFloat("_RimPower", 2.5f);
            material.SetFloat("_RimIntensity", 1.2f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0.8f, 0.2f, 0.9f, 1f));
            material.SetFloat("_FresnelPower", 1.8f);
            material.SetFloat("_FresnelIntensity", 0.9f);
            material.EnableKeyword("_PULSE_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_LINES_ON");
            material.DisableKeyword("_GLITCH_ON");
            break;
                
        case 5: // Cyber Identity
            material.SetColor("_Color", new Color(0.1f, 0.5f, 0.9f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 2.5f);
            material.SetFloat("_HologramFlickerIntensity", 0.2f);
            material.SetFloat("_FlickerPattern", 1f);
            material.SetFloat("_EnableCircuit", 1f);
            material.SetFloat("_CircuitIntensity", 0.5f);
            material.SetColor("_CircuitColor", new Color(0.2f, 0.6f, 1f, 1f));
            material.SetFloat("_CircuitSpeed", 0.8f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.1f, 0.5f, 0.9f, 1f));
            material.SetFloat("_ScanLineWidth", 0.06f);
            material.SetFloat("_ScanLineSpeed", 1.0f);
            material.SetFloat("_ScanLineAmount", 20f);
            material.EnableKeyword("_CIRCUIT_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 6: // Ghostly Apparition
            material.SetColor("_Color", new Color(0.7f, 0.7f, 0.7f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 1.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.1f);
            material.SetFloat("_FlickerPattern", 2f);
            material.SetFloat("_EnableVignette", 1f);
            material.SetColor("_VignetteColor", new Color(0.5f, 0.5f, 0.5f, 1f));
            material.SetFloat("_VignettePower", 2.5f);
            material.SetFloat("_VignetteIntensity", 0.6f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.8f, 0.8f, 0.8f, 1f));
            material.SetFloat("_RimPower", 3.0f);
            material.SetFloat("_RimIntensity", 1.0f);
            material.EnableKeyword("_VIGNETTE_ON");
            material.EnableKeyword("_RIM_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 7: // Neon Pulse
            material.SetColor("_Color", new Color(1f, 0.2f, 0.2f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.6f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 3.5f);
            material.SetFloat("_HologramFlickerIntensity", 0.15f);
            material.SetFloat("_FlickerPattern", 0f);
            material.SetFloat("_EnableBeam", 1f);
            material.SetFloat("_BeamSpeed", 2.5f);
            material.SetFloat("_BeamWidth", 0.08f);
            material.SetColor("_BeamColor", new Color(1f, 0.3f, 0.3f, 1f));
            material.SetFloat("_BeamCount", 4f);
            material.SetFloat("_EnableEmission", 1f);
            material.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.2f, 1f));
            material.SetFloat("_EmissionIntensity", 1.5f);
            material.EnableKeyword("_BEAM_ON");
            material.EnableKeyword("_EMISSION_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;
                
        case 8: // Quantum Matrix
            material.SetColor("_Color", new Color(0.2f, 0.8f, 0.2f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.4f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 2.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.1f);
            material.SetFloat("_FlickerPattern", 1f);
            material.SetFloat("_EnableSquareGrid", 1f);
            material.SetFloat("_SquareSize", 25f);
            material.SetFloat("_SquareIntensity", 0.4f);
            material.SetColor("_SquareColor", new Color(0.3f, 0.9f, 0.3f, 1f));
            material.SetFloat("_SquareEdgeWidth", 0.1f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0.2f, 0.8f, 0.2f, 1f));
            material.SetFloat("_FresnelPower", 2.0f);
            material.SetFloat("_FresnelIntensity", 1.0f);
            material.EnableKeyword("_SQUAREGRID_ON");
            material.EnableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 9: // Holo Blueprint
            material.SetColor("_Color", new Color(0.5f, 0.5f, 1f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 2.2f);
            material.SetFloat("_HologramFlickerIntensity", 0.12f);
            material.SetFloat("_FlickerPattern", 0f);
            material.SetFloat("_EnableWireframe", 1f);
            material.SetColor("_WireframeColor", new Color(0.4f, 0.4f, 0.9f, 1f));
            material.SetFloat("_WireframeThickness", 0.03f);
            material.SetFloat("_WireframeSmoothing", 0.02f);
            material.SetFloat("_WireframeDensity", 2f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.5f, 0.5f, 1f, 1f));
            material.SetFloat("_ScanLineWidth", 0.07f);
            material.SetFloat("_ScanLineSpeed", 1.5f);
            material.SetFloat("_ScanLineAmount", 25f);
            material.EnableKeyword("_WIREFRAME_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 10: // Digital Mirage
            material.SetColor("_Color", new Color(0.9f, 0.7f, 0.2f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.5f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 1.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.1f);
            material.SetFloat("_FlickerPattern", 2f);
            material.SetFloat("_EnableVolumetric", 1f);
            material.SetColor("_VolumetricColor", new Color(0.8f, 0.6f, 0.1f, 0.3f));
            material.SetFloat("_VolumetricIntensity", 1.2f);
            material.SetFloat("_VolumetricNoise", 0.5f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.9f, 0.7f, 0.2f, 1f));
            material.SetFloat("_RimPower", 2.8f);
            material.SetFloat("_RimIntensity", 1.0f);
            material.EnableKeyword("_VOLUMETRIC_ON");
            material.EnableKeyword("_RIM_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 11: // Starfield Hologram
            material.SetColor("_Color", new Color(0.3f, 0.7f, 0.9f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.4f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 2.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.12f);
            material.SetFloat("_FlickerPattern", 0f);
            material.SetFloat("_EnableColorShift", 1f);
            material.SetFloat("_ColorShiftSpeed", 1.5f);
            material.SetFloat("_ColorShiftIntensity", 0.6f);
            material.SetFloat("_ColorShiftHue", 0.8f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.3f, 0.7f, 0.9f, 1f));
            material.SetFloat("_ScanLineWidth", 0.05f);
            material.SetFloat("_ScanLineSpeed", 1.0f);
            material.SetFloat("_ScanLineAmount", 20f);
            material.SetFloat("_EnableNoise", 1f);
            material.SetFloat("_NoiseIntensity", 0.08f);
            material.SetFloat("_NoiseSpeed", 1.5f);
            material.SetFloat("_NoiseTiling", 4.0f);
            material.EnableKeyword("_COLORSHIFT_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_NOISE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
                
        case 12: // Glitch Core
            material.SetColor("_Color", new Color(0.8f, 0.2f, 0.8f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 2.5f);
            material.SetFloat("_HologramFlickerIntensity", 0.15f);
            material.SetFloat("_FlickerPattern", 1f);
            material.SetFloat("_EnableGlitch", 1f);
            material.SetFloat("_GlitchIntensity", 0.3f);
            material.SetFloat("_GlitchSpeed", 10.0f);
            material.SetFloat("_GlitchColorIntensity", 0.4f);
            material.SetFloat("_EnableDistortion", 1f);
            material.SetFloat("_DistortionSpeed", 2.0f);
            material.SetFloat("_DistortionIntensity", 0.1f);
            material.SetFloat("_DistortionTiling", 3.0f);
            material.SetFloat("_EnableColorBanding", 1f);
            material.SetFloat("_ColorBands", 5f);
            material.SetFloat("_BandingContrast", 1.4f);
            material.SetFloat("_BandingSaturation", 1.2f);
            material.EnableKeyword("_GLITCH_ON");
            material.EnableKeyword("_DISTORT_ON");
            material.EnableKeyword("_COLORBANDING_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;
                
        case 13: // Vaporwave Hologram
            material.SetColor("_Color", new Color(0.9f, 0.3f, 0.5f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.4f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 2.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.1f);
            material.SetFloat("_FlickerPattern", 0f);
            material.SetFloat("_EnableChromatic", 1f);
            material.SetFloat("_ChromaticIntensity", 0.05f);
            material.SetFloat("_ChromaticOffset", 0.5f);
            material.SetFloat("_EnableGrids", 1f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.9f, 0.3f, 0.5f, 1f));
            material.SetFloat("_RimPower", 2.5f);
            material.SetFloat("_RimIntensity", 1.0f);
            material.SetFloat("_EnableSquareGrid", 1f);
            material.SetFloat("_SquareSize", 30f);
            material.SetFloat("_SquareIntensity", 0.3f);
            material.SetColor("_SquareColor", new Color(0.1f, 0.8f, 0.8f, 1f));
            material.SetFloat("_SquareEdgeWidth", 0.05f);
            material.EnableKeyword("_CHROMATIC_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_SQUAREGRID_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
            
        case 14: // Tech Essence
            material.SetColor("_Color", new Color(0.0f, 0.8f, 0.6f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 1.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.08f);
            material.SetFloat("_FlickerPattern", 1f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0.0f, 0.9f, 0.7f, 1f));
            material.SetFloat("_FresnelPower", 1.8f);
            material.SetFloat("_FresnelIntensity", 0.9f);
            material.SetFloat("_EnableEdges", 1f);
            material.SetColor("_EdgeColor", new Color(0.0f, 0.9f, 0.7f, 1f));
            material.SetFloat("_EdgeThickness", 0.02f);
            material.SetFloat("_EdgeSharpness", 6f);
            material.SetFloat("_EdgePower", 1.5f);
            material.SetFloat("_EdgeEmission", 1.8f);
            material.SetFloat("_EnableLines", 1f);
            material.SetFloat("_LineSpacing", 40f);
            material.SetFloat("_LineSpeed", 0.4f);
            material.SetFloat("_LineIntensity", 0.2f);
            material.SetColor("_LineColor", new Color(0.0f, 0.8f, 0.6f, 1f));
            material.SetFloat("_LineWidth", 0.7f);
            material.EnableKeyword("_FRESNEL_ON");
            material.EnableKeyword("_EDGES_ON");
            material.EnableKeyword("_LINES_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_GLITCH_ON");
            break;
    }
    
    SetupShaderKeywords(material, 0);
}
       
       private void ApplyDataStreamPreset(Material material, int presetIndex)
{
    switch (presetIndex)
    {
        case 1: // Data Stream
            material.SetColor("_Color", new Color(0.1f, 0.7f, 0.6f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.6f);
            material.SetFloat("_HologramFlickerIntensity", 0.06f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.2f);
            material.SetFloat("_DataStreamIntensity", 0.8f);
            material.SetFloat("_DataStreamTiling", 1.5f);
            material.SetColor("_DataStreamColor", new Color(0.2f, 0.9f, 0.8f, 1f));
            material.SetFloat("_DataStreamGlow", 1.3f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.0f);
            material.SetFloat("_EnableLines", 1f);
            material.SetFloat("_LineSpacing", 45f);
            material.SetFloat("_LineSpeed", 0.7f);
            material.SetFloat("_LineIntensity", 0.2f);
            material.SetColor("_LineColor", new Color(0.2f, 0.9f, 0.8f, 1f));
            material.SetFloat("_LineWidth", 0.7f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.2f, 0.9f, 0.8f, 1f));
            material.SetFloat("_RimPower", 2.2f);
            material.SetFloat("_RimIntensity", 0.8f);
            material.SetFloat("_EnableChromatic", 1f);
            material.SetFloat("_ChromaticIntensity", 0.04f);
            material.SetFloat("_ChromaticOffset", 0.6f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_LINES_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_CHROMATIC_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 2: // Matrix Code
            material.SetColor("_Color", new Color(0.0f, 0.8f, 0.2f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.08f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.5f);
            material.SetFloat("_DataStreamIntensity", 0.9f);
            material.SetFloat("_DataStreamTiling", 2.0f);
            material.SetColor("_DataStreamColor", new Color(0.0f, 1.0f, 0.3f, 1f));
            material.SetFloat("_DataStreamGlow", 1.5f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 4.0f);
            material.SetFloat("_EnableGlitch", 1f);
            material.SetFloat("_GlitchIntensity", 0.1f);
            material.SetFloat("_GlitchSpeed", 5.0f);
            material.SetFloat("_GlitchColorIntensity", 0.2f);
            material.SetFloat("_EnableNoise", 1f);
            material.SetFloat("_NoiseIntensity", 0.05f);
            material.SetFloat("_NoiseSpeed", 1.0f);
            material.SetFloat("_NoiseTiling", 2.0f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_GLITCH_ON");
            material.EnableKeyword("_NOISE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 3: // Neural Network
            material.SetColor("_Color", new Color(0.2f, 0.5f, 0.8f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.5f);
            material.SetFloat("_HologramFlickerIntensity", 0.05f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 0.8f);
            material.SetFloat("_DataStreamIntensity", 0.7f);
            material.SetFloat("_DataStreamTiling", 1.2f);
            material.SetColor("_DataStreamColor", new Color(0.3f, 0.6f, 0.9f, 1f));
            material.SetFloat("_DataStreamGlow", 1.2f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0.5f, 0.5f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 2.5f);
            material.SetFloat("_EnableCircuit", 1f);
            material.SetFloat("_CircuitIntensity", 0.4f);
            material.SetColor("_CircuitColor", new Color(0.3f, 0.6f, 0.9f, 1f));
            material.SetFloat("_CircuitSpeed", 0.5f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_CIRCUIT_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 4: // Digital Virus
            material.SetColor("_Color", new Color(0.8f, 0.2f, 0.2f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 1.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.1f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.8f);
            material.SetFloat("_DataStreamIntensity", 0.9f);
            material.SetFloat("_DataStreamTiling", 1.8f);
            material.SetColor("_DataStreamColor", new Color(1.0f, 0.3f, 0.3f, 1f));
            material.SetFloat("_DataStreamGlow", 1.4f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.5f);
            material.SetFloat("_EnableGlitch", 1f);
            material.SetFloat("_GlitchIntensity", 0.2f);
            material.SetFloat("_GlitchSpeed", 7.0f);
            material.SetFloat("_GlitchColorIntensity", 0.3f);
            material.SetFloat("_EnableDistortion", 1f);
            material.SetFloat("_DistortionSpeed", 1.0f);
            material.SetFloat("_DistortionIntensity", 0.05f);
            material.SetFloat("_DistortionTiling", 2.0f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_GLITCH_ON");
            material.EnableKeyword("_DISTORT_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 5: // Binary Flow
            material.SetColor("_Color", new Color(0.1f, 0.9f, 0.9f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.0f);
            material.SetFloat("_DataStreamIntensity", 0.8f);
            material.SetFloat("_DataStreamTiling", 1.5f);
            material.SetColor("_DataStreamColor", new Color(0.2f, 1.0f, 1.0f, 1f));
            material.SetFloat("_DataStreamGlow", 1.3f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.0f);
            material.SetFloat("_EnableLines", 1f);
            material.SetFloat("_LineSpacing", 40f);
            material.SetFloat("_LineSpeed", 0.5f);
            material.SetFloat("_LineIntensity", 0.2f);
            material.SetColor("_LineColor", new Color(0.2f, 1.0f, 1.0f, 1f));
            material.SetFloat("_LineWidth", 0.6f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_LINES_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 6: // Crypto Stream
            material.SetColor("_Color", new Color(0.5f, 0.3f, 0.8f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.9f);
            material.SetFloat("_HologramFlickerIntensity", 0.09f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.3f);
            material.SetFloat("_DataStreamIntensity", 0.8f);
            material.SetFloat("_DataStreamTiling", 1.7f);
            material.SetColor("_DataStreamColor", new Color(0.6f, 0.4f, 0.9f, 1f));
            material.SetFloat("_DataStreamGlow", 1.4f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0.3f, 0.7f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.2f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 20f);
            material.SetFloat("_HexIntensity", 0.3f);
            material.SetColor("_HexColor", new Color(0.6f, 0.4f, 0.9f, 1f));
            material.SetFloat("_HexEmission", 1.3f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_HEXGRID_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 7: // Code Cascade
            material.SetColor("_Color", new Color(0.2f, 0.8f, 0.4f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.6f);
            material.SetFloat("_HologramFlickerIntensity", 0.06f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.4f);
            material.SetFloat("_DataStreamIntensity", 0.9f);
            material.SetFloat("_DataStreamTiling", 1.6f);
            material.SetColor("_DataStreamColor", new Color(0.3f, 0.9f, 0.5f, 1f));
            material.SetFloat("_DataStreamGlow", 1.3f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.5f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.3f, 0.9f, 0.5f, 1f));
            material.SetFloat("_ScanLineWidth", 0.04f);
            material.SetFloat("_ScanLineSpeed", 0.8f);
            material.SetFloat("_ScanLineAmount", 40f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 8: // Signal Pulse
            material.SetColor("_Color", new Color(0.9f, 0.7f, 0.2f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 1.0f);
            material.SetFloat("_HologramFlickerIntensity", 0.1f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.6f);
            material.SetFloat("_DataStreamIntensity", 0.9f);
            material.SetFloat("_DataStreamTiling", 1.8f);
            material.SetColor("_DataStreamColor", new Color(1.0f, 0.8f, 0.3f, 1f));
            material.SetFloat("_DataStreamGlow", 1.5f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.0f);
            material.SetFloat("_EnablePulse", 1f);
            material.SetFloat("_PulseSpeed", 1.2f);
            material.SetFloat("_PulseAmplitude", 0.2f);
            material.SetColor("_PulseColor", new Color(1.0f, 0.8f, 0.3f, 1f));
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_PULSE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 9: // Data Vortex
            material.SetColor("_Color", new Color(0.3f, 0.6f, 0.9f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.08f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.3f);
            material.SetFloat("_DataStreamIntensity", 0.8f);
            material.SetFloat("_DataStreamTiling", 1.5f);
            material.SetColor("_DataStreamColor", new Color(0.4f, 0.7f, 1.0f, 1f));
            material.SetFloat("_DataStreamGlow", 1.4f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0.7f, 0.7f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.0f);
            material.SetFloat("_EnableDistortion", 1f);
            material.SetFloat("_DistortionSpeed", 0.8f);
            material.SetFloat("_DistortionIntensity", 0.06f);
            material.SetFloat("_DistortionTiling", 1.5f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_DISTORT_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 10: // Quantum Data
            material.SetColor("_Color", new Color(0.7f, 0.2f, 0.8f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.9f);
            material.SetFloat("_HologramFlickerIntensity", 0.09f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.4f);
            material.SetFloat("_DataStreamIntensity", 0.9f);
            material.SetFloat("_DataStreamTiling", 1.7f);
            material.SetColor("_DataStreamColor", new Color(0.8f, 0.3f, 0.9f, 1f));
            material.SetFloat("_DataStreamGlow", 1.5f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.5f);
            material.SetFloat("_EnableColorShift", 1f);
            material.SetFloat("_ColorShiftSpeed", 1.0f);
            material.SetFloat("_ColorShiftIntensity", 0.5f);
            material.SetFloat("_ColorShiftHue", 0.7f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_COLORSHIFT_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 11: // Info Wave
            material.SetColor("_Color", new Color(0.2f, 0.9f, 0.7f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.2f);
            material.SetFloat("_DataStreamIntensity", 0.8f);
            material.SetFloat("_DataStreamTiling", 1.5f);
            material.SetColor("_DataStreamColor", new Color(0.3f, 1.0f, 0.8f, 1f));
            material.SetFloat("_DataStreamGlow", 1.3f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.0f);
            material.SetFloat("_EnableWave", 1f);
            material.SetFloat("_WaveSpeed", 0.8f);
            material.SetFloat("_WaveAmplitude", 0.1f);
            material.SetColor("_WaveColor", new Color(0.3f, 1.0f, 0.8f, 1f));
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_WAVE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 12: // Encrypted Flow
            material.SetColor("_Color", new Color(0.4f, 0.4f, 0.9f, 0.7f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.7f);
            material.SetFloat("_HologramFlickerSpeed", 0.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.08f);
            material.SetFloat("_EnableDataStream", 1f);
            material.SetFloat("_DataStreamSpeed", 1.3f);
            material.SetFloat("_DataStreamIntensity", 0.8f);
            material.SetFloat("_DataStreamTiling", 1.6f);
            material.SetColor("_DataStreamColor", new Color(0.5f, 0.5f, 1.0f, 1f));
            material.SetFloat("_DataStreamGlow", 1.4f);
            material.SetVector("_DataStreamScrollDir", new Vector4(0.4f, 0.6f, 0f, 0f));
            material.SetFloat("_DataStreamDensity", 3.2f);
            material.SetFloat("_EnableNoise", 1f);
            material.SetFloat("_NoiseIntensity", 0.06f);
            material.SetFloat("_NoiseSpeed", 1.2f);
            material.SetFloat("_NoiseTiling", 2.0f);
            material.EnableKeyword("_DATASTREAM_ON");
            material.EnableKeyword("_NOISE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;
    }

    SetupShaderKeywords(material, 1);
}
       
       private void ApplyInterfacePreset(Material material, int presetIndex)
{
    switch (presetIndex)
    {
        case 1: // Advanced Interface
            material.SetColor("_Color", new Color(0.2f, 0.7f, 0.9f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.3f);
            material.SetFloat("_HologramFlickerIntensity", 0.04f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.4f);
            material.SetColor("_InterfaceColor", new Color(0.3f, 0.8f, 1f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.7f);
            material.SetFloat("_InterfaceTiling", 1.5f);
            material.SetFloat("_InterfaceGlow", 1.2f);
            material.SetFloat("_InterfaceScrollX", 0.1f);
            material.SetFloat("_InterfaceScrollY", 0.05f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 15f);
            material.SetFloat("_HexIntensity", 0.2f);
            material.SetColor("_HexColor", new Color(0.3f, 0.8f, 1f, 1f));
            material.SetFloat("_HexEmission", 1.2f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.3f, 0.8f, 1f, 1f));
            material.SetFloat("_ScanLineWidth", 0.02f);
            material.SetFloat("_ScanLineSpeed", 0.3f);
            material.SetFloat("_ScanLineAmount", 50f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.4f, 0.8f, 1f, 1f));
            material.SetFloat("_RimPower", 2.0f);
            material.SetFloat("_RimIntensity", 0.7f);
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_HEXGRID_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_GLITCH_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 2: // Tactical Display
            material.SetColor("_Color", new Color(0.1f, 0.6f, 0.3f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.4f);
            material.SetFloat("_HologramFlickerIntensity", 0.05f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.5f);
            material.SetColor("_InterfaceColor", new Color(0.2f, 0.7f, 0.4f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.8f);
            material.SetFloat("_InterfaceTiling", 1.7f);
            material.SetFloat("_InterfaceGlow", 1.3f);
            material.SetFloat("_InterfaceScrollX", 0.15f);
            material.SetFloat("_InterfaceScrollY", 0.1f);
            material.SetFloat("_EnableSquareGrid", 1f);
            material.SetFloat("_SquareSize", 20f);
            material.SetFloat("_SquareIntensity", 0.3f);
            material.SetColor("_SquareColor", new Color(0.2f, 0.7f, 0.4f, 1f));
            material.SetFloat("_SquareEdgeWidth", 0.05f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.2f, 0.7f, 0.4f, 1f));
            material.SetFloat("_ScanLineWidth", 0.03f);
            material.SetFloat("_ScanLineSpeed", 0.4f);
            material.SetFloat("_ScanLineAmount", 40f);
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_SQUAREGRID_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 3: // Medical Scan
            material.SetColor("_Color", new Color(0.2f, 0.8f, 0.9f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.3f);
            material.SetFloat("_HologramFlickerIntensity", 0.04f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.3f);
            material.SetColor("_InterfaceColor", new Color(0.3f, 0.9f, 1f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.7f);
            material.SetFloat("_InterfaceTiling", 1.5f);
            material.SetFloat("_InterfaceGlow", 1.2f);
            material.SetFloat("_InterfaceScrollX", 0.05f);
            material.SetFloat("_InterfaceScrollY", 0.05f);
            material.SetFloat("_EnableScanning", 1f);
            material.SetFloat("_ScanningSpeed", 0.5f);
            material.SetFloat("_ScanningWidth", 0.1f);
            material.SetColor("_ScanningColor", new Color(0.3f, 0.9f, 1f, 1f));
            material.SetFloat("_ScanningIntensity", 0.8f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.3f, 0.9f, 1f, 1f));
            material.SetFloat("_RimPower", 2.0f);
            material.SetFloat("_RimIntensity", 0.7f);
            material.EnableKeyword("_SCANNING_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 4: // Space Navigation
            material.SetColor("_Color", new Color(0.3f, 0.5f, 0.8f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.4f);
            material.SetFloat("_HologramFlickerIntensity", 0.05f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.4f);
            material.SetColor("_InterfaceColor", new Color(0.4f, 0.6f, 0.9f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.8f);
            material.SetFloat("_InterfaceTiling", 1.6f);
            material.SetFloat("_InterfaceGlow", 1.3f);
            material.SetFloat("_InterfaceScrollX", 0.1f);
            material.SetFloat("_InterfaceScrollY", 0.1f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 25f);
            material.SetFloat("_HexIntensity", 0.3f);
            material.SetColor("_HexColor", new Color(0.4f, 0.6f, 0.9f, 1f));
            material.SetFloat("_HexEmission", 1.3f);
            material.SetFloat("_EnableColorShift", 1f);
            material.SetFloat("_ColorShiftSpeed", 0.5f);
            material.SetFloat("_ColorShiftIntensity", 0.4f);
            material.SetFloat("_ColorShiftHue", 0.6f);
            material.EnableKeyword("_HEXGRID_ON");
            material.EnableKeyword("_COLORSHIFT_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 5: // Cyber Console
            material.SetColor("_Color", new Color(0.2f, 0.9f, 0.4f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.3f);
            material.SetFloat("_HologramFlickerIntensity", 0.04f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.5f);
            material.SetColor("_InterfaceColor", new Color(0.3f, 1.0f, 0.5f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.7f);
            material.SetFloat("_InterfaceTiling", 1.5f);
            material.SetFloat("_InterfaceGlow", 1.2f);
            material.SetFloat("_InterfaceScrollX", 0.15f);
            material.SetFloat("_InterfaceScrollY", 0.05f);
            material.SetFloat("_EnableCircuit", 1f);
            material.SetFloat("_CircuitIntensity", 0.4f);
            material.SetColor("_CircuitColor", new Color(0.3f, 1.0f, 0.5f, 1f));
            material.SetFloat("_CircuitSpeed", 0.6f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.3f, 1.0f, 0.5f, 1f));
            material.SetFloat("_ScanLineWidth", 0.02f);
            material.SetFloat("_ScanLineSpeed", 0.3f);
            material.SetFloat("_ScanLineAmount", 50f);
            material.EnableKeyword("_CIRCUIT_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 6: // Holo Dashboard
            material.SetColor("_Color", new Color(0.7f, 0.3f, 0.8f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.4f);
            material.SetFloat("_HologramFlickerIntensity", 0.05f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.4f);
            material.SetColor("_InterfaceColor", new Color(0.8f, 0.4f, 0.9f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.8f);
            material.SetFloat("_InterfaceTiling", 1.7f);
            material.SetFloat("_InterfaceGlow", 1.3f);
            material.SetFloat("_InterfaceScrollX", 0.1f);
            material.SetFloat("_InterfaceScrollY", 0.1f);
            material.SetFloat("_EnableSquareGrid", 1f);
            material.SetFloat("_SquareSize", 15f);
            material.SetFloat("_SquareIntensity", 0.3f);
            material.SetColor("_SquareColor", new Color(0.8f, 0.4f, 0.9f, 1f));
            material.SetFloat("_SquareEdgeWidth", 0.04f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.8f, 0.4f, 0.9f, 1f));
            material.SetFloat("_RimPower", 2.0f);
            material.SetFloat("_RimIntensity", 0.7f);
            material.EnableKeyword("_SQUAREGRID_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 7: // Augmented Reality
            material.SetColor("_Color", new Color(0.9f, 0.7f, 0.2f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.3f);
            material.SetFloat("_HologramFlickerIntensity", 0.04f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.3f);
            material.SetColor("_InterfaceColor", new Color(1.0f, 0.8f, 0.3f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.7f);
            material.SetFloat("_InterfaceTiling", 1.5f);
            material.SetFloat("_InterfaceGlow", 1.2f);
            material.SetFloat("_InterfaceScrollX", 0.05f);
            material.SetFloat("_InterfaceScrollY", 0.05f);
            material.SetFloat("_EnableEdges", 1f);
            material.SetColor("_EdgeColor", new Color(1.0f, 0.8f, 0.3f, 1f));
            material.SetFloat("_EdgeThickness", 0.02f);
            material.SetFloat("_EdgeSharpness", 5.0f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(1.0f, 0.8f, 0.3f, 1f));
            material.SetFloat("_ScanLineWidth", 0.02f);
            material.SetFloat("_ScanLineSpeed", 0.3f);
            material.SetFloat("_ScanLineAmount", 50f);
            material.EnableKeyword("_EDGES_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 8: // Control Panel
            material.SetColor("_Color", new Color(0.2f, 0.6f, 0.9f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.4f);
            material.SetFloat("_HologramFlickerIntensity", 0.05f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.4f);
            material.SetColor("_InterfaceColor", new Color(0.3f, 0.7f, 1.0f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.8f);
            material.SetFloat("_InterfaceTiling", 1.6f);
            material.SetFloat("_InterfaceGlow", 1.3f);
            material.SetFloat("_InterfaceScrollX", 0.1f);
            material.SetFloat("_InterfaceScrollY", 0.1f);
            material.SetFloat("_EnableCircuit", 1f);
            material.SetFloat("_CircuitIntensity", 0.4f);
            material.SetColor("_CircuitColor", new Color(0.3f, 0.7f, 1.0f, 1f));
            material.SetFloat("_CircuitSpeed", 0.5f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.3f, 0.7f, 1.0f, 1f));
            material.SetFloat("_RimPower", 2.0f);
            material.SetFloat("_RimIntensity", 0.7f);
            material.EnableKeyword("_CIRCUIT_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 9: // Data Grid
            material.SetColor("_Color", new Color(0.3f, 0.8f, 0.5f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.3f);
            material.SetFloat("_HologramFlickerIntensity", 0.04f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.3f);
            material.SetColor("_InterfaceColor", new Color(0.4f, 0.9f, 0.6f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.7f);
            material.SetFloat("_InterfaceTiling", 1.5f);
            material.SetFloat("_InterfaceGlow", 1.2f);
            material.SetFloat("_InterfaceScrollX", 0.05f);
            material.SetFloat("_InterfaceScrollY", 0.05f);
            material.SetFloat("_EnableSquareGrid", 1f);
            material.SetFloat("_SquareSize", 20f);
            material.SetFloat("_SquareIntensity", 0.3f);
            material.SetColor("_SquareColor", new Color(0.4f, 0.9f, 0.6f, 1f));
            material.SetFloat("_SquareEdgeWidth", 0.05f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.4f, 0.9f, 0.6f, 1f));
            material.SetFloat("_ScanLineWidth", 0.02f);
            material.SetFloat("_ScanLineSpeed", 0.3f);
            material.SetFloat("_ScanLineAmount", 0.50f);
            material.EnableKeyword("_SQUAREGRID_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 10: // Tech HUD
            material.SetColor("_Color", new Color(0.8f, 0.2f, 0.3f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.4f);
            material.SetFloat("_HologramFlickerIntensity", 0.05f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.4f);
            material.SetColor("_InterfaceColor", new Color(0.9f, 0.3f, 0.4f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.8f);
            material.SetFloat("_InterfaceTiling", 1.7f);
            material.SetFloat("_InterfaceGlow", 1.3f);
            material.SetFloat("_InterfaceScrollX", 0.1f);
            material.SetFloat("_InterfaceScrollY", 0.1f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 15f);
            material.SetFloat("_HexIntensity", 0.3f);
            material.SetColor("_HexColor", new Color(0.9f, 0.3f, 0.4f, 1f));
            material.SetFloat("_HexEmission", 1.3f);
            material.SetFloat("_EnableEdges", 1f);
            material.SetColor("_EdgeColor", new Color(0.9f, 0.3f, 0.4f, 1f));
            material.SetFloat("_EdgeThickness", 0.02f);
            material.SetFloat("_EdgeSharpness", 5.0f);
            material.EnableKeyword("_HEXGRID_ON");
            material.EnableKeyword("_EDGES_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 11: // Virtual Terminal
            material.SetColor("_Color", new Color(0.2f, 0.9f, 0.7f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.1f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.3f);
            material.SetFloat("_HologramFlickerIntensity", 0.04f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.3f);
            material.SetColor("_InterfaceColor", new Color(0.3f, 1.0f, 0.8f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.7f);
            material.SetFloat("_InterfaceTiling", 1.5f);
            material.SetFloat("_InterfaceGlow", 1.2f);
            material.SetFloat("_InterfaceScrollX", 0.05f);
            material.SetFloat("_InterfaceScrollY", 0.05f);
            material.SetFloat("_EnableCircuit", 1f);
            material.SetFloat("_CircuitIntensity", 0.4f);
            material.SetColor("_CircuitColor", new Color(0.3f, 1.0f, 0.8f, 1f));
            material.SetFloat("_CircuitSpeed", 0.5f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.3f, 1.0f, 0.8f, 1f));
            material.SetFloat("_ScanLineWidth", 0.02f);
            material.SetFloat("_ScanLineSpeed", 0.3f);
            material.SetFloat("_ScanLineAmount", 50f);
            material.EnableKeyword("_CIRCUIT_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 12: // SciFi Interface
            material.SetColor("_Color", new Color(0.3f, 0.7f, 0.9f, 0.8f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.8f);
            material.SetFloat("_HologramFlickerSpeed", 0.4f);
            material.SetFloat("_HologramFlickerIntensity", 0.05f);
            material.SetFloat("_EnableInterface", 1f);
            material.SetFloat("_InterfaceSpeed", 0.4f);
            material.SetColor("_InterfaceColor", new Color(0.4f, 0.8f, 1.0f, 1f));
            material.SetFloat("_InterfaceIntensity", 0.8f);
            material.SetFloat("_InterfaceTiling", 1.7f);
            material.SetFloat("_InterfaceGlow", 1.3f);
            material.SetFloat("_InterfaceScrollX", 0.1f);
            material.SetFloat("_InterfaceScrollY", 0.1f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 20f);
            material.SetFloat("_HexIntensity", 0.3f);
            material.SetColor("_HexColor", new Color(0.4f, 0.8f, 1.0f, 1f));
            material.SetFloat("_HexEmission", 1.3f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.4f, 0.8f, 1.0f, 1f));
            material.SetFloat("_RimPower", 2.0f);
            material.SetFloat("_RimIntensity", 0.7f);
            material.EnableKeyword("_HEXGRID_ON");
            material.EnableKeyword("_RIM_ON");
            material.EnableKeyword("_INTERFACE_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
    }

    SetupShaderKeywords(material, 2);
}
       
       private void ApplyProjectionPreset(Material material, int presetIndex)
{
    switch (presetIndex)
    {
        case 1: // 3D Projection
            material.SetColor("_Color", new Color(0.0f, 0.6f, 0.9f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.5f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.1f, 0.7f, 1f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.2f);
            material.SetFloat("_ProjectionFlicker", 0.15f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.0f, 0.7f, 1f, 1f));
            material.SetFloat("_ScanLineWidth", 0.05f);
            material.SetFloat("_ScanLineSpeed", 0.5f);
            material.SetFloat("_ScanLineAmount", 30f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0.0f, 0.6f, 0.9f, 1f));
            material.SetFloat("_FresnelPower", 2.0f);
            material.SetFloat("_FresnelIntensity", 0.9f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_GLITCH_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 2: // Star Map
            material.SetColor("_Color", new Color(0.1f, 0.3f, 0.8f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.2f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.6f);
            material.SetFloat("_HologramFlickerIntensity", 0.06f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.7f);
            material.SetFloat("_ProjectionFadeDistance", 0.4f);
            material.SetColor("_ProjectionColor", new Color(0.2f, 0.4f, 0.9f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.3f);
            material.SetFloat("_ProjectionFlicker", 0.1f);
            material.SetFloat("_ProjectionSpread", 0.7f);
            material.SetFloat("_EnableNoise", 1f);
            material.SetFloat("_NoiseIntensity", 0.05f);
            material.SetFloat("_NoiseSpeed", 0.5f);
            material.SetFloat("_NoiseTiling", 2.0f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.2f, 0.4f, 0.9f, 1f));
            material.SetFloat("_RimPower", 2.5f);
            material.SetFloat("_RimIntensity", 0.8f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_NOISE_ON");
            material.EnableKeyword("_RIM_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 3: // Molecular Structure
            material.SetColor("_Color", new Color(0.2f, 0.8f, 0.4f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.08f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.4f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.3f, 0.9f, 0.5f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.2f);
            material.SetFloat("_ProjectionFlicker", 0.12f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnableWireframe", 1f);
            material.SetColor("_WireframeColor", new Color(0.3f, 0.9f, 0.5f, 1f));
            material.SetFloat("_WireframeThickness", 0.02f);
            material.SetFloat("_WireframeSmoothing", 0.01f);
            material.SetFloat("_WireframeDensity", 2.0f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0.3f, 0.9f, 0.5f, 1f));
            material.SetFloat("_FresnelPower", 2.0f);
            material.SetFloat("_FresnelIntensity", 0.9f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_WIREFRAME_ON");
            material.EnableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 4: // Character Projection
            material.SetColor("_Color", new Color(0.7f, 0.3f, 0.3f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.4f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.9f);
            material.SetFloat("_HologramFlickerIntensity", 0.09f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.6f);
            material.SetFloat("_ProjectionFadeDistance", 0.4f);
            material.SetColor("_ProjectionColor", new Color(0.8f, 0.4f, 0.4f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.3f);
            material.SetFloat("_ProjectionFlicker", 0.15f);
            material.SetFloat("_ProjectionSpread", 0.7f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.8f, 0.4f, 0.4f, 1f));
            material.SetFloat("_ScanLineWidth", 0.04f);
            material.SetFloat("_ScanLineSpeed", 0.6f);
            material.SetFloat("_ScanLineAmount", 35f);
            material.SetFloat("_EnableVignette", 1f);
            material.SetColor("_VignetteColor", new Color(0.8f, 0.4f, 0.4f, 1f));
            material.SetFloat("_VignettePower", 2.0f);
            material.SetFloat("_VignetteIntensity", 0.5f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.EnableKeyword("_VIGNETTE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 5: // Planet Hologram
            material.SetColor("_Color", new Color(0.2f, 0.5f, 0.8f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.5f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.3f, 0.6f, 0.9f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.2f);
            material.SetFloat("_ProjectionFlicker", 0.1f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 20f);
            material.SetFloat("_HexIntensity", 0.3f);
            material.SetColor("_HexColor", new Color(0.3f, 0.6f, 0.9f, 1f));
            material.SetFloat("_HexEmission", 1.2f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.3f, 0.6f, 0.9f, 1f));
            material.SetFloat("_RimPower", 2.5f);
            material.SetFloat("_RimIntensity", 0.8f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_HEXGRID_ON");
            material.EnableKeyword("_RIM_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 6: // Ship Blueprint
            material.SetColor("_Color", new Color(0.3f, 0.7f, 0.9f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.08f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.4f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.4f, 0.8f, 1.0f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.2f);
            material.SetFloat("_ProjectionFlicker", 0.12f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnableWireframe", 1f);
            material.SetColor("_WireframeColor", new Color(0.4f, 0.8f, 1.0f, 1f));
            material.SetFloat("_WireframeThickness", 0.03f);
            material.SetFloat("_WireframeSmoothing", 0.02f);
            material.SetFloat("_WireframeDensity", 2.0f);
            material.SetFloat("_EnableScanLine", 1f);
            material.SetColor("_ScanLineColor", new Color(0.4f, 0.8f, 1.0f, 1f));
            material.SetFloat("_ScanLineWidth", 0.05f);
            material.SetFloat("_ScanLineSpeed", 0.5f);
            material.SetFloat("_ScanLineAmount", 30f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_WIREFRAME_ON");
            material.EnableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 7: // Orbital Scan
            material.SetColor("_Color", new Color(0.2f, 0.8f, 0.5f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.5f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.3f, 0.9f, 0.6f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.2f);
            material.SetFloat("_ProjectionFlicker", 0.1f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnableScanning", 1f);
            material.SetFloat("_ScanningSpeed", 0.6f);
            material.SetFloat("_ScanningWidth", 0.08f);
            material.SetColor("_ScanningColor", new Color(0.3f, 0.9f, 0.6f, 1f));
            material.SetFloat("_ScanningIntensity", 0.9f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.3f, 0.9f, 0.6f, 1f));
            material.SetFloat("_RimPower", 2.5f);
            material.SetFloat("_RimIntensity", 0.8f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_SCANNING_ON");
            material.EnableKeyword("_RIM_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 8: // Holo Globe
            material.SetColor("_Color", new Color(0.1f, 0.6f, 0.9f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.5f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.2f, 0.7f, 1.0f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.2f);
            material.SetFloat("_ProjectionFlicker", 0.1f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnableHexGrid", 1f);
            material.SetFloat("_HexSize", 15f);
            material.SetFloat("_HexIntensity", 0.3f);
            material.SetColor("_HexColor", new Color(0.2f, 0.7f, 1.0f, 1f));
            material.SetFloat("_HexEmission", 1.2f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0.2f, 0.7f, 1.0f, 1f));
            material.SetFloat("_FresnelPower", 2.0f);
            material.SetFloat("_FresnelIntensity", 0.9f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_HEXGRID_ON");
            material.EnableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 9: // Cosmic Projection
            material.SetColor("_Color", new Color(0.3f, 0.3f, 0.8f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.6f);
            material.SetFloat("_ProjectionFadeDistance", 0.4f);
            material.SetColor("_ProjectionColor", new Color(0.4f, 0.4f, 0.9f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.3f);
            material.SetFloat("_ProjectionFlicker", 0.1f);
            material.SetFloat("_ProjectionSpread", 0.7f);
            material.SetFloat("_EnableNoise", 1f);
            material.SetFloat("_NoiseIntensity", 0.06f);
            material.SetFloat("_NoiseSpeed", 0.6f);
            material.SetFloat("_NoiseTiling", 2.0f);
            material.SetFloat("_EnableColorShift", 1f);
            material.SetFloat("_ColorShiftSpeed", 0.5f);
            material.SetFloat("_ColorShiftIntensity", 0.4f);
            material.SetFloat("_ColorShiftHue", 0.6f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_NOISE_ON");
            material.EnableKeyword("_COLORSHIFT_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 10: // Galactic Map
            material.SetColor("_Color", new Color(0.2f, 0.4f, 0.9f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.7f);
            material.SetFloat("_HologramFlickerIntensity", 0.07f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.7f);
            material.SetFloat("_ProjectionFadeDistance", 0.4f);
            material.SetColor("_ProjectionColor", new Color(0.3f, 0.5f, 1.0f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.3f);
            material.SetFloat("_ProjectionFlicker", 0.1f);
            material.SetFloat("_ProjectionSpread", 0.7f);
            material.SetFloat("_EnableSquareGrid", 1f);
            material.SetFloat("_SquareSize", 20f);
            material.SetFloat("_SquareIntensity", 0.3f);
            material.SetColor("_SquareColor", new Color(0.3f, 0.5f, 1.0f, 1f));
            material.SetFloat("_SquareEdgeWidth", 0.05f);
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.3f, 0.5f, 1.0f, 1f));
            material.SetFloat("_RimPower", 2.5f);
            material.SetFloat("_RimIntensity", 0.8f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_SQUAREGRID_ON");
            material.EnableKeyword("_RIM_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 11: // Structure Scan
            material.SetColor("_Color", new Color(0.3f, 0.8f, 0.4f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.3f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.8f);
            material.SetFloat("_HologramFlickerIntensity", 0.08f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.4f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.4f, 0.9f, 0.5f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.2f);
            material.SetFloat("_ProjectionFlicker", 0.12f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnableScanning", 1f);
            material.SetFloat("_ScanningSpeed", 0.6f);
            material.SetFloat("_ScanningWidth", 0.08f);
            material.SetColor("_ScanningColor", new Color(0.4f, 0.9f, 0.5f, 1f));
            material.SetFloat("_ScanningIntensity", 0.9f);
            material.SetFloat("_EnableWireframe", 1f);
            material.SetColor("_WireframeColor", new Color(0.4f, 0.9f, 0.5f, 1f));
            material.SetFloat("_WireframeThickness", 0.03f);
            material.SetFloat("_WireframeSmoothing", 0.02f);
            material.SetFloat("_WireframeDensity", 2.0f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_SCANNING_ON");
            material.EnableKeyword("_WIREFRAME_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_FRESNEL_ON");
            break;

        case 12: // Holo Avatar
            material.SetColor("_Color", new Color(0.7f, 0.4f, 0.3f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.4f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.9f);
            material.SetFloat("_HologramFlickerIntensity", 0.09f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.6f);
            material.SetFloat("_ProjectionFadeDistance", 0.4f);
            material.SetColor("_ProjectionColor", new Color(0.8f, 0.5f, 0.4f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.3f);
            material.SetFloat("_ProjectionFlicker", 0.15f);
            material.SetFloat("_ProjectionSpread", 0.7f);
            material.SetFloat("_EnableVignette", 1f);
            material.SetColor("_VignetteColor", new Color(0.8f, 0.5f, 0.4f, 1f));
            material.SetFloat("_VignettePower", 2.0f);
            material.SetFloat("_VignetteIntensity", 0.5f);
            material.SetFloat("_EnableFresnel", 1f);
            material.SetColor("_FresnelColor", new Color(0.8f, 0.5f, 0.4f, 1f));
            material.SetFloat("_FresnelPower", 2.0f);
            material.SetFloat("_FresnelIntensity", 0.9f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_VIGNETTE_ON");
            material.EnableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_RIM_ON");
            material.DisableKeyword("_LINES_ON");
            break;

        case 13: // Quantum Core
            material.SetColor("_Color", new Color(0.8f, 0.2f, 0.8f, 0.6f));
            material.SetFloat("_HologramIntensity", 1.4f);
            material.SetFloat("_HologramOpacity", 0.6f);
            material.SetFloat("_HologramFlickerSpeed", 0.9f);
            material.SetFloat("_HologramFlickerIntensity", 0.09f);
            material.SetFloat("_EnableProjection", 1f);
            material.SetFloat("_ProjectionHeight", 1.5f);
            material.SetFloat("_ProjectionFadeDistance", 0.3f);
            material.SetColor("_ProjectionColor", new Color(0.9f, 0.3f, 0.9f, 0.4f));
            material.SetFloat("_ProjectionIntensity", 1.3f);
            material.SetFloat("_ProjectionFlicker", 0.1f);
            material.SetFloat("_ProjectionSpread", 0.6f);
            material.SetFloat("_EnablePulse", 1f);
            material.SetFloat("_PulseSpeed", 0.8f);
            material.SetFloat("_PulseAmplitude", 0.2f);
            material.SetColor("_PulseColor", new Color(0.9f, 0.3f, 0.9f, 1f));
            material.SetFloat("_EnableRim", 1f);
            material.SetColor("_RimColor", new Color(0.9f, 0.3f, 0.9f, 1f));
            material.SetFloat("_RimPower", 2.5f);
            material.SetFloat("_RimIntensity", 0.8f);
            material.EnableKeyword("_PROJECTION_ON");
            material.EnableKeyword("_PULSE_ON");
            material.EnableKeyword("_RIM_ON");
            material.DisableKeyword("_SCANLINE_ON");
            material.DisableKeyword("_FRESNEL_ON");
            material.DisableKeyword("_LINES_ON");
            break;
    }

    SetupShaderKeywords(material, 3);
}
       
       private void SetupShaderKeywords(Material material, int shaderType)
       {
           // Set the shader type
           material.SetFloat("_ShaderType", shaderType);
           
           // Set blend mode
           SetupBlendMode(material, (int)material.GetFloat("_BlendMode"));
           
           // Update keywords based on toggles
           UpdateKeyword(material, "_SCANLINE_ON", material.GetFloat("_EnableScanLine") > 0.5f);
           UpdateKeyword(material, "_RIM_ON", material.GetFloat("_EnableRim") > 0.5f);
           UpdateKeyword(material, "_GLITCH_ON", material.GetFloat("_EnableGlitch") > 0.5f);
           UpdateKeyword(material, "_EMISSION_ON", material.GetFloat("_EnableEmission") > 0.5f);
           UpdateKeyword(material, "_FRESNEL_ON", material.GetFloat("_EnableFresnel") > 0.5f);
           UpdateKeyword(material, "_DISTORT_ON", material.GetFloat("_EnableDistortion") > 0.5f);
           UpdateKeyword(material, "_LINES_ON", material.GetFloat("_EnableLines") > 0.5f);
           UpdateKeyword(material, "_NOISE_ON", material.GetFloat("_EnableNoise") > 0.5f);
           UpdateKeyword(material, "_DATASTREAM_ON", material.GetFloat("_EnableDataStream") > 0.5f);
           UpdateKeyword(material, "_PROJECTION_ON", material.GetFloat("_EnableProjection") > 0.5f);
           UpdateKeyword(material, "_INTERFACE_ON", material.GetFloat("_EnableInterface") > 0.5f);
           UpdateKeyword(material, "_EDGES_ON", material.GetFloat("_EnableEdges") > 0.5f);
           UpdateKeyword(material, "_HEXGRID_ON", material.GetFloat("_EnableHexGrid") > 0.5f);
           UpdateKeyword(material, "_SQUAREGRID_ON", material.GetFloat("_EnableSquareGrid") > 0.5f);
           UpdateKeyword(material, "_CIRCUIT_ON", material.GetFloat("_EnableCircuit") > 0.5f);
           UpdateKeyword(material, "_WIREFRAME_ON", material.GetFloat("_EnableWireframe") > 0.5f);
           UpdateKeyword(material, "_PULSE_ON", material.GetFloat("_EnablePulse") > 0.5f);
           UpdateKeyword(material, "_SCANNING_ON", material.GetFloat("_EnableScanning") > 0.5f);
           UpdateKeyword(material, "_BEAM_ON", material.GetFloat("_EnableBeam") > 0.5f);
           UpdateKeyword(material, "_COLORSHIFT_ON", material.GetFloat("_EnableColorShift") > 0.5f);
           UpdateKeyword(material, "_COLORBANDING_ON", material.GetFloat("_EnableColorBanding") > 0.5f);
           UpdateKeyword(material, "_CHROMATIC_ON", material.GetFloat("_EnableChromatic") > 0.5f);
           UpdateKeyword(material, "_VIGNETTE_ON", material.GetFloat("_EnableVignette") > 0.5f);
           UpdateKeyword(material, "_VOLUMETRIC_ON", material.GetFloat("_EnableVolumetric") > 0.5f);
           UpdateKeyword(material, "_DEPTH_ON", material.GetFloat("_EnableDepth") > 0.5f);
       }
       
       private void SetupBlendMode(Material material, int blendMode)
       {
           if (blendMode == 0) // Opaque
           {
               material.SetOverrideTag("RenderType", "Opaque");
               material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
               material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
               material.SetInt("_ZWrite", 1);
               material.DisableKeyword("_ALPHABLEND_ON");
               material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
           }
           else // Transparent
           {
               material.SetOverrideTag("RenderType", "Transparent");
               material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
               material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
               material.SetInt("_ZWrite", 0);
               material.EnableKeyword("_ALPHABLEND_ON");
               material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
           }
       }
       
       private void UpdateKeyword(Material material, string keyword, bool state)
       {
           if (state)
               material.EnableKeyword(keyword);
           else
               material.DisableKeyword(keyword);
       }
   }
}