using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using MapEditorTool.Runtime.Jobs;
using Unity.Burst;
using Unity.Jobs;

[System.Serializable]
public class MapLayer : System.IDisposable
{
    public string name;
    public NativeArray<int> colorData;
    public bool isVisible = true;
    public bool isLocked = false;
    public float opacity = 1.0f;
    public BlendMode blendMode = BlendMode.Normal;
    public int order;
    
    public MapLayer(string layerName, int width, int height)
    {
        name = layerName;
        colorData = new NativeArray<int>(width * height, Allocator.Persistent);
        order = 0;
    }
    
    public void Dispose()
    {
        if (colorData.IsCreated)
            colorData.Dispose();
    }
    
    public void Clear()
    {
        for (int i = 0; i < colorData.Length; i++)
        {
            colorData[i] = 0;
        }
    }
}

public enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Add,
    Subtract
}

public class LayerSystem : System.IDisposable
{
    private List<MapLayer> layers;
    private NativeArray<int> compositeBuffer;
    private ComputeShader layerComputeShader;
    private ComputeBuffer layerBuffer;
    private ComputeBuffer compositeBufferGPU;

    private int width;
    private int height;
    private int activeLayerIndex = 0;

    public int LayerCount => layers.Count;
    public int ActiveLayerIndex => activeLayerIndex;
    public MapLayer ActiveLayer => layers.Count > 0 ? layers[activeLayerIndex] : null;

    public System.Action OnLayersChanged;

    public LayerSystem(int mapWidth, int mapHeight)
    {
        width = mapWidth;
        height = mapHeight;
        layers = new List<MapLayer>();
        compositeBuffer = new NativeArray<int>(width * height, Allocator.Persistent);

        layerComputeShader = Resources.Load<ComputeShader>("Shaders/LayerCompute");

        // 修复：正确的缓冲区大小和stride
        int maxLayers = 16;
        int layerDataSize = width * height;

        // 每个图层的数据缓冲区（分开存储）
        // layerBuffer 不再需要，因为我们会为每个图层创建单独的缓冲区

        compositeBufferGPU = new ComputeBuffer(layerDataSize, sizeof(int)); // 每个元素4字节

        CreateLayer("背景层");
    }

    #region 图层管理

    public MapLayer CreateLayer(string name)
    {
        var newLayer = new MapLayer(name, width, height);
        newLayer.order = layers.Count;
        layers.Add(newLayer);

        OnLayersChanged?.Invoke();
        return newLayer;
    }

    public void DeleteLayer(int index)
    {
        if (index < 0 || index >= layers.Count) return;

        layers[index].Dispose();
        layers.RemoveAt(index);

        for (int i = 0; i < layers.Count; i++)
        {
            layers[i].order = i;
        }

        if (activeLayerIndex >= layers.Count)
        {
            activeLayerIndex = Mathf.Max(0, layers.Count - 1);
        }

        OnLayersChanged?.Invoke();
    }

    public void MoveLayer(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= layers.Count ||
            toIndex < 0 || toIndex >= layers.Count) return;

        var layer = layers[fromIndex];
        layers.RemoveAt(fromIndex);
        layers.Insert(toIndex, layer);

        for (int i = 0; i < layers.Count; i++)
        {
            layers[i].order = i;
        }

