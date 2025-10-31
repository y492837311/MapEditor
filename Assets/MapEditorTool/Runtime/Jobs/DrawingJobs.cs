using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

[BurstCompile]
public struct DrawCircleJob : IJobParallelFor
{
    public NativeArray<int> pixels;
    public int2 center;
    public int radius;
    public int color;
    public int width;
    public int height;
    
    public void Execute(int index)
    {
        int x = index % width;
        int y = index / width;
        
        int dx = x - center.x;
        int dy = y - center.y;
        int distanceSquared = dx * dx + dy * dy;
        
        if (distanceSquared <= radius * radius)
        {
            pixels[index] = color;
        }
    }
}

[BurstCompile]
public struct FloodFillJob : IJob
{
    public NativeArray<int> pixels;
    public int2 startPos;
    public int targetColor;
    public int fillColor;
    public int width;
    public int height;
    
    public void Execute()
    {
        if (startPos.x < 0 || startPos.x >= width || startPos.y < 0 || startPos.y >= height)
            return;
            
        int startIndex = startPos.y * width + startPos.x;
        int startPixelColor = pixels[startIndex];
        
        if (startPixelColor == fillColor) return;
        if (startPixelColor != targetColor) return;
        
        var stack = new NativeList<int2>(Allocator.Temp);
        stack.Add(startPos);
        
        while (stack.Length > 0)
        {
            int2 pos = stack[^1];
            stack.RemoveAt(stack.Length - 1);
            
            int index = pos.y * width + pos.x;
            if (pixels[index] != targetColor) continue;
            
            pixels[index] = fillColor;
            
            if (pos.x > 0) stack.Add(new int2(pos.x - 1, pos.y));
            if (pos.x < width - 1) stack.Add(new int2(pos.x + 1, pos.y));
            if (pos.y > 0) stack.Add(new int2(pos.x, pos.y - 1));
            if (pos.y < height - 1) stack.Add(new int2(pos.x, pos.y + 1));
        }
        
        stack.Dispose();
    }
}

[BurstCompile]
public struct ClearCanvasJob : IJobParallelFor
{
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> pixels;
    public int clearColor;
    
    public void Execute(int index)
    {
        pixels[index] = clearColor;
    }
}