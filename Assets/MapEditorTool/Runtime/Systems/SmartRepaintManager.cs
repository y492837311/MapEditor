using System.Collections.Generic;
using UnityEngine;

public class SmartRepaintManager
{
    private bool _isDirty = true;
    private float _lastRepaintTime;
    private const float MIN_REPAINT_INTERVAL = 0.033f; // 30 FPS
    private readonly List<System.Action> _repaintActions = new List<System.Action>();
    private readonly HashSet<object> _dirtySources = new HashSet<object>();

    // 注册重绘回调
    public void RegisterRepaintAction(System.Action action)
    {
        if (!_repaintActions.Contains(action))
            _repaintActions.Add(action);
    }

    // 标记需要重绘（可指定来源）
    public void MarkDirty(object source = null)
    {
        _isDirty = true;
        if (source != null)
            _dirtySources.Add(source);
    }

    // 智能重绘入口
    public void SmartRepaint()
    {
        if (!_isDirty) return;
        
        // 频率限制
        float currentTime = Time.realtimeSinceStartup;
        if (currentTime - _lastRepaintTime < MIN_REPAINT_INTERVAL)
            return;

        ExecuteRepaint();
    }

    // 强制立即重绘
    public void ForceRepaint()
    {
        ExecuteRepaint();
    }

    private void ExecuteRepaint()
    {
        _lastRepaintTime = Time.realtimeSinceStartup;
        
        // 执行所有注册的重绘操作
        foreach (var action in _repaintActions)
        {
            action?.Invoke();
        }
        
        _isDirty = false;
        _dirtySources.Clear();
    }

    // 检查特定来源是否导致脏标记
    public bool IsSourceDirty(object source)
    {
        return _dirtySources.Contains(source);
    }
}