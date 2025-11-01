using UnityEngine;
using System.Collections.Generic;
using System;

namespace MapEditor
{
    [CreateAssetMenu(fileName = "MapData", menuName = "Map Editor/Map Data")]
    public class MapDataAsset : ScriptableObject
    {
        [System.Serializable]
        public struct GridPixel
        {
            public Color32 color;
            public int blockId;
            
            public GridPixel(Color32 color, int blockId)
            {
                this.color = color;
                this.blockId = blockId;
            }
            
            public bool IsEmpty => color.a == 0;
            
            public bool Equals(GridPixel other)
            {
                return color.r == other.color.r && 
                       color.g == other.color.g && 
                       color.b == other.color.b && 
                       color.a == other.color.a && 
                       blockId == other.blockId;
            }
        }

        [System.Serializable]
        public struct PixelOperation
        {
            public int x;
            public int y;
            public Color32 color;
            public int blockId;
            
            public PixelOperation(int x, int y, Color32 color, int blockId)
            {
                this.x = x;
                this.y = y;
                this.color = color;
                this.blockId = blockId;
            }
        }

        [Header("Map Settings")]
        public int width = 1024;
        public int height = 512;
        public Texture2D backgroundTexture;
        public List<ColorBlock> colorBlocks = new List<ColorBlock>();
        
        // 核心网格数据
        [System.NonSerialized]
        private GridPixel[,] pixelGrid;
        
        // 渲染纹理
        [System.NonSerialized]
        private Texture2D renderTexture;
        private bool isTextureDirty = true;

        // 初始化网格
        public void InitializeGrid()
        {
            if (pixelGrid == null || pixelGrid.GetLength(0) != width || pixelGrid.GetLength(1) != height)
            {
                pixelGrid = new GridPixel[width, height];
                ClearGrid();
                isTextureDirty = true;
            }
        }

        // 清空网格
        public void ClearGrid()
        {
            if (pixelGrid == null) InitializeGrid();
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixelGrid[x, y] = new GridPixel(new Color32(0, 0, 0, 0), 0);
                }
            }
            isTextureDirty = true;
        }

        // 获取网格像素
        public GridPixel GetGridPixel(int x, int y)
        {
            if (pixelGrid == null) InitializeGrid();
            
            if (x < 0 || x >= width || y < 0 || y >= height)
                return new GridPixel(new Color32(0, 0, 0, 0), 0);
                
            return pixelGrid[x, y];
        }

        // 设置网格像素
        public void SetGridPixel(int x, int y, Color32 color, int blockId)
        {
            if (pixelGrid == null) InitializeGrid();
            
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            var newPixel = new GridPixel(color, blockId);
            if (!pixelGrid[x, y].Equals(newPixel))
            {
                pixelGrid[x, y] = newPixel;
                isTextureDirty = true;
            }
        }

        /// <summary>
        /// 批量设置网格像素 - 超高性能版本
        /// </summary>
        public void SetGridPixelsBatch(List<PixelOperation> operations)
        {
            if (pixelGrid == null) InitializeGrid();
    
            bool changed = false;
            int operationCount = operations.Count;
    
            // 使用快速循环
            for (int i = 0; i < operationCount; i++)
            {
                var op = operations[i];
                if (op.x >= 0 && op.x < width && op.y >= 0 && op.y < height)
                {
                    var newPixel = new GridPixel(op.color, op.blockId);
                    ref var currentPixel = ref pixelGrid[op.x, op.y]; // 使用ref避免拷贝
            
                    // 超快速比较
                    if (currentPixel.color.r != newPixel.color.r ||
                        currentPixel.color.g != newPixel.color.g ||
                        currentPixel.color.b != newPixel.color.b ||
                        currentPixel.color.a != newPixel.color.a ||
                        currentPixel.blockId != newPixel.blockId)
                    {
                        currentPixel = newPixel;
                        changed = true;
                    }
                }
            }
    
            // 延迟纹理更新 - 关键优化！
            if (changed) 
            {
                isTextureDirty = true;
                // 不立即更新纹理，等待渲染时更新
            }
        }

        // 获取渲染纹理
        public Texture2D GetRenderTexture()
        {
            if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
            {
                if (renderTexture != null)
                    Texture2D.DestroyImmediate(renderTexture);
                    
                renderTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                renderTexture.filterMode = FilterMode.Point;
                renderTexture.wrapMode = TextureWrapMode.Clamp;
                isTextureDirty = true;
            }
            
            if (isTextureDirty)
            {
                RegenerateTexture();
            }
            
            return renderTexture;
        }

        // 重新生成纹理
        private void RegenerateTexture()
        {
            if (pixelGrid == null || renderTexture == null) return;
            
            Color32[] pixels = new Color32[width * height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    pixels[index] = pixelGrid[x, y].color;
                }
            }
            
            renderTexture.SetPixels32(pixels);
            renderTexture.Apply();
            isTextureDirty = false;
        }

        // 从纹理导入到网格
        public void ImportFromTexture(Texture2D sourceTexture)
        {
            if (sourceTexture == null) return;
            
            InitializeGrid();
            
            // 创建临时纹理来确保尺寸匹配
            Texture2D resizedTexture = sourceTexture;
            if (sourceTexture.width != width || sourceTexture.height != height)
            {
                Debug.Log($"Resizing texture from {sourceTexture.width}x{sourceTexture.height} to {width}x{height}");
                resizedTexture = ResizeTexture(sourceTexture, width, height);
            }
            
            Color32[] sourcePixels = resizedTexture.GetPixels32();
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    pixelGrid[x, y] = new GridPixel(sourcePixels[index], 0);
                }
            }
            
            isTextureDirty = true;
            
            // 清理临时纹理
            if (resizedTexture != sourceTexture)
            {
                Texture2D.DestroyImmediate(resizedTexture);
            }
        }

        // 纹理缩放
        private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    float u = (float)x / newWidth;
                    float v = (float)y / newHeight;
                    result.SetPixel(x, y, source.GetPixelBilinear(u, v));
                }
            }
            result.Apply();
            return result;
        }

        // 创建网格快照
        public GridPixel[,] CreateGridSnapshot()
        {
            if (pixelGrid == null) InitializeGrid();
            
            var snapshot = new GridPixel[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    snapshot[x, y] = pixelGrid[x, y];
                }
            }
            return snapshot;
        }

        // 从快照恢复网格
        public void RestoreGridFromSnapshot(GridPixel[,] snapshot)
        {
            if (snapshot == null || snapshot.GetLength(0) != width || snapshot.GetLength(1) != height)
            {
                Debug.LogError("Grid snapshot size mismatch");
                return;
            }
                
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixelGrid[x, y] = snapshot[x, y];
                }
            }
            isTextureDirty = true;
        }

        // 工具方法
        public int FindBlockIdByColor(Color32 color)
        {
            foreach (var block in colorBlocks)
            {
                if (ColorsEqual(block.color, color))
                {
                    return block.id;
                }
            }
            return 0;
        }
        
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

        // 强制刷新纹理
        public void ForceTextureRefresh()
        {
            if (isTextureDirty)
            {
                RegenerateTexture();
            }
        }
    }
}