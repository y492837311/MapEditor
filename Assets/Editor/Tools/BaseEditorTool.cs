using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public abstract class BaseEditorTool
    {
        protected MapEditorWindow editorWindow;
        protected ToolType toolType;

        public BaseEditorTool(MapEditorWindow window, ToolType type)
        {
            editorWindow = window;
            toolType = type;
        }

        public virtual void OnMouseDown(Vector2Int position) { }
        public virtual void OnMouseDrag(Vector2Int position) { }
        public virtual void OnMouseUp(Vector2Int position) { }
        public virtual void OnMouseMove(Vector2Int position) { }
        public virtual void DrawPreview(Rect canvasArea) { }

        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }

        public ToolType GetToolType() => toolType;

        protected void DrawBrushPreview(Rect canvasArea, Vector2Int position, int brushSize, Color color)
        {
            if (brushSize <= 1) return;

            Handles.BeginGUI();
            
            Vector2 screenPos = MapToScreenPosition(position, canvasArea);
            float screenBrushSize = brushSize * (canvasArea.width / editorWindow.GetCurrentMapData().width);
            
            Handles.color = new Color(color.r, color.g, color.b, 0.3f);
            Handles.DrawSolidDisc(screenPos, Vector3.forward, screenBrushSize * 0.5f);
            
            Handles.color = color;
            Handles.DrawWireDisc(screenPos, Vector3.forward, screenBrushSize * 0.5f);
            
            Handles.EndGUI();
        }

        protected Vector2 MapToScreenPosition(Vector2Int mapPos, Rect canvasArea)
        {
            float x = canvasArea.x + (mapPos.x / (float)editorWindow.GetCurrentMapData().width) * canvasArea.width;
            float y = canvasArea.y + (1 - mapPos.y / (float)editorWindow.GetCurrentMapData().height) * canvasArea.height;
            return new Vector2(x, y);
        }

// 在 BaseEditorTool 类中更新位置验证方法：

        protected bool IsPositionValid(Vector2Int position)
        {
            if (editorWindow.GetCurrentMapData() == null)
                return false;
        
            return position.x >= 0 && 
                   position.x < editorWindow.GetCurrentMapData().width && 
                   position.y >= 0 && 
                   position.y < editorWindow.GetCurrentMapData().height;
        }
        protected bool IsPositionValid(int x, int y)
        {
            return editorWindow.IsMapPositionValid(x, y);
        }
        
        protected Vector2 MapToScreenPosition(Vector2 mapPos, Rect canvasArea)
        {
            return editorWindow.MapToScreenPosition(mapPos, canvasArea);
        }

        protected Vector2Int ScreenToMapPosition(Vector2 screenPos, Rect canvasArea)
        {
            return editorWindow.ScreenToMapPositionInt(screenPos, canvasArea);
        }

        protected Vector2 ScreenToMapPositionFloat(Vector2 screenPos, Rect canvasArea)
        {
            return editorWindow.ScreenToMapPosition(screenPos, canvasArea);
        }
        
    }
}