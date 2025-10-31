using System;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class MapFileSerializer : IDisposable
{
    private const string BINARY_SIGNATURE = "MAPEDIT";
    private const int BINARY_VERSION = 2;
    
    public MapConfigData ImportFile(string filePath, FileFormat format)
    {
        try
        {
            switch (format)
            {
                case FileFormat.Binary: return ImportBinary(filePath);
                case FileFormat.Json: return ImportJson(filePath);
                case FileFormat.Legacy: return ImportLegacy(filePath);
                default: throw new NotSupportedException($"不支持的格式: {format}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"导入文件失败: {e.Message}");
            return null;
        }
    }
    
    public bool ExportFile(MapConfigData data, string filePath, FileFormat format, bool backupOriginal = true)
    {
        try
        {
            if (backupOriginal && File.Exists(filePath))
            {
                string backupPath = GetBackupPath(filePath);
                File.Copy(filePath, backupPath, true);
                Debug.Log($"已创建备份: {backupPath}");
            }
            
            switch (format)
            {
                case FileFormat.Binary: return ExportBinary(data, filePath);
                case FileFormat.Json: return ExportJson(data, filePath);
                case FileFormat.PNG: return ExportPNG(data, filePath);
                case FileFormat.Legacy: return ExportLegacy(data, filePath);
                default: throw new NotSupportedException($"不支持的格式: {format}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"导出文件失败: {e.Message}");
            return false;
        }
    }
    
    #region 二进制格式
    private MapConfigData ImportBinary(string filePath)
    {
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(stream))
        {
            string signature = Encoding.ASCII.GetString(reader.ReadBytes(7));
            if (signature != BINARY_SIGNATURE)
                throw new InvalidDataException("无效的二进制文件格式");
            
            int version = reader.ReadInt32();
            if (version > BINARY_VERSION)
                throw new InvalidDataException($"不支持的版本: {version}");
            
            var data = new MapConfigData();
            data.header = new MapHeader
            {
                version = version,
                width = reader.ReadInt32(),
                height = reader.ReadInt32(),
                timestamp = reader.ReadInt64(),
                author = ReadString(reader),
                description = ReadString(reader),
                colorCount = reader.ReadInt32()
            };
            
            int regionCount = reader.ReadInt32();
            data.regions = new ColorRegion[regionCount];
            
            for (int i = 0; i < regionCount; i++)
            {
                var region = new ColorRegion
                {
                    color = reader.ReadInt32(),
                    regionName = ReadString(reader),
                    configId = reader.ReadInt32()
                };
                
                int pixelCount = reader.ReadInt32();
                region.pixels = new int2[pixelCount];
                
                for (int j = 0; j < pixelCount; j++)
                {
                    region.pixels[j] = new int2(reader.ReadInt32(), reader.ReadInt32());
                }
                
                data.regions[i] = region;
            }
            
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                int dataLength = reader.ReadInt32();
                data.fullColorData = new int[dataLength];
                for (int i = 0; i < dataLength; i++)
                {
                    data.fullColorData[i] = reader.ReadInt32();
                }
            }
            
            return data;
        }
    }
    
    private bool ExportBinary(MapConfigData data, string filePath)
    {
        using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes(BINARY_SIGNATURE));
            writer.Write(BINARY_VERSION);
            
            writer.Write(data.header.width);
            writer.Write(data.header.height);
            writer.Write(DateTime.Now.Ticks);
            WriteString(writer, data.header.author ?? "Unknown");
            WriteString(writer, data.header.description ?? "");
            writer.Write(data.header.colorCount);
            
            writer.Write(data.regions?.Length ?? 0);
            if (data.regions != null)
            {
                foreach (var region in data.regions)
                {
                    writer.Write(region.color);
                    WriteString(writer, region.regionName ?? "");
                    writer.Write(region.configId);
                    
                    writer.Write(region.pixels?.Length ?? 0);
                    if (region.pixels != null)
                    {
                        foreach (var pixel in region.pixels)
                        {
                            writer.Write(pixel.x);
                            writer.Write(pixel.y);
                        }
                    }
                }
            }
            
            if (data.fullColorData != null)
            {
                writer.Write(data.fullColorData.Length);
                foreach (int color in data.fullColorData)
                {
                    writer.Write(color);
                }
            }
            
            return true;
        }
    }
    #endregion
    
    #region JSON格式
    private MapConfigData ImportJson(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonUtility.FromJson<MapConfigData>(json);
    }
    
    private bool ExportJson(MapConfigData data, string filePath)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(filePath, json);
        return true;
    }
    #endregion
    
    #region 游戏原有格式
    private MapConfigData ImportLegacy(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        var data = new MapConfigData();
        var regions = new System.Collections.Generic.List<ColorRegion>();
        
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
                
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                var region = new ColorRegion
                {
                    color = int.Parse(parts[0]),
                    regionName = parts[1],
                    configId = int.Parse(parts[2])
                };
                
                var pixels = new System.Collections.Generic.List<int2>();
                for (int i = 3; i < parts.Length; i += 2)
                {
                    if (i + 1 < parts.Length)
                    {
                        pixels.Add(new int2(int.Parse(parts[i]), int.Parse(parts[i + 1])));
                    }
                }
                region.pixels = pixels.ToArray();
                
                regions.Add(region);
            }
        }
        
        data.regions = regions.ToArray();
        data.header = new MapHeader
        {
            version = 1,
            width = 1024,
            height = 1024,
            timestamp = DateTime.Now.Ticks,
            colorCount = regions.Count
        };
        
        return data;
    }
    
    private bool ExportLegacy(MapConfigData data, string filePath)
    {
        using (var writer = new StreamWriter(filePath))
        {
            writer.WriteLine("# 地图色块配置 - 导出时间: " + DateTime.Now);
            
            if (data.regions != null)
            {
                foreach (var region in data.regions)
                {
                    var line = new System.Text.StringBuilder();
                    line.Append(region.color);
                    line.Append(",");
                    line.Append(region.regionName);
                    line.Append(",");
                    line.Append(region.configId);
                    
                    if (region.pixels != null)
                    {
                        foreach (var pixel in region.pixels)
                        {
                            line.Append(",");
                            line.Append(pixel.x);
                            line.Append(",");
                            line.Append(pixel.y);
                        }
                    }
                    
                    writer.WriteLine(line.ToString());
                }
            }
        }
        
        return true;
    }
    #endregion
    
    #region PNG导出
    public bool ExportPNG(MapConfigData data, string filePath)
    {
        return ExportPNG(data, filePath, 2048, true);
    }
    
    public bool ExportPNG(MapConfigData data, string filePath, int resolution, bool transparentBackground)
    {
        if (data.fullColorData == null)
        {
            Debug.LogWarning("无法导出PNG：没有完整的像素数据");
            return false;
        }
        
        int width = data.header.width;
        int height = data.header.height;
        
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var colors = new Color32[width * height];
        
        for (int i = 0; i < data.fullColorData.Length; i++)
        {
            colors[i] = IntToColor32(data.fullColorData[i]);
        }
        
        texture.SetPixels32(colors);
        texture.Apply();
        
        if (resolution != width)
        {
            texture = ScaleTexture(texture, resolution, resolution);
        }
        
        byte[] pngData = texture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);
        
        UnityEngine.Object.DestroyImmediate(texture);
        return true;
    }
    
    private Texture2D ScaleTexture(Texture2D source, int newWidth, int newHeight)
    {
        var scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                float xRatio = (float)x / newWidth;
                float yRatio = (float)y / newHeight;
                Color color = source.GetPixelBilinear(xRatio, yRatio);
                scaled.SetPixel(x, y, color);
            }
        }
        
        scaled.Apply();
        return scaled;
    }
    #endregion
    
    #region 工具方法
    private string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length == 0) return string.Empty;
        byte[] bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
    
    private void WriteString(BinaryWriter writer, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            writer.Write(0);
            return;
        }
        
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
    
    private Color32 IntToColor32(int colorInt)
    {
        return new Color32(
            (byte)((colorInt >> 24) & 0xFF),
            (byte)((colorInt >> 16) & 0xFF),
            (byte)((colorInt >> 8) & 0xFF),
            (byte)(colorInt & 0xFF)
        );
    }
    
    private string GetBackupPath(string originalPath)
    {
        string directory = Path.GetDirectoryName(originalPath);
        string fileName = Path.GetFileNameWithoutExtension(originalPath);
        string extension = Path.GetExtension(originalPath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        return Path.Combine(directory, $"{fileName}_backup_{timestamp}{extension}");
    }
    #endregion
    
    public void Dispose() { }
}