        OnLayersChanged?.Invoke();
    }

    public void SetActiveLayer(int index)
    {
        if (index >= 0 && index < layers.Count)
        {
            activeLayerIndex = index;
            OnLayersChanged?.Invoke();
        }
    }

    public MapLayer GetLayer(int index)
    {
        return index >= 0 && index < layers.Count ? layers[index] : null;
    }

    public List<MapLayer> GetAllLayers()
    {
        return new List<MapLayer>(layers);
    }

    public void DuplicateLayer(int index)
    {
        if (index < 0 || index >= layers.Count) return;

        var sourceLayer = layers[index];
        var newLayer = CreateLayer($"{sourceLayer.name} 副本");

        sourceLayer.colorData.CopyTo(newLayer.colorData);
        newLayer.opacity = sourceLayer.opacity;
        newLayer.blendMode = sourceLayer.blendMode;

        OnLayersChanged?.Invoke();
    }

    public void MergeLayer(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= layers.Count ||
            targetIndex < 0 || targetIndex >= layers.Count) return;

        var sourceLayer = layers[sourceIndex];
        var targetLayer = layers[targetIndex];

        MergeLayersWithComputeShader(sourceLayer, targetLayer);
        DeleteLayer(sourceIndex);
    }

    #endregion

    #region 图层合成

    public NativeArray<int> GetCompositeResult()
    {
        if (layers.Count == 0)
            return compositeBuffer;

     
        // 清空合成缓冲区为透明
        var clearCompositeBufferColorJob = new ClearCanvasJob
        {
            pixels = compositeBuffer,
        };
        clearCompositeBufferColorJob.Schedule(compositeBuffer.Length, 64).Complete();

        // 按顺序合成所有可见图层
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (layer.isVisible && layer.opacity > 0.001f)
            {
                // 使用简化的CPU混合，避免Compute Buffer问题
                BlendLayer(layer, compositeBuffer);
            }
        }

        return compositeBuffer;
    }

    private void BlendLayer(MapLayer layer, NativeArray<int> targetBuffer)
    {
        var blendLayerJob = new BlendLayersJob
        {
            source = layer.colorData,
            target = targetBuffer,
            opacity = layer.opacity,
            blendMode = layer.blendMode,
        };
        blendLayerJob.Schedule(layer.colorData.Length, 64).Complete();
        
        /*for (int i = 0; i < layer.colorData.Length; i++)
        {
            int sourceColor = layer.colorData[i];
            // 跳过完全透明的源像素
            if (sourceColor == 0) continue;
            int targetColor = targetBuffer[i];
            Color src = IntToColor(sourceColor);
            Color dst = IntToColor(targetColor);

            // 应用图层不透明度
            src.a *= layer.opacity;

            // 简单alpha混合
            Color result = Color.Lerp(dst, src, src.a);
            result.a = math.clamp(dst.a + src.a * (1 - dst.a),0 ,1); // 正确的alpha混合

            targetBuffer[i] = ColorToInt(result);
        }*/
    }

    // 修复颜色转换工具方法
    private int ColorToInt(Color color)
    {
        color.a = math.clamp(color.a, 0, 1);
        if (color.a < 0.001f) return 0; // 完全透明

        color.r = math.clamp(color.r, 0, 1);
        color.g = math.clamp(color.g, 0, 1);
        color.b = math.clamp(color.b, 0, 1);

        return ((int)(color.r * 255) << 24) |
               ((int)(color.g * 255) << 16) |
               ((int)(color.b * 255) << 8) |
               (int)(color.a * 255);
    }

    private Color IntToColor(int colorInt)
    {
        if (colorInt == 0) return Color.clear;

        return new Color(
            ((colorInt >> 24) & 0xFF) / 255f,
            ((colorInt >> 16) & 0xFF) / 255f,
            ((colorInt >> 8) & 0xFF) / 255f,
            (colorInt & 0xFF) / 255f
        );
    }

    // 在 LayerSystem.cs 中修复 BlendLayersJob 结构体
    [BurstCompile]
    private struct BlendLayersJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> source;
        public NativeArray<int> target;
        public float opacity;
        public BlendMode blendMode;

        public void Execute(int index)
        {
            int sourceColor = source[index];
            // 如果源像素完全透明，跳过
            if (sourceColor == 0) return;
            int targetColor = target[index];

            // 如果目标像素完全透明，直接使用源像素（应用不透明度）
            if (targetColor == 0)
            {
                Color src = IntToColor(sourceColor);
                src.a *= opacity; // 应用不透明度
                target[index] = ColorToInt(src);
                return;
            }

            Color srcColor = IntToColor(sourceColor);
            Color dstColor = IntToColor(targetColor);
            Color result = BlendColors(srcColor, dstColor, blendMode, opacity);

            target[index] = ColorToInt(result);
        }

        private Color BlendColors(Color src, Color dst, BlendMode mode, float opacity)
        {
            Color result = Color.clear;

            switch (mode)
            {
                case BlendMode.Normal:
                    result = Color.Lerp(dst, src, src.a * opacity);
                    result.a = Mathf.Max(src.a, dst.a);
                    break;

                case BlendMode.Multiply:
                    result.r = dst.r * src.r * opacity;
                    result.g = dst.g * src.g * opacity;
                    result.b = dst.b * src.b * opacity;
                    result.a = Mathf.Max(src.a, dst.a);
                    break;

                case BlendMode.Screen:
                    result.r = 1 - (1 - dst.r) * (1 - src.r) * opacity;
                    result.g = 1 - (1 - dst.g) * (1 - src.g) * opacity;
                    result.b = 1 - (1 - dst.b) * (1 - src.b) * opacity;
                    result.a = Mathf.Max(src.a, dst.a);
                    break;

                case BlendMode.Overlay:
                    result.r = (dst.r < 0.5f) ? (2 * dst.r * src.r) : (1 - 2 * (1 - dst.r) * (1 - src.r));
                    result.g = (dst.g < 0.5f) ? (2 * dst.g * src.g) : (1 - 2 * (1 - dst.g) * (1 - src.g));
                    result.b = (dst.b < 0.5f) ? (2 * dst.b * src.b) : (1 - 2 * (1 - dst.b) * (1 - src.b));
                    result.a = Mathf.Max(src.a, dst.a);
                    break;

                case BlendMode.Add:
                    result.r = Mathf.Clamp01(dst.r + src.r * opacity);
                    result.g = Mathf.Clamp01(dst.g + src.g * opacity);
                    result.b = Mathf.Clamp01(dst.b + src.b * opacity);
                    result.a = Mathf.Max(src.a, dst.a);
                    break;

                case BlendMode.Subtract:
                    result.r = Mathf.Clamp01(dst.r - src.r * opacity);
                    result.g = Mathf.Clamp01(dst.g - src.g * opacity);
                    result.b = Mathf.Clamp01(dst.b - src.b * opacity);
                    result.a = Mathf.Max(src.a, dst.a);
                    break;
            }

            return result;
        }

        private int ColorToInt(Color color)
        {
            color = Color.Lerp(Color.clear, color, color.a); // 预乘alpha
            return ((int)(color.r * 255) << 24) |
                   ((int)(color.g * 255) << 16) |
                   ((int)(color.b * 255) << 8) |
                   (int)(color.a * 255);
        }

        private Color IntToColor(int colorInt)
        {
            Color color = new Color(
                ((colorInt >> 24) & 0xFF) / 255f,
                ((colorInt >> 16) & 0xFF) / 255f,
                ((colorInt >> 8) & 0xFF) / 255f,
                (colorInt & 0xFF) / 255f
            );

            // 如果颜色完全透明，返回透明黑
            if (color.a <= 0.001f) return Color.clear;

            return color;
        }
    }

    // 在 LayerSystem.cs 中替换整个合并系统
    private void MergeLayersWithComputeShader(MapLayer source, MapLayer target)
    {
        try
        {
            int kernel = layerComputeShader.FindKernel("MergeLayers");

            // 创建缓冲区 - 修复stride问题
            int bufferSize = source.colorData.Length;
            ComputeBuffer sourceBuffer = new ComputeBuffer(bufferSize, sizeof(int)); // 4字节stride
            ComputeBuffer targetBuffer = new ComputeBuffer(bufferSize, sizeof(int)); // 4字节stride
            ComputeBuffer resultBuffer = new ComputeBuffer(bufferSize, sizeof(int)); // 4字节stride

            // 设置数据
            sourceBuffer.SetData(source.colorData);
            targetBuffer.SetData(target.colorData);

            // 设置参数
            layerComputeShader.SetBuffer(kernel, "Source", sourceBuffer);
            layerComputeShader.SetBuffer(kernel, "Target", targetBuffer);
            layerComputeShader.SetBuffer(kernel, "Result", resultBuffer);
            layerComputeShader.SetFloat("Opacity", source.opacity);
            layerComputeShader.SetInt("BlendMode", (int)source.blendMode);

            // 计算线程组数量
            int threadGroups = Mathf.CeilToInt(bufferSize / 64.0f);
            threadGroups = Mathf.Max(1, threadGroups); // 确保至少1个线程组

            // 分派计算
            layerComputeShader.Dispatch(kernel, threadGroups, 1, 1);

            // 获取结果
            var resultData = new int[bufferSize];
            resultBuffer.GetData(resultData);
            target.colorData.CopyFrom(resultData);

            // 释放缓冲区
            sourceBuffer.Release();
            targetBuffer.Release();
            resultBuffer.Release();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"图层合并失败: {e.Message}");
            // 回退到CPU合并
            FallbackCPUMerge(source, target);
        }
    }

