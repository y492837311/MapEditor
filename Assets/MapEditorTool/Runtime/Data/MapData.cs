using System;
using Unity.Collections;
using Unity.Mathematics;

[Serializable]
public struct MapData
{
    public int width;
    public int height;
    public NativeArray<int> colorData;
    public NativeArray<int> backupData;
    
    public MapData(int width, int height)
    {
        this.width = width;
        this.height = height;
        this.colorData = new NativeArray<int>(width * height, Allocator.Persistent);
        this.backupData = new NativeArray<int>(width * height, Allocator.Persistent);
    }
    
    public void Dispose()
    {
        if (colorData.IsCreated) colorData.Dispose();
        if (backupData.IsCreated) backupData.Dispose();
    }
}