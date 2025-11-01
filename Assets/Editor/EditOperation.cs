using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace MapEditor
{
    [System.Serializable]
    public class EditOperation
    {
        public enum OperationType { Draw, Fill, Erase, ColorChange, Selection, Clear, Paste, Transform }
        
        public OperationType type;
        public string description;
        public DateTime timestamp;
        
        // 通用数据存储
        public List<PixelChange> pixelChanges;
        public SelectionData selectionData;
        public ToolData toolData;
        
        // 序列化支持
        [System.Serializable]
        public struct PixelChange
        {
            public int x;
            public int y;
            public Color32 previousColor;
            public int previousBlockId;
            public Color32 newColor;
            public int newBlockId;
        }
        
        [System.Serializable]
        public struct SelectionData
        {
            public List<Vector2Int> selectedPixels;
            public RectInt bounds;
        }
        
        [System.Serializable]
        public struct ToolData
        {
            public Vector2Int position;
            public Color32 color;
            public int brushSize;
            public float tolerance;
            public ToolType toolType;
        }

        public EditOperation()
        {
            pixelChanges = new List<PixelChange>();
            selectionData = new SelectionData();
            toolData = new ToolData();
            timestamp = DateTime.Now;
        }

        public void Dispose()
        {
            pixelChanges?.Clear();
            selectionData.selectedPixels?.Clear();
        }

        public override string ToString()
        {
            return $"{timestamp:HH:mm:ss} - {type}: {description} ({pixelChanges.Count} pixels)";
        }

        // 通用工厂方法
        public static EditOperation Create(string description, OperationType type)
        {
            return new EditOperation
            {
                description = description,
                type = type
            };
        }

        // 添加像素变更
        public void AddPixelChange(int x, int y, Color32 previousColor, int previousBlockId, Color32 newColor, int newBlockId)
        {
            pixelChanges.Add(new PixelChange
            {
                x = x,
                y = y,
                previousColor = previousColor,
                previousBlockId = previousBlockId,
                newColor = newColor,
                newBlockId = newBlockId
            });
        }

        // 设置选择数据
        public void SetSelectionData(List<Vector2Int> pixels, RectInt bounds)
        {
            selectionData.selectedPixels = new List<Vector2Int>(pixels);
            selectionData.bounds = bounds;
        }

        // 设置工具数据
        public void SetToolData(Vector2Int position, Color32 color, int brushSize, float tolerance, ToolType toolType)
        {
            toolData.position = position;
            toolData.color = color;
            toolData.brushSize = brushSize;
            toolData.tolerance = tolerance;
            toolData.toolType = toolType;
        }
        
        public static EditOperation CreateSelectionOperation(List<Vector2Int> selectedPixels, RectInt bounds)
        {
            var operation = new EditOperation
            {
                type = OperationType.Selection,
                description = $"Select {selectedPixels.Count} pixels",
                timestamp = DateTime.Now
            };
    
            operation.SetSelectionData(selectedPixels, bounds);
            return operation;
        }
        
        public static EditOperation CreateDeleteOperation(List<Vector2Int> pixelsToDelete, 
            List<PixelChange> pixelChanges)
        {
            var operation = new EditOperation
            {
                type = OperationType.Erase,
                description = $"Delete {pixelsToDelete.Count} pixels",
                timestamp = DateTime.Now
            };
    
            operation.pixelChanges = new List<PixelChange>(pixelChanges);
            operation.SetSelectionData(pixelsToDelete, CalculateBounds(pixelsToDelete));
            return operation;
        }
        
        // 辅助方法：计算像素列表的边界
        private static RectInt CalculateBounds(List<Vector2Int> pixels)
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
        
        // 在 EditOperation 结构中添加其他常用的工厂方法：

// 绘制操作的工厂方法
        public static EditOperation CreateDrawOperation(string description, List<PixelChange> pixelChanges)
        {
            var operation = new EditOperation
            {
                type = OperationType.Draw,
                description = description,
                timestamp = DateTime.Now
            };
    
            operation.pixelChanges = new List<PixelChange>(pixelChanges);
            return operation;
        }

// 填充操作的工厂方法
        public static EditOperation CreateFillOperation(Vector2Int position, int filledPixels)
        {
            return new EditOperation
            {
                type = OperationType.Fill,
                description = $"Fill at ({position.x}, {position.y}) - {filledPixels} pixels",
                timestamp = DateTime.Now
            };
        }

// 清除操作的工厂方法
        public static EditOperation CreateClearOperation(int clearedPixels)
        {
            return new EditOperation
            {
                type = OperationType.Clear,
                description = $"Clear {clearedPixels} pixels",
                timestamp = DateTime.Now
            };
        }
    }
}