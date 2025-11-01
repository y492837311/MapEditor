// Scripts/Runtime/MapDataAsset.cs
using UnityEngine;
using System.Collections.Generic;

namespace MapEditor
{
    [CreateAssetMenu(fileName = "MapData", menuName = "Map Editor/Map Data")]
    public class MapDataAsset : ScriptableObject
    {
        public int width = 4096;
        public int height = 2048;
        public Texture2D backgroundTexture;
        public List<ColorBlock> colorBlocks = new List<ColorBlock>();
        
        [System.NonSerialized]
        public Texture2D colorMapTexture;
        [System.NonSerialized]
        private int[,] blockIdMap;
        
        [System.NonSerialized]
        private bool isDirty = false;

        public Texture2D GetColorMapTexture()
        {
            if (colorMapTexture == null)
            {
                InitializeTexture();
            }
            return colorMapTexture;
        }
        
        private void InitializeBlockIdMap()
        {
            if (blockIdMap == null)
            {
                blockIdMap = new int[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        blockIdMap[x, y] = 0;
                    }
                }
            }
        }
        
        private void InitializeTexture()
        {
            colorMapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            colorMapTexture.filterMode = FilterMode.Point;
            colorMapTexture.wrapMode = TextureWrapMode.Clamp;
            
            // 初始化透明纹理
            Color32[] transparentPixels = new Color32[width * height];
            for (int i = 0; i < transparentPixels.Length; i++)
            {
                transparentPixels[i] = new Color32(0, 0, 0, 0);
            }
            colorMapTexture.SetPixels32(transparentPixels);
            colorMapTexture.Apply();
        }
        

        public void SetPixel(int x, int y, Color color, int blockId)
        {
            if (colorMapTexture == null) 
                InitializeTexture();
        
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            // 设置颜色
            colorMapTexture.SetPixel(x, y, color);
    
            // 设置 Block ID
            InitializeBlockIdMap();
            blockIdMap[x, y] = blockId;
            isDirty = true;
        }
        
        public Color32 GetPixel(int x, int y)
        {
            if (colorMapTexture == null) 
                return new Color32(0, 0, 0, 0);
                
            if (x < 0 || x >= width || y < 0 || y >= height)
                return new Color32(0, 0, 0, 0);
                
            return colorMapTexture.GetPixel(x, y);
        }
        
        /// <summary>
        /// 获取指定位置的 Block ID
        /// </summary>
        public int GetBlockId(int x, int y)
        {
            if (blockIdMap == null)
            {
                InitializeBlockIdMap();
            }
    
            if (x < 0 || x >= width || y < 0 || y >= height)
                return 0;
        
            return blockIdMap[x, y];
        }
        
        /// <summary>
        /// 设置 Block ID（不改变颜色）
        /// </summary>
        public void SetBlockId(int x, int y, int blockId)
        {
            InitializeBlockIdMap();
        
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;
            
            blockIdMap[x, y] = blockId;
        }
        
        /// <summary>
        /// 根据颜色查找对应的 Block ID
        /// </summary>
        public int FindBlockIdByColor(Color32 color)
        {
            foreach (var block in colorBlocks)
            {
                if (ColorsEqual(block.color, color))
                {
                    return block.id;
                }
            }
            return 0; // 没有找到对应的block
        }
        
        /// <summary>
        /// 根据 Block ID 获取颜色块信息
        /// </summary>
        public ColorBlock? GetColorBlock(int blockId)
        {
            foreach (var block in colorBlocks)
            {
                if (block.id == blockId)
                {
                    return block;
                }
            }
            return null;
        }
    
        private bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }
        
        /// <summary>
        /// 立即应用所有更改到纹理
        /// </summary>
        public void ApplyChangesImmediate()
        {
            if (colorMapTexture != null && isDirty)
            {
                colorMapTexture.Apply();
                isDirty = false;
            }
        }
        
        /// <summary>
        /// 标记为需要更新（用于批量操作）
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
        }
        
        public void ApplyChanges()
        {
            ApplyChangesImmediate();
        }
        
        /// <summary>
        /// 修复颜色比较方法 - 使用容差比较
        /// </summary>
        public static bool ColorsEqualWithTolerance(Color32 a, Color32 b, byte tolerance = 1)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance &&
                   Mathf.Abs(a.a - b.a) <= tolerance;
        }

        /// <summary>
        /// 修复颜色比较方法 - 用于撤销重做
        /// </summary>
        public static bool ColorsEqualForUndo(Color32 a, Color32 b)
        {
            // 直接比较整数值，避免任何浮点数问题
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }
        
        public static bool ColorsEqualForUndo(Color a, Color b)
        {
            // 转换为 Color32 进行比较，避免浮点数精度问题
            return ColorsEqualForUndo((Color32)a, (Color32)b);
        }
        
        [System.Serializable]
        public struct PixelOperation
        {
            public int x;
            public int y;
            public Color color;
            public int blockId;
        }

        /// <summary>
        /// 批量设置像素 - 提高性能并确保一致性
        /// </summary>
        public void SetPixelsBatch(List<PixelOperation> operations)
        {
            if (colorMapTexture == null) 
                InitializeTexture();
        
            InitializeBlockIdMap();

            bool hasChanges = false;
    
            foreach (var op in operations)
            {
                if (op.x < 0 || op.x >= width || op.y < 0 || op.y >= height)
                    continue;

                // 设置颜色
                colorMapTexture.SetPixel(op.x, op.y, op.color);
        
                // 设置 Block ID
                blockIdMap[op.x, op.y] = op.blockId;
                hasChanges = true;
            }
    
            if (hasChanges)
            {
                isDirty = true;
                ApplyChangesImmediate(); // 只应用一次
            }
        }

        /// <summary>
        /// 强制重新应用所有纹理更改
        /// </summary>
        public void ForceApplyChanges()
        {
            if (colorMapTexture != null)
            {
                colorMapTexture.Apply();
                isDirty = false;
            }
        }
        
        /// <summary>
        /// 调试方法：检查纹理状态
        /// </summary>
        public void DebugTextureState(string context)
        {
            if (colorMapTexture == null)
            {
                Debug.Log($"[{context}] Texture is null");
                return;
            }
    
            int nonTransparentPixels = 0;
            for (int y = 0; y < height; y += height / 10) // 抽样检查
            {
                for (int x = 0; x < width; x += width / 10)
                {
                    var pixel = GetPixel(x, y);
                    if (pixel.a > 0) nonTransparentPixels++;
                }
            }
            Debug.Log($"[{context}] Texture: {width}x{height}, Non-transparent pixels: ~{nonTransparentPixels}");
        }

        /// <summary>
        /// 验证像素操作
        /// </summary>
        public void ValidatePixelOperation(int x, int y, Color32 expectedColor, int expectedBlockId, string operation)
        {
            var actualColor = GetPixel(x, y);
            var actualBlockId = GetBlockId(x, y);
    
            bool colorMatch = ColorsEqualForUndo(actualColor, expectedColor);
            bool blockMatch = actualBlockId == expectedBlockId;
    
            if (!colorMatch || !blockMatch)
            {
                Debug.LogError($"[Validation Failed] {operation} at ({x},{y}): " +
                               $"Color: {actualColor} vs {expectedColor} (match: {colorMatch}), " +
                               $"Block: {actualBlockId} vs {expectedBlockId} (match: {blockMatch})");
            }
        }
    }
}