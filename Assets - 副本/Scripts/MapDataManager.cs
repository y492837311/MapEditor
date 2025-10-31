
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace MapEditor
{
    /// <summary>
    /// 地图数据管理器 - 负责处理地图色块数据的存储、加载和编辑操作
    /// </summary>
    public class MapDataManager : MonoBehaviour
    {
        [Header("地图配置")]
        public int mapWidth = 256;
        public int mapHeight = 256;
        
        [Header("性能设置")]
        public bool useComputeShaderForProcessing = true;
        
        // 地图数据 - 存储每个像素的颜色信息
        private Color32[] mapData;
        private Texture2D mapTexture;
        
        // 用于多线程处理的数据结构
        private NativeArray<Color32> nativeMapData;
        
        // 事件系统
        public event Action OnMapDataChanged;
        public event Action OnMapLoaded;
        
        void Start()
        {
            InitializeMap();
        }
        
        /// <summary>
        /// 初始化地图数据
        /// </summary>
        public void InitializeMap()
        {
            // 初始化地图数据数组
            mapData = new Color32[mapWidth * mapHeight];
            
            // 创建纹理用于显示
            if (mapTexture != null)
            {
                DestroyImmediate(mapTexture);
            }
            mapTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false);
            mapTexture.filterMode = FilterMode.Point; // 使用点过滤以保持像素清晰
            
            // 填充默认颜色（透明）
            for (int i = 0; i < mapData.Length; i++)
            {
                mapData[i] = new Color32(0, 0, 0, 0);
            }
            
            UpdateTexture();
        }
        
        /// <summary>
        /// 获取地图数据
        /// </summary>
        public Color32[] GetMapData()
        {
            return mapData;
        }
        
        /// <summary>
        /// 设置地图数据
        /// </summary>
        public void SetMapData(Color32[] newData)
        {
            if (newData.Length != mapData.Length)
            {
                Debug.LogError("新地图数据长度与当前地图不匹配");
                return;
            }
            
            Array.Copy(newData, mapData, newData.Length);
            UpdateTexture();
            OnMapDataChanged?.Invoke();
        }
        
        /// <summary>
        /// 获取指定坐标的颜色
        /// </summary>
        public Color32 GetPixelColor(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight)
            {
                Debug.LogWarning($"坐标 ({x}, {y}) 超出地图边界");
                return Color.clear;
            }
            
            int index = y * mapWidth + x;
            return mapData[index];
        }
        
        /// <summary>
        /// 设置指定坐标的颜色
        /// </summary>
        public void SetPixelColor(int x, int y, Color32 color)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight)
            {
                return;
            }
            
            int index = y * mapWidth + x;
            mapData[index] = color;
        }
        
        /// <summary>
        /// 批量设置像素颜色（使用Job System优化）
        /// </summary>
        public void SetPixels(int[] xCoords, int[] yCoords, Color32[] colors)
        {
            if (xCoords.Length != yCoords.Length || xCoords.Length != colors.Length)
            {
                Debug.LogError("坐标数组和颜色数组长度不匹配");
                return;
            }
            
            // 使用Job System进行批量处理
            // 这里是简化版本，实际实现会使用Job System
            for (int i = 0; i < xCoords.Length; i++)
            {
                SetPixelColor(xCoords[i], yCoords[i], colors[i]);
            }
            
            UpdateTexture();
            OnMapDataChanged?.Invoke();
        }
        
        /// <summary>
        /// 更新纹理显示
        /// </summary>
        public void UpdateTexture()
        {
            if (mapTexture != null)
            {
                mapTexture.SetPixels32(mapData);
                mapTexture.Apply();
            }
        }
        
        /// <summary>
        /// 获取地图纹理
        /// </summary>
        public Texture2D GetMapTexture()
        {
            return mapTexture;
        }
        
        /// <summary>
        /// 导出地图为PNG
        /// </summary>
        public bool ExportToPNG(string path)
        {
            // 激活当前纹理以便读取像素
            RenderTexture rt = RenderTexture.GetTemporary(mapWidth, mapHeight, 0);
            RenderTexture.active = rt;
            
            Graphics.Blit(mapTexture, rt);
            
            Texture2D exportTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false);
            exportTexture.ReadPixels(new Rect(0, 0, mapWidth, mapHeight), 0, 0);
            exportTexture.Apply();
            
            byte[] bytes = exportTexture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(exportTexture);
            
            Debug.Log($"地图已导出到: {path}");
            return true;
        }

        /// <summary>
        /// 从文件导入地图数据
        /// </summary>
        public bool ImportFromPNG(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"文件不存在: {path}");
                return false;
            }

            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);

            if (texture.LoadImage(fileData))
            {
                // 调整导入纹理大小以匹配当前地图尺寸
                if (texture.width != mapWidth || texture.height != mapHeight)
                {
                    // 如果尺寸不匹配，创建新纹理并调整大小
                    Texture2D resizedTexture = ResizeTexture(texture, mapWidth, mapHeight);
                    mapData = resizedTexture.GetPixels32();
                    DestroyImmediate(resizedTexture);
                }
                else
                {
                    mapData = texture.GetPixels32();
                }

                UpdateTexture();
                OnMapLoaded?.Invoke();
                OnMapDataChanged?.Invoke();
            }

            DestroyImmediate(texture);
            return true;
        }

        /// <summary>
        /// 调整纹理大小
        /// </summary>
        private Texture2D ResizeTexture(Texture2D originalTexture, int newWidth, int newHeight)
        {
            // 创建临时渲染纹理
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            RenderTexture.active = rt;
            
            // 将原纹理绘制到新尺寸的渲染纹理上
            Graphics.Blit(originalTexture, rt);
            
            // 从渲染纹理创建新的Texture2D
            Texture2D result = new Texture2D(newWidth, newHeight);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();
            
            // 恢复渲染状态
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }
        
        /// <summary>
        /// 清空地图
        /// </summary>
        public void ClearMap()
        {
            for (int i = 0; i < mapData.Length; i++)
            {
                mapData[i] = new Color32(0, 0, 0, 0);
            }
            
            UpdateTexture();
            OnMapDataChanged?.Invoke();
        }
        
        /// <summary>
        /// 检查颜色冲突
        /// </summary>
        public List<Color32> FindColorConflicts()
        {
            List<Color32> conflicts = new List<Color32>();
            Dictionary<uint, int> colorCount = new Dictionary<uint, int>();
            
            // 统计每种颜色的出现次数
            foreach (Color32 color in mapData)
            {
                // 将Color32转换为uint以用作字典键
                uint colorKey = (uint)(color.r << 24 | color.g << 16 | color.b << 8 | color.a);
                
                if (colorCount.ContainsKey(colorKey))
                {
                    colorCount[colorKey]++;
                }
                else
                {
                    colorCount[colorKey] = 1;
                }
            }
            
            // 找出相似颜色（可能存在冲突的颜色）
            foreach (var kvp1 in colorCount)
            {
                Color32 color1 = new Color32(
                    (byte)(kvp1.Key >> 24),
                    (byte)((kvp1.Key >> 16) & 0xFF),
                    (byte)((kvp1.Key >> 8) & 0xFF),
                    (byte)(kvp1.Key & 0xFF)
                );
                
                foreach (var kvp2 in colorCount)
                {
                    if (kvp1.Key == kvp2.Key) continue;
                    
                    Color32 color2 = new Color32(
                        (byte)(kvp2.Key >> 24),
                        (byte)((kvp2.Key >> 16) & 0xFF),
                        (byte)((kvp2.Key >> 8) & 0xFF),
                        (byte)(kvp2.Key & 0xFF)
                    );
                    
                    // 计算颜色差异（简单的欧几里得距离）
                    float diff = Mathf.Sqrt(
                        Mathf.Pow(color1.r - color2.r, 2) +
                        Mathf.Pow(color1.g - color2.g, 2) +
                        Mathf.Pow(color1.b - color2.b, 2) +
                        Mathf.Pow(color1.a - color2.a, 2)
                    );
                    
                    // 如果颜色差异很小，认为是冲突
                    if (diff < 30.0f) // 阈值可调整
                    {
                        if (!conflicts.Contains(color1))
                        {
                            conflicts.Add(color1);
                        }
                        if (!conflicts.Contains(color2))
                        {
                            conflicts.Add(color2);
                        }
                    }
                }
            }
            
            return conflicts;
        }
        
        void OnDestroy()
        {
            if (mapTexture != null)
            {
                DestroyImmediate(mapTexture);
            }
        }
    }
}
