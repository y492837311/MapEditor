using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class PropertiesPanel
    {
        private MapEditorWindow editorWindow;

        public PropertiesPanel(MapEditorWindow window)
        {
            editorWindow = window;
        }

        public void DrawPanel()
        {
            EditorGUILayout.BeginVertical("HelpBox");
            {
                EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                DrawMapProperties();
                EditorGUILayout.Space();
                
                DrawToolProperties();
                EditorGUILayout.Space();
                
                DrawSelectionProperties();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawMapProperties()
        {
            EditorGUILayout.LabelField("Map Properties", EditorStyles.miniBoldLabel);
            
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null)
            {
                EditorGUILayout.HelpBox("No map loaded", MessageType.Info);
                return;
            }
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Width", mapData.width);
            EditorGUILayout.IntField("Height", mapData.height);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();
            
            // 背景图设置
            EditorGUILayout.LabelField("Background Image", EditorStyles.miniLabel);
            mapData.backgroundTexture = (Texture2D)EditorGUILayout.ObjectField(
                mapData.backgroundTexture, typeof(Texture2D), false);
        }

        private void DrawToolProperties()
        {
            EditorGUILayout.LabelField("Tool Properties", EditorStyles.miniBoldLabel);
            
            var currentTool = editorWindow.GetCurrentTool();
            if (currentTool == null) return;
            
            switch (currentTool.GetToolType())
            {
                case ToolType.Pencil:
                case ToolType.Eraser:
                    DrawBrushProperties();
                    break;
                case ToolType.Bucket:
                    DrawBucketProperties();
                    break;
                case ToolType.Eyedropper:
                    DrawEyedropperProperties();
                    break;
            }
        }

        private void DrawBrushProperties()
        {
            int brushSize = editorWindow.GetBrushSize();
            brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 50);
            editorWindow.SetBrushSize(brushSize);
            
            EditorGUILayout.Space();
            
            // 笔刷形状选择
            EditorGUILayout.LabelField("Brush Shape", EditorStyles.miniLabel);
            int brushShape = 0; // 0: Circle, 1: Square
            brushShape = EditorGUILayout.Popup(brushShape, new string[] { "Circle", "Square" });
            
            // 透明度控制
            Color currentColor = editorWindow.GetCurrentColor();
            float alpha = EditorGUILayout.Slider("Opacity", currentColor.a, 0f, 1f);
            if (alpha != currentColor.a)
            {
                currentColor.a = alpha;
                editorWindow.SetCurrentColor(currentColor);
            }
        }

        private void DrawBucketProperties()
        {
            float tolerance = editorWindow.GetFillTolerance();
            tolerance = EditorGUILayout.Slider("Fill Tolerance", tolerance, 0f, 1f);
            // 这里需要添加设置tolerance的方法到editorWindow
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Fill Mode", EditorStyles.miniLabel);
            int fillMode = EditorGUILayout.Popup(0, new string[] { "Contiguous", "Global" });
            
            EditorGUILayout.HelpBox("Contiguous: Fills connected areas only\nGlobal: Fills all matching colors", MessageType.Info);
        }

        private void DrawEyedropperProperties()
        {
            EditorGUILayout.LabelField("Sample Size", EditorStyles.miniLabel);
            int sampleSize = EditorGUILayout.Popup(0, new string[] { "Single Pixel", "3x3 Area", "5x5 Area" });
            
            EditorGUILayout.HelpBox("Click to pick color from canvas", MessageType.Info);
        }

        private void DrawSelectionProperties()
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.miniBoldLabel);
            
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Select All"))
                {
                    // 实现全选逻辑
                }
                
                if (GUILayout.Button("Clear Selection"))
                {
                    // 实现清除选择逻辑
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 选择操作
            EditorGUILayout.LabelField("Selection Actions", EditorStyles.miniLabel);
            
            if (GUILayout.Button("Fill Selection"))
            {
                // 实现填充选择区域
            }
            
            if (GUILayout.Button("Delete Selection"))
            {
                // 实现删除选择区域
            }
        }
        
        // 在 PropertiesPanel 中添加 Block 信息显示：

        private void DrawBlockInfo()
        {
            var currentBlock = editorWindow.GetCurrentColorBlock();
            if (currentBlock.HasValue)
            {
                EditorGUILayout.LabelField("Current Block", EditorStyles.miniBoldLabel);
        
                var block = currentBlock.Value;
                EditorGUILayout.BeginHorizontal();
                {
                    // 颜色显示
                    Rect colorRect = GUILayoutUtility.GetRect(20, 20);
                    EditorGUI.DrawRect(colorRect, block.color);
            
                    // Block 信息
                    EditorGUILayout.BeginVertical();
                    {
                        EditorGUILayout.LabelField(block.name, EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField($"ID: {block.id}", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}