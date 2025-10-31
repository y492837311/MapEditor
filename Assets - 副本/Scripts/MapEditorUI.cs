
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MapEditor
{
    /// <summary>
    /// 地图编辑器UI管理器 - 管理编辑器界面和工具
    /// </summary>
    public class MapEditorUI : MonoBehaviour
    {
        [Header("UI References")]
        public Camera mainCamera;
        public RectTransform canvasRect;
        public RawImage mapDisplay;
        public MapDataManager mapDataManager;
        public MapDataProcessor mapDataProcessor;
        
        [Header("Tool UI")]
        public Button pencilButton;
        public Button eraserButton;
        public Button bucketButton;
        public Button pickerButton;
        public Button paletteButton;
        public Slider brushSizeSlider;
        public Text brushSizeText;
        public Image currentColorDisplay;
        
        [Header("Panels")]
        public GameObject toolsPanel;
        public GameObject layersPanel;
        public GameObject propertiesPanel;
        
        [Header("Status UI")]
        public Text coordinateText;
        public Text statusText;
        
        // 当前选中的工具
        public enum ToolType
        {
            Pencil,
            Eraser,
            Bucket,
            Picker,
            Palette
        }
        
        private ToolType currentTool = ToolType.Pencil;
        private Color32 currentColor = Color.red;
        private int currentBrushSize = 3;
        private bool isDrawing = false;
        private Vector2 lastMousePosition;
        
        // 工具状态
        private Color32 pickedColor;
        private Stack<Color32> colorHistory = new Stack<Color32>();
        
        void Start()
        {
            InitializeUI();
            SetupEventHandlers();
        }
        
        void Update()
        {
            UpdateMousePosition();
            HandleInput();
        }
        
        /// <summary>
        /// 初始化UI
        /// </summary>
        void InitializeUI()
        {
            if (mapDataManager != null)
            {
                // 设置地图显示纹理
                mapDisplay.texture = mapDataManager.GetMapTexture();
                
                // 设置画布大小以匹配地图
                canvasRect.sizeDelta = new Vector2(mapDataManager.mapWidth, mapDataManager.mapHeight);
            }
            
            // 初始化UI元素
            if (brushSizeSlider != null)
            {
                brushSizeSlider.minValue = 1;
                brushSizeSlider.maxValue = 20;
                brushSizeSlider.value = currentBrushSize;
                UpdateBrushSizeUI();
            }
            
            if (currentColorDisplay != null)
            {
                currentColorDisplay.color = currentColor;
            }
            
            // 设置默认选中工具
            SelectTool(ToolType.Pencil);
        }
        
        /// <summary>
        /// 设置事件处理器
        /// </summary>
        void SetupEventHandlers()
        {
            if (pencilButton != null)
                pencilButton.onClick.AddListener(() => SelectTool(ToolType.Pencil));
            
            if (eraserButton != null)
                eraserButton.onClick.AddListener(() => SelectTool(ToolType.Eraser));
            
            if (bucketButton != null)
                bucketButton.onClick.AddListener(() => SelectTool(ToolType.Bucket));
            
            if (pickerButton != null)
                pickerButton.onClick.AddListener(() => SelectTool(ToolType.Picker));
            
            if (paletteButton != null)
                paletteButton.onClick.AddListener(() => SelectTool(ToolType.Palette));
            
            if (brushSizeSlider != null)
                brushSizeSlider.onValueChanged.AddListener(OnBrushSizeChanged);
        }
        
        /// <summary>
        /// 选择工具
        /// </summary>
        public void SelectTool(ToolType tool)
        {
            currentTool = tool;
            
            // 更新UI状态
            UpdateToolButtons();
            
            // 更新状态文本
            switch (tool)
            {
                case ToolType.Pencil:
                    statusText.text = "工具: 铅笔 - 点击并拖动以绘制";
                    break;
                case ToolType.Eraser:
                    statusText.text = "工具: 橡皮擦 - 点击并拖动以擦除";
                    break;
                case ToolType.Bucket:
                    statusText.text = "工具: 油漆桶 - 点击区域以填充";
                    break;
                case ToolType.Picker:
                    statusText.text = "工具: 取色器 - 点击以获取颜色";
                    break;
                case ToolType.Palette:
                    statusText.text = "工具: 调色盘 - 选择新颜色";
                    break;
            }
        }
        
        /// <summary>
        /// 更新工具按钮状态
        /// </summary>
        void UpdateToolButtons()
        {
            // 重置所有按钮颜色
            if (pencilButton != null) pencilButton.GetComponent<Image>().color = Color.white;
            if (eraserButton != null) eraserButton.GetComponent<Image>().color = Color.white;
            if (bucketButton != null) bucketButton.GetComponent<Image>().color = Color.white;
            if (pickerButton != null) pickerButton.GetComponent<Image>().color = Color.white;
            if (paletteButton != null) paletteButton.GetComponent<Image>().color = Color.white;
            
            // 高亮当前选中的工具
            Button currentButton = null;
            switch (currentTool)
            {
                case ToolType.Pencil: currentButton = pencilButton; break;
                case ToolType.Eraser: currentButton = eraserButton; break;
                case ToolType.Bucket: currentButton = bucketButton; break;
                case ToolType.Picker: currentButton = pickerButton; break;
                case ToolType.Palette: currentButton = paletteButton; break;
            }
            
            if (currentButton != null)
            {
                currentButton.GetComponent<Image>().color = Color.yellow;
            }
        }
        
        /// <summary>
        /// 画笔大小改变事件
        /// </summary>
        void OnBrushSizeChanged(float value)
        {
            currentBrushSize = (int)value;
            UpdateBrushSizeUI();
        }
        
        /// <summary>
        /// 更新画笔大小UI
        /// </summary>
        void UpdateBrushSizeUI()
        {
            if (brushSizeText != null)
            {
                brushSizeText.text = $"大小: {currentBrushSize}";
            }
        }
        
        /// <summary>
        /// 更新鼠标位置信息
        /// </summary>
        void UpdateMousePosition()
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                // 如果鼠标在UI上，不更新坐标
                return;
            }
            
            Vector2 mousePosition = GetMouseWorldPosition();
            int x = (int)mousePosition.x;
            int y = (int)mousePosition.y;
            
            if (coordinateText != null)
            {
                coordinateText.text = $"坐标: ({x}, {y})";
            }
            
            // 检查鼠标是否在地图区域内
            if (mapDataManager != null)
            {
                if (x >= 0 && x < mapDataManager.mapWidth && y >= 0 && y < mapDataManager.mapHeight)
                {
                    Color32 pixelColor = mapDataManager.GetPixelColor(x, y);
                    statusText.text += $" | 颜色: ({pixelColor.r}, {pixelColor.g}, {pixelColor.b}, {pixelColor.a})";
                }
            }
        }
        
        /// <summary>
        /// 获取鼠标在世界坐标中的位置
        /// </summary>
        Vector2 GetMouseWorldPosition()
        {
            Vector3 mouseScreenPosition = Input.mousePosition;
            
            // 将屏幕坐标转换为世界坐标
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(
                new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 
                mainCamera.transform.position.z * -1));
            
            // 转换为地图坐标（相对于地图左下角）
            Vector2 mapPosition = new Vector2(
                worldPosition.x - canvasRect.position.x + (canvasRect.rect.width / 2),
                worldPosition.y - canvasRect.position.y + (canvasRect.rect.height / 2)
            );
            
            // 确保坐标在地图范围内
            if (mapDataManager != null)
            {
                mapPosition.x = Mathf.Clamp(mapPosition.x, 0, mapDataManager.mapWidth - 1);
                mapPosition.y = Mathf.Clamp(mapPosition.y, 0, mapDataManager.mapHeight - 1);
            }
            
            return mapPosition;
        }
        
        /// <summary>
        /// 处理输入
        /// </summary>
        void HandleInput()
        {
            Vector2 mousePosition = GetMouseWorldPosition();
            int x = (int)mousePosition.x;
            int y = (int)mousePosition.y;
            
            // 检查是否在地图区域内
            if (mapDataManager == null || 
                x < 0 || x >= mapDataManager.mapWidth || 
                y < 0 || y >= mapDataManager.mapHeight)
            {
                return;
            }
            
            // 检查是否点击了鼠标
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    // 如果点击在UI上，不执行地图编辑操作
                    return;
                }
                
                isDrawing = true;
                lastMousePosition = mousePosition;
                
                switch (currentTool)
                {
                    case ToolType.Pencil:
                        DrawAtPosition(x, y);
                        break;
                    case ToolType.Eraser:
                        EraseAtPosition(x, y);
                        break;
                    case ToolType.Bucket:
                        FillAtPosition(x, y);
                        break;
                    case ToolType.Picker:
                        PickColorAtPosition(x, y);
                        break;
                    case ToolType.Palette:
                        // 调色盘工具不在此处处理点击
                        break;
                }
            }
            else if (Input.GetMouseButton(0) && isDrawing)
            {
                // 检查鼠标是否移动了足够距离以避免过度绘制
                if (Vector2.Distance(mousePosition, lastMousePosition) > 0.5f)
                {
                    switch (currentTool)
                    {
                        case ToolType.Pencil:
                            DrawLine(lastMousePosition, mousePosition);
                            break;
                        case ToolType.Eraser:
                            EraseLine(lastMousePosition, mousePosition);
                            break;
                    }
                    
                    lastMousePosition = mousePosition;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDrawing = false;
            }
        }
        
        /// <summary>
        /// 在指定位置绘制
        /// </summary>
        void DrawAtPosition(int x, int y)
        {
            if (mapDataProcessor != null)
            {
                mapDataProcessor.DrawAtPosition(x, y, currentColor, currentBrushSize);
            }
            else if (mapDataManager != null)
            {
                // 回退到普通绘制方法
                for (int dy = -currentBrushSize/2; dy <= currentBrushSize/2; dy++)
                {
                    for (int dx = -currentBrushSize/2; dx <= currentBrushSize/2; dx++)
                    {
                        if (dx * dx + dy * dy <= (currentBrushSize/2) * (currentBrushSize/2)) // 圆形笔刷
                        {
                            int px = x + dx;
                            int py = y + dy;
                            
                            if (px >= 0 && px < mapDataManager.mapWidth && 
                                py >= 0 && py < mapDataManager.mapHeight)
                            {
                                mapDataManager.SetPixelColor(px, py, currentColor);
                            }
                        }
                    }
                }
                mapDataManager.UpdateTexture();
            }
        }
        
        /// <summary>
        /// 在指定位置擦除
        /// </summary>
        void EraseAtPosition(int x, int y)
        {
            if (mapDataProcessor != null)
            {
                mapDataProcessor.EraseAtPosition(x, y, currentBrushSize);
            }
            else if (mapDataManager != null)
            {
                // 回退到普通擦除方法
                for (int dy = -currentBrushSize/2; dy <= currentBrushSize/2; dy++)
                {
                    for (int dx = -currentBrushSize/2; dx <= currentBrushSize/2; dx++)
                    {
                        if (dx * dx + dy * dy <= (currentBrushSize/2) * (currentBrushSize/2)) // 圆形笔刷
                        {
                            int px = x + dx;
                            int py = y + dy;
                            
                            if (px >= 0 && px < mapDataManager.mapWidth && 
                                py >= 0 && py < mapDataManager.mapHeight)
                            {
                                mapDataManager.SetPixelColor(px, py, new Color32(0, 0, 0, 0));
                            }
                        }
                    }
                }
                mapDataManager.UpdateTexture();
            }
        }
        
        /// <summary>
        /// 填充指定位置
        /// </summary>
        void FillAtPosition(int x, int y)
        {
            if (mapDataProcessor != null)
            {
                mapDataProcessor.FillAtPosition(x, y, currentColor);
            }
            else if (mapDataManager != null)
            {
                // 简单的填充实现（实际应用中需要完整的Flood Fill算法）
                Color32 originalColor = mapDataManager.GetPixelColor(x, y);
                
                // 如果点击的颜色和当前颜色相同，则不执行操作
                if (originalColor.r == currentColor.r && 
                    originalColor.g == currentColor.g && 
                    originalColor.b == currentColor.b && 
                    originalColor.a == currentColor.a)
                    return;
                
                // 简单的区域填充 - 实际应用中需要完整的Flood Fill算法
                for (int py = 0; py < mapDataManager.mapHeight; py++)
                {
                    for (int px = 0; px < mapDataManager.mapWidth; px++)
                    {
                        Color32 pixelColor = mapDataManager.GetPixelColor(px, py);
                        if (pixelColor.r == originalColor.r && 
                            pixelColor.g == originalColor.g && 
                            pixelColor.b == originalColor.b && 
                            pixelColor.a == originalColor.a)
                        {
                            mapDataManager.SetPixelColor(px, py, currentColor);
                        }
                    }
                }
                mapDataManager.UpdateTexture();
            }
        }
        
        /// <summary>
        /// 在指定位置取色
        /// </summary>
        void PickColorAtPosition(int x, int y)
        {
            if (mapDataManager != null)
            {
                pickedColor = mapDataManager.GetPixelColor(x, y);
                
                // 如果不是透明色，则更新当前颜色
                if (pickedColor.a > 0)
                {
                    currentColor = pickedColor;
                    
                    if (currentColorDisplay != null)
                    {
                        currentColorDisplay.color = currentColor;
                    }
                    
                    // 添加到颜色历史
                    colorHistory.Push(currentColor);
                    if (colorHistory.Count > 10) // 限制历史记录数量
                    {
                        colorHistory = new Stack<Color32>(new Stack<Color32>(colorHistory).ToArray());
                    }
                    
                    statusText.text = $"取色: ({currentColor.r}, {currentColor.g}, {currentColor.b}, {currentColor.a})";
                }
            }
        }
        
        /// <summary>
        /// 绘制线条
        /// </summary>
        void DrawLine(Vector2 start, Vector2 end)
        {
            // 使用Bresenham算法绘制线条
            int x0 = (int)start.x;
            int y0 = (int)start.y;
            int x1 = (int)end.x;
            int y1 = (int)end.y;
            
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = (x0 < x1) ? 1 : -1;
            int sy = (y0 < y1) ? 1 : -1;
            int err = dx - dy;
            
            int x = x0;
            int y = y0;
            
            while (true)
            {
                // 在每个点上绘制
                for (int dy_brush = -currentBrushSize/2; dy_brush <= currentBrushSize/2; dy_brush++)
                {
                    for (int dx_brush = -currentBrushSize/2; dx_brush <= currentBrushSize/2; dx_brush++)
                    {
                        if (dx_brush * dx_brush + dy_brush * dy_brush <= (currentBrushSize/2) * (currentBrushSize/2)) // 圆形笔刷
                        {
                            int px = x + dx_brush;
                            int py = y + dy_brush;
                            
                            if (px >= 0 && px < mapDataManager.mapWidth && 
                                py >= 0 && py < mapDataManager.mapHeight)
                            {
                                if (mapDataProcessor != null)
                                {
                                    mapDataProcessor.DrawAtPosition(px, py, currentColor, 1);
                                }
                                else
                                {
                                    mapDataManager.SetPixelColor(px, py, currentColor);
                                }
                            }
                        }
                    }
                }
                
                if (x == x1 && y == y1) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
            
            if (mapDataProcessor == null)
            {
                mapDataManager.UpdateTexture();
            }
        }
        
        /// <summary>
        /// 擦除线条
        /// </summary>
        void EraseLine(Vector2 start, Vector2 end)
        {
            // 使用Bresenham算法擦除线条
            int x0 = (int)start.x;
            int y0 = (int)start.y;
            int x1 = (int)end.x;
            int y1 = (int)end.y;
            
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = (x0 < x1) ? 1 : -1;
            int sy = (y0 < y1) ? 1 : -1;
            int err = dx - dy;
            
            int x = x0;
            int y = y0;
            
            while (true)
            {
                // 在每个点上擦除
                for (int dy_brush = -currentBrushSize/2; dy_brush <= currentBrushSize/2; dy_brush++)
                {
                    for (int dx_brush = -currentBrushSize/2; dx_brush <= currentBrushSize/2; dx_brush++)
                    {
                        if (dx_brush * dx_brush + dy_brush * dy_brush <= (currentBrushSize/2) * (currentBrushSize/2)) // 圆形笔刷
                        {
                            int px = x + dx_brush;
                            int py = y + dy_brush;
                            
                            if (px >= 0 && px < mapDataManager.mapWidth && 
                                py >= 0 && py < mapDataManager.mapHeight)
                            {
                                if (mapDataProcessor != null)
                                {
                                    mapDataProcessor.EraseAtPosition(px, py, 1);
                                }
                                else
                                {
                                    mapDataManager.SetPixelColor(px, py, new Color32(0, 0, 0, 0));
                                }
                            }
                        }
                    }
                }
                
                if (x == x1 && y == y1) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
            
            if (mapDataProcessor == null)
            {
                mapDataManager.UpdateTexture();
            }
        }
        
        /// <summary>
        /// 设置当前颜色
        /// </summary>
        public void SetCurrentColor(Color32 color)
        {
            currentColor = color;
            
            if (currentColorDisplay != null)
            {
                currentColorDisplay.color = currentColor;
            }
        }
        
        /// <summary>
        /// 获取当前颜色
        /// </summary>
        public Color32 GetCurrentColor()
        {
            return currentColor;
        }
        
        /// <summary>
        /// 获取当前工具
        /// </summary>
        public ToolType GetCurrentTool()
        {
            return currentTool;
        }
        
        /// <summary>
        /// 获取颜色历史
        /// </summary>
        public Color32[] GetColorHistory()
        {
            return colorHistory.ToArray();
        }
    }
}
