using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapEditor
{
    /// <summary>
    /// 地图数据处理器 - 使用Burst Compiler和Job System进行高性能地图处理
    /// </summary>
    public class MapDataProcessor : MonoBehaviour
    {
        [BurstCompile]
        struct ProcessMapDataJob : IJob
        {
            public NativeArray<Color32> mapData;
            public int mapWidth;
            public int mapHeight;
            public int2 position;
            public Color32 color;
            public int brushSize;
            public int operationType; // 0=绘制, 1=擦除, 2=填充
            
            public void Execute()
            {
                switch (operationType)
                {
                    case 0: // 绘制
                        DrawCircle(position.x, position.y);
                        break;
                    case 1: // 擦除
                        EraseCircle(position.x, position.y);
                        break;
                    case 2: // 填充
                        FloodFill(position.x, position.y, color);
                        break;
                }
            }
            
            void DrawCircle(int centerX, int centerY)
            {
                int radius = brushSize / 2;
                
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y <= radius * radius) // 圆形笔刷
                        {
                            int pixelX = centerX + x;
                            int pixelY = centerY + y;
                            
                            if (pixelX >= 0 && pixelX < mapWidth && pixelY >= 0 && pixelY < mapHeight)
                            {
                                int index = pixelY * mapWidth + pixelX;
                                mapData[index] = color;
                            }
                        }
                    }
                }
            }
            
            void EraseCircle(int centerX, int centerY)
            {
                int radius = brushSize / 2;
                
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y <= radius * radius) // 圆形笔刷
                        {
                            int pixelX = centerX + x;
                            int pixelY = centerY + y;
                            
                            if (pixelX >= 0 && pixelX < mapWidth && pixelY >= 0 && pixelY < mapHeight)
                            {
                                int index = pixelY * mapWidth + pixelX;
                                mapData[index] = new Color32(0, 0, 0, 0); // 透明色
                            }
                        }
                    }
                }
            }
            
            // 简化的Flood Fill实现，实际应用中可能需要更复杂的算法
            void FloodFill(int startX, int startY, Color32 newColor)
            {
                // 这里使用简化的实现，实际的Flood Fill需要更复杂的算法
                // 由于Job中不能使用递归或复杂的数据结构，我们使用简单的近似实现
                int index = startY * mapWidth + startX;
                Color32 originalColor = mapData[index];
                
                // 如果点击的颜色和目标颜色相同，则不执行操作
                if (originalColor.r == newColor.r && 
                    originalColor.g == newColor.g && 
                    originalColor.b == newColor.b && 
                    originalColor.a == newColor.a)
                    return;
                
                // 简单的区域填充 - 实际应用中需要完整的Flood Fill算法
                for (int y = 0; y < mapHeight; y++)
                {
                    for (int x = 0; x < mapWidth; x++)
                    {
                        int idx = y * mapWidth + x;
                        if (mapData[idx].r == originalColor.r && 
                            mapData[idx].g == originalColor.g && 
                            mapData[idx].b == originalColor.b && 
                            mapData[idx].a == originalColor.a)
                        {
                            mapData[idx] = newColor;
                        }
                    }
                }
            }
        }
        
        [BurstCompile]
        struct DetectPixelErrorsJob : IJob
        {
            [ReadOnly] public NativeArray<Color32> mapData;
            public int mapWidth;
            public int mapHeight;
            public NativeList<int2> isolatedPixels;
            public NativeList<int2> threeColorIntersections;
            public NativeList<int2> singlePixelLines;
            
            public void Execute()
            {
                isolatedPixels.Clear();
                threeColorIntersections.Clear();
                singlePixelLines.Clear();
                
                for (int y = 1; y < mapHeight - 1; y++)
                {
                    for (int x = 1; x < mapWidth - 1; x++)
                    {
                        int currentIndex = y * mapWidth + x;
                        Color32 currentColor = mapData[currentIndex];
                        
                        // 跳过透明像素
                        if (currentColor.a == 0) continue;
                        
                        // 检查孤立像素
                        CheckIsolatedPixel(x, y, currentColor);
                        
                        // 检查三色交点
                        CheckThreeColorIntersection(x, y, currentColor);
                        
                        // 检查单像素线
                        CheckSinglePixelLine(x, y, currentColor);
                    }
                }
            }
            
            void CheckIsolatedPixel(int x, int y, Color32 currentColor)
            {
                int currentIndex = y * mapWidth + x;
                
                // 检查周围8个像素是否都不同
                bool allDifferent = true;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue; // 跳过中心像素
                        
                        int nx = x + dx;
                        int ny = y + dy;
                        
                        if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                        {
                            int neighborIndex = ny * mapWidth + nx;
                            Color32 neighborColor = mapData[neighborIndex];
                            
                            // 如果相邻像素颜色相同，则不是孤立像素
                            if (currentColor.r == neighborColor.r && 
                                currentColor.g == neighborColor.g && 
                                currentColor.b == neighborColor.b && 
                                currentColor.a == neighborColor.a)
                            {
                                allDifferent = false;
                                break;
                            }
                        }
                    }
                    if (!allDifferent) break;
                }
                
                if (allDifferent)
                {
                    isolatedPixels.Add(new int2(x, y));
                }
            }
            
            void CheckThreeColorIntersection(int x, int y, Color32 currentColor)
            {
                // 检查3x3区域内的颜色数量
                var colors = new NativeHashSet<uint>(9, Allocator.Temp);
                
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        
                        if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                        {
                            int index = ny * mapWidth + nx;
                            Color32 color = mapData[index];
                            
                            // 将颜色编码为uint作为哈希集的键
                            uint colorKey = (uint)(color.r << 24 | color.g << 16 | color.b << 8 | color.a);
                            colors.Add(colorKey);
                        }
                    }
                }
                
                // 如果3x3区域内有3种或更多颜色，则标记为三色交点
                if (colors.Count >= 3)
                {
                    threeColorIntersections.Add(new int2(x, y));
                }
                
                colors.Dispose();
            }
            
            void CheckSinglePixelLine(int x, int y, Color32 currentColor)
            {
                int currentIndex = y * mapWidth + x;
                
                // 检查是否是单像素宽的连接线
                // 检查水平和垂直方向的连接情况
                int horizontalConnections = 0;
                int verticalConnections = 0;
                
                // 检查水平方向
                if (x > 0 && x < mapWidth - 1)
                {
                    Color32 leftColor = mapData[y * mapWidth + (x - 1)];
                    Color32 rightColor = mapData[y * mapWidth + (x + 1)];
                    
                    if (HasSameColor(currentColor, leftColor)) horizontalConnections++;
                    if (HasSameColor(currentColor, rightColor)) horizontalConnections++;
                }
                
                // 检查垂直方向
                if (y > 0 && y < mapHeight - 1)
                {
                    Color32 topColor = mapData[(y - 1) * mapWidth + x];
                    Color32 bottomColor = mapData[(y + 1) * mapWidth + x];
                    
                    if (HasSameColor(currentColor, topColor)) verticalConnections++;
                    if (HasSameColor(currentColor, bottomColor)) verticalConnections++;
                }
                
                // 如果只在一个方向上有连接（或没有连接），则可能是单像素线
                if ((horizontalConnections == 1 && verticalConnections == 0) || 
                    (horizontalConnections == 0 && verticalConnections == 1))
                {
                    singlePixelLines.Add(new int2(x, y));
                }
            }
            
            bool HasSameColor(Color32 a, Color32 b)
            {
                return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
            }
        }
        
        [BurstCompile]
        struct ColorConflictDetectionJob : IJob
        {
            [ReadOnly] public NativeArray<Color32> mapData;
            public NativeParallelHashSet<float3> conflictColors;
            
            public void Execute()
            {
                // 简化的颜色冲突检测
                // 实际应用中可能需要更复杂的算法
                var colorSet = new NativeHashSet<uint>(mapData.Length, Allocator.Temp);
                
                for (int i = 0; i < mapData.Length; i++)
                {
                    Color32 color = mapData[i];
                    
                    // 跳过透明像素
                    if (color.a == 0) continue;
                    
                    uint colorKey = (uint)(color.r << 24 | color.g << 16 | color.b << 8 | color.a);
                    
                    // 检查是否有相似颜色
                    bool isConflict = false;
                    foreach (uint existingColorKey in colorSet)
                    {
                        Color32 existingColor = new Color32(
                            (byte)(existingColorKey >> 24),
                            (byte)((existingColorKey >> 16) & 0xFF),
                            (byte)((existingColorKey >> 8) & 0xFF),
                            (byte)(existingColorKey & 0xFF)
                        );
                        
                        // 计算颜色差异
                        float diff = Mathf.Sqrt(
                            Mathf.Pow(color.r - existingColor.r, 2) +
                            Mathf.Pow(color.g - existingColor.g, 2) +
                            Mathf.Pow(color.b - existingColor.b, 2) +
                            Mathf.Pow(color.a - existingColor.a, 2)
                        );
                        
                        if (diff < 30.0f) // 颜色差异阈值
                        {
                            isConflict = true;
                            conflictColors.Add(new float3(color.r, color.g, color.b));
                            conflictColors.Add(new float3(existingColor.r, existingColor.g, existingColor.b));
                        }
                    }
                    
                    if (!isConflict)
                    {
                        colorSet.Add(colorKey);
                    }
                }
                
                colorSet.Dispose();
            }
        }
        
        // 引用地图数据管理器
        private MapDataManager mapDataManager;
        private NativeArray<Color32> nativeMapData;
        private JobHandle processJobHandle;
        
        void Start()
        {
            mapDataManager = GetComponent<MapDataManager>();
            if (mapDataManager == null)
            {
                Debug.LogError("MapDataProcessor需要MapDataManager组件");
                return;
            }
            
            // 初始化NativeArray
            InitializeNativeData();
        }
        
        void Update()
        {
            // 完成处理作业
            if (processJobHandle.IsCompleted)
            {
                processJobHandle.Complete();
                
                // 将NativeArray数据复制回普通数组
                Color32[] managedData = new Color32[nativeMapData.Length];
                nativeMapData.CopyTo(managedData);
                
                // 更新地图数据管理器
                mapDataManager.SetMapData(managedData);
                
                // 重新初始化NativeArray以供下次使用
                InitializeNativeData();
            }
        }
        
        void OnDestroy()
        {
            if (nativeMapData.IsCreated)
            {
                nativeMapData.Dispose();
            }
        }
        
        /// <summary>
        /// 初始化NativeArray数据
        /// </summary>
        void InitializeNativeData()
        {
            if (nativeMapData.IsCreated)
            {
                nativeMapData.Dispose();
            }
            
            Color32[] managedData = mapDataManager.GetMapData();
            nativeMapData = new NativeArray<Color32>(managedData, Allocator.Persistent);
        }
        
        /// <summary>
        /// 执行地图绘制操作
        /// </summary>
        public void DrawAtPosition(int x, int y, Color32 color, int brushSize = 1)
        {
            if (processJobHandle.IsCompleted)
            {
                ProcessMapDataJob job = new ProcessMapDataJob
                {
                    mapData = nativeMapData,
                    mapWidth = mapDataManager.mapWidth,
                    mapHeight = mapDataManager.mapHeight,
                    position = new int2(x, y),
                    color = color,
                    brushSize = brushSize,
                    operationType = 0 // 绘制
                };
                
                processJobHandle = job.Schedule();
            }
            else
            {
                Debug.LogWarning("前一个作业尚未完成，跳过当前操作");
            }
        }
        
        /// <summary>
        /// 执行擦除操作
        /// </summary>
        public void EraseAtPosition(int x, int y, int brushSize = 1)
        {
            if (processJobHandle.IsCompleted)
            {
                ProcessMapDataJob job = new ProcessMapDataJob
                {
                    mapData = nativeMapData,
                    mapWidth = mapDataManager.mapWidth,
                    mapHeight = mapDataManager.mapHeight,
                    position = new int2(x, y),
                    color = new Color32(0, 0, 0, 0), // 透明色
                    brushSize = brushSize,
                    operationType = 1 // 擦除
                };
                
                processJobHandle = job.Schedule();
            }
            else
            {
                Debug.LogWarning("前一个作业尚未完成，跳过当前操作");
            }
        }
        
        /// <summary>
        /// 执行填充操作
        /// </summary>
        public void FillAtPosition(int x, int y, Color32 color)
        {
            if (processJobHandle.IsCompleted)
            {
                ProcessMapDataJob job = new ProcessMapDataJob
                {
                    mapData = nativeMapData,
                    mapWidth = mapDataManager.mapWidth,
                    mapHeight = mapDataManager.mapHeight,
                    position = new int2(x, y),
                    color = color,
                    brushSize = 1,
                    operationType = 2 // 填充
                };
                
                processJobHandle = job.Schedule();
            }
            else
            {
                Debug.LogWarning("前一个作业尚未完成，跳过当前操作");
            }
        }
        
        /// <summary>
        /// 检测像素级错误
        /// </summary>
        public (int2[], int2[], int2[]) DetectPixelErrors()
        {
            NativeList<int2> isolatedPixels = new NativeList<int2>(Allocator.TempJob);
            NativeList<int2> threeColorIntersections = new NativeList<int2>(Allocator.TempJob);
            NativeList<int2> singlePixelLines = new NativeList<int2>(Allocator.TempJob);
            
            DetectPixelErrorsJob job = new DetectPixelErrorsJob
            {
                mapData = nativeMapData,
                mapWidth = mapDataManager.mapWidth,
                mapHeight = mapDataManager.mapHeight,
                isolatedPixels = isolatedPixels,
                threeColorIntersections = threeColorIntersections,
                singlePixelLines = singlePixelLines
            };
            
            JobHandle handle = job.Schedule();
            handle.Complete();
            
            // 转换为数组返回
            int2[] isolatedArray = new int2[isolatedPixels.Length];
            for (int i = 0; i < isolatedPixels.Length; i++)
            {
                isolatedArray[i] = isolatedPixels[i];
            }
            
            int2[] threeColorArray = new int2[threeColorIntersections.Length];
            for (int i = 0; i < threeColorIntersections.Length; i++)
            {
                threeColorArray[i] = threeColorIntersections[i];
            }
            
            int2[] singlePixelArray = new int2[singlePixelLines.Length];
            for (int i = 0; i < singlePixelLines.Length; i++)
            {
                singlePixelArray[i] = singlePixelLines[i];
            }
            
            isolatedPixels.Dispose();
            threeColorIntersections.Dispose();
            singlePixelLines.Dispose();
            
            return (isolatedArray, threeColorArray, singlePixelArray);
        }
        
        /// <summary>
        /// 检测颜色冲突
        /// </summary>
        public Color32[] DetectColorConflicts()
        {
            var conflictColors = new NativeParallelHashSet<float3>(nativeMapData.Length, Allocator.TempJob);
            ColorConflictDetectionJob job = new ColorConflictDetectionJob
            {
                mapData = nativeMapData,
                conflictColors = conflictColors
            };
            
            job.Schedule().Complete();
            using var array = conflictColors.ToNativeArray(Allocator.Temp);
            var colors = array.ToArray().Select(x => new Color32((byte)x.x, (byte)x.y, (byte)x.z, 255)).ToArray();
            conflictColors.Dispose();
            return colors;
        }
    }
}
