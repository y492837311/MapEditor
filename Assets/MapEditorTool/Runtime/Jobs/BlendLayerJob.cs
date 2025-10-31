using MapEditorTool.Runtime.Helpers;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapEditorTool.Runtime.Jobs
{
    [BurstCompile]
    public struct BlendLayerJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> targetBuffer;
        public NativeArray<int> sourceBuffer;
        public float opacity;

        public void Execute(int index)
        {
             int sourceColor = sourceBuffer[index];
            // 跳过完全透明的源像素
            if (sourceColor == 0) return;
            
            int targetColor = targetBuffer[index];
            Color src = ColorUtils.IntToColor(sourceColor);
            Color dst = ColorUtils.IntToColor(targetColor);

            // 应用图层不透明度
            src.a *= opacity;

            // 简单alpha混合
            Color result = ColorUtils.Lerp(dst, src, src.a);
            result.a = math.clamp(dst.a + src.a * (1 - dst.a),0 ,1); // 正确的alpha混合

            targetBuffer[index] = ColorUtils.ColorToInt(result);
        }
        

    }
}