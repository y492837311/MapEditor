using UnityEditor;
using UnityEngine;
using Unity.Mathematics;

public class RegionNamePopup : EditorWindow
{
    private int2 position;
    private SmartAnnotationSystem annotationSystem;
    private string regionName = "";
    private string colorName = "";
    
    public void Initialize(int2 pos, SmartAnnotationSystem system)
    {
        position = pos;
        annotationSystem = system;
        regionName = system.GetRegionName(pos);
        
        minSize = new Vector2(300, 120);
        maxSize = new Vector2(300, 120);
    }
    
    private void OnGUI()
    {
        GUILayout.Label($"设置区域名称 - 坐标: ({position.x}, {position.y})", EditorStyles.boldLabel);
        
        GUILayout.Space(10);
        
        GUILayout.Label("区域名称:");
        regionName = EditorGUILayout.TextField(regionName);
        
        GUILayout.Label("颜色名称:");
        colorName = EditorGUILayout.TextField(colorName);
        
        GUILayout.Space(20);
        
        GUILayout.BeginHorizontal();
        
        if (GUILayout.Button("确定"))
        {
            if (!string.IsNullOrEmpty(regionName))
            {
                annotationSystem.AddRegionName(position, regionName);
            }
            
            Close();
        }
        
        if (GUILayout.Button("取消"))
        {
            Close();
        }
        
        GUILayout.EndHorizontal();
    }
}