using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct MapHeader
{
    public const int CURRENT_VERSION = 2;
    
    public int version;
    public int width;
    public int height;
    public long timestamp;
    public string author;
    public string description;
    public int colorCount;
}

[Serializable]
public struct ColorRegion
{
    public int color;
    public int2[] pixels;
    public string regionName;
    public int configId;
}

[Serializable]
public class MapConfigData
{
    public MapHeader header;
    public ColorRegion[] regions;
    public int[] fullColorData;
    
    public string[] colorPalette;
    public Vector2[] importantPoints;
    public string[] regionNames;
}

public enum FileFormat
{
    Binary,
    Json,
    PNG,
    Legacy
}

[Serializable]
public struct PNGExportOptions
{
    public int resolution;
    public bool transparentBackground;
    public bool includeGrid;
    public bool includeLabels;
    public bool includeRegionNames;
}