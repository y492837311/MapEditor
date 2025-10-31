// Editor/UI/IconGenerator.cs
using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public static class IconGenerator
    {
        public static Texture2D CreatePencilIcon(int size = 16)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[size * size];
            
            // 创建铅笔形状
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color color = Color.clear;
                    
                    // 铅笔尖形状
                    if (x >= size/4 && x < size*3/4 && y >= size/4 && y < size*3/4)
                    {
                        color = Color.red;
                    }
                    
                    pixels[y * size + x] = color;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        public static Texture2D CreateBucketIcon(int size = 16)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[size * size];
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color color = Color.clear;
                    
                    // 油漆桶形状
                    if (y > size/2 && x > size/4 && x < size*3/4)
                    {
                        color = Color.blue;
                    }
                    else if (y == size/2 && x >= size/4 && x < size*3/4)
                    {
                        color = Color.blue;
                    }
                    
                    pixels[y * size + x] = color;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        public static Texture2D CreateEraserIcon(int size = 16)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[size * size];
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color color = Color.clear;
                    
                    // 橡皮擦形状 - 矩形
                    if (x >= size/4 && x < size*3/4 && y >= size/4 && y < size*3/4)
                    {
                        color = Color.gray;
                    }
                    
                    pixels[y * size + x] = color;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        public static Texture2D CreateEyedropperIcon(int size = 16)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[size * size];
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color color = Color.clear;
                    
                    // 取色器形状
                    if ((x == size/2 && y >= size/4) || 
                        (y == size/4 && x >= size/4 && x <= size*3/4))
                    {
                        color = Color.green;
                    }
                    
                    pixels[y * size + x] = color;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        public static Texture2D CreateDefaultIcon(Color color, int size = 16)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[size * size];
            
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}