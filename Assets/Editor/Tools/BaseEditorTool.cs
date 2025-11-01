using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MapEditor
{
    public abstract class BaseEditorTool
    {
        protected MapEditorWindow editorWindow;
        protected ToolType toolType;
        protected EditOperation currentOperation;
        protected bool isRecording = false;
        
        // 批量操作支持
        protected List<MapDataAsset.PixelOperation> pendingOperations = new List<MapDataAsset.PixelOperation>();
        protected const int BATCH_FLUSH_THRESHOLD = 50;
        protected float lastFlushTime = 0f;
        protected const float FLUSH_INTERVAL = 0.1f;

        // 绘制区域裁剪优化
        protected Rect currentDrawArea;
        protected bool isInDrawArea = false;

        public BaseEditorTool(MapEditorWindow window, ToolType type)
        {
            editorWindow = window;
            toolType = type;
        }

        public abstract void OnMouseDown(Vector2Int position);
        public abstract void OnMouseDrag(Vector2Int position);
        public abstract void OnMouseUp(Vector2Int position);
        public abstract void OnMouseMove(Vector2Int position);
        public abstract void DrawPreview(Rect canvasArea);
        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }

        public ToolType GetToolType() => toolType;

        // 开始记录操作（使用网格快照）
        protected void StartRecordingWithSnapshot(string description)
        {
            if (isRecording)
            {
                Debug.LogWarning("Already recording, finishing previous operation");
                FinishRecording();
            }

            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            // 创建操作前的网格快照
            var snapshot = mapData.CreateGridSnapshot();
            currentOperation = EditOperation.CreateGridSnapshot(description, snapshot);
            isRecording = true;
            
            Debug.Log($"Started recording with snapshot: {description}");
        }

        // 开始轻量级记录
        protected void StartLightRecording(string description, EditOperation.OperationType operationType)
        {
            if (isRecording)
            {
                FinishRecording();
            }

            currentOperation = EditOperation.CreateLightOperation(description, operationType);
            isRecording = true;
            pendingOperations.Clear();
            
            Debug.Log($"Started light recording: {description}");
        }

        protected void FinishRecording()
        {
            if (isRecording && currentOperation != null)
            {
                // 刷新所有挂起的操作
                FlushPendingOperations();
                
                // 记录到撤销管理器
                if (currentOperation.gridSnapshot != null || currentOperation.lightChanges.Count > 0)
                {
                    editorWindow.GetUndoRedoManager().RecordOperation(currentOperation);
                    Debug.Log($"Finished recording: {currentOperation.description}");
                }
                else
                {
                    Debug.LogWarning($"Finished recording with no changes: {currentOperation.description}");
                }
            }
            
            isRecording = false;
            currentOperation = null;
            pendingOperations.Clear();
        }

        // 设置单个网格像素
        protected void SetGridPixel(int x, int y, Color32 color, int blockId)
        {
            if (!IsPositionValid(x, y)) return;

            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;
            
            // 获取原始状态
            var originalPixel = mapData.GetGridPixel(x, y);
            
            // 检查是否需要更改
            if (originalPixel.color.r == color.r && 
                originalPixel.color.g == color.g && 
                originalPixel.color.b == color.b && 
                originalPixel.color.a == color.a &&
                originalPixel.blockId == blockId)
            {
                return; // 颜色和blockId都相同，跳过
            }
            
            // 添加到批量操作
            pendingOperations.Add(new MapDataAsset.PixelOperation(x, y, color, blockId));
            
            // 记录变更（用于轻量级操作）
            if (isRecording && currentOperation != null && currentOperation.gridSnapshot == null)
            {
                currentOperation.AddPixelChange(x, y, originalPixel.color, originalPixel.blockId, color, blockId);
            }
            
            // 达到阈值时刷新
            if (pendingOperations.Count >= BATCH_FLUSH_THRESHOLD)
            {
                FlushPendingOperations();
            }
        }

        // 刷新挂起的操作
        protected void FlushPendingOperations()
        {
            if (pendingOperations.Count == 0) return;
            
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData != null)
            {
                mapData.SetGridPixelsBatch(pendingOperations);
                pendingOperations.Clear();
                editorWindow.Repaint();
            }
        }

        // 位置验证
        protected bool IsPositionValid(Vector2Int position)
        {
            return editorWindow.IsMapPositionValid(position);
        }

        protected bool IsPositionValid(int x, int y)
        {
            return editorWindow.IsMapPositionValid(x, y);
        }

        // 坐标转换
        protected Vector2 MapToScreenPosition(Vector2 mapPos, Rect canvasArea)
        {
            return editorWindow.MapToScreenPosition(mapPos, canvasArea);
        }

        protected Vector2Int ScreenToMapPosition(Vector2 screenPos, Rect canvasArea)
        {
            return editorWindow.ScreenToMapPositionInt(screenPos, canvasArea);
        }

        // 绘制笔刷预览 - 网格对齐版本
        protected void DrawBrushPreview(Rect canvasArea, Vector2Int position, int brushSize, Color color)
        {
            if (brushSize <= 0) return;

            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;
            
            // 计算每个网格在屏幕上的精确大小
            float gridWidth = canvasArea.width / mapData.width;
            float gridHeight = canvasArea.height / mapData.height;
            
            DrawBrushPreviewAligned(canvasArea, position, brushSize, color, gridWidth, gridHeight);
        }
        
        
        /// <summary>
        /// 获取平移偏移
        /// </summary>
        protected Vector2 GetPanOffset()
        {
            return editorWindow != null ? editorWindow.GetPanOffset() : Vector2.zero;
        }
        
        /// <summary>
        /// 获取缩放级别
        /// </summary>
        protected float GetZoomLevel()
        {
            return editorWindow != null ? editorWindow.GetZoomLevel() : 1.0f;
        }
        
        /// <summary>
        /// 获取绘制区域（考虑缩放和平移）
        /// </summary>
        public Rect GetAdjustedDrawArea(Rect baseDrawArea)
        {
            Vector2 panOffset = GetPanOffset();
            float zoomLevel = GetZoomLevel();
    
            return new Rect(
                baseDrawArea.x + panOffset.x,
                baseDrawArea.y + panOffset.y,
                baseDrawArea.width * zoomLevel,
                baseDrawArea.height * zoomLevel
            );
        }
        
        /// <summary>
        /// 网格对齐的笔刷预览 - 修复位置计算
        /// </summary>
        protected void DrawBrushPreviewAligned(Rect drawArea, Vector2Int position, int brushSize, Color color, float gridWidth, float gridHeight)
        {
            if (brushSize <= 0) return;

            Handles.BeginGUI();
    
            // 获取平移偏移
            Vector2 panOffset = GetPanOffset();
    
            // 关键修复：使用正确的坐标计算
            // 网格位置 = 绘制区域起点 + 网格坐标 × 网格大小 + 平移偏移
            Vector2 screenPos = new Vector2(
                drawArea.x + (position.x * gridWidth) + panOffset.x,
                drawArea.y + (position.y * gridHeight) + panOffset.y  // Y轴方向与屏幕一致
            );
    
            // 笔刷大小基于网格单位
            float screenBrushWidth = brushSize * gridWidth;
            float screenBrushHeight = brushSize * gridHeight;
    
            // 绘制矩形预览
            Rect brushRect = new Rect(
                screenPos.x,
                screenPos.y,
                screenBrushWidth,
                screenBrushHeight
            );
    
            // 检查矩形是否在可见区域内
            if (brushRect.Overlaps(new Rect(drawArea.x, drawArea.y, drawArea.width, drawArea.height)))
            {
                // 绘制半透明填充
                EditorGUI.DrawRect(brushRect, new Color(color.r, color.g, color.b, 0.3f));
        
                // 绘制边框
                Handles.color = color;
                Handles.DrawSolidRectangleWithOutline(brushRect, Color.clear, color);
        
                // 调试：显示预览位置
                if (editorWindow.showDebugInfo)
                {
                    Handles.color = Color.green;
                    Handles.DrawWireDisc(screenPos, Vector3.forward, 3f);
                }
            }
    
            Handles.EndGUI();
        }

        // 网格对齐的预览绘制（供子类重写）
        public virtual void DrawPreviewAligned(Rect canvasArea, float gridWidth, float gridHeight)
        {
            // 默认实现调用原有方法
            DrawPreview(canvasArea);
        }

        // 设置绘制区域
        public void SetDrawArea(Rect drawArea)
        {
            currentDrawArea = drawArea;
            isInDrawArea = true;
        }

        // 清除绘制区域限制
        public void ClearDrawArea()
        {
            isInDrawArea = false;
        }

        /// <summary>
        /// 检查位置是否在绘制区域内（考虑平移）
        /// </summary>
        protected bool IsPositionInDrawArea(int x, int y)
        {
            if (!isInDrawArea) return true;
    
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return false;
    
            Vector2 panOffset = GetPanOffset();
            Vector2 screenPos = MapToScreenPosition(new Vector2(x, y), currentDrawArea);
    
            // 调整检查区域考虑平移
            Rect adjustedDrawArea = new Rect(
                currentDrawArea.x + panOffset.x,
                currentDrawArea.y + panOffset.y,
                currentDrawArea.width,
                currentDrawArea.height
            );
    
            return adjustedDrawArea.Contains(screenPos);
        }

        protected abstract EditOperation.OperationType GetOperationType();
    }
}