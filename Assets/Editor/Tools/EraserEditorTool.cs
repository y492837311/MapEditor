using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class EraserEditorTool : BaseEditorTool
    {
        private Vector2Int lastPosition;
        private bool isErasing = false;

        public EraserEditorTool(MapEditorWindow window) : base(window, ToolType.Eraser)
        {
        }
        
        protected override EditOperation.OperationType GetOperationType()
        {
            return EditOperation.OperationType.Erase;
        }
        
        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) return;

            StartRecordingWithSnapshot($"Erase at ({position.x}, {position.y})");
            
            isErasing = true;
            lastPosition = position;
            EraseAtPosition(position);
        }

        public override void OnMouseDrag(Vector2Int position)
        {
            if (!isErasing || !IsPositionValid(position)) return;

            if (position != lastPosition)
            {
                EraseLine(lastPosition, position);
                lastPosition = position;
            }
        }

        public override void OnMouseUp(Vector2Int position)
        {
            if (!isErasing) return;

            isErasing = false;
            FlushPendingOperations();
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
                    DrawBrushPreview(canvasArea, mapPos, editorWindow.GetBrushSize(), new Color(1, 0, 0, 0.3f));
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
                        new Color(1, 0, 0, 0.3f), gridWidth, gridHeight);
                }
            }
        }

        private void EraseAtPosition(Vector2Int position)
        {
            int brushSize = editorWindow.GetBrushSize();

            if (brushSize == 1)
            {
                SetGridPixel(position.x, position.y, new Color32(0, 0, 0, 0), 0);
            }
            else
            {
                EraseBrush(position, brushSize);
            }
        }

        private void EraseLine(Vector2Int start, Vector2Int end)
        {
            int brushSize = editorWindow.GetBrushSize();

            if (brushSize == 1)
            {
                EraseLineSinglePixel(start, end);
            }
            else
            {
                EraseLineThick(start, end, brushSize);
            }
        }

        private void EraseBrush(Vector2Int center, int size)
        {
            int radius = size / 2;
            Color32 eraseColor = new Color32(0, 0, 0, 0);

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        Vector2Int pos = new Vector2Int(center.x + x, center.y + y);
                        SetGridPixel(pos.x, pos.y, eraseColor, 0);
                    }
                }
            }
        }

        private void EraseLineSinglePixel(Vector2Int start, Vector2Int end)
        {
            Color32 eraseColor = new Color32(0, 0, 0, 0);
            
            int dx = Mathf.Abs(end.x - start.x);
            int dy = Mathf.Abs(end.y - start.y);
            int sx = start.x < end.x ? 1 : -1;
            int sy = start.y < end.y ? 1 : -1;
            int err = dx - dy;

            Vector2Int current = start;

            while (true)
            {
                SetGridPixel(current.x, current.y, eraseColor, 0);

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

        private void EraseLineThick(Vector2Int start, Vector2Int end, int thickness)
        {
            EraseLineSinglePixel(start, end);
            
            if (thickness > 1)
            {
                int extra = thickness - 1;
                for (int i = 1; i <= extra; i++)
                {
                    EraseLineSinglePixel(
                        new Vector2Int(start.x, start.y + i),
                        new Vector2Int(end.x, end.y + i)
                    );
                    EraseLineSinglePixel(
                        new Vector2Int(start.x, start.y - i),
                        new Vector2Int(end.x, end.y - i)
                    );
                }
            }
        }
    }
}