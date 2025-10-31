using UnityEngine;
using UnityEditor;
using System.IO;

namespace MapEditor
{
    public static class MapDataImporter
    {
        public static MapDataAsset ImportFromPNG(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError("PNG file not found: " + filePath);
                return null;
            }

            // 读取PNG文件
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            
            if (!texture.LoadImage(fileData))
            {
                Debug.LogError("Failed to load PNG image");
                return null;
            }

            // 创建MapDataAsset
            MapDataAsset mapData = ScriptableObject.CreateInstance<MapDataAsset>();
            mapData.width = texture.width;
            mapData.height = texture.height;
            mapData.name = Path.GetFileNameWithoutExtension(filePath);

            // 创建颜色映射纹理
            mapData.colorMapTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            mapData.colorMapTexture.filterMode = FilterMode.Point;
            mapData.colorMapTexture.wrapMode = TextureWrapMode.Clamp;

            // 复制像素数据
            Color32[] pixels = texture.GetPixels32();
            mapData.colorMapTexture.SetPixels32(pixels);
            mapData.colorMapTexture.Apply();

            // 分析颜色块
            AnalyzeColorBlocks(mapData, pixels);

            Debug.Log($"Successfully imported PNG: {texture.width}x{texture.height}");

            // 清理临时纹理
            Object.DestroyImmediate(texture);

            return mapData;
        }

        private static void AnalyzeColorBlocks(MapDataAsset mapData, Color32[] pixels)
        {
            mapData.colorBlocks.Clear();

            // 简单的颜色分析：找出所有不透明的独特颜色
            var uniqueColors = new System.Collections.Generic.Dictionary<Color32, int>();

            foreach (Color32 pixel in pixels)
            {
                if (pixel.a > 0) // 只考虑不透明像素
                {
                    if (!uniqueColors.ContainsKey(pixel))
                    {
                        uniqueColors[pixel] = 1;
                    }
                    else
                    {
                        uniqueColors[pixel]++;
                    }
                }
            }

            // 创建颜色块
            int blockId = 1;
            foreach (var colorEntry in uniqueColors)
            {
                if (colorEntry.Value > 10) // 忽略太小的颜色区域
                {
                    string blockName = $"Block_{blockId}";
                    mapData.colorBlocks.Add(new ColorBlock(blockId, colorEntry.Key, blockName));
                    blockId++;
                }
            }

            Debug.Log($"Found {mapData.colorBlocks.Count} color blocks");
        }

        public static MapDataAsset ImportFromConfig(string filePath)
        {
            // 这里实现从游戏配置文件导入的逻辑
            // 根据具体的配置文件格式进行解析
            
            Debug.Log("Config import not yet implemented");
            return null;
        }

        public static Texture2D ImportBackgroundImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError("Background image file not found: " + filePath);
                return null;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            
            if (texture.LoadImage(fileData))
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }

            Debug.LogError("Failed to load background image");
            return null;
        }
    }
}