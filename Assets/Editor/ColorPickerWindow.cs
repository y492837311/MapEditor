// Editor/UI/ColorPickerWindow.cs
using UnityEngine;
using UnityEditor;
using System;

namespace MapEditor
{
    public class ColorPickerWindow : EditorWindow
    {
        private static Action<Color> onColorSelected;
        private static Color currentColor;
        private static ColorPickerWindow instance;

        private float hue = 0f;
        private float saturation = 1f;
        private float value = 1f;
        private float alpha = 1f;

        public static void Show(Color initialColor, Action<Color> callback)
        {
            if (instance != null)
            {
                instance.Close();
            }

            instance = CreateInstance<ColorPickerWindow>();
            instance.position = new Rect(100, 100, 300, 400);
            instance.titleContent = new GUIContent("Color Picker");
            onColorSelected = callback;
            currentColor = initialColor;
            
            // 转换RGB到HSV
            Color.RGBToHSV(currentColor, out instance.hue, out instance.saturation, out instance.value);
            instance.alpha = currentColor.a;
            
            instance.ShowUtility();
        }

        private void OnGUI()
        {
            try
            {
                EditorGUILayout.Space();
                
                // 当前颜色预览
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    EditorGUILayout.LabelField("Selected Color", EditorStyles.boldLabel);
                    
                    // 颜色预览区域
                    Rect previewRect = GUILayoutUtility.GetRect(0, 60);
                    EditorGUI.DrawRect(previewRect, currentColor);
                    
                    // 边框
                    Handles.BeginGUI();
                    Handles.color = Color.white;
                    Handles.DrawWireCube(previewRect.center, new Vector3(previewRect.width, previewRect.height, 0));
                    Handles.EndGUI();
                    
                    // RGB值显示
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField($"R: {currentColor.r:F2}", GUILayout.Width(60));
                        EditorGUILayout.LabelField($"G: {currentColor.g:F2}", GUILayout.Width(60));
                        EditorGUILayout.LabelField($"B: {currentColor.b:F2}", GUILayout.Width(60));
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.LabelField($"Hex: #{ColorUtility.ToHtmlStringRGB(currentColor)}");
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();

                // RGB滑块
                EditorGUILayout.LabelField("RGB Values", EditorStyles.boldLabel);
                float r = EditorGUILayout.Slider("Red", currentColor.r, 0f, 1f);
                float g = EditorGUILayout.Slider("Green", currentColor.g, 0f, 1f);
                float b = EditorGUILayout.Slider("Blue", currentColor.b, 0f, 1f);
                float a = EditorGUILayout.Slider("Alpha", currentColor.a, 0f, 1f);

                // 如果值发生变化，更新颜色
                if (r != currentColor.r || g != currentColor.g || b != currentColor.b || a != currentColor.a)
                {
                    currentColor = new Color(r, g, b, a);
                    Color.RGBToHSV(currentColor, out hue, out saturation, out value);
                    alpha = a;
                }

                EditorGUILayout.Space();

                // HSV滑块
                EditorGUILayout.LabelField("HSV Values", EditorStyles.boldLabel);
                float newHue = EditorGUILayout.Slider("Hue", hue, 0f, 1f);
                float newSaturation = EditorGUILayout.Slider("Saturation", saturation, 0f, 1f);
                float newValue = EditorGUILayout.Slider("Value", value, 0f, 1f);

                // 如果HSV值发生变化，更新颜色
                if (newHue != hue || newSaturation != saturation || newValue != value)
                {
                    hue = newHue;
                    saturation = newSaturation;
                    value = newValue;
                    currentColor = Color.HSVToRGB(hue, saturation, value);
                    currentColor.a = alpha;
                }

                EditorGUILayout.Space();

                // 预设颜色
                DrawColorPresets();

                EditorGUILayout.Space();

                // 按钮
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Apply"))
                    {
                        onColorSelected?.Invoke(currentColor);
                        Close();
                    }
                    
                    if (GUILayout.Button("Cancel"))
                    {
                        Close();
                    }
                    
                    if (GUILayout.Button("Reset"))
                    {
                        currentColor = Color.white;
                        Color.RGBToHSV(currentColor, out hue, out saturation, out value);
                        alpha = 1f;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error in color picker: {e.Message}", MessageType.Error);
            }
        }

        private void DrawColorPresets()
        {
            EditorGUILayout.LabelField("Preset Colors", EditorStyles.boldLabel);
            
            Color[] presets = new Color[]
            {
                Color.red, new Color(1, 0.5f, 0), Color.yellow, Color.green,
                Color.cyan, Color.blue, new Color(0.5f, 0, 1), Color.magenta,
                Color.white, new Color(0.5f, 0.5f, 0.5f), Color.black, new Color(0.5f, 0.25f, 0)
            };
            
            string[] presetNames = new string[]
            {
                "Red", "Orange", "Yellow", "Green",
                "Cyan", "Blue", "Purple", "Magenta", 
                "White", "Gray", "Black", "Brown"
            };
            
            int columns = 4;
            for (int i = 0; i < presets.Length; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < columns && i + j < presets.Length; j++)
                {
                    int index = i + j;
                    Color preset = presets[index];
                    string name = presetNames[index];
                    
                    if (GUILayout.Button(name, GUILayout.Height(25)))
                    {
                        currentColor = preset;
                        Color.RGBToHSV(currentColor, out hue, out saturation, out value);
                        alpha = preset.a;
                    }
                    
                    // 在按钮上绘制颜色预览
                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    Rect colorRect = new Rect(lastRect.x + 2, lastRect.y + 2, 20, lastRect.height - 4);
                    EditorGUI.DrawRect(colorRect, preset);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void OnDestroy()
        {
            instance = null;
        }
    }
}