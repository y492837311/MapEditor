using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace MapEditor
{
    public partial class MapEditorWindow
    {
        public UndoRedoManager GetUndoRedoManager()
        {
            return undoRedoManager;
        }

        private void OnUndo()
        {
            if (undoRedoManager.CanUndo())
            {
                var operation = undoRedoManager.Undo();
                ApplyOperation(operation, true);
                Debug.Log($"Undo: {operation.description}");
                Repaint();
            }
        }

        private void OnRedo()
        {
            if (undoRedoManager.CanRedo())
            {
                var operation = undoRedoManager.Redo();
                ApplyOperation(operation, false);
                Debug.Log($"Redo: {operation.description}");
                Repaint();
            }
        }

        // 在 MapEditorWindow 中更新 ApplyOperation 方法，添加调试信息
        private void ApplyOperation(EditOperation operation, bool isUndo)
        {
            if (currentMapData == null || operation == null)
            {
                Debug.LogError("ApplyOperation: MapData or operation is null");
                return;
            }

            Debug.Log($"=== Applying Operation: {operation.description} ===");
            Debug.Log($"Type: {operation.type}, Undo: {isUndo}, Pixels: {operation.pixelChanges.Count}");

            // 操作前状态
            currentMapData.DebugTextureState($"Before {(isUndo ? "Undo" : "Redo")}");

            int pixelsApplied = 0;
            int pixelsSkipped = 0;
            var failedPixels = new List<string>();

            // 使用批量操作确保一致性
            var batchOperations = new List<MapDataAsset.PixelOperation>();

            foreach (var change in operation.pixelChanges)
            {
                var position = new Vector2Int(change.x, change.y);

                if (IsMapPositionValid(position))
                {
                    Color32 colorToApply = isUndo ? change.previousColor : change.newColor;
                    int blockIdToApply = isUndo ? change.previousBlockId : change.newBlockId;

                    // 获取当前状态用于验证
                    Color32 currentColor = currentMapData.GetPixel(change.x, change.y);
                    int currentBlockId = currentMapData.GetBlockId(change.x, change.y);

                    // 添加到批量操作
                    batchOperations.Add(new MapDataAsset.PixelOperation
                    {
                        x = change.x,
                        y = change.y,
                        color = colorToApply,
                        blockId = blockIdToApply
                    });

                    pixelsApplied++;

                    // 详细调试
                    Debug.Log($"{(isUndo ? "Undo" : "Redo")} pixel ({change.x}, {change.y}): " +
                              $"Color: {currentColor} -> {colorToApply}, " +
                              $"Block: {currentBlockId} -> {blockIdToApply}");
                }
                else
                {
                    pixelsSkipped++;
                    failedPixels.Add($"({change.x}, {change.y})");
                    Debug.LogWarning($"Skipped invalid pixel position: ({change.x}, {change.y})");
                }
            }

            // 批量应用所有更改
            if (batchOperations.Count > 0)
            {
                Debug.Log($"Applying batch operation with {batchOperations.Count} pixels");
                currentMapData.SetPixelsBatch(batchOperations);

                // 强制应用并验证
                currentMapData.ForceApplyChanges();

                // 验证关键像素
                foreach (var op in batchOperations.Take(5)) // 验证前5个像素作为样本
                {
                    currentMapData.ValidatePixelOperation(op.x, op.y, (Color32)op.color, op.blockId,
                        isUndo ? "Undo" : "Redo");
                }
            }

            // 操作后状态
            currentMapData.DebugTextureState($"After {(isUndo ? "Undo" : "Redo")}");

            // 处理选择操作
            if (operation.type == EditOperation.OperationType.Selection)
            {
                if (isUndo)
                {
                    ClearSelection();
                }
                else if (operation.selectionData.selectedPixels != null)
                {
                    SetSelection(operation.selectionData.selectedPixels, operation.selectionData.bounds);
                }
            }

            Debug.Log($"=== Operation Complete: {pixelsApplied} applied, {pixelsSkipped} skipped ===");
            if (failedPixels.Count > 0)
            {
                Debug.LogWarning($"Failed pixels: {string.Join(", ", failedPixels)}");
            }

            // 强制重绘界面
            Repaint();
        }

        private void OnSelectAll()
        {
            if (currentMapData == null) return;

            var selectedPixels = new List<Vector2Int>();
            var bounds = new RectInt(0, 0, currentMapData.width, currentMapData.height);

            // 选择所有有效像素（非透明）
            for (int y = 0; y < currentMapData.height; y++)
            {
                for (int x = 0; x < currentMapData.width; x++)
                {
                    Color32 pixel = currentMapData.GetPixel(x, y);
                    if (pixel.a > 0) // 只选择不透明像素
                    {
                        selectedPixels.Add(new Vector2Int(x, y));
                    }
                }
            }

            SetSelection(selectedPixels, bounds);

            // 记录选择操作
            var operation = EditOperation.CreateSelectionOperation(selectedPixels, bounds);
            undoRedoManager.RecordOperation(operation);

            Debug.Log($"Selected all: {selectedPixels.Count} pixels");
            Repaint();
        }

        private void OnClearSelection()
        {
            ClearSelection();
            Debug.Log("Selection cleared");
            Repaint();
        }

// 选择管理方法
        public void SetSelection(List<Vector2Int> pixels, RectInt bounds)
        {
            selectedPixels = new List<Vector2Int>(pixels);
            selectionBounds = bounds;
            hasSelection = selectedPixels.Count > 0;
        }

        public void ClearSelection()
        {
            selectedPixels.Clear();
            selectionBounds = new RectInt();
            hasSelection = false;
        }

        public void UpdateSelectionPreview(RectInt bounds)
        {
            selectionBounds = bounds;
            // 这里可以添加实时预览的逻辑
        }

// 在绘制方法中添加选择可视化
        private void DrawSelection(Rect drawArea)
        {
            if (!hasSelection) return;

            Handles.BeginGUI();

            // 绘制选择区域
            Vector2 startScreen =
                MapToScreenPosition(new Vector2(selectionBounds.xMin, selectionBounds.yMin), drawArea);
            Vector2 endScreen = MapToScreenPosition(new Vector2(selectionBounds.xMax, selectionBounds.yMax), drawArea);

            Rect selectionRect = new Rect(startScreen.x, startScreen.y, endScreen.x - startScreen.x,
                endScreen.y - startScreen.y);

            // 绘制选择边框
            Handles.color = new Color(0, 0.5f, 1f, 0.8f);
            Handles.DrawWireCube(selectionRect.center, new Vector3(selectionRect.width, selectionRect.height, 0));

            // 绘制控制点（用于调整大小）
            float handleSize = 6f;
            DrawSelectionHandle(selectionRect.x, selectionRect.y, handleSize);
            DrawSelectionHandle(selectionRect.xMax, selectionRect.y, handleSize);
            DrawSelectionHandle(selectionRect.x, selectionRect.yMax, handleSize);
            DrawSelectionHandle(selectionRect.xMax, selectionRect.yMax, handleSize);

            Handles.EndGUI();
        }

        private void DrawSelectionHandle(float x, float y, float size)
        {
            Rect handleRect = new Rect(x - size / 2, y - size / 2, size, size);
            EditorGUI.DrawRect(handleRect, new Color(0, 0.5f, 1f, 1f));
        }

        // 添加删除选择的方法
        // 在 MapEditorWindow 类中更新 DeleteSelectionOptimized：
        private void DeleteSelectionOptimized()
        {
            if (!hasSelection || currentMapData == null) return;

            Debug.Log($"Starting optimized deletion of {selectedPixels.Count} pixels");

            // 创建操作记录
            var operation = EditOperation.Create("Delete selection", EditOperation.OperationType.Erase);
    
            // 记录并执行删除
            foreach (var pixel in selectedPixels)
            {
                if (IsMapPositionValid(pixel))
                {
                    Color32 originalColor = currentMapData.GetPixel(pixel.x, pixel.y);
                    int originalBlockId = currentMapData.GetBlockId(pixel.x, pixel.y);
            
                    // 记录像素变更
                    operation.AddPixelChange(pixel.x, pixel.y, originalColor, originalBlockId, 
                        new Color32(0, 0, 0, 0), 0);
            
                    // 执行删除
                    currentMapData.SetPixel(pixel.x, pixel.y, new Color32(0, 0, 0, 0), 0);
                }
            }
    
            // 设置选择数据（用于撤销时恢复选择状态）
            operation.SetSelectionData(new List<Vector2Int>(selectedPixels), 
                CalculateSelectionBounds(selectedPixels));
    
            currentMapData.ApplyChangesImmediate();
    
            // 记录操作
            undoRedoManager.RecordOperation(operation);
    
            ClearSelection();
            Debug.Log($"Deleted {selectedPixels.Count} pixels");
            Repaint();
        }

// 辅助结构
        private struct PixelInfo
        {
            public Vector2Int position;
            public Color32 originalColor;
            public int originalBlockId;
        }
        
// 新的 CreateOptimizedDeleteOperation 方法（如果需要）
        private EditOperation CreateOptimizedDeleteOperation(List<PixelInfo> deletedPixels)
        {
            var pixelChanges = new List<EditOperation.PixelChange>();
            var selectedPixelPositions = new List<Vector2Int>();
    
            foreach (var pixelInfo in deletedPixels)
            {
                pixelChanges.Add(new EditOperation.PixelChange
                {
                    x = pixelInfo.position.x,
                    y = pixelInfo.position.y,
                    previousColor = pixelInfo.originalColor,
                    previousBlockId = pixelInfo.originalBlockId,
                    newColor = new Color32(0, 0, 0, 0),
                    newBlockId = 0
                });
        
                selectedPixelPositions.Add(pixelInfo.position);
            }
    
            return EditOperation.CreateDeleteOperation(selectedPixelPositions, pixelChanges);
        }

        /// <summary>
        /// 计算选择区域的边界
        /// </summary>
        private RectInt CalculateSelectionBounds(List<Vector2Int> pixels)
        {
            if (pixels == null || pixels.Count == 0)
                return new RectInt();
    
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
    
            foreach (var pixel in pixels)
            {
                minX = Mathf.Min(minX, pixel.x);
                minY = Mathf.Min(minY, pixel.y);
                maxX = Mathf.Max(maxX, pixel.x);
                maxY = Mathf.Max(maxY, pixel.y);
            }
    
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}