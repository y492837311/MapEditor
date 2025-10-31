using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MapEditor
{
    /// <summary>
    /// 使用Compute Shader的地图处理器 - 进一步优化性能
    /// </summary>
    public class MapComputeProcessor : MonoBehaviour
    {
        [Header("Compute Shader")]
        public ComputeShader mapComputeShader;
        
        private int drawKernel;
        private int fillKernel;
        private int errorDetectionKernel;
        
        private MapDataManager mapDataManager;
        private RenderTexture processedMapTexture;
        
        void Start()
        {
            mapDataManager = GetComponent<MapDataManager>();
            
            if (mapDataManager == null)
            {
                Debug.LogError("MapComputeProcessor: MapDataManager component not found!");
                return;
            }
            
            if (mapComputeShader != null)
            {
                // 获取内核索引
                drawKernel = mapComputeShader.FindKernel("DrawBrushKernel");
                fillKernel = mapComputeShader.FindKernel("FloodFillKernel");
                errorDetectionKernel = mapComputeShader.FindKernel("DetectErrorsKernel");
                
                // 验证内核是否存在
                if (drawKernel < 0) Debug.LogWarning("DrawBrushKernel not found in compute shader!");
                if (fillKernel < 0) Debug.LogWarning("FloodFillKernel not found in compute shader!");
                if (errorDetectionKernel < 0) Debug.LogWarning("DetectErrorsKernel not found in compute shader!");
                
                // 创建输出纹理
                CreateRenderTexture();
            }
            else
            {
                // 如果没有Compute Shader，尝试加载默认的
                LoadDefaultComputeShader();
            }
        }
        
        void OnDestroy()
        {
            ReleaseRenderTexture();
        }
        
        /// <summary>
        /// 创建渲染纹理
        /// </summary>
        void CreateRenderTexture()
        {
            if (mapDataManager == null) return;
            
            ReleaseRenderTexture();
            
            processedMapTexture = new RenderTexture(mapDataManager.mapWidth, mapDataManager.mapHeight, 0, RenderTextureFormat.ARGB32);
            processedMapTexture.enableRandomWrite = true;
            processedMapTexture.Create();
        }
        
        /// <summary>
        /// 释放渲染纹理资源
        /// </summary>
        void ReleaseRenderTexture()
        {
            if (processedMapTexture != null)
            {
                processedMapTexture.Release();
                processedMapTexture = null;
            }
        }
        
        /// <summary>
        /// 加载默认Compute Shader
        /// </summary>
        void LoadDefaultComputeShader()
        {
            // 在实际项目中，这将从Resources或AssetBundle加载
            mapComputeShader = Resources.Load<ComputeShader>("MapProcessing");
            
            if (mapComputeShader == null)
            {
                Debug.LogError("Failed to load default compute shader!");
            }
            else
            {
                // 重新初始化内核索引
                drawKernel = mapComputeShader.FindKernel("DrawBrushKernel");
                fillKernel = mapComputeShader.FindKernel("FloodFillKernel");
                errorDetectionKernel = mapComputeShader.FindKernel("DetectErrorsKernel");
                
                CreateRenderTexture();
            }
        }
        
        /// <summary>
        /// 使用Compute Shader绘制笔刷
        /// </summary>
        public void DrawWithComputeShader(int centerX, int centerY, Color32 color, int brushSize)
        {
            if (mapComputeShader == null || mapDataManager == null) 
            {
                Debug.LogWarning("Compute shader or map data manager not available!");
                return;
            }
            
            if (drawKernel < 0)
            {
                Debug.LogWarning("Draw kernel not available!");
                return;
            }
            
            // 验证坐标范围
            centerX = Mathf.Clamp(centerX, 0, mapDataManager.mapWidth - 1);
            centerY = Mathf.Clamp(centerY, 0, mapDataManager.mapHeight - 1);
            
            // 设置内核参数
            mapComputeShader.SetInt("mapWidth", mapDataManager.mapWidth);
            mapComputeShader.SetInt("mapHeight", mapDataManager.mapHeight);
            mapComputeShader.SetInt("centerX", centerX);
            mapComputeShader.SetInt("centerY", centerY);
            mapComputeShader.SetVector("color", new Vector4(color.r / 255f, color.g / 255f, color.b / 255f, color.a / 255f));
            mapComputeShader.SetInt("brushSize", brushSize);
            
            // 设置纹理
            Texture currentTexture = mapDataManager.GetMapTexture();
            if (currentTexture == null)
            {
                Debug.LogError("Current map texture is null!");
                return;
            }
            
            // 确保处理纹理尺寸正确
            if (processedMapTexture == null || 
                processedMapTexture.width != mapDataManager.mapWidth || 
                processedMapTexture.height != mapDataManager.mapHeight)
            {
                CreateRenderTexture();
            }
            
            // 先将当前纹理复制到处理纹理
            Graphics.CopyTexture(currentTexture, processedMapTexture);
            
            mapComputeShader.SetTexture(drawKernel, "Result", processedMapTexture);
            mapComputeShader.SetTexture(drawKernel, "Source", currentTexture);
            
            // 计算线程组数量
            int threadGroupX = Mathf.CeilToInt(mapDataManager.mapWidth / 8.0f);
            int threadGroupY = Mathf.CeilToInt(mapDataManager.mapHeight / 8.0f);
            
            // 分发计算
            mapComputeShader.Dispatch(drawKernel, threadGroupX, threadGroupY, 1);
            
            // 将结果复制回原纹理
            Graphics.CopyTexture(processedMapTexture, currentTexture);
            mapDataManager.UpdateTexture();
        }
        
        /// <summary>
        /// 使用Compute Shader进行区域填充
        /// </summary>
        public void FillWithComputeShader(int startX, int startY, Color32 fillColor)
        {
            if (mapComputeShader == null || mapDataManager == null) 
            {
                Debug.LogWarning("Compute shader or map data manager not available!");
                return;
            }
            
            if (fillKernel < 0)
            {
                Debug.LogWarning("Fill kernel not available!");
                return;
            }
            
            // 验证坐标范围
            startX = Mathf.Clamp(startX, 0, mapDataManager.mapWidth - 1);
            startY = Mathf.Clamp(startY, 0, mapDataManager.mapHeight - 1);
            
            // 设置内核参数
            mapComputeShader.SetInt("mapWidth", mapDataManager.mapWidth);
            mapComputeShader.SetInt("mapHeight", mapDataManager.mapHeight);
            mapComputeShader.SetInt("startX", startX);
            mapComputeShader.SetInt("startY", startY);
            mapComputeShader.SetVector("fillColor", new Vector4(fillColor.r / 255f, fillColor.g / 255f, fillColor.b / 255f, fillColor.a / 255f));
            
            // 获取起始点的原始颜色
            Color32 originalColor = mapDataManager.GetPixelColor(startX, startY);
            mapComputeShader.SetVector("originalColor", new Vector4(originalColor.r / 255f, originalColor.g / 255f, originalColor.b / 255f, originalColor.a / 255f));
            
            // 设置纹理
            Texture currentTexture = mapDataManager.GetMapTexture();
            if (currentTexture == null)
            {
                Debug.LogError("Current map texture is null!");
                return;
            }
            
            // 确保处理纹理尺寸正确
            if (processedMapTexture == null || 
                processedMapTexture.width != mapDataManager.mapWidth || 
                processedMapTexture.height != mapDataManager.mapHeight)
            {
                CreateRenderTexture();
            }
            
            // 先将当前纹理复制到处理纹理
            Graphics.CopyTexture(currentTexture, processedMapTexture);
            
            mapComputeShader.SetTexture(fillKernel, "Result", processedMapTexture);
            mapComputeShader.SetTexture(fillKernel, "Source", currentTexture);
            
            // 计算线程组数量
            int threadGroupX = Mathf.CeilToInt(mapDataManager.mapWidth / 8.0f);
            int threadGroupY = Mathf.CeilToInt(mapDataManager.mapHeight / 8.0f);
            
            // 分发计算
            mapComputeShader.Dispatch(fillKernel, threadGroupX, threadGroupY, 1);
            
            // 将结果复制回原纹理
            Graphics.CopyTexture(processedMapTexture, currentTexture);
            mapDataManager.UpdateTexture();
        }
        
        /// <summary>
        /// 使用Compute Shader检测错误
        /// </summary>
        public void DetectErrorsWithComputeShader()
        {
            if (mapComputeShader == null || mapDataManager == null) 
            {
                Debug.LogWarning("Compute shader or map data manager not available!");
                return;
            }
            
            if (errorDetectionKernel < 0)
            {
                Debug.LogWarning("Error detection kernel not available!");
                return;
            }
            
            // 设置内核参数
            mapComputeShader.SetInt("mapWidth", mapDataManager.mapWidth);
            mapComputeShader.SetInt("mapHeight", mapDataManager.mapHeight);
            
            // 设置纹理
            Texture currentTexture = mapDataManager.GetMapTexture();
            if (currentTexture == null)
            {
                Debug.LogError("Current map texture is null!");
                return;
            }
            
            mapComputeShader.SetTexture(errorDetectionKernel, "Source", currentTexture);
            
            // 计算线程组数量
            int threadGroupX = Mathf.CeilToInt(mapDataManager.mapWidth / 8.0f);
            int threadGroupY = Mathf.CeilToInt(mapDataManager.mapHeight / 8.0f);
            
            // 分发计算
            mapComputeShader.Dispatch(errorDetectionKernel, threadGroupX, threadGroupY, 1);
            
            Debug.Log("Error detection completed. Note: Error data retrieval not implemented.");
        }
        
        /// <summary>
        /// 重新初始化处理器（当地图尺寸改变时调用）
        /// </summary>
        public void Reinitialize()
        {
            CreateRenderTexture();
        }
    }
}