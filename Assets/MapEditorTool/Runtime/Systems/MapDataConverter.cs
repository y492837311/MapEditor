using System;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class MapDataConverter : IDisposable
{
    public MapConfigData ConvertToConfigData(NativeArray<int> colorData, int width, int height, string author = "Unknown")
    {
        var configData = new MapConfigData();
        
        configData.header = new MapHeader
        {
            version = MapHeader.CURRENT_VERSION,
            width = width,
            height = height,
            timestamp = DateTime.Now.Ticks,
            author = author,
            description = $"导出时间: {DateTime.Now}",
            colorCount = 0
        };
        
        var regions = ExtractColorRegions(colorData, width, height);
        configData.regions = regions.ToArray();
        configData.header.colorCount = regions.Count;
        
        configData.fullColorData = colorData.ToArray();
        
        return configData;
    }
    
    public NativeArray<int> ConvertToPixelData(MapConfigData configData, out int width, out int height)
    {
        width = configData.header.width;
        height = configData.header.height;
        int totalPixels = width * height;
        
        var pixelData = new NativeArray<int>(totalPixels, Allocator.Persistent);
        
        if (configData.fullColorData != null && configData.fullColorData.Length == totalPixels)
        {
            pixelData.CopyFrom(configData.fullColorData);
            return pixelData;
        }
        
        if (configData.regions != null)
        {
            foreach (var region in configData.regions)
            {
                if (region.pixels != null)
                {
                    foreach (var pixel in region.pixels)
                    {
                        if (pixel.x >= 0 && pixel.x < width && pixel.y >= 0 && pixel.y < height)
                        {
                            int index = pixel.y * width + pixel.x;
                            pixelData[index] = region.color;
                        }
                    }
                }
            }
        }
        
        return pixelData;
    }
    
    private List<ColorRegion> ExtractColorRegions(NativeArray<int> colorData, int width, int height)
    {
        var regions = new List<ColorRegion>();
        var visited = new NativeArray<bool>(colorData.Length, Allocator.Temp);
        
        for (int i = 0; i < colorData.Length; i++)
        {
            if (visited[i] || colorData[i] == 0)
                continue;
                
            int color = colorData[i];
            var regionPixels = FloodFillRegion(colorData, visited, i, color, width, height);
            
            if (regionPixels.Count > 0)
            {
                regions.Add(new ColorRegion
                {
                    color = color,
                    pixels = regionPixels.ToArray(),
                    regionName = $"Region_{regions.Count + 1}",
                    configId = regions.Count + 1
                });
            }
        }
        
        visited.Dispose();
        return regions;
    }
    
    private List<int2> FloodFillRegion(NativeArray<int> colorData, NativeArray<bool> visited, 
                                     int startIndex, int targetColor, int width, int height)
    {
        var pixels = new List<int2>();
        var stack = new Stack<int>();
        stack.Push(startIndex);
        
        while (stack.Count > 0)
        {
            int index = stack.Pop();
            
            if (visited[index] || colorData[index] != targetColor)
                continue;
                
            visited[index] = true;
            int x = index % width;
            int y = index / width;
            pixels.Add(new int2(x, y));
            
            if (x > 0) stack.Push(index - 1);
            if (x < width - 1) stack.Push(index + 1);
            if (y > 0) stack.Push(index - width);
            if (y < height - 1) stack.Push(index + width);
        }
        
        return pixels;
    }
    
    public void Dispose() { }
}