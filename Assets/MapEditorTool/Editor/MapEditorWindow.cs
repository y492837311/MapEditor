using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.UIElements;
using System.IO;
using System.Threading.Tasks;
using MapEditorTool.Runtime.Helpers;
using MapEditorTool.Runtime.Jobs;

public class MapEditorWindow : EditorWindow
{
    [SerializeField] private Texture2D baseMapTexture;
    [SerializeField] private RenderTexture canvasTexture;
    private MapData mapData;
    
    private ToolType currentTool = ToolType.Pencil;
    private Color currentColor = Color.red;
    private int brushSize = 3;
    private float zoomLevel = 1f;
    // 网格设置
    private bool showGrid = true;
    private bool showGridCoordinates = true;
    private Material gridMaterial;
    private Vector2 panOffset = Vector2.zero;
    
    private Material blitMaterial;
    private ComputeShader colorComputeShader;
    private ComputeShaderManager computeManager;
    
    // 系统组件
    private UndoRedoSystem undoRedoSystem;
    private OperationRecorder operationRecorder;
    private MapFileSerializer fileSerializer;
    private MapDataConverter dataConverter;
    private SmartAnnotationSystem annotationSystem;
    private AnnotationRenderer annotationRenderer;
    private LayerSystem layerSystem;
    
    // UI状态
    private bool showHistoryPanel = false;
    private bool showLayerPanel = true;
    private Vector2 historyScrollPos;
    private int2 currentMousePosition;
    
    // 标注设置
    private bool showCoordinateInfo = true;
    private bool showRegionInfo = true;
    private bool showColorWarnings = true;
    private bool showPixelErrors = true;
    private bool showThinLineWarnings = true;
    private bool showTripleJunctionMarkers = true;
    
    // 文件设置
    private string currentFilePath;
    private FileFormat currentFormat = FileFormat.Binary;
    private bool autoBackup = true;
    
    private enum ToolType { Pencil, Eraser, Bucket, Eyedropper }
    private NativeList<float4> colorHistory;
    private bool isMouseDown = false;
    private string lastOperationDescription = "";
    
    private Color32[] reusableColorArray;
    private Texture2D reusableTexture;

    [MenuItem("Tools/地图色块编辑器")]
    public static void ShowWindow()
    {
        var window = GetWindow<MapEditorWindow>();
        window.titleContent = new GUIContent("地图色块编辑器");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        InitializeData();
        InitializeComputeShader();
        InitializeLayerSystem();
        InitializeUndoRedo();
        InitializeFileSystem();
        InitializeAnnotationSystem();
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        if (gridMaterial == null)
        {
            gridMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        showGrid = EditorPrefs.GetBool("MapEditor_ShowGrid", true);
        showGridCoordinates = EditorPrefs.GetBool("MapEditor_ShowGridCoordinates", true);
    }

    private void InitializeData()
    {
        var width = 4096;
        var height = 2048;
        
        mapData = new MapData(width, height);
        canvasTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        canvasTexture.enableRandomWrite = true;
        canvasTexture.Create();
        
        colorComputeShader = Resources.Load<ComputeShader>("Shaders/ColorCompute");
        blitMaterial = new Material(Shader.Find("Unlit/Texture"));
        
        colorHistory = new NativeList<float4>(256, Allocator.Persistent);
        
        // 预分配重用数组
        int pixelCount = mapData.width * mapData.height;
        reusableColorArray = new Color32[pixelCount];
        reusableTexture = new Texture2D(mapData.width, mapData.height, TextureFormat.RGBA32, false);
    }

    private void InitializeComputeShader()
    {
        ComputeShader colorShader = Resources.Load<ComputeShader>("Shaders/ColorCompute");
        computeManager = new ComputeShaderManager(colorShader, mapData.width, mapData.height);
        computeManager.SetColorData(mapData.colorData);
    }

    private void InitializeUndoRedo()
    {
        undoRedoSystem = new UndoRedoSystem(mapData.width, mapData.height, 100);
        operationRecorder = new OperationRecorder();
        undoRedoSystem.OnStateChanged += OnUndoRedoStateChanged;
        SaveState("初始状态");
    }

    private void InitializeFileSystem()
    {
        fileSerializer = new MapFileSerializer();
        dataConverter = new MapDataConverter();
    }

    private void InitializeAnnotationSystem()
    {
        annotationSystem = new SmartAnnotationSystem(mapData.width, mapData.height);
        annotationRenderer = new AnnotationRenderer();
    }

    private void InitializeLayerSystem()
    {
        layerSystem = new LayerSystem(mapData.width, mapData.height);
        layerSystem.OnLayersChanged += OnLayersChanged;
    }

    private void OnDisable()
    {
        mapData.Dispose();
        computeManager?.Dispose();
        undoRedoSystem?.Dispose();
        operationRecorder?.Dispose();
        fileSerializer?.Dispose();
        dataConverter?.Dispose();
        annotationSystem?.Dispose();
        annotationRenderer?.Dispose();
        layerSystem?.Dispose();
        
        EditorPrefs.SetBool("MapEditor_ShowGrid", showGrid);
        EditorPrefs.SetBool("MapEditor_ShowGridCoordinates", showGridCoordinates);
        
        if (gridMaterial != null)
        {
            UnityEngine.Object.DestroyImmediate(gridMaterial);
        }
        
        // 释放重用资源
        if (reusableTexture != null)
            UnityEngine.Object.DestroyImmediate(reusableTexture);
        
        if (colorHistory.IsCreated) colorHistory.Dispose();
        if (canvasTexture != null) canvasTexture.Release();
    }

    private void OnGUI()
    {
        HandleDragAndDrop();
        DrawToolbar();
        DrawLayerPanel();
        DrawCanvas();
        DrawHistoryPanel();
        HandleInput();
    }

    #region 绘制方法
    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        DrawFileMenu();
        DrawEditMenu();
        DrawViewMenu();
        DrawLayerMenu();
        DrawTools();
        DrawAnnotationStatus();
        
        GUILayout.EndHorizontal();
    }

