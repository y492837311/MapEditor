
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MapEditor
{
    /// <summary>
    /// 高级功能管理器 - 实现智能标注系统和像素级错误检测
    /// </summary>
    public class AdvancedFeatures : MonoBehaviour
    {
        [Header("References")]
        public MapDataManager mapDataManager;
        public MapDataProcessor mapDataProcessor;
        public MapEditorUI mapEditorUI;
        
        [Header("UI Elements")]
        public GameObject errorPanel;
        public Text errorListText;
        public Toggle showCoordinateToggle;
        public Toggle showColorNameToggle;
        public Button detectErrorsButton;
        public Button clearErrorsButton;
        
        [Header("Performance Settings")]
        public bool autoDetectErrors = true;
        public float errorDetectionInterval = 5.0f; // 自动检测间隔（秒）
        
        // 错误检测结果
        private List<PixelError> detectedErrors = new List<PixelError>();
        private float lastErrorDetectionTime = 0f;
        
        // 坐标显示
        private GameObject coordinateDisplay;
        private Text coordinateText;
        
        // 颜色名称映射（根据策划需求配置）
        private Dictionary<Color32, string> colorNameMap = new Dictionary<Color32, string>();
        
        // 颜色冲突检测
        private List<Color32> conflictColors = new List<Color32>();
        
        void Start()
        {
            if (mapDataManager == null)
            {
                mapDataManager = GetComponent<MapDataManager>();
            }
            
            if (mapDataProcessor == null)
            {
                mapDataProcessor = GetComponent<MapDataProcessor>();
            }
            
            if (mapEditorUI == null)
            {
                mapEditorUI = GetComponent<MapEditorUI>();
            }
            
            SetupEventHandlers();
            InitializeColorNameMap();
            
            // 注册地图数据变化事件
            if (mapDataManager != null)
            {
                mapDataManager.OnMapDataChanged += OnMapDataChanged;
            }
        }
        
        void Update()
        {
            // 自动错误检测
            if (autoDetectErrors && 
                Time.time - lastErrorDetectionTime > errorDetectionInterval)
            {
                DetectAllErrors();
                lastErrorDetectionTime = Time.time;
            }
        }
        
        void OnDestroy()
        {
            if (mapDataManager != null)
            {
                mapDataManager.OnMapDataChanged -= OnMapDataChanged;
            }
        }
        
        /// <summary>
        /// 设置事件处理器
        /// </summary>
        void SetupEventHandlers()
        {
            if (detectErrorsButton != null)
                detectErrorsButton.onClick.AddListener(DetectAllErrors);
            
            if (clearErrorsButton != null)
                clearErrorsButton.onClick.AddListener(ClearErrors);
            
            if (showCoordinateToggle != null)
                showCoordinateToggle.onValueChanged.AddListener(OnCoordinateToggleChanged);
            
            if (showColorNameToggle != null)
                showColorNameToggle.onValueChanged.AddListener(OnColorNameToggleChanged);
        }
        
        /// <summary>
        /// 初始化颜色名称映射
        /// </summary>
        void InitializeColorNameMap()
        {
            // 根据策划需求配置颜色名称映射
            // 这里是一些示例配置，实际项目中可能需要从配置文件加载
            colorNameMap.Add(new Color32(255, 0, 0, 255), "平原");
            colorNameMap.Add(new Color32(0, 255, 0, 255), "森林");
            colorNameMap.Add(new Color32(0, 0, 255, 255), "水域");
            colorNameMap.Add(new Color32(255, 255, 0, 255), "沙漠");
            colorNameMap.Add(new Color32(139, 69, 19, 255), "山地");
            colorNameMap.Add(new Color32(255, 165, 0, 255), "关隘");
            colorNameMap.Add(new Color32(128, 128, 128, 255), "城市");
        }
        
        /// <summary>
        /// 地图数据变化事件处理
        /// </summary>
        void OnMapDataChanged()
        {
            if (autoDetectErrors)
            {
                // 延迟检测，避免频繁调用
                Invoke(nameof(DetectAllErrors), 0.5f);
            }
        }
        
        /// <summary>
        /// 坐标显示开关事件
        /// </summary>
        void OnCoordinateToggleChanged(bool isOn)
        {
            if (coordinateDisplay != null)
            {
                coordinateDisplay.SetActive(isOn);
            }
        }
        
        /// <summary>
        /// 颜色名称显示开关事件
        /// </summary>
        void OnColorNameToggleChanged(bool isOn)
        {
            // 实现颜色名称显示逻辑
            // 这里可能需要更新地图显示以显示颜色名称
        }
        
        /// <summary>
        /// 检测所有错误
        /// </summary>
        public void DetectAllErrors()
        {
            if (mapDataProcessor != null)
            {
                // 使用高性能Job检测像素级错误
                var (isolatedPixels, threeColorIntersections, singlePixelLines) = 
                    mapDataProcessor.DetectPixelErrors();
                
                // 清除之前的错误
                detectedErrors.Clear();
                
                // 添加孤立像素错误
                foreach (var pos in isolatedPixels)
                {
                    detectedErrors.Add(new PixelError
                    {
                        position = new Vector2Int(pos.x, pos.y),
                        errorType = PixelErrorType.IsolatedPixel,
                        description = $"孤立像素点 ({pos.x}, {pos.y})"
                    });
                }
                
                // 添加三色交点错误
                foreach (var pos in threeColorIntersections)
                {
                    detectedErrors.Add(new PixelError
                    {
                        position = new Vector2Int(pos.x, pos.y),
                        errorType = PixelErrorType.ThreeColorIntersection,
                        description = $"三色交点 ({pos.x}, {pos.y})"
                    });
                }
                
                // 添加单像素线错误
                foreach (var pos in singlePixelLines)
                {
                    detectedErrors.Add(new PixelError
                    {
                        position = new Vector2Int(pos.x, pos.y),
                        errorType = PixelErrorType.SinglePixelLine,
                        description = $"单像素线 ({pos.x}, {pos.y})"
                    });
                }
                
                // 检测颜色冲突
                conflictColors = mapDataProcessor.DetectColorConflicts().ToList();
                
                // 更新UI显示
                UpdateErrorUI();
            }
            else if (mapDataManager != null)
            {
                // 回退到普通检测方法
                DetectErrorsFallback();
            }
        }
        
        /// <summary>
        /// 回退错误检测方法
        /// </summary>
        void DetectErrorsFallback()
        {
            Color32[] mapData = mapDataManager.GetMapData();
            int width = mapDataManager.mapWidth;
            int height = mapDataManager.mapHeight;
            
            detectedErrors.Clear();
            
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int currentIndex = y * width + x;
                    Color32 currentColor = mapData[currentIndex];
                    
                    // 跳过透明像素
                    if (currentColor.a == 0) continue;
                    
                    // 检查孤立像素
                    CheckIsolatedPixelFallback(x, y, currentColor, width, height, mapData);
                    
                    // 检查三色交点
                    CheckThreeColorIntersectionFallback(x, y, currentColor, width, height, mapData);
                    
                    // 检查单像素线
                    CheckSinglePixelLineFallback(x, y, currentColor, width, height, mapData);
                }
            }
            
            // 检测颜色冲突
            DetectColorConflictsFallback();
            
            // 更新UI显示
            UpdateErrorUI();
        }
        
        /// <summary>
        /// 检查孤立像素（回退方法）
        /// </summary>
        void CheckIsolatedPixelFallback(int x, int y, Color32 currentColor, int width, int height, Color32[] mapData)
        {
            int currentIndex = y * width + x;
            
            // 检查周围8个像素是否都不同
            bool allDifferent = true;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue; // 跳过中心像素
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        int neighborIndex = ny * width + nx;
                        Color32 neighborColor = mapData[neighborIndex];
                        
                        // 如果相邻像素颜色相同，则不是孤立像素
                        if (currentColor.r == neighborColor.r && 
                            currentColor.g == neighborColor.g && 
                            currentColor.b == neighborColor.b && 
                            currentColor.a == neighborColor.a)
                        {
                            allDifferent = false;
                            break;
                        }
                    }
                    if (!allDifferent) break;
                }
                if (!allDifferent) break;
            }
            
            if (allDifferent)
            {
                detectedErrors.Add(new PixelError
                {
                    position = new Vector2Int(x, y),
                    errorType = PixelErrorType.IsolatedPixel,
                    description = $"孤立像素点 ({x}, {y})"
                });
            }
        }
        
        /// <summary>
        /// 检查三色交点（回退方法）
        /// </summary>
        void CheckThreeColorIntersectionFallback(int x, int y, Color32 currentColor, int width, int height, Color32[] mapData)
        {
            // 检查3x3区域内的颜色数量
            var colors = new HashSet<string>();
            
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        int index = ny * width + nx;
                        Color32 color = mapData[index];
                        
                        // 将颜色转换为字符串作为集合的键
                        string colorKey = $"{color.r},{color.g},{color.b},{color.a}";
                        colors.Add(colorKey);
                    }
                }
            }
            
            // 如果3x3区域内有3种或更多颜色，则标记为三色交点
            if (colors.Count >= 3)
            {
                detectedErrors.Add(new PixelError
                {
                    position = new Vector2Int(x, y),
                    errorType = PixelErrorType.ThreeColorIntersection,
                    description = $"三色交点 ({x}, {y})"
                });
            }
        }
        
        /// <summary>
        /// 检查单像素线（回退方法）
        /// </summary>
        void CheckSinglePixelLineFallback(int x, int y, Color32 currentColor, int width, int height, Color32[] mapData)
        {
            int currentIndex = y * width + x;
            
            // 检查是否是单像素宽的连接线
            // 检查水平和垂直方向的连接情况
            int horizontalConnections = 0;
            int verticalConnections = 0;
            
            // 检查水平方向
            if (x > 0 && x < width - 1)
            {
                Color32 leftColor = mapData[y * width + (x - 1)];
                Color32 rightColor = mapData[y * width + (x + 1)];
                
                if (HasSameColor(currentColor, leftColor)) horizontalConnections++;
                if (HasSameColor(currentColor, rightColor)) horizontalConnections++;
            }
            
            // 检查垂直方向
            if (y > 0 && y < height - 1)
            {
                Color32 topColor = mapData[(y - 1) * width + x];
                Color32 bottomColor = mapData[(y + 1) * width + x];
                
                if (HasSameColor(currentColor, topColor)) verticalConnections++;
                if (HasSameColor(currentColor, bottomColor)) verticalConnections++;
            }
            
            // 如果只在一个方向上有连接（或没有连接），则可能是单像素线
            if ((horizontalConnections == 1 && verticalConnections == 0) || 
                (horizontalConnections == 0 && verticalConnections == 1))
            {
                detectedErrors.Add(new PixelError
                {
                    position = new Vector2Int(x, y),
                    errorType = PixelErrorType.SinglePixelLine,
                    description = $"单像素线 ({x}, {y})"
                });
            }
        }
        
        /// <summary>
        /// 检测颜色冲突（回退方法）
        /// </summary>
        void DetectColorConflictsFallback()
        {
            Color32[] mapData = mapDataManager.GetMapData();
            conflictColors.Clear();
            
            var colorSet = new HashSet<Color32>();
            var conflictSet = new HashSet<Color32>();
            
            for (int i = 0; i < mapData.Length; i++)
            {
                Color32 color = mapData[i];
                
                // 跳过透明像素
                if (color.a == 0) continue;
                
                // 检查是否有相似颜色
                bool isConflict = false;
                foreach (Color32 existingColor in colorSet)
                {
                    // 计算颜色差异
                    float diff = Mathf.Sqrt(
                        Mathf.Pow(color.r - existingColor.r, 2) +
                        Mathf.Pow(color.g - existingColor.g, 2) +
                        Mathf.Pow(color.b - existingColor.b, 2) +
                        Mathf.Pow(color.a - existingColor.a, 2)
                    );
                    
                    if (diff < 30.0f) // 颜色差异阈值
                    {
                        isConflict = true;
                        if (!conflictSet.Contains(color))
                        {
                            conflictSet.Add(color);
                        }
                        if (!conflictSet.Contains(existingColor))
                        {
                            conflictSet.Add(existingColor);
                        }
                    }
                }
                
                if (!isConflict)
                {
                    colorSet.Add(color);
                }
            }
            
            conflictColors = conflictSet.ToList();
        }
        
        /// <summary>
        /// 更新错误UI显示
        /// </summary>
        void UpdateErrorUI()
        {
            if (errorListText != null)
            {
                string errorText = "";
                
                if (detectedErrors.Count > 0)
                {
                    errorText += $"检测到 {detectedErrors.Count} 个像素级错误:\n";
                    foreach (var error in detectedErrors.Take(10)) // 只显示前10个错误
                    {
                        errorText += $"- {error.description}\n";
                    }
                    
                    if (detectedErrors.Count > 10)
                    {
                        errorText += $"... 还有 {detectedErrors.Count - 10} 个错误\n";
                    }
                }
                else
                {
                    errorText += "未检测到像素级错误\n";
                }
                
                if (conflictColors.Count > 0)
                {
                    errorText += $"\n检测到 {conflictColors.Count} 种颜色冲突:\n";
                    foreach (var color in conflictColors.Take(5)) // 只显示前5种冲突颜色
                    {
                        errorText += $"- 颜色 ({color.r}, {color.g}, {color.b}, {color.a})\n";
                    }
                    
                    if (conflictColors.Count > 5)
                    {
                        errorText += $"... 还有 {conflictColors.Count - 5} 种冲突颜色\n";
                    }
                }
                
                errorListText.text = errorText;
            }
            
            if (errorPanel != null)
            {
                errorPanel.SetActive(detectedErrors.Count > 0 || conflictColors.Count > 0);
            }
        }
        
        /// <summary>
        /// 清除错误标记
        /// </summary>
        public void ClearErrors()
        {
            detectedErrors.Clear();
            conflictColors.Clear();
            
            if (errorListText != null)
            {
                errorListText.text = "错误已清除";
            }
            
            if (errorPanel != null)
            {
                errorPanel.SetActive(false);
            }
        }
        
        /// <summary>
        /// 获取检测到的错误
        /// </summary>
        public List<PixelError> GetDetectedErrors()
        {
            return detectedErrors;
        }
        
        /// <summary>
        /// 获取冲突颜色
        /// </summary>
        public List<Color32> GetConflictColors()
        {
            return conflictColors;
        }
        
        /// <summary>
        /// 检查两个颜色是否相同
        /// </summary>
        bool HasSameColor(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }
        
        /// <summary>
        /// 获取颜色名称
        /// </summary>
        public string GetColorName(Color32 color)
        {
            // 首先尝试精确匹配
            if (colorNameMap.ContainsKey(color))
            {
                return colorNameMap[color];
            }
            
            // 如果没有精确匹配，尝试相似颜色匹配
            foreach (var kvp in colorNameMap)
            {
                Color32 mappedColor = kvp.Key;
                
                // 计算颜色差异
                float diff = Mathf.Sqrt(
                    Mathf.Pow(color.r - mappedColor.r, 2) +
                    Mathf.Pow(color.g - mappedColor.g, 2) +
                    Mathf.Pow(color.b - mappedColor.b, 2) +
                    Mathf.Pow(color.a - mappedColor.a, 2)
                );
                
                // 如果颜色差异很小，返回匹配的名称
                if (diff < 30.0f) // 阈值可调整
                {
                    return kvp.Value;
                }
            }
            
            // 如果没有找到匹配的颜色，返回RGB值
            return $"自定义({color.r},{color.g},{color.b})";
        }
    }
    
    /// <summary>
    /// 像素错误类型
    /// </summary>
    public enum PixelErrorType
    {
        IsolatedPixel,        // 孤立像素
        ThreeColorIntersection, // 三色交点
        SinglePixelLine,      // 单像素线
        SpecialTerrainViolation // 特殊地形违规（如关隘与三个普通地块相接）
    }
    
    /// <summary>
    /// 像素错误信息
    /// </summary>
    [System.Serializable]
    public class PixelError
    {
        public Vector2Int position;      // 错误位置
        public PixelErrorType errorType; // 错误类型
        public string description;       // 错误描述
    }
}
