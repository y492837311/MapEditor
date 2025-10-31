using MapEditorTool.Runtime.Helpers;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MapEditorTool.Runtime.Jobs
{
    public struct FillReusableColorJob : IJobParallelFor
    {
        public Color32[] reusableColorArray;
        [ReadOnly]
        public NativeArray<int> compositeData;

        public void Execute(int index)
        {
            reusableColorArray[index] = ColorUtils.IntToColor32(compositeData[index]);

        }
    }
}