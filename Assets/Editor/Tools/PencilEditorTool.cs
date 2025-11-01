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

        public PencilEditorTool(MapEditorWindow window) : base(window, ToolType.Pencil)
        {
        }

        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) return;

            // 开始记录操作
            StartRecording($"Draw at ({position.x}, {position.y})");

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
                FlushPendingPixels();
            }
        }

        public override void OnMouseUp(Vector2Int position)
        {
            if (!isDrawing) return;

            isDrawing = false;
    
            // 确保所有挂起的像素都被应用
            FlushPendingPixels();
    
            // 强制应用纹理更改
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData != null)
            {
                mapData.ForceApplyChanges();
            }
    
            FinishRecording();
            EditorUtility.SetDirty(editorWindow.GetCurrentMapData());
    
            // 强制重绘
            editorWindow.Repaint();
    
            Debug.Log($"Pencil tool operation completed at ({position.x}, {position.y})");
        }
        
        protected override EditOperation.OperationType GetOperationType()
        {
            return EditOperation.OperationType.Draw;
        }

        private void DrawAtPosition(Vector2Int position)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;
        
            int brushSize = editorWindow.GetBrushSize();
            Color color = editorWindow.GetCurrentColor();
            int blockId = editorWindow.GetCurrentBlockId();

            if (brushSize == 1)
            {
                DrawSinglePixel(position, color, blockId, mapData);
            }
            else
            {
                DrawBrush(position, brushSize, color, blockId, mapData);
            }
        }
        
        private void DrawSinglePixel(Vector2Int position, Color color, int blockId, MapDataAsset mapData)
        {
            if (!IsPositionValid(position)) return;

            // 确保使用 Color32 避免浮点数问题
            Color32 newColor32 = color;
            Color32 previousColor = mapData.GetPixel(position.x, position.y);
            int previousBlockId = mapData.GetBlockId(position.x, position.y);

            // 严格检查是否需要更改
            bool shouldChange = !MapDataAsset.ColorsEqualForUndo(previousColor, newColor32) || 
                                previousBlockId != blockId;

            if (shouldChange)
            {
                // 调试信息
                Debug.Log($"Drawing pixel at ({position.x}, {position.y}): " +
                          $"{previousColor} -> {newColor32}, " +
                          $"Block: {previousBlockId} -> {blockId}");

                // 记录变更（在应用之前记录原始状态）
                RecordPixelChange(position.x, position.y, previousColor, previousBlockId, newColor32, blockId);

                // 应用新状态
                mapData.SetPixel(position.x, position.y, newColor32, blockId);
        
                // 立即验证
                mapData.ValidatePixelOperation(position.x, position.y, newColor32, blockId, "Draw");
            }
            else
            {
                Debug.Log($"Pixel unchanged at ({position.x}, {position.y}) - identical state");
            }
        }
        
        private bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }
        
        private void DrawLine(Vector2Int start, Vector2Int end)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;
        
            int brushSize = editorWindow.GetBrushSize();
            Color color = editorWindow.GetCurrentColor();
            int blockId = editorWindow.GetCurrentBlockId();

            if (brushSize == 1)
            {
                DrawLineSinglePixel(start, end, color, blockId, mapData);
            }
            else
            {
                DrawLineThick(start, end, brushSize, color, blockId, mapData);
            }
        }

        private void DrawBrush(Vector2Int center, int size, Color color, int blockId, MapDataAsset mapData)
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
                            DrawSinglePixel(pos, color, blockId, mapData);
                        }
                    }
                }
            }
        }

        // 在 PencilEditorTool 中添加调试信息
        private void DrawLineSinglePixel(Vector2Int start, Vector2Int end, Color color, int blockId, MapDataAsset mapData)
        {
            int pixelsDrawn = 0;
            int pixelsRecorded = 0;
    
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
                    pixelsDrawn++;
            
                    // 记录原始状态
                    Color32 previousColor = mapData.GetPixel(current.x, current.y);
                    int previousBlockId = mapData.GetBlockId(current.x, current.y);
            
                    // 应用新状态
                    mapData.SetPixel(current.x, current.y, color, blockId);
            
                    // 记录变更
                    if (isRecording && currentOperation != null)
                    {
                        RecordPixelChange(current.x, current.y, previousColor, previousBlockId, color, blockId);
                        pixelsRecorded++;
                    }
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
    
            // 调试信息
            if (pixelsDrawn > 0)
            {
                Debug.Log($"Line drawn: {pixelsDrawn} pixels, recorded: {pixelsRecorded} pixels, isRecording: {isRecording}");
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

        /// <summary>
        /// 批量应用像素更改
        /// </summary>
        private void FlushPendingPixels()
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;
            mapData.ApplyChangesImmediate();
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