using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

            var mapData = editorWindow.GetCurrentMapData();
            Color targetColor = mapData.GetPixel(position.x, position.y);
            Color newColor = editorWindow.GetCurrentColor();
            float tolerance = editorWindow.GetFillTolerance();

            FloodFill(position, targetColor, newColor, tolerance);

            EditorUtility.SetDirty(mapData);
            mapData.ApplyChanges();
        }

        public override void DrawPreview(Rect canvasArea)
        {
            // 油漆桶工具不需要实时预览
        }

        private void FloodFill(Vector2Int startPos, Color targetColor, Color newColor, float tolerance)
        {
            var mapData = editorWindow.GetCurrentMapData();
            
            if (ColorsEqual(targetColor, newColor, tolerance)) return;

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            
            queue.Enqueue(startPos);
            visited.Add(startPos);

            int filledPixels = 0;
            const int maxFillPixels = 1000000; // 安全限制

            while (queue.Count > 0 && filledPixels < maxFillPixels)
            {
                Vector2Int current = queue.Dequeue();
                
                if (IsPositionValid(current))
                {
                    mapData.SetPixel(current.x, current.y, newColor, editorWindow.GetCurrentBlockId());
                    filledPixels++;
                }

                // 检查四个方向的邻居
                CheckNeighbor(current + Vector2Int.right, targetColor, tolerance, visited, queue);
                CheckNeighbor(current + Vector2Int.left, targetColor, tolerance, visited, queue);
                CheckNeighbor(current + Vector2Int.up, targetColor, tolerance, visited, queue);
                CheckNeighbor(current + Vector2Int.down, targetColor, tolerance, visited, queue);
            }

            if (filledPixels >= maxFillPixels)
            {
                Debug.LogWarning("Flood fill reached maximum pixel limit. The area might be too large.");
            }
        }

        private void CheckNeighbor(Vector2Int pos, Color targetColor, float tolerance, 
                                 HashSet<Vector2Int> visited, Queue<Vector2Int> queue)
        {
            if (!IsPositionValid(pos) || visited.Contains(pos)) return;

            var mapData = editorWindow.GetCurrentMapData();
            Color currentColor = mapData.GetPixel(pos.x, pos.y);

            if (ColorsEqual(currentColor, targetColor, tolerance))
            {
                visited.Add(pos);
                queue.Enqueue(pos);
            }
        }

        private bool ColorsEqual(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance &&
                   Mathf.Abs(a.a - b.a) <= tolerance;
        }
    }
}