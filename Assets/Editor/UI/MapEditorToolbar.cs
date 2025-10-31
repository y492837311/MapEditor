// Editor/UI/MapEditorToolbar.cs - 修复版本
using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class MapEditorToolbar
    {
        private MapEditorWindow editorWindow;
        private GUIContent[] toolIcons;

        public MapEditorToolbar(MapEditorWindow window)
        {
            editorWindow = window;
            InitializeToolIcons();
        }

        private void InitializeToolIcons()
        {
            toolIcons = new GUIContent[]
            {
                CreateToolContent("Pencil", "Pencil Tool (B) - Draw freehand", Color.red),
                CreateToolContent("Bucket", "Paint Bucket (G) - Fill areas", Color.blue),
                CreateToolContent("Eraser", "Eraser Tool (E) - Erase pixels", Color.gray),
                CreateToolContent("Dropper", "Eyedropper (I) - Pick colors", Color.green)
            };
        }

        private GUIContent CreateToolContent(string text, string tooltip, Color color)
        {
            // 创建简单的文本内容，避免图标问题
            return new GUIContent(text, tooltip);
        }

        public void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                DrawToolButtons();
                GUILayout.FlexibleSpace();
                DrawUtilityButtons();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolButtons()
        {
            var currentTool = editorWindow.GetCurrentTool();
            
            EditorGUILayout.LabelField("Tools:", GUILayout.Width(40));
            
            for (int i = 0; i < toolIcons.Length; i++)
            {
                ToolType toolType = (ToolType)i;
                bool isActive = currentTool != null && currentTool.GetToolType() == toolType;
                
                GUIStyle style = isActive ? 
                    new GUIStyle(EditorStyles.toolbarButton) { normal = EditorStyles.toolbarButton.active } : 
                    EditorStyles.toolbarButton;
                
                if (GUILayout.Button(toolIcons[i], style, GUILayout.Width(80), GUILayout.Height(22)))
                {
                    editorWindow.SetCurrentTool(toolType);
                }
            }
            
            GUILayout.Space(10);
        }

// 在 MapEditorToolbar 的 DrawUtilityButtons 方法中添加：
        private void DrawUtilityButtons()
        {
            // 当前颜色显示
            DrawColorButton();
    
            GUILayout.Space(10);
    
            // 视图控制按钮
            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                editorWindow.ResetView();
            }
    
            if (GUILayout.Button(editorWindow.IsGridVisible() ? "Grid ON" : "Grid OFF", 
                    EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                editorWindow.ToggleGrid();
            }
    
            if (GUILayout.Button("Layers", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                editorWindow.ToggleLayersPanel();
            }
    
            if (GUILayout.Button("Errors", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                editorWindow.RunErrorCheck();
            }
            
            if (GUILayout.Button("Zoom -", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                editorWindow.SetZoomLevel(editorWindow.GetZoomLevel() / 1.2f);
            }
    
            EditorGUILayout.LabelField($"{editorWindow.GetZoomLevel():F1}x", GUILayout.Width(40));
    
            if (GUILayout.Button("Zoom +", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                editorWindow.SetZoomLevel(editorWindow.GetZoomLevel() * 1.2f);
            }
    
            if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                editorWindow.ZoomToFit();
            }
        }

        private void DrawColorButton()
        {
            GUILayout.BeginHorizontal(GUILayout.Width(100));
            {
                Color currentColor = editorWindow.GetCurrentColor();
                
                // 颜色预览
                Rect colorRect = GUILayoutUtility.GetRect(30, 20);
                EditorGUI.DrawRect(colorRect, currentColor);
                
                // 边框
                Handles.BeginGUI();
                Handles.color = Color.white;
                Handles.DrawWireCube(colorRect.center, new Vector3(colorRect.width, colorRect.height, 0));
                Handles.EndGUI();
                
                // 颜色选择按钮
                if (GUI.Button(colorRect, ""))
                {
                    ShowColorPicker(currentColor);
                }
                
                // 颜色标签
                GUILayout.Label("Color", GUILayout.Width(40));
            }
            GUILayout.EndHorizontal();
        }

        private void ShowColorPicker(Color currentColor)
        {
            ColorPickerWindow.Show(currentColor, (newColor) => {
                editorWindow.SetCurrentColor(newColor);
            });
        }

        
// 更新 MapEditorToolbar 中的方法：

        private void ToggleGridVisibility()
        {
            editorWindow.ToggleGrid();
        }

        private void ToggleLayersVisibility()
        {
            editorWindow.ToggleLayersPanel();
        }

// 添加更多工具栏按钮方法
        private void ToggleBackgroundVisibility()
        {
            editorWindow.ToggleBackground();
        }

        private void ToggleFullScreen()
        {
            if (editorWindow.AreAllPanelsVisible())
            {
                editorWindow.HideAllPanels();
            }
            else
            {
                editorWindow.ShowAllPanels();
            }
        }
    }
}