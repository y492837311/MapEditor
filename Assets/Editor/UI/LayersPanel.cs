using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class LayersPanel
    {
        private MapEditorWindow editorWindow;
        private bool showBackground = true;
        private bool showGrid = true;
        private bool showLabels = true;

        public LayersPanel(MapEditorWindow window)
        {
            editorWindow = window;
        }

        public void DrawPanel()
        {
            EditorGUILayout.BeginVertical("HelpBox");
            {
                EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                DrawLayerToggle("Background Layer", ref showBackground);
                DrawLayerToggle("Grid Overlay", ref showGrid);
                DrawLayerToggle("Block Labels", ref showLabels);
                DrawLayerToggle("Error Overlay", ref showLabels);
                
                EditorGUILayout.Space();
                
                DrawLayerList();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLayerToggle(string label, ref bool value)
        {
            EditorGUILayout.BeginHorizontal();
            {
                value = EditorGUILayout.Toggle(value, GUILayout.Width(20));
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLayerList()
        {
            EditorGUILayout.LabelField("Color Blocks", EditorStyles.miniBoldLabel);
            
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null)
            {
                EditorGUILayout.HelpBox("No map data loaded", MessageType.Info);
                return;
            }
            
            // 这里可以显示每个颜色块的可见性控制
            foreach (var block in mapData.colorBlocks)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    bool visible = true; // 这里应该从数据中读取
                    visible = EditorGUILayout.Toggle(visible, GUILayout.Width(20));
                    
                    Rect colorRect = GUILayoutUtility.GetRect(15, 15);
                    EditorGUI.DrawRect(colorRect, block.color);
                    
                    EditorGUILayout.LabelField(block.name, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}