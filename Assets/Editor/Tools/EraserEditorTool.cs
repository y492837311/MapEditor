using UnityEngine;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace MapEditor
{
    public class EraserEditorTool : BaseEditorTool
    {
        private Vector2Int lastPosition;
        private bool isErasing = false;

        public EraserEditorTool(MapEditorWindow window) : base(window, ToolType.Eraser)
        {
        }

        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) return;

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
            EditorUtility.SetDirty(editorWindow.GetCurrentMapData());
            editorWindow.GetCurrentMapData().ApplyChanges();
        }

        public override void DrawPreview(Rect canvasArea)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (canvasArea.Contains(mousePos))
            {
                Vector2 mapPos = editorWindow.ScreenToMapPosition(mousePos, canvasArea);
                Vector2Int intPos = new Vector2Int(Mathf.FloorToInt(mapPos.x), Mathf.FloorToInt(mapPos.y));
                
                if (IsPositionValid(intPos))
                {
                    DrawBrushPreview(canvasArea, intPos, editorWindow.GetBrushSize(), new Color(1, 0, 0, 0.3f));
                }
            }
        }

        private void EraseAtPosition(Vector2Int position)
        {
            var mapData = editorWindow.GetCurrentMapData();
            int brushSize = editorWindow.GetBrushSize();

            if (brushSize == 1)
            {
                mapData.SetPixel(position.x, position.y, new Color(0, 0, 0, 0), 0);
            }
            else
            {
                EraseBrush(position, brushSize);
            }
        }

        private void EraseLine(Vector2Int start, Vector2Int end)
        {
            var mapData = editorWindow.GetCurrentMapData();
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
            var mapData = editorWindow.GetCurrentMapData();
            int radius = size / 2;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        Vector2Int pos = new Vector2Int(center.x + x, center.y + y);
                        if (IsPositionValid(pos))
                        {
                            mapData.SetPixel(pos.x, pos.y, new Color(0, 0, 0, 0), 0);
                        }
                    }
                }
            }
        }

        private void EraseLineSinglePixel(Vector2Int start, Vector2Int end)
        {
            var mapData = editorWindow.GetCurrentMapData();
            
            int dx = Mathf.Abs(end.x - start.x);
            int dy = Mathf.Abs(end.y - start.y);
            int sx = start.x < end.x ? 1 : -1;
            int sy = start.y < end.y ? 1 : -1;
            int err = dx - dy;

            Vector2Int current = start;

            while (true)
            {
                if (IsPositionValid(current))
                {
                    mapData.SetPixel(current.x, current.y, new Color(0, 0, 0, 0), 0);
                }

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
            
            Vector2 dir = ((Vector2)(end - start)).normalized;
            Vector2 perpendicular = new Vector2(-dir.y, dir.x);
            
            int radius = thickness / 2;
            for (int i = -radius; i <= radius; i++)
            {
                Vector2 offset = perpendicular * i;
                Vector2Int offsetStart = start + Vector2Int.RoundToInt(offset);
                Vector2Int offsetEnd = end + Vector2Int.RoundToInt(offset);
                
                if (IsPositionValid(offsetStart) && IsPositionValid(offsetEnd))
                {
                    EraseLineSinglePixel(offsetStart, offsetEnd);
                }
            }
        }
    }
}