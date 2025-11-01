using System.Collections.Generic;
using MapEditor;
using UnityEditor;
using UnityEngine;

public class BucketEditorTool : BaseEditorTool
{
    private Vector2Int lastFillPosition;

    public BucketEditorTool(MapEditorWindow window) : base(window, ToolType.Bucket)
    {
    }

    public override void OnMouseDown(Vector2Int position)
    {
        if (!IsPositionValid(position)) return;

        lastFillPosition = position;

        // 开始记录操作
        StartRecording($"Fill at ({position.x}, {position.y})");

        var mapData = editorWindow.GetCurrentMapData();
        if (mapData == null) return;

        Color targetColor = mapData.GetPixel(position.x, position.y);
        Color newColor = editorWindow.GetCurrentColor();
        float tolerance = editorWindow.GetFillTolerance();
        int newBlockId = editorWindow.GetCurrentBlockId();

        // 执行填充算法
        FloodFillWithRecording(position, targetColor, newColor, tolerance, newBlockId, mapData);

        // 完成记录
        FinishRecording();

        // 应用更改
        editorWindow.GetCurrentMapData()?.ApplyChangesImmediate();
        EditorUtility.SetDirty(editorWindow.GetCurrentMapData());

        editorWindow.Repaint();
    }

    public override void OnMouseDrag(Vector2Int position)
    {
        // 油漆桶工具通常不需要拖动操作
        // 但您可以实现连续填充或其他高级功能
    }

    public override void OnMouseUp(Vector2Int position)
    {
        // 操作在 MouseDown 中已经完成
    }

    protected override EditOperation.OperationType GetOperationType()
    {
        return EditOperation.OperationType.Fill;
    }

    public override void DrawPreview(Rect drawArea)
    {
        // 油漆桶工具的预览可以显示填充区域或目标颜色
        var mapData = editorWindow.GetCurrentMapData();
        if (mapData == null) return;

        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;

        if (drawArea.Contains(mousePos))
        {
            Vector2Int mapPos = editorWindow.ScreenToMapPositionInt(mousePos, drawArea);

            if (editorWindow.IsMapPositionValid(mapPos))
            {
                DrawFillPreview(drawArea, mapPos);
            }
        }
    }

    private void DrawFillPreview(Rect drawArea, Vector2Int mapPos)
    {
        Handles.BeginGUI();

        var mapData = editorWindow.GetCurrentMapData();
        Color targetColor = mapData.GetPixel(mapPos.x, mapPos.y);
        Color newColor = editorWindow.GetCurrentColor();

        // 绘制目标颜色预览
        Vector2 screenPos = editorWindow.MapToScreenPosition(mapPos, drawArea);

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

    // 这里插入上面实现的 FloodFillWithRecording 方法
    private void FloodFillWithRecording(Vector2Int startPos, Color targetColor, Color newColor, float tolerance,
        int newBlockId, MapDataAsset mapData)
    {
        // ... 使用上面实现的代码 ...
    }

    private bool ColorsEqual(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private void FloodFillWithRecording(Vector2Int startPos, Color targetColor, Color32 newColor,
        float tolerance, int newBlockId, MapDataAsset mapData)
    {
        if (!IsPositionValid(startPos)) return;

        // 快速检查：如果颜色相同且容差为0，不需要填充
        Color32 startPixelColor = mapData.GetPixel(startPos.x, startPos.y);
        if (tolerance == 0 && MapDataAsset.ColorsEqualForUndo(startPixelColor, newColor))
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

            Color32 currentColor = mapData.GetPixel(current.x, current.y);
            int currentBlockId = mapData.GetBlockId(current.x, current.y);

            // 使用预计算的范围进行快速颜色匹配检查
            if (IsColorInRange(currentColor, minR, maxR, minG, maxG, minB, maxB, minA, maxA))
            {
                // 记录和应用变更
                RecordPixelChange(current.x, current.y, currentColor, currentBlockId, newColor, newBlockId);
                mapData.SetPixel(current.x, current.y, newColor, newBlockId);
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

        if (isRecording && currentOperation != null)
        {
            Debug.Log($"Fill operation: {currentOperation.pixelChanges.Count} pixels recorded");
        }
        
        Debug.Log($"Flood fill completed: {filledPixels} pixels filled");
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

        Color32 neighborColor = mapData.GetPixel(neighbor.x, neighbor.y);

        if (IsColorInRange(neighborColor, minR, maxR, minG, maxG, minB, maxB, minA, maxA))
        {
            visited[neighbor.x, neighbor.y] = true;
            queue.Enqueue(neighbor);
        }
    }
}