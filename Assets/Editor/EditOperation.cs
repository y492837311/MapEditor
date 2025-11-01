using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditor
{
    [System.Serializable]
    public class EditOperation
    {
        public enum OperationType { Draw, Fill, Erase, ColorChange, Selection, Clear, Paste, Transform, GridSnapshot }
        
        public OperationType type;
        public string description;
        public DateTime timestamp;
        
        // 网格快照数据
        public MapDataAsset.GridPixel[,] gridSnapshot;
        
        // 轻量级变更记录
        public List<PixelChange> lightChanges;
        
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

        public SelectionData selectionData;
        public ToolData toolData;

        public EditOperation()
        {
            timestamp = DateTime.Now;
            lightChanges = new List<PixelChange>();
            selectionData = new SelectionData();
            toolData = new ToolData();
        }

        // 网格快照工厂方法
        public static EditOperation CreateGridSnapshot(string description, MapDataAsset.GridPixel[,] snapshot)
        {
            return new EditOperation
            {
                type = OperationType.GridSnapshot,
                description = description,
                gridSnapshot = snapshot,
                timestamp = DateTime.Now
            };
        }

        // 轻量级操作工厂方法
        public static EditOperation CreateLightOperation(string description, OperationType type)
        {
            return new EditOperation
            {
                type = type,
                description = description,
                timestamp = DateTime.Now
            };
        }

        public void AddPixelChange(int x, int y, Color32 previousColor, int previousBlockId, Color32 newColor, int newBlockId)
        {
            lightChanges.Add(new PixelChange
            {
                x = x,
                y = y,
                previousColor = previousColor,
                previousBlockId = previousBlockId,
                newColor = newColor,
                newBlockId = newBlockId
            });
        }

        public void SetSelectionData(List<Vector2Int> pixels, RectInt bounds)
        {
            selectionData.selectedPixels = new List<Vector2Int>(pixels);
            selectionData.bounds = bounds;
        }

        public void Dispose()
        {
            gridSnapshot = null;
            lightChanges?.Clear();
            selectionData.selectedPixels?.Clear();
        }

        public override string ToString()
        {
            return $"{timestamp:HH:mm:ss} - {type}: {description} " +
                   $"{(gridSnapshot != null ? "[Snapshot]" : "")}" +
                   $"{(lightChanges.Count > 0 ? $"[{lightChanges.Count} changes]" : "")}";
        }
    }
}