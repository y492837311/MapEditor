// Editor/Tools/PencilEditorTool.cs

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class PencilEditorTool : BaseEditorTool
    {
        private Vector2Int lastPosition;
        private bool isDrawing = false;
        private List<Vector2Int> pendingPixels = new List<Vector2Int>();

        public PencilEditorTool(MapEditorWindow window) : base(window, ToolType.Pencil)
        {
        }

        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) 
            {
                Debug.LogWarning($"Invalid position: {position}");
                return;
            }

            isDrawing = true;
            lastPosition = position;
    
            // 调试输出
            Debug.Log($"MouseDown at map position: {position}");
    
            DrawAtPosition(position);
            editorWindow.GetCurrentMapData()?.ApplyChangesImmediate();
            editorWindow.Repaint();
        }

        public override void OnMouseDrag(Vector2Int position)
        {
            if (!isDrawing || !IsPositionValid(position)) return;

            if (position != lastPosition)
            {
                DrawLine(lastPosition, position);
                lastPosition = position;

                // 每10个像素或每帧刷新一次
                if (pendingPixels.Count >= 10)
                {
                    FlushPendingPixels();
                }
            }
        }

        public override void OnMouseUp(Vector2Int position)
        {
            if (!isDrawing) return;

            isDrawing = false;
            FlushPendingPixels(); // 最终刷新
            EditorUtility.SetDirty(editorWindow.GetCurrentMapData());
        }

        private void DrawAtPosition(Vector2Int position)
        {
            int brushSize = editorWindow.GetBrushSize();
            if (brushSize == 1)
            {
                pendingPixels.Add(position);
            }
            else
            {
                DrawBrush(position, brushSize);
            }
        }

        private void DrawLine(Vector2Int start, Vector2Int end)
        {
            int brushSize = editorWindow.GetBrushSize();
            if (brushSize == 1)
            {
                DrawLineSinglePixel(start, end);
            }
            else
            {
                DrawLineThick(start, end, brushSize);
            }
        }

        private void DrawBrush(Vector2Int center, int size)
        {
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
                            pendingPixels.Add(pos);
                        }
                    }
                }
            }
        }

        private void DrawLineSinglePixel(Vector2Int start, Vector2Int end)
        {
            // Bresenham 直线算法
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
                    pendingPixels.Add(current);
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

        private void DrawLineThick(Vector2Int start, Vector2Int end, int thickness)
        {
            DrawLineSinglePixel(start, end);

            if (thickness > 1)
            {
                int extra = thickness - 1;
                for (int i = 1; i <= extra; i++)
                {
                    DrawLineSinglePixel(
                        new Vector2Int(start.x, start.y + i),
                        new Vector2Int(end.x, end.y + i)
                    );
                    DrawLineSinglePixel(
                        new Vector2Int(start.x, start.y - i),
                        new Vector2Int(end.x, end.y - i)
                    );
                }
            }
        }

        /// <summary>
        /// 批量应用像素更改
        /// </summary>
        private void FlushPendingPixels()
        {
            if (pendingPixels.Count == 0) return;

            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Color color = editorWindow.GetCurrentColor();
            int blockId = editorWindow.GetCurrentBlockId();

            foreach (var pos in pendingPixels)
            {
                mapData.SetPixel(pos.x, pos.y, color, blockId);
            }

            mapData.ApplyChangesImmediate();
            pendingPixels.Clear();

            editorWindow.Repaint(); // 请求重绘
        }
        
        // 在 PencilEditorTool 中修复预览绘制
        public override void DrawPreview(Rect drawArea)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (drawArea.Contains(mousePos))
            {
                Vector2Int mapPos = editorWindow.ScreenToMapPositionInt(mousePos, drawArea);
        
                if (editorWindow.IsMapPositionValid(mapPos))
                {
                    DrawBrushPreview(drawArea, mapPos, editorWindow.GetBrushSize(), editorWindow.GetCurrentColor());
                }
            }
        }

        private void DrawBrushPreview(Rect drawArea, Vector2Int mapPos, int brushSize, Color color)
        {
            Handles.BeginGUI();
    
            // 将地图坐标转换为屏幕坐标
            Vector2 screenPos = editorWindow.MapToScreenPosition(mapPos, drawArea);
    
            // 计算笔刷在屏幕上的大小（基于缩放级别）
            float screenBrushSize = brushSize * (drawArea.width / editorWindow.GetCurrentMapData().width) * editorWindow.GetZoomLevel();
    
            // 确保最小可见大小
            screenBrushSize = Mathf.Max(screenBrushSize, 3f);
    
            // 绘制半透明预览
            Handles.color = new Color(color.r, color.g, color.b, 0.3f);
            Handles.DrawSolidDisc(screenPos, Vector3.forward, screenBrushSize * 0.5f);
    
            // 绘制边框
            Handles.color = color;
            Handles.DrawWireDisc(screenPos, Vector3.forward, screenBrushSize * 0.5f);
    
            Handles.EndGUI();
        }
    }
}