using System;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class UndoRedoSystem : IDisposable
{
    private struct UndoState
    {
        public NativeArray<int> colorData;
        public string description;
        public long timestamp;
    }
    
    private Stack<UndoState> undoStack;
    private Stack<UndoState> redoStack;
    private int maxHistorySteps;
    private int textureWidth;
    private int textureHeight;
    
    public int UndoCount => undoStack.Count;
    public int RedoCount => redoStack.Count;
    
    public event System.Action OnStateChanged;
    
    public UndoRedoSystem(int width, int height, int maxSteps = 50)
    {
        textureWidth = width;
        textureHeight = height;
        maxHistorySteps = maxSteps;
        
        undoStack = new Stack<UndoState>(maxSteps);
        redoStack = new Stack<UndoState>(maxSteps);
    }
    
    public void PushState(NativeArray<int> currentState, string description = "编辑")
    {
        ClearStack(redoStack);
        
        var newState = new UndoState
        {
            colorData = new NativeArray<int>(currentState.Length, Allocator.Persistent),
            description = description,
            timestamp = DateTime.Now.Ticks
        };
        
        currentState.CopyTo(newState.colorData);
        
        if (undoStack.Count >= maxHistorySteps)
        {
            var oldestState = undoStack.ToArray()[0];
            oldestState.colorData.Dispose();
            
            var tempList = new List<UndoState>(undoStack);
            tempList.RemoveAt(0);
            undoStack = new Stack<UndoState>(tempList);
        }
        
        undoStack.Push(newState);
        OnStateChanged?.Invoke();
    }
    
    public bool CanUndo()
    {
        return undoStack.Count > 0;
    }
    
    public bool CanRedo()
    {
        return redoStack.Count > 0;
    }
    
    public bool Undo(NativeArray<int> targetBuffer, out string description)
    {
        description = null;
        
        if (!CanUndo())
            return false;
        
        if (undoStack.Count > 1)
        {
            var currentState = new UndoState
            {
                colorData = new NativeArray<int>(targetBuffer.Length, Allocator.Persistent),
                description = "重做状态",
                timestamp = DateTime.Now.Ticks
            };
            targetBuffer.CopyTo(currentState.colorData);
            redoStack.Push(currentState);
        }
        
        var undoState = undoStack.Pop();
        description = undoState.description;
        
        undoState.colorData.CopyTo(targetBuffer);
        undoState.colorData.Dispose();
        
        OnStateChanged?.Invoke();
        return true;
    }
    
    public bool Redo(NativeArray<int> targetBuffer, out string description)
    {
        description = null;
        
        if (!CanRedo())
            return false;
        
        var currentState = new UndoState
        {
            colorData = new NativeArray<int>(targetBuffer.Length, Allocator.Persistent),
            description = "撤销状态",
            timestamp = DateTime.Now.Ticks
        };
        targetBuffer.CopyTo(currentState.colorData);
        undoStack.Push(currentState);
        
        var redoState = redoStack.Pop();
        description = redoState.description;
        
        redoState.colorData.CopyTo(targetBuffer);
        redoState.colorData.Dispose();
        
        OnStateChanged?.Invoke();
        return true;
    }
    
    public void Clear()
    {
        ClearStack(undoStack);
        ClearStack(redoStack);
        OnStateChanged?.Invoke();
    }
    
    private void ClearStack(Stack<UndoState> stack)
    {
        foreach (var state in stack)
        {
            if (state.colorData.IsCreated)
                state.colorData.Dispose();
        }
        stack.Clear();
    }
    
    public string GetUndoDescription()
    {
        return CanUndo() ? undoStack.Peek().description : null;
    }
    
    public string GetRedoDescription()
    {
        return CanRedo() ? redoStack.Peek().description : null;
    }
    
    public void Dispose()
    {
        ClearStack(undoStack);
        ClearStack(redoStack);
    }
}