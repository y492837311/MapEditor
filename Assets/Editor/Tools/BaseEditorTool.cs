using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public abstract class BaseEditorTool
    {
        protected MapEditorWindow editorWindow;
        protected ToolType toolType;

        protected EditOperation currentOperation;
        protected bool isRecording = false;
        
        public BaseEditorTool(MapEditorWindow window, ToolType type)
        {
            editorWindow = window;
            toolType = type;
        }

        public virtual void OnMouseDrag(Vector2Int position) { }
        public virtual void OnMouseMove(Vector2Int position) { }
        public virtual void DrawPreview(Rect canvasArea) { }

        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }

        public ToolType GetToolType() => toolType;
        
        protected void DebugPixelChange(int x, int y, Color32 previousColor, Color32 newColor, int previousBlockId, int newBlockId)
        {
            if (isRecording)
            {
                Debug.Log($"Recording pixel change at ({x}, {y}): " +
                          $"Color: {previousColor} -> {newColor}, " +
                          $"Block: {previousBlockId} -> {newBlockId}, " +
                          $"ColorsEqual: {MapDataAsset.ColorsEqualForUndo(previousColor, newColor)}");
            }
        }
        
        public virtual void OnMouseDown(Vector2Int position)
        {
            StartRecording($"{toolType} at ({position.x}, {position.y})");
        }

        public virtual void OnMouseUp(Vector2Int position)
        {
            FinishRecording();
        }

        protected void StartRecording(string description)
        {
            // 确保没有正在进行的操作
            if (isRecording && currentOperation != null)
            {
                Debug.LogWarning($"Overwriting unfinished operation: {currentOperation.description}");
                FinishRecording();
            }
    
            currentOperation = EditOperation.Create(description, GetOperationType());
            isRecording = true;
    
            Debug.Log($"Started recording: {description}");
        }

        protected void FinishRecording()
        {
            if (isRecording && currentOperation != null)
            {
                if (currentOperation.pixelChanges.Count > 0)
                {
                    // 更新描述以包含像素数量
                    currentOperation.description = $"{currentOperation.description} - {currentOperation.pixelChanges.Count} pixels";
                    editorWindow.GetUndoRedoManager().RecordOperation(currentOperation);
                    Debug.Log($"Finished recording: {currentOperation.description}");
                }
                else
                {
                    Debug.LogWarning($"Finished recording with no pixel changes: {currentOperation.description}");
                }
            }
            else
            {
                Debug.LogWarning("FinishRecording called but no active recording");
            }
    
            isRecording = false;
            currentOperation = null;
        }

        protected void RecordPixelChange(int x, int y, Color32 previousColor, int previousBlockId, Color32 newColor, int newBlockId)
        {
            if (isRecording && currentOperation != null)
            {
                currentOperation.AddPixelChange(x, y, previousColor, previousBlockId, newColor, newBlockId);
            }
        }
        
        protected abstract EditOperation.OperationType GetOperationType();
        
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