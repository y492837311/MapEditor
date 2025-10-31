using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using MapEditorTool.Runtime.Jobs;
using Unity.Jobs;

public class SmartAnnotationSystem : IDisposable
{
    public struct AnnotationData
    {
        public int2 position;
        public FixedString128Bytes label;
        public Color color;
        public AnnotationType type;
        public float displayTime;
    }
    
    public enum AnnotationType
    {
        Info,
        Warning,
        Error,
        Success
    }
    
    private NativeHashMap<int2, FixedString128Bytes> regionNames;
    private NativeHashMap<int, FixedString128Bytes> colorToNameMap;
    private NativeList<AnnotationData> activeAnnotations;
    private NativeList<int2> conflictPixels;
    private NativeList<int2> isolatedPixels;
    private NativeList<int2> thinLinePixels;
    private NativeList<int2> tripleJunctionPixels;
    private NativeParallelHashMap<int, int> colorCount;
    
    private ComputeShader annotationComputeShader;
    private ComputeBuffer conflictBuffer;
    private ComputeBuffer isolatedBuffer;
    private ComputeBuffer thinLineBuffer;
    private ComputeBuffer tripleJunctionBuffer;
    
    private bool showCoordinates = true;
    private bool showRegionNames = true;
    private bool showWarnings = true;
    private bool showErrors = true;
    
    public SmartAnnotationSystem(int width, int height)
    {
        regionNames = new NativeHashMap<int2, FixedString128Bytes>(1024, Allocator.Persistent);
        colorToNameMap = new NativeHashMap<int, FixedString128Bytes>(256, Allocator.Persistent);
        activeAnnotations = new NativeList<AnnotationData>(64, Allocator.Persistent);
        conflictPixels = new NativeList<int2>(width * height, Allocator.Persistent);
        isolatedPixels = new NativeList<int2>(width * height, Allocator.Persistent);
        thinLinePixels = new NativeList<int2>(width * height, Allocator.Persistent);
        tripleJunctionPixels = new NativeList<int2>(width * height, Allocator.Persistent);
        colorCount = new NativeParallelHashMap<int, int>(256, Allocator.Persistent);

        annotationComputeShader = Resources.Load<ComputeShader>("Shaders/AnnotationCompute");
        
        conflictBuffer = new ComputeBuffer(128, sizeof(int) * 2);
        isolatedBuffer = new ComputeBuffer(128, sizeof(int) * 2);
        thinLineBuffer = new ComputeBuffer(128, sizeof(int) * 2);
        tripleJunctionBuffer = new ComputeBuffer(128, sizeof(int) * 2);
        
        InitializeDefaultRegionNames();
    }
    
    private void InitializeDefaultRegionNames()
    {
        AddRegionName(new int2(100, 100), "起始点");
        AddRegionName(new int2(500, 500), "中心区域");
        AddRegionName(new int2(900, 900), "终点区域");
        
        AddColorName(ColorToInt(Color.red), "危险区域");
        AddColorName(ColorToInt(Color.green), "安全区域");
        AddColorName(ColorToInt(Color.blue), "水域");
        AddColorName(ColorToInt(Color.yellow), "资源区");
    }
    
    public void UpdateAnnotations(NativeArray<int> colorData, int width, int height, int2 mousePosition)
    {
        activeAnnotations.Clear();
        
        if (showCoordinates)
        {
            AddCoordinateInfo(mousePosition);
        }
        
        if (showRegionNames)
        {
            AddRegionInfo(colorData, mousePosition, width, height);
        }
        
        if (showWarnings || showErrors)
        {
            RunDetectionJobs(colorData, width, height);
            AddDetectionAnnotations();
        }
    }
    
    private void AddCoordinateInfo(int2 position)
    {
        var annotation = new AnnotationData
        {
            position = position,
            label = $"坐标: ({position.x}, {position.y})",
            color = Color.white,
            type = AnnotationType.Info,
            displayTime = Time.time
        };
        activeAnnotations.Add(annotation);
    }
    
    private void AddRegionInfo(NativeArray<int> colorData, int2 mousePos, int width, int height)
    {
        if (mousePos.x < 0 || mousePos.x >= width || mousePos.y < 0 || mousePos.y >= height)
            return;
            
        int index = mousePos.y * width + mousePos.x;
        int color = colorData[index];
        
        if (color == 0) return;
        
        string regionName = GetRegionName(mousePos);
        string colorName = GetColorName(color);
        
        var annotation = new AnnotationData
        {
            position = mousePos,
            label = $"{regionName}\n{colorName}",
            color = IntToColor(color),
            type = AnnotationType.Info,
            displayTime = Time.time
        };
        activeAnnotations.Add(annotation);
    }
    
