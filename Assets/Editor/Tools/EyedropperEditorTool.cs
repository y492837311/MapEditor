using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class EyedropperEditorTool : BaseEditorTool
    {
        public EyedropperEditorTool(MapEditorWindow window) : base(window, ToolType.Eyedropper)
        {
        }

        public override void OnMouseDown(Vector2Int position)
        {
            if (!IsPositionValid(position)) return;

            PickColorAtPosition(position);
        }

        public override void OnMouseDrag(Vector2Int position)
        {
            // 取色器通常不需要拖动操作
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
            return EditOperation.OperationType.ColorChange;
        }

        public override void DrawPreview(Rect canvasArea)
        {
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null) return;

            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (canvasArea.Contains(mousePos))
            {
                Vector2 mapPos = ScreenToMapPosition(mousePos, canvasArea);
                Vector2Int intPos = new Vector2Int(Mathf.FloorToInt(mapPos.x), Mathf.FloorToInt(mapPos.y));
                
                if (IsPositionValid(intPos))
                {
                    DrawEyedropperPreview(canvasArea, intPos);
                }
            }
        }

        private void PickColorAtPosition(Vector2Int position)
        {
            var mapData = editorWindow.GetCurrentMapData();
            var pixel = mapData.GetGridPixel(position.x, position.y);
            
            if (pixel.color.a > 0.1f) // 只选取非透明颜色
            {
                editorWindow.SetCurrentColor(pixel.color);
                
                // 可以在这里添加选择对应颜色块的逻辑
                Debug.Log($"Picked color: {pixel.color} at position ({position.x}, {position.y})");
            }
        }

        private void DrawEyedropperPreview(Rect canvasArea, Vector2Int position)
        {
            Handles.BeginGUI();
            
            Vector2 screenPos = MapToScreenPosition(new Vector2(position.x + 0.5f, position.y + 0.5f), canvasArea);
            
            // 绘制十字准星
            float size = 10f;
            Handles.color = Color.white;
            Handles.DrawLine(new Vector3(screenPos.x - size, screenPos.y), new Vector3(screenPos.x + size, screenPos.y));
            Handles.DrawLine(new Vector3(screenPos.x, screenPos.y - size), new Vector3(screenPos.x, screenPos.y + size));
            
            // 绘制取色区域预览
            var mapData = editorWindow.GetCurrentMapData();
            var pixel = mapData.GetGridPixel(position.x, position.y);
            
            Rect colorRect = new Rect(screenPos.x + 15, screenPos.y - 15, 30, 30);
            EditorGUI.DrawRect(colorRect, pixel.color);
            Handles.DrawSolidRectangleWithOutline(colorRect, pixel.color, Color.white);
            
            Handles.EndGUI();
        }
    }
}