using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapEditorTool.Runtime.Jobs
{
    [BurstCompile]
    public struct DetectionColorJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> colorData;
        [WriteOnly] public NativeList<int2>.ParallelWriter isolatedPixels;
        [WriteOnly] public NativeList<int2>.ParallelWriter thinLinePixels;
        [WriteOnly] public NativeList<int2>.ParallelWriter tripleJunctionPixels;
        //
        // [NativeDisableContainerSafetyRestriction, WriteOnly]
        // public NativeParallelHashMap<int, int> colorCount;
        public int width;
        public int height;

        public void Execute(int index)
        {
            int color = colorData[index];
            if (color == 0) return;

            int x = index % width;
            int y = index / width;

            if (IsPixelIsolated(colorData, x, y, width, height))
            {
                isolatedPixels.AddNoResize(new int2(x, y));
            }

            /*if (colorCount.ContainsKey(color))
            {
                colorCount[color]++;
            }
            else
            {
                colorCount.TryAdd(color, 1);
            }*/

            if (IsThinLinePixel(colorData, x, y, width, height))
            {
                thinLinePixels.AddNoResize(new int2(x, y));
            }

            if (IsTripleJunction(colorData, x, y, width, height))
            {
                tripleJunctionPixels.AddNoResize(new int2(x, y));
            }
        }

        #region 检测算法

        private bool IsPixelIsolated(NativeArray<int> colorData, int x, int y, int width, int height)
        {
            int centerColor = colorData[y * width + x];

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        int neighborColor = colorData[ny * width + nx];
                        if (neighborColor == centerColor)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool IsThinLinePixel(NativeArray<int> colorData, int x, int y, int width, int height)
        {
            int centerColor = colorData[y * width + x];

            int horizontalNeighbors = 0;
            int verticalNeighbors = 0;

            if (x > 0 && colorData[y * width + (x - 1)] == centerColor) horizontalNeighbors++;
            if (x < width - 1 && colorData[y * width + (x + 1)] == centerColor) horizontalNeighbors++;

            if (y > 0 && colorData[(y - 1) * width + x] == centerColor) verticalNeighbors++;
            if (y < height - 1 && colorData[(y + 1) * width + x] == centerColor) verticalNeighbors++;

            return (horizontalNeighbors >= 1 && verticalNeighbors == 0) ||
                   (horizontalNeighbors == 0 && verticalNeighbors >= 1);
        }

        private bool IsTripleJunction(NativeArray<int> colorData, int x, int y, int width, int height)
        {
            if (x <= 0 || x >= width - 1 || y <= 0 || y >= height - 1)
                return false;

            Span<int> neighbors = stackalloc int[8];
            neighbors[0] = colorData[(y - 1) * width + (x - 1)];
            neighbors[1] = colorData[(y - 1) * width + x];
            neighbors[2] = colorData[(y - 1) * width + (x + 1)];
            neighbors[3] = colorData[y * width + (x - 1)];
            neighbors[4] = colorData[y * width + (x + 1)];
            neighbors[5] = colorData[(y + 1) * width + (x - 1)];
            neighbors[6] = colorData[(y + 1) * width + x];
            neighbors[7] = colorData[(y + 1) * width + (x + 1)];

            var count = 0;
            foreach (int color in neighbors)
            {
                if (color != 0)
                {
                    count++;
                }
            }

            return count >= 3;
        }

        #endregion
    }
}