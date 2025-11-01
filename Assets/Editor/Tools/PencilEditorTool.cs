using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MapEditor
{
    public class PencilEditorTool : BaseEditorTool
    {
        private Vector2Int lastPosition;
        private bool isDrawing = false;

        public PencilEditorTool(MapEditorWindow window) : base(window, ToolType.Pencil)
        {
        }

        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) return;

            // 使用网格快照确保撤销可靠性
            StartRecordingWithSnapshot($"Draw at ({position.x}, {position.y})");
            
            isDrawing = true;
            lastPosition = position;
            DrawAtPosition(position);
        }

        public override void OnMouseDrag(Vector2Int position)
        {
            if (!isDrawing || !IsPositionValid(position)) return;

            if (position != lastPosition)
            {
                DrawLine(lastPosition, position);
                lastPosition = position;
            }
        }

        public override void OnMouseUp(Vector2Int position)
        {
            if (!isDrawing) return;

            isDrawing = false;
            FlushPendingOperations(); // 确保所有操作都应用
            FinishRecording();
            
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData != null)
            {
                EditorUtility.SetDirty(mapData);
            }
        }

        public override void OnMouseMove(Vector2Int position)
        {
            // 可选的鼠标移动处理
        }

        public override void DrawPreview(Rect canvasArea)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (canvasArea.Contains(mousePos))
            {
                Vector2Int mapPos = ScreenToMapPosition(mousePos, canvasArea);
                if (IsPositionValid(mapPos))
                {
                    DrawBrushPreview(canvasArea, mapPos, editorWindow.GetBrushSize(), editorWindow.GetCurrentColor());
                }
            }
        }

        public override void DrawPreviewAligned(Rect canvasArea, float gridWidth, float gridHeight)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (canvasArea.Contains(mousePos))
            {
                Vector2Int mapPos = editorWindow.ScreenToMapPositionInt(mousePos, canvasArea);
                if (IsPositionValid(mapPos))
                {
                    DrawBrushPreviewAligned(canvasArea, mapPos, editorWindow.GetBrushSize(), 
                        editorWindow.GetCurrentColor(), gridWidth, gridHeight);
                }
            }
        }
        
        protected override EditOperation.OperationType GetOperationType()
        {
            return EditOperation.OperationType.Draw;
        }

        private void DrawAtPosition(Vector2Int position)
        {
            int brushSize = editorWindow.GetBrushSize();
            Color color = editorWindow.GetCurrentColor();
            int blockId = editorWindow.GetCurrentBlockId();

            if (brushSize == 1)
            {
                SetGridPixel(position.x, position.y, color, blockId);
            }
            else
            {
                DrawBrush(position, brushSize, color, blockId);
            }
        }

        private void DrawLine(Vector2Int start, Vector2Int end)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;
    
            int brushSize = editorWindow.GetBrushSize();
            Color color = editorWindow.GetCurrentColor();
            int blockId = editorWindow.GetCurrentBlockId();

            // 直接使用现有的非优化版本，避免参数问题
            if (brushSize == 1)
            {
                DrawLineSinglePixel(start, end, color, blockId, mapData);
            }
            else
            {
                DrawLineThick(start, end, brushSize, color, blockId, mapData);
            }
    
            FlushPendingOperations();
        }

        private void DrawBrush(Vector2Int center, int size, Color color, int blockId)
        {
            int radius = size / 2;
            Color32 color32 = color;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        Vector2Int pos = new Vector2Int(center.x + x, center.y + y);
                        SetGridPixel(pos.x, pos.y, color32, blockId);
                    }
                }
            }
        }

        private void DrawLineSinglePixel(Vector2Int start, Vector2Int end, Color color, int blockId, MapDataAsset mapData)
        {
            Color32 color32 = color;
    
            int dx = Mathf.Abs(end.x - start.x);
            int dy = Mathf.Abs(end.y - start.y);
            int sx = start.x < end.x ? 1 : -1;
            int sy = start.y < end.y ? 1 : -1;
            int err = dx - dy;

            Vector2Int current = start;

            while (true)
            {
                SetGridPixel(current.x, current.y, color32, blockId);

                if (current.x == end.x && current.y == end.y) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    current.x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    current.y += sy;
                }
            }
        }

        private void DrawLineThick(Vector2Int start, Vector2Int end, int thickness, Color color, int blockId, MapDataAsset mapData)
        {
            DrawLineSinglePixel(start, end, color, blockId, mapData);
    
            if (thickness > 1)
            {
                int extra = thickness - 1;
                for (int i = 1; i <= extra; i++)
                {
                    DrawLineSinglePixel(
                        new Vector2Int(start.x, start.y + i),
                        new Vector2Int(end.x, end.y + i),
                        color, blockId, mapData
                    );
                    DrawLineSinglePixel(
                        new Vector2Int(start.x, start.y - i),
                        new Vector2Int(end.x, end.y - i),
                        color, blockId, mapData
                    );
                }
            }
        }
    }
}