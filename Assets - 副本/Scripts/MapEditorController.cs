using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace MapEditor
{
    /// <summary>
    /// 地图编辑器主控制器 - 协调所有功能模块
    /// </summary>
    public class MapEditorController : MonoBehaviour
    {
        [Header("核心组件")]
        public MapDataManager mapDataManager;
        public MapDataProcessor mapDataProcessor;
        public MapEditorUI mapEditorUI;
        public AdvancedFeatures advancedFeatures;
        
        [Header("UI Elements")]
        public Button loadButton;
        public Button saveButton;
        public Button exportButton;
        public Button clearButton;
        public InputField filePathInput;
        public Slider mapSizeSlider;
        public Text mapSizeText;
        
        [Header("Performance Monitor")]
        public Text performanceText;

        // 添加缺失的类引用
        [System.Serializable]
        public class PixelError
        {
            public int x;
            public int y;
            public string errorType;
            public string description;
        }
        
        void Start()
        {
            InitializeComponents();
            SetupEventHandlers();
            UpdateUI();
        }
        
        void Update()
        {
            UpdatePerformanceMonitor();
        }
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        void InitializeComponents()
        {
            // 自动查找组件（如果未在Inspector中设置）
            if (mapDataManager == null)
                mapDataManager = GetComponent<MapDataManager>();
            
            if (mapDataProcessor == null)
                mapDataProcessor = GetComponent<MapDataProcessor>();
            
            if (mapEditorUI == null)
                mapEditorUI = GetComponent<MapEditorUI>();
            
            if (advancedFeatures == null)
                advancedFeatures = GetComponent<AdvancedFeatures>();
            
            // 如果组件仍然为null，则创建它们
            if (mapDataManager == null)
            {
                mapDataManager = gameObject.AddComponent<MapDataManager>();
                Debug.LogWarning("MapDataManager was not found and has been added automatically.");
            }
            
            if (mapDataProcessor == null)
            {
                mapDataProcessor = gameObject.AddComponent<MapDataProcessor>();
                Debug.LogWarning("MapDataProcessor was not found and has been added automatically.");
            }
            
            // 设置组件间的引用
            if (mapEditorUI != null)
            {
                if (mapEditorUI.mapDataManager == null)
                    mapEditorUI.mapDataManager = mapDataManager;
                
                if (mapEditorUI.mapDataProcessor == null)
                    mapEditorUI.mapDataProcessor = mapDataProcessor;
            }
            else
            {
                Debug.LogWarning("MapEditorUI component is missing!");
            }
            
            if (advancedFeatures != null)
            {
                if (advancedFeatures.mapDataManager == null)
                    advancedFeatures.mapDataManager = mapDataManager;
                
                if (advancedFeatures.mapDataProcessor == null)
                    advancedFeatures.mapDataProcessor = mapDataProcessor;
                
                if (advancedFeatures.mapEditorUI == null)
                    advancedFeatures.mapEditorUI = mapEditorUI;
            }
            else
            {
                Debug.LogWarning("AdvancedFeatures component is missing!");
            }

            // 确保地图数据管理器已初始化
            if (mapDataManager != null)
            {
                mapDataManager.InitializeMap();
            }
        }
        
        /// <summary>
        /// 设置事件处理器
        /// </summary>
        void SetupEventHandlers()
        {
            if (loadButton != null)
                loadButton.onClick.AddListener(LoadMap);
            else
                Debug.LogWarning("Load button is not assigned in inspector!");
            
            if (saveButton != null)
                saveButton.onClick.AddListener(SaveMap);
            else
                Debug.LogWarning("Save button is not assigned in inspector!");
            
            if (exportButton != null)
                exportButton.onClick.AddListener(ExportMap);
            else
                Debug.LogWarning("Export button is not assigned in inspector!");
            
            if (clearButton != null)
                clearButton.onClick.AddListener(ClearMap);
            else
                Debug.LogWarning("Clear button is not assigned in inspector!");
            
            if (mapSizeSlider != null)
                mapSizeSlider.onValueChanged.AddListener(OnMapSizeChanged);
            else
                Debug.LogWarning("Map size slider is not assigned in inspector!");
        }
        
        /// <summary>
        /// 更新UI
        /// </summary>
        void UpdateUI()
        {
            if (mapSizeSlider != null && mapDataManager != null)
            {
                mapSizeSlider.minValue = 64;
                mapSizeSlider.maxValue = 2048;
                mapSizeSlider.value = mapDataManager.mapWidth;
                UpdateMapSizeUI();
            }
        }
        
        /// <summary>
        /// 地图大小改变事件
        /// </summary>
        void OnMapSizeChanged(float value)
        {
            int size = Mathf.ClosestPowerOfTwo((int)value); // 使用2的幂次以获得更好的性能
            size = Mathf.Clamp(size, 64, 2048); // 限制在合理范围内
            
            if (mapDataManager != null)
            {
                // 保存当前地图数据
                Color32[] currentData = mapDataManager.GetMapData();
                int oldSize = mapDataManager.mapWidth;
                
                mapDataManager.mapWidth = size;
                mapDataManager.mapHeight = size;
                mapDataManager.InitializeMap();
                
                // 如果尺寸变小，尝试保留部分数据
                if (currentData != null && oldSize > 0)
                {
                    int copySize = Mathf.Min(oldSize, size);
                    Color32[] newData = mapDataManager.GetMapData();
                    
                    for (int y = 0; y < copySize; y++)
                    {
                        for (int x = 0; x < copySize; x++)
                        {
                            int oldIndex = y * oldSize + x;
                            int newIndex = y * size + x;
                            if (oldIndex < currentData.Length && newIndex < newData.Length)
                            {
                                newData[newIndex] = currentData[oldIndex];
                            }
                        }
                    }
                    
                    mapDataManager.SetMapData(newData);
                    mapDataManager.UpdateTexture();
                }
            }
            
            UpdateMapSizeUI();
        }
        
        /// <summary>
        /// 更新地图大小UI
        /// </summary>
        void UpdateMapSizeUI()
        {
            if (mapSizeText != null && mapDataManager != null)
            {
                mapSizeText.text = $"地图大小: {mapDataManager.mapWidth}x{mapDataManager.mapHeight}";
            }
        }
        
        /// <summary>
        /// 加载地图
        /// </summary>
        void LoadMap()
        {
            if (mapDataManager == null) 
            {
                Debug.LogError("MapDataManager is not available!");
                return;
            }
            
            string path = filePathInput != null ? filePathInput.text : "";
            if (string.IsNullOrEmpty(path))
            {
                // 使用默认路径
                path = Path.Combine(Application.persistentDataPath, "map.png");
                if (filePathInput != null)
                    filePathInput.text = path;
            }
            
            if (!File.Exists(path))
            {
                Debug.LogError($"文件不存在: {path}");
                return;
            }
            
            try
            {
                bool success = mapDataManager.ImportFromPNG(path);
                if (success)
                {
                    Debug.Log($"地图已从 {path} 加载");
                    
                    // 更新UI显示
                    if (mapSizeSlider != null)
                    {
                        mapSizeSlider.value = mapDataManager.mapWidth;
                        UpdateMapSizeUI();
                    }
                }
                else
                {
                    Debug.LogError($"加载地图失败: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"加载地图时出错: {e.Message}");
            }
        }
        
        /// <summary>
        /// 保存地图
        /// </summary>
        void SaveMap()
        {
            if (mapDataManager == null) 
            {
                Debug.LogError("MapDataManager is not available!");
                return;
            }
            
            string path = filePathInput != null ? filePathInput.text : "";
            if (string.IsNullOrEmpty(path))
            {
                // 使用默认路径
                path = Path.Combine(Application.persistentDataPath, "map.png");
                if (filePathInput != null)
                    filePathInput.text = path;
            }
            
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                bool success = mapDataManager.ExportToPNG(path);
                if (success)
                {
                    Debug.Log($"地图已保存到 {path}");
                }
                else
                {
                    Debug.LogError($"保存地图失败: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"保存地图时出错: {e.Message}");
            }
        }
        
        /// <summary>
        /// 导出地图
        /// </summary>
        void ExportMap()
        {
            if (mapDataManager == null) 
            {
                Debug.LogError("MapDataManager is not available!");
                return;
            }
            
            string path = filePathInput != null ? filePathInput.text : "";
            if (string.IsNullOrEmpty(path))
            {
                // 使用默认导出路径
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                path = Path.Combine(Application.persistentDataPath, $"map_export_{timestamp}.png");
                if (filePathInput != null)
                    filePathInput.text = path;
            }
            
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                bool success = mapDataManager.ExportToPNG(path);
                if (success)
                {
                    Debug.Log($"地图已导出到 {path}");
                }
                else
                {
                    Debug.LogError($"导出地图失败: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"导出地图时出错: {e.Message}");
            }
        }
        
        /// <summary>
        /// 清空地图
        /// </summary>
        void ClearMap()
        {
            if (mapDataManager != null)
            {
                mapDataManager.ClearMap();
                Debug.Log("地图已清空");
            }
            else
            {
                Debug.LogError("MapDataManager is not available!");
            }
        }
        
        /// <summary>
        /// 更新性能监控
        /// </summary>
        void UpdatePerformanceMonitor()
        {
            if (performanceText != null)
            {
                try
                {
                    // 显示一些基本性能指标
                    float memoryMB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);
                    float fps = 1f / Time.unscaledDeltaTime;
                    performanceText.text = $"内存: {memoryMB:F1} MB\nFPS: {(int)fps}";
                }
                catch (System.Exception e)
                {
                    performanceText.text = $"性能监控错误: {e.Message}";
                }
            }
        }
        
        /// <summary>
        /// 获取当前地图数据
        /// </summary>
        public Color32[] GetCurrentMapData()
        {
            return mapDataManager != null ? mapDataManager.GetMapData() : null;
        }
        
        /// <summary>
        /// 设置地图数据
        /// </summary>
        public void SetMapData(Color32[] data, int width, int height)
        {
            if (mapDataManager != null)
            {
                mapDataManager.mapWidth = width;
                mapDataManager.mapHeight = height;
                mapDataManager.SetMapData(data);
            }
        }
        
        /// <summary>
        /// 检测所有错误
        /// </summary>
        public void DetectAllErrors()
        {
            if (advancedFeatures != null)
            {
                advancedFeatures.DetectAllErrors();
            }
            else
            {
                Debug.LogWarning("AdvancedFeatures component is not available!");
            }
        }
        
        /// <summary>
        /// 获取检测到的错误
        /// </summary>
        public List<MapEditor.PixelError> GetDetectedErrors()
        {
            if (advancedFeatures != null)
            {
                // 注意：这里需要AdvancedFeatures类中有对应的GetDetectedErrors方法
                // 如果不存在，可能需要修改AdvancedFeatures类
                return advancedFeatures.GetDetectedErrors();
            }
            
            Debug.LogWarning("AdvancedFeatures component is not available!");
            return new List<MapEditor.PixelError>();
        }

        /// <summary>
        /// 重新初始化地图编辑器
        /// </summary>
        public void Reinitialize()
        {
            InitializeComponents();
            UpdateUI();
            
            if (mapDataManager != null)
            {
                mapDataManager.InitializeMap();
            }
        }
    }
}