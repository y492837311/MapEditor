using System;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class OperationRecorder : IDisposable
{
    public struct PixelOperation
    {
        public int2 position;
        public int oldColor;
        public int newColor;
    }
    
    private List<PixelOperation> currentOperation;
    private bool isRecording = false;
    
    public void BeginRecording()
    {
        if (isRecording)
            EndRecording();
            
        currentOperation = new List<PixelOperation>();
        isRecording = true;
    }
    
    public void RecordPixelChange(int2 position, int oldColor, int newColor)
    {
        if (!isRecording) return;
        
        for (int i = 0; i < currentOperation.Count; i++)
        {
            if (currentOperation[i].position.Equals(position))
            {
                var op = currentOperation[i];
                op.newColor = newColor;
                currentOperation[i] = op;
                return;
            }
        }
        
        currentOperation.Add(new PixelOperation
        {
            position = position,
            oldColor = oldColor,
            newColor = newColor
        });
    }
    
    public void EndRecording()
    {
        isRecording = false;
    }
    
    public List<PixelOperation> GetCurrentOperation()
    {
        return currentOperation;
    }
    
    public void Clear()
    {
        currentOperation?.Clear();
        isRecording = false;
    }
    
    public void Dispose()
    {
        Clear();
    }
}