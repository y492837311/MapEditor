using UnityEngine;

namespace MapEditor
{
    public partial class MapEditorWindow
    {
        // 在 MapEditorWindow 类的成员变量区域添加：
        private Vector2 panOffset = Vector2.zero;
        private bool isPanning = false;
        private Vector2 panStartMousePosition;
        private Vector2 panStartOffset;
        private Vector2 lastMousePosition;
        
        // 在 MapEditorWindow 类中添加这些方法：

        /// <summary>
        /// 开始拖动地图
        /// </summary>
        public void StartPanning(Vector2 mousePosition)
        {
            isPanning = true;
            panStartMousePosition = mousePosition;
            panStartOffset = panOffset;
        }

        /// <summary>
        /// 更新拖动位置
        /// </summary>
        public void UpdatePanning(Vector2 mousePosition)
        {
            if (!isPanning) return;
    
            Vector2 delta = mousePosition - panStartMousePosition;
            panOffset = panStartOffset + delta;
    
            // 限制拖动范围
            ClampPanOffset();
    
            Repaint();
        }

        /// <summary>
        /// 停止拖动地图
        /// </summary>
        public void StopPanning()
        {
            isPanning = false;
        }

        /// <summary>
        /// 限制拖动偏移范围
        /// </summary>
        private void ClampPanOffset()
        {
            if (currentMapData == null) return;
    
            // 计算画布区域
            Rect canvasArea = CalculateCanvasArea();
            Rect drawArea = CalculateDrawArea(canvasArea);
    
            // 如果绘制区域小于画布区域，不允许平移
            if (drawArea.width <= canvasArea.width && drawArea.height <= canvasArea.height)
            {
                panOffset = Vector2.zero;
                return;
            }
    
            // 计算最大允许的平移偏移
            float maxOffsetX = Mathf.Max(0, (drawArea.width - canvasArea.width) * 0.5f);
            float maxOffsetY = Mathf.Max(0, (drawArea.height - canvasArea.height) * 0.5f);
    
            panOffset.x = Mathf.Clamp(panOffset.x, -maxOffsetX, maxOffsetX);
            panOffset.y = Mathf.Clamp(panOffset.y, -maxOffsetY, maxOffsetY);
    
            // 调试信息
            if (showDebugInfo)
            {
                Debug.Log($"ClampPanOffset: drawArea({drawArea.width:F0}x{drawArea.height:F0}), " +
                          $"canvas({canvasArea.width:F0}x{canvasArea.height:F0}), " +
                          $"maxOffset({maxOffsetX:F0}, {maxOffsetY:F0}), " +
                          $"panOffset({panOffset.x:F0}, {panOffset.y:F0})");
            }
        }
        
        /// <summary>
        /// 重置视图位置
        /// </summary>
        public void ResetView()
        {
            zoomLevel = 1.0f;
            panOffset = Vector2.zero;
            Repaint();
        }

        /// <summary>
        /// 获取当前平移偏移
        /// </summary>
        public Vector2 GetPanOffset()
        {
            return panOffset;
        }
    }
}