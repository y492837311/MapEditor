using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class BucketEditorTool : BaseEditorTool
    {
        public BucketEditorTool(MapEditorWindow window) : base(window, ToolType.Bucket)
        {
        }

        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) return;

            StartRecordingWithSnapshot($"Fill at ({position.x}, {position.y})");
            
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Color targetColor = mapData.GetGridPixel(position.x, position.y).color;
            Color newColor = editorWindow.GetCurrentColor();
            float tolerance = editorWindow.GetFillTolerance();
            int newBlockId = editorWindow.GetCurrentBlockId();

            // 执行填充算法
            FloodFill(position, targetColor, newColor, tolerance, newBlockId, mapData);
            
            FlushPendingOperations();
            FinishRecording();
            
            EditorUtility.SetDirty(mapData);
            editorWindow.Repaint();
        }

        public override void OnMouseDrag(Vector2Int position)
        {
            // 油漆桶工具通常不需要拖动操作
        }

        public override void OnMouseUp(Vector2Int position)
        {
            // 操作在 MouseDown 中已经完成
        }

        public override void OnMouseMove(Vector2Int position)
        {
            // 可选的鼠标移动处理
        }

        protected override EditOperation.OperationType GetOperationType()
        {
            return EditOperation.OperationType.Fill;
        }

        public override void DrawPreview(Rect drawArea)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (drawArea.Contains(mousePos))
            {
                Vector2Int mapPos = ScreenToMapPosition(mousePos, drawArea);
                if (IsPositionValid(mapPos))
                {
                    DrawFillPreview(drawArea, mapPos);
                }
            }
        }

        private void DrawFillPreview(Rect drawArea, Vector2Int mapPos)
        {
            Handles.BeginGUI();

            var mapData = editorWindow.GetCurrentMapData();
            Color targetColor = mapData.GetGridPixel(mapPos.x, mapPos.y).color;
            Color newColor = editorWindow.GetCurrentColor();

            // 绘制目标颜色预览
            Vector2 screenPos = MapToScreenPosition(new Vector2(mapPos.x + 0.5f, mapPos.y + 0.5f), drawArea);

            // 绘制目标颜色圆圈
            Rect colorRect = new Rect(screenPos.x - 15, screenPos.y - 15, 30, 30);
            EditorGUI.DrawRect(colorRect, targetColor);
            Handles.color = Color.white;
            Handles.DrawWireCube(colorRect.center, new Vector3(30, 30, 0));

            // 绘制填充颜色指示
            Rect fillIndicator = new Rect(screenPos.x + 5, screenPos.y + 5, 10, 10);
            EditorGUI.DrawRect(fillIndicator, newColor);

            Handles.EndGUI();
        }

        private void FloodFill(Vector2Int startPos, Color targetColor, Color newColor, float tolerance, int newBlockId, MapDataAsset mapData)
        {
            if (!IsPositionValid(startPos)) return;

            // 快速检查：如果颜色相同且容差为0，不需要填充
            Color32 startPixelColor = mapData.GetGridPixel(startPos.x, startPos.y).color;
            Color32 newColor32 = newColor;
            
            if (tolerance == 0 && ColorsEqual(startPixelColor, newColor32))
                return;

            var visited = new bool[mapData.width, mapData.height];
            var queue = new Queue<Vector2Int>();

            queue.Enqueue(startPos);
            visited[startPos.x, startPos.y] = true;

            int filledPixels = 0;
            const int maxFillPixels = 1000000;

            // 预先计算目标颜色的容差范围
            Color32 targetColor32 = targetColor;
            byte minR = (byte)Mathf.Max(0, targetColor32.r - (byte)(tolerance * 255));
            byte maxR = (byte)Mathf.Min(255, targetColor32.r + (byte)(tolerance * 255));
            byte minG = (byte)Mathf.Max(0, targetColor32.g - (byte)(tolerance * 255));
            byte maxG = (byte)Mathf.Min(255, targetColor32.g + (byte)(tolerance * 255));
            byte minB = (byte)Mathf.Max(0, targetColor32.b - (byte)(tolerance * 255));
            byte maxB = (byte)Mathf.Min(255, targetColor32.b + (byte)(tolerance * 255));
            byte minA = (byte)Mathf.Max(0, targetColor32.a - (byte)(tolerance * 255));
            byte maxA = (byte)Mathf.Min(255, targetColor32.a + (byte)(tolerance * 255));

            while (queue.Count > 0 && filledPixels < maxFillPixels)
            {
                Vector2Int current = queue.Dequeue();

                Color32 currentColor = mapData.GetGridPixel(current.x, current.y).color;

                // 使用预计算的范围进行快速颜色匹配检查
                if (IsColorInRange(currentColor, minR, maxR, minG, maxG, minB, maxB, minA, maxA))
                {
                    SetGridPixel(current.x, current.y, newColor32, newBlockId);
                    filledPixels++;

                    // 处理四个方向的邻居
                    ProcessNeighbor(current + Vector2Int.right, visited, queue, mapData,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);
                    ProcessNeighbor(current + Vector2Int.left, visited, queue, mapData,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);
                    ProcessNeighbor(current + Vector2Int.up, visited, queue, mapData,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);
                    ProcessNeighbor(current + Vector2Int.down, visited, queue, mapData,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);
                }
            }

            if (filledPixels >= maxFillPixels)
            {
                Debug.LogWarning($"Flood fill reached maximum pixel limit ({maxFillPixels}). Stopped early.");
            }

            Debug.Log($"Flood fill completed: {filledPixels} pixels filled");
        }

        private bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private bool IsColorInRange(Color32 color, byte minR, byte maxR, byte minG, byte maxG, byte minB, byte maxB,
            byte minA, byte maxA)
        {
            return color.r >= minR && color.r <= maxR &&
                   color.g >= minG && color.g <= maxG &&
                   color.b >= minB && color.b <= maxB &&
                   color.a >= minA && color.a <= maxA;
        }

        private void ProcessNeighbor(Vector2Int neighbor, bool[,] visited, Queue<Vector2Int> queue, MapDataAsset mapData,
            byte minR, byte maxR, byte minG, byte maxG, byte minB, byte maxB, byte minA, byte maxA)
        {
            if (!IsPositionValid(neighbor) || visited[neighbor.x, neighbor.y])
                return;

            Color32 neighborColor = mapData.GetGridPixel(neighbor.x, neighbor.y).color;

            if (IsColorInRange(neighborColor, minR, maxR, minG, maxG, minB, maxB, minA, maxA))
            {
                visited[neighbor.x, neighbor.y] = true;
                queue.Enqueue(neighbor);
            }
        }
    }
}