// CPU回退合并方法
    private void FallbackCPUMerge(MapLayer source, MapLayer target)
    {
        for (int i = 0; i < source.colorData.Length; i++)
        {
            int sourceColor = source.colorData[i];
            int targetColor = target.colorData[i];

            if (sourceColor == 0) continue; // 透明像素跳过

            Color src = IntToColor(sourceColor);
            Color dst = IntToColor(targetColor);

            // 简单alpha混合
            Color result = Color.Lerp(dst, src, src.a * source.opacity);
            result.a = Mathf.Max(src.a, dst.a);

            target.colorData[i] = ColorToInt(result);
        }
    }

    #endregion

    #region 图层操作

    public void ClearActiveLayer()
    {
        if (ActiveLayer != null && !ActiveLayer.isLocked)
        {
            ActiveLayer.Clear();
            OnLayersChanged?.Invoke();
        }
    }

    public void SetLayerVisibility(int index, bool visible)
    {
        if (index >= 0 && index < layers.Count)
        {
            layers[index].isVisible = visible;
            OnLayersChanged?.Invoke();
        }
    }

    public void SetLayerLocked(int index, bool locked)
    {
        if (index >= 0 && index < layers.Count)
        {
            layers[index].isLocked = locked;
            OnLayersChanged?.Invoke();
        }
    }

    public void SetLayerOpacity(int index, float opacity)
    {
        if (index >= 0 && index < layers.Count)
        {
            layers[index].opacity = Mathf.Clamp01(opacity);
            OnLayersChanged?.Invoke();
        }
    }

    public void SetLayerBlendMode(int index, BlendMode blendMode)
    {
        if (index >= 0 && index < layers.Count)
        {
            layers[index].blendMode = blendMode;
            OnLayersChanged?.Invoke();
        }
    }

    public NativeArray<int> GetActiveLayerData()
    {
        return ActiveLayer?.colorData ?? default;
    }

    public bool CanEditActiveLayer()
    {
        return ActiveLayer != null && !ActiveLayer.isLocked;
    }

    #endregion

    public void Dispose()
    {
        foreach (var layer in layers)
        {
            layer.Dispose();
        }

        layers.Clear();

        if (compositeBuffer.IsCreated)
            compositeBuffer.Dispose();

        compositeBufferGPU?.Release();
    }
}