    private void DrawFileMenu()
    {
        if (GUILayout.Button("文件", EditorStyles.toolbarButton))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("导入配置..."), false, ImportConfig);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("导出配置..."), false, ExportConfig);
            menu.AddItem(new GUIContent("导出PNG..."), false, ExportPNG);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("格式/二进制"), currentFormat == FileFormat.Binary, () => currentFormat = FileFormat.Binary);
            menu.AddItem(new GUIContent("格式/JSON"), currentFormat == FileFormat.Json, () => currentFormat = FileFormat.Json);
            menu.AddItem(new GUIContent("格式/游戏格式"), currentFormat == FileFormat.Legacy, () => currentFormat = FileFormat.Legacy);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("自动备份"), autoBackup, () => autoBackup = !autoBackup);
            menu.AddItem(new GUIContent("清空历史"), false, ClearHistory);
            menu.ShowAsContext();
        }
    }

    private void DrawEditMenu()
    {
        if (GUILayout.Button("编辑", EditorStyles.toolbarButton))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"撤销 ({undoRedoSystem.UndoCount})"), undoRedoSystem.CanUndo(), Undo);
            menu.AddItem(new GUIContent($"重做 ({undoRedoSystem.RedoCount})"), undoRedoSystem.CanRedo(), Redo);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("清空画布"), false, ClearCanvas);
            menu.ShowAsContext();
        }
    }

