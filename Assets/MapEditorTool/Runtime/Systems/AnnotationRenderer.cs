using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class AnnotationRenderer : IDisposable
{
    private Material annotationMaterial;
    private GUIStyle annotationStyle;
    private Texture2D warningIcon;
    private Texture2D errorIcon;
    private Texture2D infoIcon;

    private ComputeShader renderComputeShader;
    private ComputeBuffer annotationBuffer;
    private List<AnnotationScreenData> reusableScreenAnnotations = new List<AnnotationScreenData>();
    private GUIStyle reusableLabelStyle;

    public AnnotationRenderer()
    {
        annotationMaterial = new Material(Shader.Find("UI/Default"));

        annotationStyle = new GUIStyle();
        annotationStyle.normal.textColor = Color.white;
        annotationStyle.fontSize = 11;
        annotationStyle.padding = new RectOffset(4, 4, 2, 2);
        annotationStyle.normal.background = CreateBackgroundTexture(2, 2, new Color(0, 0, 0, 0.8f));

        warningIcon = CreateIconTexture(12, 12, Color.yellow);
        errorIcon = CreateIconTexture(12, 12, Color.red);
        infoIcon = CreateIconTexture(12, 12, Color.cyan);

        renderComputeShader = Resources.Load<ComputeShader>("Shaders/AnnotationRenderCompute");
        annotationBuffer = new ComputeBuffer(64, sizeof(float) * 8);
    }

    public void RenderAnnotations(NativeList<SmartAnnotationSystem.AnnotationData> annotations,
        Rect canvasRect, int mapWidth, int mapHeight, float zoomLevel, Vector2 panOffset)
    {
        if (annotations.Length == 0) return;

        reusableScreenAnnotations.Clear();

        // 重用列表，避免分配
        for (int i = 0; i < annotations.Length; i++)
        {
            var annotation = annotations[i];
            reusableScreenAnnotations.Add(new AnnotationScreenData
            {
                screenPosition = MapToScreenPosition(annotation.position, canvasRect, mapWidth, mapHeight, zoomLevel,
                    panOffset),
                color = annotation.color,
                label = annotation.label,
                type = (int)annotation.type
            });
        }

        RenderWithGUI(reusableScreenAnnotations);
    }

    private void RenderWithGUI(List<AnnotationScreenData> annotations)
    {
        // 重用GUIStyle
        if (reusableLabelStyle == null)
        {
            reusableLabelStyle = new GUIStyle();
            reusableLabelStyle.normal.textColor = Color.white;
            reusableLabelStyle.fontSize = 11;
            reusableLabelStyle.padding = new RectOffset(4, 4, 2, 2);
        }

        for (int i = 0; i < annotations.Count; i++)
        {
            RenderSingleAnnotation(annotations[i], reusableLabelStyle);
        }
    }

    private void RenderSingleAnnotation(AnnotationScreenData annotation, GUIStyle labelStyle)
    {
        string label = annotation.label.ToString();
        Color color = annotation.color;
        int type = annotation.type;
        Vector2 screenPos = annotation.screenPosition;
        GUIContent content = new GUIContent(annotation.label.ToString());
        Vector2 labelSize = labelStyle.CalcSize(content);

        Rect backgroundRect = new Rect(screenPos.x, screenPos.y - labelSize.y, labelSize.x + 20, labelSize.y);
        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        GUI.Box(backgroundRect, GUIContent.none, annotationStyle);
        GUI.backgroundColor = Color.white;

        Texture2D icon = GetIconForType((SmartAnnotationSystem.AnnotationType)type);
        if (icon != null)
        {
            Rect iconRect = new Rect(screenPos.x + 2, screenPos.y - labelSize.y + 2, 12, 12);
            GUI.DrawTexture(iconRect, icon);
        }

        Rect labelRect = new Rect(screenPos.x + 16, screenPos.y - labelSize.y, labelSize.x, labelSize.y);
        annotationStyle.normal.textColor = color;
        GUI.Label(labelRect, label, annotationStyle);
        annotationStyle.normal.textColor = Color.white;

        DrawConnectionLine(screenPos, new Vector2(screenPos.x + 8, screenPos.y - 8), color);
    }

    private void DrawConnectionLine(Vector2 from, Vector2 to, Color color)
    {
        Handles.BeginGUI();
        Color originalColor = Handles.color;
        Handles.color = color;
        Handles.DrawLine(from, to);
        Handles.color = originalColor;
        Handles.EndGUI();
    }

    // 更新 MapToScreenPosition 方法（用于标注渲染）
    private Vector2 MapToScreenPosition(int2 mapPos, Rect canvasRect, int mapWidth, int mapHeight, float zoomLevel,
        Vector2 panOffset)
    {
        // 考虑平移偏移
        float x = canvasRect.x + (mapPos.x / (float)mapWidth) * canvasRect.width * zoomLevel - panOffset.x * zoomLevel;
        float y = canvasRect.y + (mapPos.y / (float)mapHeight) * canvasRect.height * zoomLevel -
                  panOffset.y * zoomLevel;
        return new Vector2(x, y);
    }

    private Texture2D GetIconForType(SmartAnnotationSystem.AnnotationType type)
    {
        switch (type)
        {
            case SmartAnnotationSystem.AnnotationType.Warning: return warningIcon;
            case SmartAnnotationSystem.AnnotationType.Error: return errorIcon;
            case SmartAnnotationSystem.AnnotationType.Info: return infoIcon;
            default: return null;
        }
    }

    private Texture2D CreateBackgroundTexture(int width, int height, Color color)
    {
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private Texture2D CreateIconTexture(int width, int height, Color color)
    {
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        Vector2 center = new Vector2(width / 2f, height / 2f);
        float radius = width / 2f - 1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    pixels[y * width + x] = color;
                }
                else
                {
                    pixels[y * width + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    public void Dispose()
    {
        // annotationMaterial?.Dispose();
        annotationBuffer?.Release();

        if (warningIcon != null) UnityEngine.Object.DestroyImmediate(warningIcon);
        if (errorIcon != null) UnityEngine.Object.DestroyImmediate(errorIcon);
        if (infoIcon != null) UnityEngine.Object.DestroyImmediate(infoIcon);
    }
}

public struct AnnotationScreenData
{
    public Vector2 screenPosition;
    public Color color;
    public FixedString128Bytes label;
    public int type;
}