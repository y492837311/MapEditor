using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class ColorPickerPanel
    {
        private MapEditorWindow editorWindow;
        private Color selectedColor;
        private int currentBlockId = 1;

        public ColorPickerPanel(MapEditorWindow window)
        {
            editorWindow = window;
            selectedColor = Color.red;
        }

        public void DrawPanel()
        {
            EditorGUILayout.BeginVertical("HelpBox");
            {
                EditorGUILayout.LabelField("Color Picker", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                // 颜色选择
                EditorGUILayout.BeginHorizontal();
                {
                    selectedColor = EditorGUILayout.ColorField(selectedColor, GUILayout.Width(60));
                    
                    if (GUILayout.Button("Apply", GUILayout.Width(50)))
                    {
                        editorWindow.SetCurrentColor(selectedColor);
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                // 快速颜色预设
                DrawColorPresets();
                
                EditorGUILayout.Space();
                
                // 最近使用的颜色
                DrawRecentColors();
                
                EditorGUILayout.Space();
                
                // 颜色块管理
                DrawColorBlocks();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawColorPresets()
        {
            EditorGUILayout.LabelField("Quick Colors", EditorStyles.miniBoldLabel);
            
            Color[] quickColors = new Color[]
            {
                Color.red, new Color(1, 0.5f, 0), Color.yellow, Color.green,
                Color.cyan, Color.blue, new Color(0.5f, 0, 1), Color.magenta,
                Color.white, new Color(0.5f, 0.5f, 0.5f), Color.black
            };
            
            int columns = 4;
            for (int i = 0; i < quickColors.Length; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < columns && i + j < quickColors.Length; j++)
                {
                    Color color = quickColors[i + j];
                    if (GUILayout.Button("", GUILayout.Height(25), GUILayout.Width(25)))
                    {
                        selectedColor = color;
                        editorWindow.SetCurrentColor(color);
                    }
                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    EditorGUI.DrawRect(lastRect, color);
                    
                    // 绘制边框
                    if (selectedColor == color)
                    {
                        Handles.BeginGUI();
                        Handles.color = Color.white;
                        Handles.DrawWireCube(lastRect.center, lastRect.size);
                        Handles.EndGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRecentColors()
        {
            EditorGUILayout.LabelField("Recent Colors", EditorStyles.miniBoldLabel);
            
            // 这里可以实现最近使用颜色的逻辑
            EditorGUILayout.HelpBox("Recent colors feature coming soon...", MessageType.Info);
        }

        private void DrawColorBlocks()
        {
            EditorGUILayout.LabelField("Color Blocks", EditorStyles.miniBoldLabel);
            
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null)
            {
                EditorGUILayout.HelpBox("No map data loaded", MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Add Block"))
                {
                    // 添加新的颜色块
                    string blockName = $"Block_{mapData.colorBlocks.Count + 1}";
                    Color blockColor = new Color(Random.value, Random.value, Random.value, 1f);
                    mapData.colorBlocks.Add(new ColorBlock(currentBlockId++, blockColor, blockName));
                    EditorUtility.SetDirty(mapData);
                }
                
                if (GUILayout.Button("Clear All"))
                {
                    if (EditorUtility.DisplayDialog("Clear All Blocks", "Are you sure you want to clear all color blocks?", "Yes", "No"))
                    {
                        mapData.colorBlocks.Clear();
                        EditorUtility.SetDirty(mapData);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // 显示颜色块列表
            for (int i = 0; i < mapData.colorBlocks.Count; i++)
            {
                DrawColorBlockItem(mapData.colorBlocks[i], i);
            }
        }

        private void DrawColorBlockItem(ColorBlock block, int index)
        {
            EditorGUILayout.BeginHorizontal();
            {
                // 颜色显示
                Rect colorRect = GUILayoutUtility.GetRect(20, 20);
                EditorGUI.DrawRect(colorRect, block.color);
                
                // 块信息
                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.LabelField(block.name, EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"ID: {block.id}", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                
                GUILayout.FlexibleSpace();
                
                // 操作按钮
                if (GUILayout.Button("Use", GUILayout.Width(40)))
                {
                    selectedColor = block.color;
                    editorWindow.SetCurrentColor(block.color);
                }
                
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    var mapData = editorWindow.GetCurrentMapData();
                    mapData.colorBlocks.RemoveAt(index);
                    EditorUtility.SetDirty(mapData);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}