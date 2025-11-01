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

            // 导入纹理到网格
            mapData.ImportFromTexture(texture);

            // 分析颜色块
            AnalyzeColorBlocks(mapData);

            Debug.Log($"Successfully imported PNG: {texture.width}x{texture.height}");

            // 清理临时纹理
            Object.DestroyImmediate(texture);

            return mapData;
        }

        private static void AnalyzeColorBlocks(MapDataAsset mapData)
        {
            mapData.colorBlocks.Clear();

            // 简单的颜色分析：找出所有不透明的独特颜色
            var uniqueColors = new System.Collections.Generic.Dictionary<Color32, int>();

            for (int y = 0; y < mapData.height; y += 10) // 抽样分析，提高性能
            {
                for (int x = 0; x < mapData.width; x += 10)
                {
                    var pixel = mapData.GetGridPixel(x, y);
                    if (pixel.color.a > 0) // 只考虑不透明像素
                    {
                        if (!uniqueColors.ContainsKey(pixel.color))
                        {
                            uniqueColors[pixel.color] = 1;
                        }
                        else
                        {
                            uniqueColors[pixel.color]++;
                        }
                    }
                }
            }

            // 创建颜色块
            int blockId = 1;
            foreach (var colorEntry in uniqueColors)
            {
                if (colorEntry.Value > 1) // 忽略太小的颜色区域
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