    private void RunDetectionJobs(NativeArray<int> colorData, int width, int height)
    {
        conflictPixels.Clear();
        isolatedPixels.Clear();
        thinLinePixels.Clear();
        tripleJunctionPixels.Clear();
        colorCount.Clear();
        var detectionJob = new DetectionColorJob
        {
            colorData = colorData,
            isolatedPixels = isolatedPixels.AsParallelWriter(),
            thinLinePixels = thinLinePixels.AsParallelWriter(),
            tripleJunctionPixels = tripleJunctionPixels.AsParallelWriter(),
            // colorCount = colorCount.AsParallelWriter(),
            width = width,
            height = height
        };
        detectionJob.Schedule(colorData.Length, 64).Complete();
        
        foreach (var pair in colorCount)
        {
            if (pair.Value >= 10) continue;
            var color = pair.Key;
            foreach (var otherPair in colorCount)
            {
                var otherColor = otherPair.Key;
                if (color != otherColor && ColorsSimilar(color, otherColor, 0.1f))
                {
                    MarkColorPixels(colorData, color, conflictPixels, width, height);
                    break;
                }
            }
        }
        
    }
    
    private void AddDetectionAnnotations()
    {
        for (int i = 0; i < conflictPixels.Length; i++)
        {
            var annotation = new AnnotationData
            {
                position = conflictPixels[i],
                label = "颜色冲突",
                color = Color.yellow,
                type = AnnotationType.Warning,
                displayTime = Time.time
            };
            activeAnnotations.Add(annotation);
        }
        
        for (int i = 0; i < isolatedPixels.Length; i++)
        {
            var annotation = new AnnotationData
            {
                position = isolatedPixels[i],
                label = "孤立像素",
                color = Color.red,
                type = AnnotationType.Error,
                displayTime = Time.time
            };
            activeAnnotations.Add(annotation);
        }
        
        for (int i = 0; i < thinLinePixels.Length; i++)
        {
            var annotation = new AnnotationData
            {
                position = thinLinePixels[i],
                label = "过细连接",
                color = Color.magenta,
                type = AnnotationType.Warning,
                displayTime = Time.time
            };
            activeAnnotations.Add(annotation);
        }
    }
    
    private void MarkColorPixels(NativeArray<int> colorData, int targetColor, NativeList<int2> output, int width,
        int height)
    {
        for (int i = 0; i < colorData.Length; i++)
        {
            if (colorData[i] == targetColor)
            {
                int x = i % width;
                int y = i / width;
                output.Add(new int2(x, y));
            }
        }
    }
    private int ColorToInt(Color color)
    {
        return ((int)(color.r * 255) << 24) |
               ((int)(color.g * 255) << 16) |
               ((int)(color.b * 255) << 8) |
               (int)(color.a * 255);
    }

    private Color IntToColor(int colorInt)
    {
        return new Color(((colorInt >> 24) & 0xFF) / 255f,
            ((colorInt >> 16) & 0xFF) / 255f,
            ((colorInt >> 8) & 0xFF) / 255f,
            (colorInt & 0xFF) / 255f
        );
    }
    
    private bool ColorsSimilar(int color1, int color2, float tolerance)
    {
        Color c1 = IntToColor(color1);
        Color c2 = IntToColor(color2);

        return math.abs(c1.r - c2.r) < tolerance &&
               math.abs(c1.g - c2.g) < tolerance &&
               math.abs(c1.b - c2.b) < tolerance;
    }
    
    #region 公共API
    public void AddRegionName(int2 position, string name)
    {
        if (regionNames.ContainsKey(position))
            regionNames[position] = name;
        else
            regionNames.TryAdd(position, name);
    }
    
    public void AddColorName(int color, string name)
    {
        if (colorToNameMap.ContainsKey(color))
            colorToNameMap[color] = name;
        else
            colorToNameMap.TryAdd(color, name);
    }
    
    public string GetRegionName(int2 position)
    {
        if (regionNames.ContainsKey(position))
            return regionNames[position].ToString();
        return "未命名区域";
    }
    
    public string GetColorName(int color)
    {
        if (colorToNameMap.ContainsKey(color))
            return colorToNameMap[color].ToString();
        return $"颜色 #{color:X8}";
    }
    
    public NativeList<AnnotationData> GetActiveAnnotations()
    {
        return activeAnnotations;
    }
    
    public void SetDisplaySettings(bool coordinates, bool regionNames, bool warnings, bool errors)
    {
        showCoordinates = coordinates;
        showRegionNames = regionNames;
        showWarnings = warnings;
        showErrors = errors;
    }
    
    public void ClearAnnotations()
    {
        activeAnnotations.Clear();
        conflictPixels.Clear();
        isolatedPixels.Clear();
        thinLinePixels.Clear();
        tripleJunctionPixels.Clear();
    }
    #endregion

    
    public void Dispose()
    {
        regionNames.Dispose();
        colorToNameMap.Dispose();
        activeAnnotations.Dispose();
        conflictPixels.Dispose();
        isolatedPixels.Dispose();
        thinLinePixels.Dispose();
        tripleJunctionPixels.Dispose();
        colorCount.Dispose();
        
        conflictBuffer?.Release();
        isolatedBuffer?.Release();
        thinLineBuffer?.Release();
        tripleJunctionBuffer?.Release();
    }
}