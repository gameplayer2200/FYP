using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace SciFiForge
{
    public class PresetManager : EditorWindow
    {
        private List<Material> presets = new List<Material>();
        private Vector2 scrollPosition;
        private Material selectedMaterial;
        private string newPresetName = "New Preset";
        private int selectedCategory = 0;
        
        private string[] categories = {
            "All",
            "Standard Hologram",
            "Data Stream",
            "Interface",
            "3D Projection"
        };
        
        [MenuItem("Window/SciFi Hologram/Preset Manager")]
        public static void ShowWindow()
        {
            GetWindow<PresetManager>("Hologram Shader Presets");
        }
        
        private void OnEnable()
        {
            LoadPresets();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("SciFi Hologram Shader", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter: ", GUILayout.Width(50));
            selectedCategory = GUILayout.Toolbar(selectedCategory, categories);
            GUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            newPresetName = EditorGUILayout.TextField("Preset Name", newPresetName);
            if (GUILayout.Button("Create New Preset", GUILayout.Height(30), GUILayout.Width(150)))
            {
                CreateNewPreset();
            }
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button("Refresh Presets", GUILayout.Height(30)))
            {
                LoadPresets();
            }
            
            EditorGUILayout.Space();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] == null) continue;
                
                float presetType = presets[i].GetFloat("_ShaderType");
                
                if (selectedCategory != 0 && selectedCategory - 1 != (int)presetType)
                    continue;
                
                GUILayout.BeginHorizontal("box");
                
                if (GUILayout.Button(presets[i].name, EditorStyles.boldLabel, GUILayout.Width(150)))
                {
                    selectedMaterial = presets[i];
                }
                
                if (GUILayout.Button("Apply to Selection", GUILayout.Width(120)))
                {
                    ApplyPresetToSelection(presets[i]);
                }
                
                if (GUILayout.Button("Delete", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("Delete Preset", 
                        "Are you sure you want to delete " + presets[i].name + "?", 
                        "Yes", "No"))
                    {
                        DeletePreset(presets[i]);
                        LoadPresets();
                        GUIUtility.ExitGUI();
                    }
                }
                
                GUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            
            if (selectedMaterial != null)
            {
                GUILayout.Label("Selected Preset: " + selectedMaterial.name, EditorStyles.boldLabel);
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Create New From Selected", GUILayout.Height(30)))
                {
                    CreatePresetFromSelected();
                }
            }
        }
        
        private void LoadPresets()
        {
            presets.Clear();
            
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/SciFiHologram/Presets" });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null && (mat.shader.name.Contains("SciFiHologram")))
                {
                    presets.Add(mat);
                }
            }
        }
        
        private void CreateNewPreset()
        {
            string path = "Assets/SciFiHologram/Presets";
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            Material material = new Material(Shader.Find("SciFiHologram"));
            
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/" + newPresetName + ".mat");
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();
            
            LoadPresets();
            selectedMaterial = material;
        }
        
        private void ApplyPresetToSelection(Material preset)
        {
            Object[] selection = Selection.objects;
            
            for (int i = 0; i < selection.Length; i++)
            {
                GameObject go = selection[i] as GameObject;
                
                if (go != null)
                {
                    Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
                    
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] materials = renderer.sharedMaterials;
                        
                        for (int j = 0; j < materials.Length; j++)
                        {
                            if (materials[j].shader.name.Contains("SciFiHologram"))
                            {
                                Material newMaterial = new Material(preset);
                                newMaterial.name = materials[j].name;
                                materials[j] = newMaterial;
                            }
                        }
                        
                        renderer.sharedMaterials = materials;
                    }
                }
            }
        }
        
        private void DeletePreset(Material preset)
        {
            string assetPath = AssetDatabase.GetAssetPath(preset);
            AssetDatabase.DeleteAsset(assetPath);
            
            if (selectedMaterial == preset)
            {
                selectedMaterial = null;
            }
        }
        
        private void CreatePresetFromSelected()
        {
            if (selectedMaterial == null) return;
            
            string path = "Assets/SciFiHologram/Presets";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/" + newPresetName + ".mat");
            
            Material newMaterial = new Material(selectedMaterial);
            AssetDatabase.CreateAsset(newMaterial, assetPath);
            AssetDatabase.SaveAssets();
            
            LoadPresets();
            selectedMaterial = newMaterial;
        }
    }
}