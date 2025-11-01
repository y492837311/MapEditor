using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MapEditor
{
    public class SelectionTool : BaseEditorTool
    {
        private Vector2Int selectionStart;
        private Vector2Int selectionEnd;
        private bool isSelecting = false;

        public SelectionTool(MapEditorWindow window) : base(window, ToolType.Selection)
        {
        }

        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) return;

            isSelecting = true;
            selectionStart = position;
            selectionEnd = position;
            
            // 开始新选择，清除旧选择
            editorWindow.ClearSelection();
        }

        public override void OnMouseDrag(Vector2Int position)
        {
            if (!isSelecting) return;

            selectionEnd = position;
            UpdateSelectionPreview();
        }

        public override void OnMouseUp(Vector2Int position)
        {
            if (!isSelecting) return;

            isSelecting = false;
            selectionEnd = position;
            FinalizeSelection();
        }

        public override void OnMouseMove(Vector2Int position)
        {
            // 可选的鼠标移动处理
        }

        protected override EditOperation.OperationType GetOperationType()
        {
            return EditOperation.OperationType.Selection;
        }

        public override void DrawPreview(Rect drawArea)
        {
            if (!isSelecting) return;

            // 绘制选择框预览
            DrawSelectionPreview(drawArea);
        }

        private void UpdateSelectionPreview()
        {
            // 实时更新选择预览
            editorWindow.UpdateSelectionPreview(GetSelectionBounds());
        }

        private void FinalizeSelection()
        {
            var bounds = GetSelectionBounds();
            var selectedPixels = CalculateSelectedPixels(bounds);
    
            editorWindow.SetSelection(selectedPixels, bounds);
    
            // 记录选择操作
            var operation = EditOperation.CreateLightOperation($"Select {selectedPixels.Count} pixels", EditOperation.OperationType.Selection);
            operation.SetSelectionData(selectedPixels, bounds);
            editorWindow.GetUndoRedoManager().RecordOperation(operation);
        }

        private RectInt GetSelectionBounds()
        {
            int minX = Mathf.Min(selectionStart.x, selectionEnd.x);
            int maxX = Mathf.Max(selectionStart.x, selectionEnd.x);
            int minY = Mathf.Min(selectionStart.y, selectionEnd.y);
            int maxY = Mathf.Max(selectionStart.y, selectionEnd.y);
            
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private List<Vector2Int> CalculateSelectedPixels(RectInt bounds)
        {
            var pixels = new List<Vector2Int>();
            var mapData = editorWindow.GetCurrentMapData();
            
            if (mapData == null) return pixels;

            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x <= bounds.xMax; x++)
                {
                    if (IsPositionValid(new Vector2Int(x, y)))
                    {
                        pixels.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            return pixels;
        }

        private void DrawSelectionPreview(Rect drawArea)
        {
            Handles.BeginGUI();
            
            var bounds = GetSelectionBounds();
            Vector2 startScreen = editorWindow.MapToScreenPosition(new Vector2(bounds.xMin, bounds.yMin), drawArea);
            Vector2 endScreen = editorWindow.MapToScreenPosition(new Vector2(bounds.xMax, bounds.yMax), drawArea);
            
            Rect selectionRect = new Rect(startScreen.x, startScreen.y, endScreen.x - startScreen.x, endScreen.y - startScreen.y);
            
            // 绘制半透明选择区域
            Handles.DrawSolidRectangleWithOutline(selectionRect, new Color(0, 0.5f, 1f, 0.3f), Color.clear);
            
            // 绘制选择边框
            Handles.color = new Color(0, 0.5f, 1f, 0.8f);
            Handles.DrawWireCube(selectionRect.center, new Vector3(selectionRect.width, selectionRect.height, 0));
            
            Handles.EndGUI();
        }
    }
}