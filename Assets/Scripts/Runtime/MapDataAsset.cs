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
        private bool isDirty = false;

        public Texture2D GetColorMapTexture()
        {
            if (colorMapTexture == null)
            {
                InitializeTexture();
            }
            return colorMapTexture;
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

            colorMapTexture.SetPixel(x, y, color);
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
    }
}