// 在 DrawViewMenu 方法中添加网格控制选项
    private void DrawViewMenu()
    {
        if (GUILayout.Button("视图", EditorStyles.toolbarButton))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("显示图层面板"), showLayerPanel, () => showLayerPanel = !showLayerPanel);
            menu.AddItem(new GUIContent("显示历史面板"), showHistoryPanel, () => showHistoryPanel = !showHistoryPanel);
            menu.AddSeparator("");
        
            // 网格控制选项
            menu.AddItem(new GUIContent("网格/显示网格"), showGrid, () => {
                showGrid = !showGrid;
                Repaint();
            });
            menu.AddItem(new GUIContent("网格/显示坐标"), showGridCoordinates, () => {
                showGridCoordinates = !showGridCoordinates;
                Repaint();
            });
            menu.AddSeparator("网格/");
            menu.AddItem(new GUIContent("网格/重置视图"), false, ResetView);
        
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("标注/显示坐标"), showCoordinateInfo, () => showCoordinateInfo = !showCoordinateInfo);
            menu.AddItem(new GUIContent("标注/显示区域信息"), showRegionInfo, () => showRegionInfo = !showRegionInfo);
            menu.AddSeparator("标注/");
            menu.AddItem(new GUIContent("标注/颜色冲突警告"), showColorWarnings, () => showColorWarnings = !showColorWarnings);
            menu.AddItem(new GUIContent("标注/像素错误"), showPixelErrors, () => showPixelErrors = !showPixelErrors);
            menu.AddItem(new GUIContent("标注/细线警告"), showThinLineWarnings, () => showThinLineWarnings = !showThinLineWarnings);
            menu.AddSeparator("标注/");
            menu.AddItem(new GUIContent("标注/刷新检测"), false, RefreshAnnotations);
            menu.ShowAsContext();
        }
    }

    // 重置视图方法
    private void ResetView()
    {
        zoomLevel = 1f;
        panOffset = Vector2.zero;
        Repaint();
        ShowNotification(new GUIContent("视图已重置"));
    }

    private void DrawLayerMenu()
    {
        if (GUILayout.Button("图层", EditorStyles.toolbarButton))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("新建图层"), false, () => layerSystem.CreateLayer($"图层 {layerSystem.LayerCount + 1}"));
            menu.AddItem(new GUIContent("复制图层"), false, () => layerSystem.DuplicateLayer(layerSystem.ActiveLayerIndex));
            menu.AddItem(new GUIContent("合并图层"), layerSystem.LayerCount > 1, () => {
                layerSystem.MergeLayer(layerSystem.ActiveLayerIndex, Mathf.Max(0, layerSystem.ActiveLayerIndex - 1));
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("清空活动图层"), false, () => layerSystem.ClearActiveLayer());
            menu.AddItem(new GUIContent("删除活动图层"), layerSystem.LayerCount > 1, () => layerSystem.DeleteLayer(layerSystem.ActiveLayerIndex));
            menu.ShowAsContext();
        }
        
        GUILayout.Label($"活动: {layerSystem.ActiveLayer?.name}", EditorStyles.miniLabel);
        if (layerSystem.ActiveLayer != null && layerSystem.ActiveLayer.isLocked)
        {
            GUILayout.Label("🔒", EditorStyles.miniLabel);
        }
    }

    private void DrawTools()
    {
        currentTool = (ToolType)GUILayout.SelectionGrid((int)currentTool, 
            new[] { "铅笔", "橡皮", "油漆桶", "取色器" }, 4, EditorStyles.toolbarButton);
        
        GUILayout.Label("笔刷:", EditorStyles.miniLabel);
        brushSize = EditorGUILayout.IntSlider(brushSize, 1, 20);
        
        currentColor = EditorGUILayout.ColorField(currentColor);
        
        EditorGUI.BeginDisabledGroup(!undoRedoSystem.CanUndo());
        if (GUILayout.Button("撤销", EditorStyles.toolbarButton)) Undo();
        EditorGUI.EndDisabledGroup();
        
        EditorGUI.BeginDisabledGroup(!undoRedoSystem.CanRedo());
        if (GUILayout.Button("重做", EditorStyles.toolbarButton)) Redo();
        EditorGUI.EndDisabledGroup();
    }

    private void DrawAnnotationStatus()
    {
        var annotations = annotationSystem.GetActiveAnnotations();
        int warningCount = 0, errorCount = 0;
        
        for (int i = 0; i < annotations.Length; i++)
        {
            if (annotations[i].type == SmartAnnotationSystem.AnnotationType.Warning) warningCount++;
            else if (annotations[i].type == SmartAnnotationSystem.AnnotationType.Error) errorCount++;
        }
        
        /*if (warningCount > 0) GUILayout.Label($"⚠{warningCount}", EditorStyles.miniLabel);
        if (errorCount > 0) GUILayout.Label($"❌{errorCount}", EditorStyles.miniLabel);*/
    }

    private void DrawLayerPanel()
    {
        if (!showLayerPanel) return;
        
        Rect panelRect = new Rect(0, 30, 200, position.height - 60);
        GUILayout.BeginArea(panelRect, "图层", EditorStyles.helpBox);
        DrawIMGUILayerPanel();
        GUILayout.EndArea();
    }

    private void DrawIMGUILayerPanel()
    {
        GUILayout.Label($"活动图层: {layerSystem.ActiveLayer?.name}", EditorStyles.boldLabel);
        GUILayout.Space(5);
        
        if (layerSystem.ActiveLayer != null)
        {
            var layer = layerSystem.ActiveLayer;
            EditorGUI.BeginChangeCheck();
            
            layer.name = EditorGUILayout.TextField("名称", layer.name);
            layer.isVisible = EditorGUILayout.Toggle("可见", layer.isVisible);
            layer.isLocked = EditorGUILayout.Toggle("锁定", layer.isLocked);
            layer.opacity = EditorGUILayout.Slider("不透明度", layer.opacity, 0, 1);
            layer.blendMode = (BlendMode)EditorGUILayout.EnumPopup("混合模式", layer.blendMode);
            
            if (EditorGUI.EndChangeCheck()) layerSystem.OnLayersChanged?.Invoke();
        }
        
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("新建图层")) layerSystem.CreateLayer($"图层 {layerSystem.LayerCount + 1}");
        if (GUILayout.Button("删除图层") && layerSystem.LayerCount > 1) layerSystem.DeleteLayer(layerSystem.ActiveLayerIndex);
        GUILayout.EndHorizontal();
        
        if (GUILayout.Button("清空活动图层")) layerSystem.ClearActiveLayer();
        
        GUILayout.Space(10);
        GUILayout.Label("图层列表:", EditorStyles.boldLabel);
        
        for (int i = 0; i < layerSystem.LayerCount; i++)
        {
            var layer = layerSystem.GetLayer(i);
            bool isActive = i == layerSystem.ActiveLayerIndex;
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(isActive, $" {layer.name}", "Button", GUILayout.ExpandWidth(true)) && !isActive)
            {
                layerSystem.SetActiveLayer(i);
            }
            
            string visibilityIcon = layer.isVisible ? "👁️" : "🚫";
            if (GUILayout.Button(visibilityIcon, GUILayout.Width(30)))
            {
                layerSystem.SetLayerVisibility(i, !layer.isVisible);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawCanvas()
    {
        Rect canvasRect = new Rect(200, 30, position.width - 220, position.height - 60);
        EditorGUI.DrawRect(canvasRect, Color.gray);
        
        if (baseMapTexture != null) GUI.DrawTexture(canvasRect, baseMapTexture, ScaleMode.ScaleToFit);
        if (blitMaterial != null) Graphics.DrawTexture(canvasRect, canvasTexture, blitMaterial);
        
        DrawAnnotations(canvasRect);
        DrawOverlayUI(canvasRect);
    }

    private void DrawAnnotations(Rect canvasRect)
    {
        var annotations = annotationSystem.GetActiveAnnotations();
        annotationRenderer.RenderAnnotations(annotations, canvasRect, mapData.width, mapData.height, zoomLevel, panOffset);
    }

    private void DrawOverlayUI(Rect canvasRect)
    {
        Handles.BeginGUI();
        DrawGrid(canvasRect);
        Handles.EndGUI();
    }

   // 替换 DrawGrid 方法中的 Handles.DrawRectangle
private void DrawGrid(Rect canvasRect)
{
    if (!showGrid) return;
    
    // 网格设置
    float gridSize = CalculateGridSize();
    Color gridColor = new Color(1f, 1f, 1f, 0.1f); // 半透明白色网格
    Color majorGridColor = new Color(1f, 1f, 1f, 0.3f); // 主网格线颜色
    
    // 计算网格起点（考虑偏移）
    float startX = canvasRect.x - (panOffset.x % gridSize) * zoomLevel;
    float startY = canvasRect.y - (panOffset.y % gridSize) * zoomLevel;
    
    // 使用GL绘制网格线（性能更好）
    DrawGridLinesGL(canvasRect, startX, startY, gridSize, gridColor, majorGridColor);
    
    // 绘制边界（使用Handles.DrawAAPolyLine）
    DrawCanvasBorder(canvasRect);
}

// 使用GL绘制网格线（避免Handles限制）
private void DrawGridLinesGL(Rect canvasRect, float startX, float startY, float gridSize, Color gridColor, Color majorGridColor)
{
    // 开始GL绘制
    GL.PushMatrix();
    GL.LoadPixelMatrix();
    
    // 设置材质
    if (gridMaterial == null)
    {
        gridMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        gridMaterial.hideFlags = HideFlags.HideAndDontSave;
    }
    
    gridMaterial.SetPass(0);
    
    // 开始绘制线条
    GL.Begin(GL.LINES);
    
    // 绘制垂直线
    for (float x = startX; x < canvasRect.x + canvasRect.width; x += gridSize * zoomLevel)
    {
        // 判断是否是主网格线
        bool isMajorLine = Mathf.Abs((x - canvasRect.x) / (gridSize * zoomLevel)) % 5 == 0;
        GL.Color(isMajorLine ? majorGridColor : gridColor);
        
        GL.Vertex3(x, canvasRect.y, 0);
        GL.Vertex3(x, canvasRect.y + canvasRect.height, 0);
        
        // 在主网格线上绘制坐标标签
        if (isMajorLine && showGridCoordinates)
        {
            DrawGridCoordinateLabel(x, canvasRect, true);
        }
    }
    
    // 绘制水平线
    for (float y = startY; y < canvasRect.y + canvasRect.height; y += gridSize * zoomLevel)
    {
        // 判断是否是主网格线
        bool isMajorLine = Mathf.Abs((y - canvasRect.y) / (gridSize * zoomLevel)) % 5 == 0;
        GL.Color(isMajorLine ? majorGridColor : gridColor);
        
        GL.Vertex3(canvasRect.x, y, 0);
        GL.Vertex3(canvasRect.x + canvasRect.width, y, 0);
        
        // 在主网格线上绘制坐标标签
        if (isMajorLine && showGridCoordinates)
        {
            DrawGridCoordinateLabel(y, canvasRect, false);
        }
    }
    
    GL.End();
    GL.PopMatrix();
}

// 使用Handles.DrawAAPolyLine绘制画布边界
private void DrawCanvasBorder(Rect canvasRect)
{
    Handles.color = new Color(1f, 0.5f, 0f, 0.8f); // 橙色边界
    
    // 定义边界四个角
    Vector3[] borderPoints = new Vector3[5]
    {
        new Vector3(canvasRect.x, canvasRect.y, 0),
        new Vector3(canvasRect.x + canvasRect.width, canvasRect.y, 0),
        new Vector3(canvasRect.x + canvasRect.width, canvasRect.y + canvasRect.height, 0),
        new Vector3(canvasRect.x, canvasRect.y + canvasRect.height, 0),
        new Vector3(canvasRect.x, canvasRect.y, 0) // 闭合回到起点
    };
    
    // 绘制抗锯齿边界线
    Handles.DrawAAPolyLine(2f, borderPoints);
}

// 或者使用简单的Handles.DrawLine绘制边界
private void DrawCanvasBorderSimple(Rect canvasRect)
{
    Handles.color = new Color(1f, 0.5f, 0f, 0.8f); // 橙色边界
    
    // 绘制四条边
    Vector3 topLeft = new Vector3(canvasRect.x, canvasRect.y, 0);
    Vector3 topRight = new Vector3(canvasRect.x + canvasRect.width, canvasRect.y, 0);
    Vector3 bottomRight = new Vector3(canvasRect.x + canvasRect.width, canvasRect.y + canvasRect.height, 0);
    Vector3 bottomLeft = new Vector3(canvasRect.x, canvasRect.y + canvasRect.height, 0);
    
    Handles.DrawLine(topLeft, topRight);
    Handles.DrawLine(topRight, bottomRight);
    Handles.DrawLine(bottomRight, bottomLeft);
    Handles.DrawLine(bottomLeft, topLeft);
}

// 计算合适的网格大小（基于缩放级别）
private float CalculateGridSize()
{
    // 基础网格大小
    float baseGridSize = 20f;
    
    // 根据缩放级别调整网格密度
    if (zoomLevel < 0.3f) return baseGridSize * 4f;
    if (zoomLevel < 0.7f) return baseGridSize * 2f;
    if (zoomLevel < 1.5f) return baseGridSize;
    if (zoomLevel < 3f) return baseGridSize / 2f;
    return baseGridSize / 4f;
}

// 绘制网格坐标标签
private void DrawGridCoordinateLabel(float position, Rect canvasRect, bool isVertical)
{
    // 计算实际地图坐标
    float mapCoordinate;
    if (isVertical)
    {
        mapCoordinate = (position - canvasRect.x) / (canvasRect.width * zoomLevel) * mapData.width;
    }
    else
    {
        mapCoordinate = (position - canvasRect.y) / (canvasRect.height * zoomLevel) * mapData.height;
    }
    
    // 只显示整数坐标
    int coordinate = Mathf.RoundToInt(mapCoordinate);
    
    // 创建标签位置
    Vector2 labelPosition;
    GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
    labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
    labelStyle.alignment = TextAnchor.MiddleCenter;
    
    if (isVertical)
    {
        labelPosition = new Vector2(position, canvasRect.y - 15f);
        GUI.Label(new Rect(labelPosition.x - 20f, labelPosition.y, 40f, 12f), coordinate.ToString(), labelStyle);
    }
    else
    {
        labelPosition = new Vector2(canvasRect.x - 25f, position - 6f);
        GUI.Label(new Rect(labelPosition.x, labelPosition.y, 25f, 12f), coordinate.ToString(), labelStyle);
    }
}

    private void DrawHistoryPanel()
    {
        if (!showHistoryPanel) return;
        
        GUILayout.BeginArea(new Rect(position.width - 300, 30, 280, 200), "历史记录", EditorStyles.helpBox);
        historyScrollPos = GUILayout.BeginScrollView(historyScrollPos);
        
        GUILayout.Label($"撤销栈: {undoRedoSystem.UndoCount} 步");
        GUILayout.Label($"重做栈: {undoRedoSystem.RedoCount} 步");
        
        GUILayout.Space(10);
        if (GUILayout.Button("显示详细信息", EditorStyles.miniButton)) ShowHistoryDetails();
        
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    #endregion

    #region 输入处理
   // 在 HandleInput 方法中添加平移支持
private void HandleInput()
{
    Event e = Event.current;
    Rect canvasRect = new Rect(200, 30, position.width - 220, position.height - 60);
    
    // 先处理键盘事件（包括快捷键）
    if (e.type == EventType.KeyDown)
    {
        HandleKeyboard(e);
    }
    
    if (canvasRect.Contains(e.mousePosition))
    {
        Vector2 localPos = GetLocalMousePosition(e.mousePosition);
        currentMousePosition = new int2((int)localPos.x, (int)localPos.y);
        annotationSystem.UpdateAnnotations(mapData.colorData, mapData.width, mapData.height, currentMousePosition);
    }
    
    switch (e.type)
    {
        case EventType.MouseDown:
            if (e.button == 0) 
            { 
                isMouseDown = true; 
                StartOperation(GetOperationDescription()); 
                HandleDrawing(e); 
            }
            else if (e.button == 1) 
            { 
                ShowRegionNamePopup(); 
                e.Use(); 
            }
            else if (e.button == 2) // 中键按下，开始平移
            {
                isPanning = true;
                lastPanPosition = e.mousePosition;
                e.Use();
            }
            break;
            
        case EventType.MouseDrag:
            if (isMouseDown && e.button == 0) 
            {
                HandleDrawing(e);
            }
            else if (isPanning && e.button == 2) // 中键拖拽平移
            {
                HandlePanning(e);
                e.Use();
            }
            break;
            
        case EventType.MouseUp:
            if (e.button == 0) 
            { 
                isMouseDown = false; 
                EndOperation(); 
            }
            else if (e.button == 2) // 中键释放，结束平移
            {
                isPanning = false;
                e.Use();
            }
            break;
            
        case EventType.ScrollWheel: 
            HandleZoom(e); 
            break;
    }
}

// 添加平移相关的字段
private bool isPanning = false;
private Vector2 lastPanPosition;

// 处理平移操作
private void HandlePanning(Event e)
{
    Vector2 delta = e.mousePosition - lastPanPosition;
    panOffset += delta / zoomLevel;
    lastPanPosition = e.mousePosition;
    Repaint();
}

// 更新缩放处理以考虑平移偏移
private void HandleZoom(Event e)
{
    float oldZoom = zoomLevel;
    zoomLevel -= e.delta.y * 0.01f;
    zoomLevel = Mathf.Clamp(zoomLevel, 0.1f, 5f);
    
    // 基于鼠标位置进行缩放中心调整
    if (showGrid)
    {
        Rect canvasRect = new Rect(200, 30, position.width - 220, position.height - 60);
        if (canvasRect.Contains(e.mousePosition))
        {
            Vector2 mouseLocalPos = GetLocalMousePosition(e.mousePosition);
            Vector2 zoomCenter = new Vector2(mouseLocalPos.x / mapData.width, mouseLocalPos.y / mapData.height);
            
            // 调整偏移以保持缩放中心
            panOffset += (zoomCenter - Vector2.one * 0.5f) * (oldZoom - zoomLevel) * 100f;
        }
    }
    
    e.Use();
    Repaint();
}

    private void HandleKeyboard(Event e)
    {
        // 撤销重做快捷键
        if (e.control || e.command) // 支持Ctrl和Cmd键
        {
            switch (e.keyCode)
            {
                case KeyCode.Z:
                    if (e.shift)
                    {
                        Redo();
                    }
                    else
                    {
                        Undo();
                    }
                    e.Use();
                    break;
                    
                case KeyCode.Y:
                    Redo();
                    e.Use();
                    break;
            }
        }
    }

    private void HandleDrawing(Event e)
    {
        Vector2 localPos = GetLocalMousePosition(e.mousePosition);
        
        switch (currentTool)
        {
            case ToolType.Pencil:
                ScheduleDrawJob(localPos, ColorUtils.ColorToInt(currentColor), true);
                break;
            case ToolType.Eraser:
                ScheduleDrawJob(localPos, 0, true); // 0表示透明
                break;
            case ToolType.Bucket:
                if (e.type == EventType.MouseDown) // 只在点击时执行
                {
                    ScheduleFloodFillJob(localPos, ColorUtils.ColorToInt(currentColor));
                }
                break;
            case ToolType.Eyedropper:
                if (e.type == EventType.MouseDown)
                {
                    SchedulePickColorJob(localPos);
                }
                break;
        }
        
        e.Use();
        Repaint();
    }

    #endregion

    #region 绘制操作
    private void ScheduleDrawJob(Vector2 position, int color, bool recordOperation = false)
    {
        int2 center = new int2((int)position.x, (int)position.y);
        
        if (recordOperation)
        {
            // 记录受影响的像素
            for (int y = center.y - brushSize; y <= center.y + brushSize; y++)
            {
                for (int x = center.x - brushSize; x <= center.x + brushSize; x++)
                {
                    if (x >= 0 && x < mapData.width && y >= 0 && y < mapData.height)
                    {
                        int index = y * mapData.width + x;
                        int dx = x - center.x;
                        int dy = y - center.y;
                        
                        if (dx * dx + dy * dy <= brushSize * brushSize)
                        {
                            int oldColor = mapData.colorData[index];
                            operationRecorder.RecordPixelChange(new int2(x, y), oldColor, color);
                        }
                    }
                }
            }
        }
        
        var drawJob = new DrawCircleJob
        {
            pixels = mapData.colorData,
            center = center,
            radius = brushSize,
            color = color,
            width = mapData.width,
            height = mapData.height
        };
        
        JobHandle handle = drawJob.Schedule(mapData.colorData.Length, 64);
        handle.Complete();
        
        UpdateCanvasTexture();
    }

    private void ScheduleFloodFillJob(Vector2 position, int fillColor)
    {
        if (!layerSystem.CanEditActiveLayer()) return;
        
        NativeArray<int> activeLayerData = layerSystem.GetActiveLayerData();
        int2 startPos = new int2((int)position.x, (int)position.y);
        int targetColor = activeLayerData[startPos.y * mapData.width + startPos.x];
        
        var fillJob = new FloodFillJob
        {
            pixels = activeLayerData,
            startPos = startPos,
            targetColor = targetColor,
            fillColor = fillColor,
            width = mapData.width,
            height = mapData.height
        };
        
        JobHandle handle = fillJob.Schedule();
        handle.Complete();
        
        UpdateCanvasTexture();
    }

    private void SchedulePickColorJob(Vector2 position)
    {
        NativeArray<int> compositeData = layerSystem.GetCompositeResult();
        int2 pos = new int2((int)position.x, (int)position.y);
        int colorInt = compositeData[pos.y * mapData.width + pos.x];
        currentColor = ColorUtils.IntToColor(colorInt);
        AddColorToHistory(colorInt);
        Repaint();
    }
    #endregion

    #region 文件操作
    private void ImportConfig()
    {
        string path = EditorUtility.OpenFilePanel("导入地图配置", "", "map,json,bin,config,txt");
        if (string.IsNullOrEmpty(path)) return;
        
        FileFormat format = DetectFileFormat(path);
        MapConfigData configData = fileSerializer.ImportFile(path, format);
        
        if (configData != null)
        {
            var pixelData = dataConverter.ConvertToPixelData(configData, out int width, out int height);
            mapData.Dispose();
            mapData = new MapData(width, height);
            pixelData.CopyTo(mapData.colorData);
            pixelData.Dispose();
            
            computeManager?.SetColorData(mapData.colorData);
            UpdateCanvasTexture();
            
            currentFilePath = path;
            currentFormat = format;
            undoRedoSystem.Clear();
            SaveState("导入文件");
            
            ShowNotification(new GUIContent($"成功导入: {Path.GetFileName(path)}"));
        }
    }

    private void ExportConfig()
    {
        string defaultName = $"map_export_{DateTime.Now:yyyyMMdd_HHmmss}";
        string path = EditorUtility.SaveFilePanel("导出地图配置", "", defaultName, GetFileExtension(currentFormat));
        if (string.IsNullOrEmpty(path)) return;
        
        NativeArray<int> compositeData = layerSystem.GetCompositeResult();
        MapConfigData configData = dataConverter.ConvertToConfigData(compositeData, mapData.width, mapData.height, "MapEditor");
        
        bool success = fileSerializer.ExportFile(configData, path, currentFormat, autoBackup);
        if (success) ShowNotification(new GUIContent($"成功导出: {Path.GetFileName(path)}"));
    }

    private void ExportPNG()
    {
        string defaultName = $"map_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = EditorUtility.SaveFilePanel("导出PNG图片", "", defaultName, "png");
        if (string.IsNullOrEmpty(path)) return;
        
        NativeArray<int> compositeData = layerSystem.GetCompositeResult();
        MapConfigData configData = dataConverter.ConvertToConfigData(compositeData, mapData.width, mapData.height);
        
        var exportOptions = new PNGExportOptions { resolution = 2048, transparentBackground = true };
        bool success = ExportPNGWithOptions(configData, path, exportOptions);
        if (success) ShowNotification(new GUIContent($"PNG导出成功: {Path.GetFileName(path)}"));
    }

    private bool ExportPNGWithOptions(MapConfigData configData, string path, PNGExportOptions options)
    {
        // 创建临时纹理，完成后立即销毁
        Texture2D exportTexture = null;
        try
        {
            exportTexture = CreateExportTexture(configData, options);
            byte[] pngData = exportTexture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            return true;
        }
        finally
        {
            if (exportTexture != null)
                UnityEngine.Object.DestroyImmediate(exportTexture);
        }
    }
    #endregion

    #region 工具方法
    // 更新 GetLocalMousePosition 方法以考虑平移
    private Vector2 GetLocalMousePosition(Vector2 mousePosition)
    {
        Rect canvasRect = new Rect(200, 30, position.width - 220, position.height - 60);
    
        // 考虑平移偏移
        float x = (mousePosition.x - canvasRect.x + panOffset.x * zoomLevel) / canvasRect.width * mapData.width / zoomLevel;
        float y = (mousePosition.y - canvasRect.y + panOffset.y * zoomLevel) / canvasRect.height * mapData.height / zoomLevel;
    
        x = Mathf.Clamp(x, 0, mapData.width - 1);
        y = Mathf.Clamp(y, 0, mapData.height - 1);
    
        return new Vector2(x, y);
    }

    private void UpdateCanvasTexture()
    {
        NativeArray<int> compositeData = layerSystem.GetCompositeResult();
        
        // 重用纹理和数组，避免GC分配
        if (reusableTexture.width != mapData.width || reusableTexture.height != mapData.height)
        {
            reusableTexture.Reinitialize(mapData.width, mapData.height);
            reusableColorArray = new Color32[mapData.width * mapData.height];
        }

        // 直接填充重用数组，避免创建新数组
        /*var fillReusableColorJob = new FillReusableColorJob
        {
            reusableColorArray = reusableColorArray,
            compositeData = compositeData
        };
        fillReusableColorJob.Schedule(compositeData.Length, 64).Complete();*/
        
        reusableTexture.SetPixels32(reusableColorArray);
        reusableTexture.Apply();
        
        Graphics.Blit(reusableTexture, canvasTexture);
    }

    private Texture2D CreateExportTexture(MapConfigData configData, PNGExportOptions options)
    {
        int targetWidth = options.resolution;
        int targetHeight = options.resolution;
        var texture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        
        Color backgroundColor = options.transparentBackground ? Color.clear : Color.white;
        for (int i = 0; i < targetWidth * targetHeight; i++)
        {
            texture.SetPixel(i % targetWidth, i / targetWidth, backgroundColor);
        }
        
        if (configData.fullColorData != null)
        {
            float scaleX = (float)targetWidth / configData.header.width;
            float scaleY = (float)targetHeight / configData.header.height;
            
            for (int i = 0; i < configData.fullColorData.Length; i++)
            {
                if (configData.fullColorData[i] != 0)
                {
                    int x = i % configData.header.width;
                    int y = i / configData.header.width;
                    int targetX = Mathf.RoundToInt(x * scaleX);
                    int targetY = Mathf.RoundToInt(y * scaleY);
                    
                    if (targetX < targetWidth && targetY < targetHeight)
                    {
                        Color color = ColorUtils.IntToColor(configData.fullColorData[i]);
                        texture.SetPixel(targetX, targetY, color);
                    }
                }
            }
        }
        
        texture.Apply();
        return texture;
    }

    private FileFormat DetectFileFormat(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        switch (extension)
        {
            case ".bin": return FileFormat.Binary;
            case ".json": return FileFormat.Json;
            case ".png": return FileFormat.PNG;
            case ".map": case ".config": case ".txt": return FileFormat.Legacy;
            default: return FileFormat.Binary;
        }
    }

    private string GetFileExtension(FileFormat format)
    {
        switch (format)
        {
            case FileFormat.Binary: return "bin";
            case FileFormat.Json: return "json";
            case FileFormat.PNG: return "png";
            case FileFormat.Legacy: return "map";
            default: return "bin";
        }
    }

    private void HandleDragAndDrop()
    {
        Event e = Event.current;
        if (e.type == EventType.DragUpdated && IsValidDragFile())
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == EventType.DragPerform && IsValidDragFile())
        {
            DragAndDrop.AcceptDrag();
            ImportFileByDrag(DragAndDrop.paths[0]);
            e.Use();
        }
    }

    private bool IsValidDragFile()
    {
        if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0) return false;
        string path = DragAndDrop.paths[0];
        string extension = Path.GetExtension(path).ToLower();
        return extension == ".map" || extension == ".json" || extension == ".bin" || 
               extension == ".config" || extension == ".png" || extension == ".txt";
    }

    private void ImportFileByDrag(string path)
    {
        FileFormat format = DetectFileFormat(path);
        MapConfigData configData = fileSerializer.ImportFile(path, format);
        
        if (configData != null)
        {
            var pixelData = dataConverter.ConvertToPixelData(configData, out int width, out int height);
            mapData.Dispose();
            mapData = new MapData(width, height);
            pixelData.CopyTo(mapData.colorData);
            pixelData.Dispose();
            
            computeManager?.SetColorData(mapData.colorData);
            UpdateCanvasTexture();
            
            currentFilePath = path;
            currentFormat = format;
            undoRedoSystem.Clear();
            SaveState("拖拽导入文件");
            
            ShowNotification(new GUIContent($"拖拽导入成功: {Path.GetFileName(path)}"));
        }
    }
    #endregion

    #region 撤销重做
    private void SaveState(string description)
    {
        undoRedoSystem.PushState(mapData.colorData, description);
    }

    private void StartOperation(string description)
    {
        lastOperationDescription = description;
        
        // 对于离散操作（如油漆桶），立即保存状态
        if (currentTool == ToolType.Bucket || currentTool == ToolType.Eyedropper)
        {
            SaveState(description);
        }
        else // 对于连续操作（如铅笔），开始记录
        {
            operationRecorder.BeginRecording();
            mapData.backupData.CopyFrom(mapData.colorData); // 备份初始状态
        }
    }

    private void EndOperation()
    {
        // 对于连续操作，保存完整操作
        if (currentTool == ToolType.Pencil || currentTool == ToolType.Eraser)
        {
            // 检查是否有实际变化
            bool hasChanges = false;
            for (int i = 0; i < mapData.colorData.Length; i++)
            {
                if (mapData.colorData[i] != mapData.backupData[i])
                {
                    hasChanges = true;
                    break;
                }
            }
            
            if (hasChanges)
            {
                SaveState(lastOperationDescription);
            }
        }
        
        operationRecorder.EndRecording();
    }

    private string GetOperationDescription()
    {
        switch (currentTool)
        {
            case ToolType.Pencil: return $"铅笔绘制 (大小:{brushSize})";
            case ToolType.Eraser: return $"擦除 (大小:{brushSize})";
            case ToolType.Bucket: return "油漆桶填充";
            case ToolType.Eyedropper: return "颜色取样";
            default: return "编辑操作";
        }
    }

    private void Undo()
    {
        if (undoRedoSystem.Undo(mapData.colorData, out string description))
        {
            ShowNotification(new GUIContent($"撤销: {description}"));
            UpdateCanvasTexture();
            
            // 更新Compute Shader数据
            computeManager?.SetColorData(mapData.colorData);
        }
    }

    private void Redo()
    {
        if (undoRedoSystem.Redo(mapData.colorData, out string description))
        {
            ShowNotification(new GUIContent($"重做: {description}"));
            UpdateCanvasTexture();
            
            // 更新Compute Shader数据
            computeManager?.SetColorData(mapData.colorData);
        }
    }
    
    #endregion

    #region 标注系统
    private void RefreshAnnotations()
    {
        annotationSystem.UpdateAnnotations(mapData.colorData, mapData.width, mapData.height, currentMousePosition);
        Repaint();
    }

    private void ClearAnnotations()
    {
        annotationSystem.ClearAnnotations();
        Repaint();
    }

    private void ShowRegionNamePopup()
    {
        var popup = EditorWindow.GetWindow<RegionNamePopup>(true, "设置区域名称");
        popup.Initialize(currentMousePosition, annotationSystem);
        popup.Show();
    }

    private void AddColorToHistory(int colorInt)
    {
        Color color = ColorUtils.IntToColor(colorInt);
        float4 colorVec = new float4(color.r, color.g, color.b, color.a);
        
        if (colorHistory.Length >= 256) colorHistory.RemoveAt(0);
        colorHistory.Add(colorVec);
    }
    #endregion

    #region 图层系统
    private void OnLayersChanged()
    {
        UpdateCanvasTexture();
        Repaint();
    }

    private void ClearCanvas()
    {
        SaveState("清空画布");
        layerSystem.ClearActiveLayer();
    }
    #endregion

    #region 工具函数

    private void OnUndoRedoStateChanged()
    {
        Repaint();
        UpdateCanvasTexture();
    }

    private void ClearHistory()
    {
        undoRedoSystem.Clear();
        ShowNotification(new GUIContent("历史记录已清空"));
    }

    private void ShowHistoryDetails()
    {
        Debug.Log("=== 撤销重做系统状态 ===");
        Debug.Log($"最大历史步数: {undoRedoSystem.UndoCount}");
    }
    #endregion
}