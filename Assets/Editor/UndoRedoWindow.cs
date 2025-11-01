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

        /// <summary>
        /// 优化的撤销重做应用
        /// </summary>
        private void ApplyOperation(EditOperation operation, bool isUndo)
        {
            if (currentMapData == null || operation == null) return;

            Debug.Log($"Applying operation: {operation.description}, Undo: {isUndo}");

            // 高性能路径：网格快照恢复
            if (operation.type == EditOperation.OperationType.GridSnapshot && operation.gridSnapshot != null)
            {
                currentMapData.RestoreGridFromSnapshot(operation.gridSnapshot);
                Debug.Log("Restored from grid snapshot");
            }
            // 轻量级路径：单个操作恢复
            else if (operation.lightChanges?.Count > 0)
            {
                var batchOperations = new List<MapDataAsset.PixelOperation>();
        
                foreach (var change in operation.lightChanges)
                {
                    batchOperations.Add(new MapDataAsset.PixelOperation(
                        change.x, change.y,
                        isUndo ? change.previousColor : change.newColor,
                        isUndo ? change.previousBlockId : change.newBlockId
                    ));
                }
        
                currentMapData.SetGridPixelsBatch(batchOperations);
                Debug.Log($"Applied {batchOperations.Count} pixel changes");
            }
    
            // 强制刷新
            currentMapData.ForceTextureRefresh();
            Repaint();
        }


        /// <summary>
        /// 全选操作
        /// </summary>
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
                    var pixel = currentMapData.GetGridPixel(x, y);
                    if (pixel.color.a > 0) // 只选择不透明像素
                    {
                        selectedPixels.Add(new Vector2Int(x, y));
                    }
                }
            }

            SetSelection(selectedPixels, bounds);

            // 记录选择操作
            var operation = EditOperation.CreateLightOperation($"Select all {selectedPixels.Count} pixels", EditOperation.OperationType.Selection);
            operation.SetSelectionData(selectedPixels, bounds);
            undoRedoManager.RecordOperation(operation);

            Debug.Log($"Selected all: {selectedPixels.Count} pixels");
            Repaint();
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        private void OnClearSelection()
        {
            ClearSelection();
            Debug.Log("Selection cleared");
            Repaint();
        }
        public void SetSelection(List<Vector2Int> pixels, RectInt bounds)
        {
            selectedPixels = new List<Vector2Int>(pixels);
            selectionBounds = bounds;
            hasSelection = selectedPixels.Count > 0;
            Repaint();
        }

        public void ClearSelection()
        {
            selectedPixels.Clear();
            selectionBounds = new RectInt();
            hasSelection = false;
            Repaint();
        }

        public void UpdateSelectionPreview(RectInt bounds)
        {
            selectionBounds = bounds;
            // 这里可以添加实时预览的逻辑
            Repaint();
        }

        private void DrawSelection(Rect drawArea)
        {
            if (!hasSelection) return;

            Handles.BeginGUI();

            // 绘制选择区域
            Vector2 startScreen = MapToScreenPosition(new Vector2(selectionBounds.xMin, selectionBounds.yMin), drawArea);
            Vector2 endScreen = MapToScreenPosition(new Vector2(selectionBounds.xMax, selectionBounds.yMax), drawArea);

            Rect selectionRect = new Rect(startScreen.x, startScreen.y, endScreen.x - startScreen.x,
                endScreen.y - startScreen.y);

            // 绘制选择边框
            Handles.color = new Color(0, 0.5f, 1f, 0.8f);
            Handles.DrawWireCube(selectionRect.center, new Vector3(selectionRect.width, selectionBounds.height, 0));

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
        /// <summary>
        /// 删除选择区域
        /// </summary>
        private void DeleteSelectionOptimized()
        {
            if (!hasSelection || currentMapData == null) return;

            Debug.Log($"Starting deletion of {selectedPixels.Count} pixels");

            // 创建操作记录 - 使用网格快照确保撤销可靠性
            var snapshot = currentMapData.CreateGridSnapshot();
            var operation = EditOperation.CreateGridSnapshot($"Delete {selectedPixels.Count} pixels", snapshot);
    
            // 执行删除
            var batchOperations = new List<MapDataAsset.PixelOperation>();
            foreach (var pixel in selectedPixels)
            {
                if (IsMapPositionValid(pixel))
                {
                    batchOperations.Add(new MapDataAsset.PixelOperation(pixel.x, pixel.y, new Color32(0, 0, 0, 0), 0));
                }
            }
    
            if (batchOperations.Count > 0)
            {
                currentMapData.SetGridPixelsBatch(batchOperations);
            }

            // 记录操作
            undoRedoManager.RecordOperation(operation);

            ClearSelection();
            Debug.Log($"Deleted {selectedPixels.Count} pixels");
            Repaint();
        }
        private void StartRecordingWithSnapshot(string description)
        {
            if (currentMapData == null) return;
        
            var snapshot = currentMapData.CreateGridSnapshot();
            var operation = EditOperation.CreateGridSnapshot(description, snapshot);
            undoRedoManager.RecordOperation(operation);
        }
        private void FlushPendingOperations()
        {
            // 如果有批量操作系统，在这里实现
            Repaint();
        }

// 辅助结构
        private struct PixelInfo
        {
            public Vector2Int position;
            public Color32 originalColor;
            public int originalBlockId;
        }
        

        /// <summary>
        /// 创建优化的删除操作
        /// </summary>
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

            var operation = EditOperation.CreateLightOperation($"Delete {deletedPixels.Count} pixels", EditOperation.OperationType.Erase);
            operation.lightChanges = pixelChanges;
            operation.SetSelectionData(selectedPixelPositions, CalculateSelectionBounds(selectedPixelPositions));

            return operation;
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