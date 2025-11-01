using System.Collections.Generic;
using UnityEngine;

namespace MapEditor
{
    public class UndoRedoManager
    {
        private Stack<EditOperation> undoStack;
        private Stack<EditOperation> redoStack;
        private int maxHistorySteps;
        
        public event System.Action OnHistoryChanged;

        public UndoRedoManager(int maxSteps = 50)
        {
            undoStack = new Stack<EditOperation>();
            redoStack = new Stack<EditOperation>();
            maxHistorySteps = maxSteps;
        }

        public void RecordOperation(EditOperation operation)
        {
            if (operation == null || operation.pixelChanges.Count == 0)
                return;

            // 清空重做栈
            redoStack.Clear();
            
            // 添加到撤销栈
            undoStack.Push(operation);
            
            // 限制历史记录数量
            while (undoStack.Count > maxHistorySteps)
            {
                var oldestOperation = undoStack.ToArray()[undoStack.Count - 1];
                oldestOperation.Dispose();
                
                var list = new List<EditOperation>(undoStack);
                list.RemoveAt(list.Count - 1);
                undoStack = new Stack<EditOperation>(list);
            }
            
            OnHistoryChanged?.Invoke();
        }

        public bool CanUndo()
        {
            return undoStack.Count > 0;
        }

        public bool CanRedo()
        {
            return redoStack.Count > 0;
        }

        public EditOperation Undo()
        {
            if (!CanUndo()) return null;
            
            var operation = undoStack.Pop();
            redoStack.Push(operation);
            
            OnHistoryChanged?.Invoke();
            return operation;
        }

        public EditOperation Redo()
        {
            if (!CanRedo()) return null;
            
            var operation = redoStack.Pop();
            undoStack.Push(operation);
            
            OnHistoryChanged?.Invoke();
            return operation;
        }

        public void Clear()
        {
            foreach (var operation in undoStack)
            {
                operation.Dispose();
            }
            foreach (var operation in redoStack)
            {
                operation.Dispose();
            }
            
            undoStack.Clear();
            redoStack.Clear();
            OnHistoryChanged?.Invoke();
        }

        public string GetUndoDescription()
        {
            return CanUndo() ? $"Undo: {undoStack.Peek().description}" : "Nothing to undo";
        }

        public string GetRedoDescription()
        {
            return CanRedo() ? $"Redo: {redoStack.Peek().description}" : "Nothing to redo";
        }

        public int GetUndoCount() => undoStack.Count;
        public int GetRedoCount() => redoStack.Count;
    }
}