using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

public class ComputeShaderManager : System.IDisposable
{
    private ComputeShader computeShader;
    private ComputeBuffer colorBuffer;
    private ComputeBuffer conflictBuffer;
    private ComputeBuffer isolatedPixelsBuffer;
    
    private int floodFillKernel;
    private int colorConflictKernel;
    private int isolatedPixelsKernel;
    
    private NativeArray<int> colorData;
    private NativeArray<int> conflictResults;
    private NativeArray<int2> isolatedPixels;
    
    public ComputeShaderManager(ComputeShader shader, int width, int height)
    {
        computeShader = shader;
        
        floodFillKernel = computeShader.FindKernel("FloodFill");
        colorConflictKernel = computeShader.FindKernel("CheckColorConflicts");
        isolatedPixelsKernel = computeShader.FindKernel("FindIsolatedPixels");
        
        int pixelCount = width * height;
        colorBuffer = new ComputeBuffer(pixelCount, sizeof(int));
        conflictBuffer = new ComputeBuffer(256, sizeof(int));
        isolatedPixelsBuffer = new ComputeBuffer(pixelCount, sizeof(int) * 2);
        
        colorData = new NativeArray<int>(pixelCount, Allocator.Persistent);
        conflictResults = new NativeArray<int>(256, Allocator.Persistent);
        isolatedPixels = new NativeArray<int2>(pixelCount, Allocator.Persistent);
    }
    
    public void FloodFill(int2 startPos, int targetColor, int fillColor, int width, int height, float tolerance = 0.01f)
    {
        computeShader.SetInts("StartPos", startPos.x, startPos.y);
        computeShader.SetVector("TargetColor", IntToColor(targetColor));
        computeShader.SetVector("FillColor", IntToColor(fillColor));
        computeShader.SetInt("Width", width);
        computeShader.SetInt("Height", height);
        computeShader.SetFloat("Tolerance", tolerance);
        
        computeShader.SetBuffer(floodFillKernel, "Result", colorBuffer);
        computeShader.SetBuffer(floodFillKernel, "SourceTexture", colorBuffer);
        
        int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
        computeShader.Dispatch(floodFillKernel, threadGroupsX, threadGroupsY, 1);
        
        colorBuffer.GetData(colorData.ToArray());
    }
    
    public NativeArray<int> CheckColorConflicts(NativeArray<int> colors, NativeArray<float4> colorHistory)
    {
        ComputeBuffer historyBuffer = new ComputeBuffer(colorHistory.Length, sizeof(float) * 4);
        historyBuffer.SetData(colorHistory);
        
        computeShader.SetBuffer(colorConflictKernel, "ColorConflicts", conflictBuffer);
        computeShader.SetBuffer(colorConflictKernel, "ColorHistory", historyBuffer);
        
        computeShader.Dispatch(colorConflictKernel, 4, 1, 1);
        
        conflictBuffer.GetData(conflictResults.ToArray());
        historyBuffer.Release();
        
        return conflictResults;
    }
    
    public NativeArray<int2> FindIsolatedPixels(NativeArray<int> colors, int width, int height)
    {
        colorBuffer.SetData(colors);
        
        computeShader.SetBuffer(isolatedPixelsKernel, "IsolatedPixels", isolatedPixelsBuffer);
        computeShader.SetBuffer(isolatedPixelsKernel, "SourceTexture", colorBuffer);
        computeShader.SetInt("Width", width);
        computeShader.SetInt("Height", height);
        
        int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
        computeShader.Dispatch(isolatedPixelsKernel, threadGroupsX, threadGroupsY, 1);
        
        isolatedPixelsBuffer.GetData(isolatedPixels.ToArray());
        
        return isolatedPixels;
    }
    
    public NativeArray<int> GetColorData()
    {
        return colorData;
    }
    
    public void SetColorData(NativeArray<int> data)
    {
        colorData.CopyFrom(data);
        colorBuffer.SetData(colorData);
    }
    
    private Color IntToColor(int colorInt)
    {
        return new Color(
            ((colorInt >> 24) & 0xFF) / 255f,
            ((colorInt >> 16) & 0xFF) / 255f,
            ((colorInt >> 8) & 0xFF) / 255f,
            (colorInt & 0xFF) / 255f
        );
    }
    
    public void Dispose()
    {
        colorBuffer?.Release();
        conflictBuffer?.Release();
        isolatedPixelsBuffer?.Release();
        
        if (colorData.IsCreated) colorData.Dispose();
        if (conflictResults.IsCreated) conflictResults.Dispose();
        if (isolatedPixels.IsCreated) isolatedPixels.Dispose();
    }
}