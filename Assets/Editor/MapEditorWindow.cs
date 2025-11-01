using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MapEditor
{
    public partial class MapEditorWindow : EditorWindow
    {
        private MapDataAsset currentMapData;
        private Texture2D backgroundTexture;

        // 工具系统
        private BaseEditorTool currentTool;
        private Dictionary<ToolType, BaseEditorTool> tools = new Dictionary<ToolType, BaseEditorTool>();

        // UI 组件
        private MapEditorToolbar toolbar;
        private LayersPanel layersPanel;
        private PropertiesPanel propertiesPanel;
        private ColorPickerPanel colorPickerPanel;

        // 渲染
        private MapTextureRenderer textureRenderer;
        private RenderTexture previewTexture;
        private Vector2 scrollPosition;
        private float zoomLevel = 1.0f;
        private bool showGrid = true;

        // 状态
        private Color currentColor = Color.red;
        private int brushSize = 3;
        private float fillTolerance = 0.1f;

        // 在 MapEditorWindow 类的成员变量区域添加：
        private bool showBackground = true;
        private bool showBlockLabels = true;
        private bool showErrorOverlay = false;
        private bool showLayersPanel = true;
        private bool showPropertiesPanel = true;
        private bool showColorPickerPanel = true;
        private bool showDebugInfo = true;
        private UndoRedoManager undoRedoManager;
        private List<Vector2Int> selectedPixels = new List<Vector2Int>();
        private RectInt selectionBounds = new RectInt();
        private bool hasSelection = false;
        private SelectionTool selectionTool;
        
        [MenuItem("Tools/Map Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<MapEditorWindow>("Map Editor");
            window.minSize = new Vector2(1200, 800);
            window.Show();
        }
        
        private void InitializeManagers()
        {
            undoRedoManager = new UndoRedoManager();
        }
        
        private void OnEnable()
        {
            InitializeTools();
            InitializeUI();
            InitializeRendering();
            InitializeManagers();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void InitializeTools()
        {
            tools.Clear();

            tools[ToolType.Pencil] = new PencilEditorTool(this);
            tools[ToolType.Bucket] = new BucketEditorTool(this);
            tools[ToolType.Eraser] = new EraserEditorTool(this);
            tools[ToolType.Eyedropper] = new EyedropperEditorTool(this);
            tools[ToolType.Selection] = new SelectionTool(this);
            
            currentTool = tools[ToolType.Pencil];
            selectionTool = tools[ToolType.Selection] as SelectionTool;

        }

        private void InitializeUI()
        {
            toolbar = new MapEditorToolbar(this);
            layersPanel = new LayersPanel(this);
            propertiesPanel = new PropertiesPanel(this);
            colorPickerPanel = new ColorPickerPanel(this);
        }

        private void InitializeRendering()
        {
            textureRenderer = new MapTextureRenderer();
            previewTexture = new RenderTexture(1024, 512, 0, RenderTextureFormat.ARGB32);
        }

        private void Cleanup()
        {
            if (previewTexture != null)
            {
                previewTexture.Release();
                DestroyImmediate(previewTexture);
            }

            textureRenderer?.Cleanup();
        }

        private void OnGUI()
        {
            try
            {
                DrawMenuBar();
                DrawToolbar();
                DrawUndoRedoStatus();
                EditorGUILayout.BeginHorizontal();
                {
                    DrawSidePanels();
                    DrawMainCanvas();
                }
                EditorGUILayout.EndHorizontal();

                DrawStatusBar();

                HandleEvents();
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error in Map Editor: {e.Message}", MessageType.Error);
                Debug.LogException(e);
            }
        }

        private void DrawMenuBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button("File", EditorStyles.toolbarDropDown))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("New Map"), false, OnNewMap);
                    menu.AddItem(new GUIContent("Open Map"), false, OnOpenMap);
                    menu.AddItem(new GUIContent("Save Map"), false, OnSaveMap);
                    menu.AddItem(new GUIContent("Save As..."), false, OnSaveAs);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Import PNG"), false, OnImportPNG);
                    menu.AddItem(new GUIContent("Export PNG"), false, OnExportPNG);
                    menu.ShowAsContext();
                }

                if (GUILayout.Button("Edit", EditorStyles.toolbarDropDown))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Undo (Ctrl+Z)"), false, OnUndo);
                    menu.AddItem(new GUIContent("Redo (Ctrl+Y)"), false, OnRedo);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Select All"), false, OnSelectAll);
                    menu.AddItem(new GUIContent("Clear Selection"), false, OnClearSelection);
                    menu.ShowAsContext();
                }

                if (GUILayout.Button("View", EditorStyles.toolbarDropDown))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Show Grid"), showGrid, () => showGrid = !showGrid);
                    menu.AddItem(new GUIContent("Show Background"), true, () => { });
                    menu.AddItem(new GUIContent("Show Block Labels"), true, () => { });
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Zoom In"), false, () => zoomLevel = Mathf.Min(zoomLevel * 1.5f, 10f));
                    menu.AddItem(new GUIContent("Zoom Out"), false,
                        () => zoomLevel = Mathf.Max(zoomLevel / 1.5f, 0.1f));
                    menu.AddItem(new GUIContent("Reset Zoom"), false, () => zoomLevel = 1.0f);
                    menu.ShowAsContext();
                }

                GUILayout.FlexibleSpace();

                if (currentMapData != null)
                {
                    EditorGUILayout.LabelField($"Current: {currentMapData.name}", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("No map loaded", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            toolbar?.DrawToolbar();
        }


        // 修改 MapEditorWindow 中的 DrawSidePanels 方法：
        private void DrawSidePanels()
        {
            // 只有至少一个面板可见时才绘制侧边栏容器
            if (showColorPickerPanel || showPropertiesPanel || showLayersPanel)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(250));
                {
                    if (showColorPickerPanel)
                    {
                        colorPickerPanel?.DrawPanel();
                        EditorGUILayout.Space(5);
                    }

                    if (showPropertiesPanel)
                    {
                        propertiesPanel?.DrawPanel();
                        EditorGUILayout.Space(5);
                    }

                    if (showLayersPanel)
                    {
                        layersPanel?.DrawPanel();
                    }
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                // 全屏模式提示
                GUILayout.Box("Full Screen Mode - Press F11 to show panels",
                    GUILayout.Width(200), GUILayout.Height(60));
            }
        }

        private void DrawMainCanvas()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                // 画布控制栏
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                {
                    EditorGUILayout.LabelField("Canvas", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 20, GUILayout.Width(200));

                    if (currentTool != null && currentTool.GetToolType() == ToolType.Bucket)
                    {
                        fillTolerance =
                            EditorGUILayout.Slider("Tolerance", fillTolerance, 0f, 1f, GUILayout.Width(200));
                    }

                    if (GUILayout.Button("Run Error Check", EditorStyles.miniButton))
                    {
                        RunErrorCheck();
                    }
                }
                EditorGUILayout.EndHorizontal();
                Repaint();

                var canvasArea = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (currentMapData != null)
                {
                    DrawCanvas(canvasArea);
                }
                else
                {
                    DrawEmptyCanvas(canvasArea);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCanvas(Rect canvasArea)
        {
            // 绘制背景
            EditorGUI.DrawRect(canvasArea, new Color(0.15f, 0.15f, 0.15f));
            Rect drawArea = CalculateDrawArea(canvasArea);
            DrawBoundsAndDebugInfo(drawArea, canvasArea);
            DrawSelection(drawArea);
            // 计算绘制区域
            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.BeginGroup(canvasArea);
            {
                drawArea = CalculateDrawArea(canvasArea);
                drawArea = new Rect(
                    drawArea.x - canvasArea.x, // 减去canvas的x偏移
                    drawArea.y - canvasArea.y, // 减去canvas的y偏移
                    drawArea.width,
                    drawArea.height
                );
                if (currentMapData != null)
                {
                    var texture = currentMapData.GetColorMapTexture();
                    if (texture != null)
                    {
                        GUI.DrawTexture(drawArea, texture, ScaleMode.StretchToFill);
                    }
                }

                // 绘制地图背景（在绘制区域内）
                if (showBackground && backgroundTexture != null)
                {
                    GUI.DrawTexture(drawArea, backgroundTexture, ScaleMode.StretchToFill);
                }

                // 绘制地图内容（在绘制区域内）
                if (currentMapData != null && currentMapData.GetColorMapTexture() != null)
                {
                    GUI.DrawTexture(drawArea, currentMapData.GetColorMapTexture(), ScaleMode.StretchToFill);
                }

                // 绘制网格
                if (showGrid && zoomLevel > 0.5f)
                {
                    DrawGrid(drawArea);
                }

                // 绘制块标签
                if (showBlockLabels && zoomLevel > 1f)
                {
                    DrawBlockLabels(drawArea);
                }

                // 绘制错误覆盖层
                if (showErrorOverlay)
                {
                    DrawErrorOverlay(drawArea);
                }

                // 绘制工具预览
                currentTool?.DrawPreview(drawArea);

                // 处理画布事件
                HandleCanvasEvents(drawArea);

                // 绘制调试信息
                DrawDebugInfo(drawArea, canvasArea);
            }
            GUI.EndGroup();

            GUI.matrix = originalMatrix;
        }

        private double lastRepaintTime;
        private const double minRepaintInterval = 0.016; // ~60 FPS

        private void RequestRepaint()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastRepaintTime >= minRepaintInterval)
            {
                Repaint();
                lastRepaintTime = currentTime;
            }
        }

        /// <summary>
        /// 绘制拖动状态指示器
        /// </summary>
        private void DrawPanningIndicator(Rect drawArea)
        {
            Handles.BeginGUI();

            // 在画布角落显示拖动指示器
            Rect indicatorRect = new Rect(drawArea.x + 10, drawArea.y + 10, 120, 40);
            EditorGUI.DrawRect(indicatorRect, new Color(0, 0, 0, 0.7f));

            GUIStyle indicatorStyle = new GUIStyle(EditorStyles.whiteLabel);
            indicatorStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Label(indicatorRect, "Panning Mode\n(Middle Mouse)", indicatorStyle);

            Handles.EndGUI();
        }

        /// <summary>
        /// 绘制调试信息
        /// </summary>
        private void DrawDebugInfo(Rect drawArea, Rect canvasArea)
        {
            Handles.BeginGUI();

            Vector2 mousePos = Event.current.mousePosition;
            Vector2 mapPos = ScreenToMapPosition(mousePos, drawArea);
            Vector2Int intMapPos = new Vector2Int(Mathf.FloorToInt(mapPos.x), Mathf.FloorToInt(mapPos.y));

            string debugInfo = $"Canvas: {canvasArea.width:F0}x{canvasArea.height:F0}\n" +
                               $"DrawArea: {drawArea.width:F0}x{drawArea.height:F0}\n" +
                               $"Mouse: ({mousePos.x:F0}, {mousePos.y:F0})\n" +
                               $"Map: ({intMapPos.x}, {intMapPos.y})\n" +
                               $"Valid: {IsMapPositionValid(intMapPos)}";

            GUIStyle debugStyle = new GUIStyle(EditorStyles.whiteLabel);
            debugStyle.normal.textColor = Color.yellow;
            debugStyle.fontSize = 10;

            Rect debugRect = new Rect(canvasArea.x + 10, canvasArea.y + 10, 180, 90);
            EditorGUI.DrawRect(debugRect, new Color(0, 0, 0, 0.8f));
            GUI.Label(debugRect, debugInfo, debugStyle);

            // 绘制鼠标位置标记
            if (drawArea.Contains(mousePos))
            {
                // 十字准星
                Handles.color = Color.red;
                float crossSize = 15f;
                Handles.DrawLine(
                    new Vector3(mousePos.x - crossSize, mousePos.y),
                    new Vector3(mousePos.x + crossSize, mousePos.y)
                );
                Handles.DrawLine(
                    new Vector3(mousePos.x, mousePos.y - crossSize),
                    new Vector3(mousePos.x, mousePos.y + crossSize)
                );

                // 对应的地图位置标记
                Vector2 mapScreenPos = MapToScreenPosition(intMapPos, drawArea);
                Handles.color = Color.blue;
                Handles.DrawWireDisc(mapScreenPos, Vector3.forward, 5f);

                // 连接线
                Handles.color = Color.cyan;
                Handles.DrawDottedLine(mousePos, mapScreenPos, 2f);
            }

            Handles.EndGUI();
        }
        // 在 MapEditorWindow 类中添加：

        /// <summary>
        /// 绘制块标签
        /// </summary>
        private void DrawBlockLabels(Rect drawArea)
        {
            if (currentMapData == null || currentMapData.colorBlocks.Count == 0) return;

            Handles.BeginGUI();

            // 使用默认的标签样式
            GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteLabel);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = Color.white;

            // 临时：在固定位置显示测试标签
            // 在实际应用中，您需要根据颜色块的实际位置来计算标签位置
            Vector2Int[] testPositions = new Vector2Int[]
            {
                new Vector2Int(100, 100),
                new Vector2Int(currentMapData.width - 100, 100),
                new Vector2Int(100, currentMapData.height - 100),
                new Vector2Int(currentMapData.width - 100, currentMapData.height - 100)
            };

            for (int i = 0; i < Mathf.Min(currentMapData.colorBlocks.Count, testPositions.Length); i++)
            {
                var block = currentMapData.colorBlocks[i];
                Vector2Int mapPos = testPositions[i];

                Vector2 screenPos = MapToScreenPosition(mapPos, drawArea);

                GUIContent labelContent = new GUIContent($"{block.name} (ID:{block.id})");
                Vector2 labelSize = labelStyle.CalcSize(labelContent);

                Rect labelRect = new Rect(
                    screenPos.x - labelSize.x / 2,
                    screenPos.y - labelSize.y / 2,
                    labelSize.x,
                    labelSize.y
                );

                // 绘制半透明背景
                EditorGUI.DrawRect(new Rect(
                    labelRect.x - 2, labelRect.y - 1,
                    labelRect.width + 4, labelRect.height + 2
                ), new Color(0, 0, 0, 0.7f));

                // 绘制标签文本
                GUI.Label(labelRect, labelContent, labelStyle);

                // 在标签位置绘制一个小点标记
                Handles.color = block.color;
                Handles.DrawSolidDisc(screenPos, Vector3.forward, 3f);
            }

            Handles.EndGUI();
        }

        /// <summary>
        /// 将地图坐标转换为屏幕坐标
        /// </summary>
        /// <param name="mapPos">地图坐标</param>
        /// <param name="canvasArea">画布区域</param>
        /// <returns>屏幕坐标</returns>
        public Vector2 MapToScreenPosition(Vector2Int mapPos, Rect canvasArea)
        {
            return MapToScreenPosition(new Vector2(mapPos.x, mapPos.y), canvasArea);
        }


        /// <summary>
        /// 将屏幕坐标转换为地图坐标（精确版本）
        /// </summary>
        public Vector2 ScreenToMapPosition(Vector2 screenPos, Rect drawArea)
        {
            if (currentMapData == null || drawArea.width <= 0 || drawArea.height <= 0)
                return Vector2.zero;

            // 计算在绘制区域内的相对位置 (0-1)
            float relativeX = (screenPos.x - drawArea.x) / drawArea.width;
            float relativeY = (screenPos.y - drawArea.y) / drawArea.height;

            // 转换为地图坐标（注意Y轴翻转）
            float mapX = relativeX * currentMapData.width;
            float mapY = (1f - relativeY) * currentMapData.height;

            return new Vector2(mapX, mapY);
        }

        /// <summary>
        /// 将地图坐标转换为屏幕坐标（精确版本）
        /// </summary>
        public Vector2 MapToScreenPosition(Vector2 mapPos, Rect drawArea)
        {
            if (currentMapData == null || drawArea.width <= 0 || drawArea.height <= 0)
                return Vector2.zero;

            // 计算相对位置 (0-1)
            float relativeX = mapPos.x / currentMapData.width;
            float relativeY = 1f - (mapPos.y / currentMapData.height); // Y轴翻转

            // 转换为屏幕坐标
            float screenX = drawArea.x + relativeX * drawArea.width;
            float screenY = drawArea.y + relativeY * drawArea.height;

            return new Vector2(screenX, screenY);
        }

        /// <summary>
        /// 将屏幕坐标转换为地图坐标（整数版本）
        /// </summary>
        /// <param name="screenPos">屏幕坐标</param>
        /// <param name="canvasArea">画布区域</param>
        /// <returns>地图坐标</returns>
        public Vector2Int ScreenToMapPositionInt(Vector2 screenPos, Rect canvasArea)
        {
            Vector2 mapPos = ScreenToMapPosition(screenPos, canvasArea);
            return new Vector2Int(Mathf.FloorToInt(mapPos.x), Mathf.FloorToInt(mapPos.y));
        }

        /// <summary>
        /// 绘制错误覆盖层
        /// </summary>
        private void DrawErrorOverlay(Rect drawArea)
        {
            // 调用错误检测管理器的绘制方法
            // errorDetectionManager?.DrawErrorOverlay(drawArea);

            // 临时实现：绘制测试错误标记
            Handles.BeginGUI();
            Handles.color = Color.red;

            // 在画布四个角绘制测试错误标记
            float markerSize = 10f;
            Vector2[] testPositions = new Vector2[]
            {
                new Vector2(drawArea.x + 20, drawArea.y + 20),
                new Vector2(drawArea.xMax - 20, drawArea.y + 20),
                new Vector2(drawArea.x + 20, drawArea.yMax - 20),
                new Vector2(drawArea.xMax - 20, drawArea.yMax - 20)
            };

            foreach (var pos in testPositions)
            {
                Handles.DrawSolidDisc(pos, Vector3.forward, markerSize);
            }

            Handles.EndGUI();
        }

        /// <summary>
        /// 获取对比色（用于标签文字颜色）
        /// </summary>
        private Color GetContrastColor(Color backgroundColor)
        {
            // 计算亮度
            float brightness = (backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f);
            return brightness > 0.5f ? Color.black : Color.white;
        }

        private void DrawEmptyCanvas(Rect canvasArea)
        {
            EditorGUI.DrawRect(canvasArea, new Color(0.1f, 0.1f, 0.1f));

            GUIStyle centeredStyle = new GUIStyle(EditorStyles.label);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.normal.textColor = Color.gray;

            EditorGUI.LabelField(canvasArea, "No map loaded. Create a new map or open an existing one.", centeredStyle);

            Rect buttonRect = new Rect(canvasArea.center.x - 50, canvasArea.center.y + 20, 100, 25);
            if (GUI.Button(buttonRect, "New Map"))
            {
                OnNewMap();
            }
        }

// 更新 DrawStatusBar 方法显示当前状态：

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                // 获取鼠标位置和画布区域的正确方式
                Vector2 mousePos = Event.current.mousePosition;

                // 创建一个固定的状态栏区域，而不是依赖 GetLastRect()
                Rect statusBarRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));

                // 计算画布区域（基于窗口布局）
                Rect canvasArea = CalculateCanvasArea();

                if (IsMouseInCanvas(mousePos, canvasArea))
                {
                    Vector2 mapPos = ScreenToMapPosition(mousePos, canvasArea);
                    EditorGUILayout.LabelField(
                        $"Position: ({Mathf.FloorToInt(mapPos.x)}, {Mathf.FloorToInt(mapPos.y)})",
                        GUILayout.Width(120));
                }
                else
                {
                    EditorGUILayout.LabelField("Position: (---, ---)", GUILayout.Width(120));
                }

                // 显示状态指示器
                EditorGUILayout.LabelField($"Grid: {(showGrid ? "ON" : "OFF")}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"BG: {(showBackground ? "ON" : "OFF")}", GUILayout.Width(50));
                EditorGUILayout.LabelField($"Labels: {(showBlockLabels ? "ON" : "OFF")}", GUILayout.Width(70));

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField($"Zoom: {zoomLevel:F1}x", GUILayout.Width(60));

                string toolName = currentTool != null ? currentTool.GetToolType().ToString() : "None";
                EditorGUILayout.LabelField($"Tool: {toolName}", GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 计算画布区域（基于当前窗口布局）
        /// </summary>
        private Rect CalculateCanvasArea()
        {
            float menuBarHeight = 20f;
            float toolbarHeight = 22f;
            float statusBarHeight = 20f;
            float sidePanelWidth = (showLayersPanel || showPropertiesPanel || showColorPickerPanel) ? 250f : 0f;

            return new Rect(
                sidePanelWidth,
                menuBarHeight + toolbarHeight,
                position.width - sidePanelWidth,
                position.height - menuBarHeight - toolbarHeight - statusBarHeight
            );
        }

        /// <summary>
        /// 计算绘制区域（确保在画布范围内）
        /// </summary>
        private Rect CalculateDrawArea(Rect canvasArea)
        {
            if (currentMapData == null)
                return canvasArea;

            float mapAspect = (float)currentMapData.width / currentMapData.height;
            float canvasAspect = canvasArea.width / canvasArea.height;

            float baseWidth, baseHeight;

            if (canvasAspect > mapAspect)
            {
                baseHeight = canvasArea.height * 0.95f;
                baseWidth = baseHeight * mapAspect;
            }
            else
            {
                baseWidth = canvasArea.width * 0.95f;
                baseHeight = baseWidth / mapAspect;
            }

            // 应用缩放
            float scaledWidth = baseWidth * zoomLevel;
            float scaledHeight = baseHeight * zoomLevel;

            // 应用平移偏移（但确保不会超出画布）
            float centerX = canvasArea.center.x;
            float centerY = canvasArea.center.y;

            // 计算初始位置
            Rect drawArea = new Rect(
                centerX - scaledWidth * 0.5f + panOffset.x,
                centerY - scaledHeight * 0.5f + panOffset.y,
                scaledWidth,
                scaledHeight
            );

            // 确保不会超出画布边界
            if (drawArea.x < canvasArea.x)
            {
                drawArea.x = canvasArea.x;
            }

            if (drawArea.y < canvasArea.y)
            {
                drawArea.y = canvasArea.y;
            }

            if (drawArea.xMax > canvasArea.xMax)
            {
                drawArea.x = canvasArea.xMax - drawArea.width;
            }

            if (drawArea.yMax > canvasArea.yMax)
            {
                drawArea.y = canvasArea.yMax - drawArea.height;
            }

            return drawArea;
        }

        /// <summary>
        /// 绘制网格
        /// </summary>
        private void DrawGrid(Rect drawArea)
        {
            if (currentMapData == null) return;

            Handles.BeginGUI();

            // 根据缩放级别调整网格密度
            float gridDensity = Mathf.Clamp(20f * zoomLevel, 5f, 200f);
            Color gridColor = new Color(1, 1, 1, Mathf.Clamp(0.1f * zoomLevel, 0.05f, 0.3f));
            Handles.color = gridColor;

            // 计算网格起始位置（考虑平移）
            float startX = -panOffset.x * zoomLevel;
            float startY = -panOffset.y * zoomLevel;
            float endX = startX + currentMapData.width * zoomLevel;
            float endY = startY + currentMapData.height * zoomLevel;

            // 绘制垂直线
            for (float x = startX; x <= endX; x += gridDensity)
            {
                Vector3 start = new Vector3(drawArea.x + x, drawArea.y);
                Vector3 end = new Vector3(drawArea.x + x, drawArea.y + drawArea.height);
                Handles.DrawLine(start, end);
            }

            // 绘制水平线
            for (float y = startY; y <= endY; y += gridDensity)
            {
                Vector3 start = new Vector3(drawArea.x, drawArea.y + y);
                Vector3 end = new Vector3(drawArea.x + drawArea.width, drawArea.y + y);
                Handles.DrawLine(start, end);
            }

            Handles.EndGUI();
        }

        /// <summary>
        /// 处理画布事件
        /// </summary>
        private void HandleCanvasEvents(Rect drawArea)
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (!drawArea.Contains(mousePos))
                return;

            Vector2 mapPos = ScreenToMapPosition(mousePos, drawArea);
            Vector2Int intMapPos = new Vector2Int(Mathf.FloorToInt(mapPos.x), Mathf.FloorToInt(mapPos.y));

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0) // 左键 - 工具操作
                    {
                        currentTool?.OnMouseDown(intMapPos);
                        e.Use();
                    }
                    else if (e.button == 2 || (e.button == 1 && e.alt)) // 中键或Alt+右键 - 开始拖动
                    {
                        StartPanning(mousePos);
                        e.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (e.button == 0) // 左键拖动 - 工具操作
                    {
                        currentTool?.OnMouseDrag(intMapPos);
                        e.Use();
                    }
                    else if (e.button == 2 || (e.button == 1 && e.alt)) // 中键或Alt+右键拖动 - 拖动地图
                    {
                        UpdatePanning(mousePos);
                        e.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (e.button == 0) // 左键释放
                    {
                        currentTool?.OnMouseUp(intMapPos);
                        e.Use();
                    }
                    else if (e.button == 2 || e.button == 1) // 中键或右键释放 - 停止拖动
                    {
                        StopPanning();
                        e.Use();
                    }

                    break;

                case EventType.MouseMove:
                    currentTool?.OnMouseMove(intMapPos);
                    break;

                case EventType.ScrollWheel:
                    HandleZoom(mousePos, drawArea, e.delta.y);
                    e.Use();
                    break;

                case EventType.KeyDown:
                    // 添加键盘快捷键
                    if (e.keyCode == KeyCode.Home) // Home键 - 重置视图
                    {
                        ResetView();
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.Equals || e.keyCode == KeyCode.Plus) // +键 - 放大
                    {
                        zoomLevel = Mathf.Clamp(zoomLevel * 1.2f, 0.1f, 10f);
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.Minus) // -键 - 缩小
                    {
                        zoomLevel = Mathf.Clamp(zoomLevel / 1.2f, 0.1f, 10f);
                        e.Use();
                    }

                    break;
            }

            // 更新鼠标光标
            UpdateMouseCursor();
        }

        /// <summary>
        /// 处理缩放（带边界检查）
        /// </summary>
        private void HandleZoom(Vector2 mousePos, Rect drawArea, float scrollDelta)
        {
            if (currentMapData == null) return;

            // 保存缩放前的状态
            Vector2 mapPosBeforeZoom = ScreenToMapPosition(mousePos, drawArea);
            float oldZoom = zoomLevel;

            // 计算新的缩放级别
            float zoomDelta = -scrollDelta * 0.1f;
            float newZoomLevel = Mathf.Clamp(zoomLevel + zoomDelta, 0.1f, 10f);

            if (Mathf.Approximately(newZoomLevel, zoomLevel))
                return;

            zoomLevel = newZoomLevel;

            // 重新计算绘制区域
            Rect canvasArea = CalculateCanvasArea();
            Rect newDrawArea = CalculateDrawArea(canvasArea);
            Vector2 mapPosAfterZoom = ScreenToMapPosition(mousePos, newDrawArea);

            // 调整平移以保持鼠标位置
            Vector2 mapPosDelta = mapPosAfterZoom - mapPosBeforeZoom;
            panOffset -= mapPosDelta;

            // 强制限制边界
            ForceClampToBounds();

            Repaint();
        }

        /// <summary>
        /// 强制限制在边界内
        /// </summary>
        private void ForceClampToBounds()
        {
            Rect canvasArea = CalculateCanvasArea();
            Rect drawArea = CalculateDrawArea(canvasArea);

            // 如果绘制区域完全在画布内，重置平移
            if (drawArea.width <= canvasArea.width && drawArea.height <= canvasArea.height)
            {
                panOffset = Vector2.zero;
                return;
            }

            // 检查左右边界
            if (drawArea.x > canvasArea.x)
            {
                // 左边超出，向右调整
                panOffset.x -= (drawArea.x - canvasArea.x) / zoomLevel;
            }
            else if (drawArea.xMax < canvasArea.xMax)
            {
                // 右边超出，向左调整
                panOffset.x += (canvasArea.xMax - drawArea.xMax) / zoomLevel;
            }

            // 检查上下边界
            if (drawArea.y > canvasArea.y)
            {
                // 上边超出，向下调整
                panOffset.y -= (drawArea.y - canvasArea.y) / zoomLevel;
            }
            else if (drawArea.yMax < canvasArea.yMax)
            {
                // 下边超出，向上调整
                panOffset.y += (canvasArea.yMax - drawArea.yMax) / zoomLevel;
            }

            // 最终限制
            ClampPanOffset();
        }

        /// <summary>
        /// 更新鼠标光标
        /// </summary>
        private void UpdateMouseCursor()
        {
            if (isPanning)
            {
                EditorGUIUtility.AddCursorRect(new Rect(0, 0, position.width, position.height), MouseCursor.Pan);
            }
            else
            {
                // 根据当前工具设置光标
                switch (currentTool?.GetToolType())
                {
                    case ToolType.Pencil:
                    case ToolType.Eraser:
                        EditorGUIUtility.AddCursorRect(new Rect(0, 0, position.width, position.height),
                            MouseCursor.ArrowPlus);
                        break;
                    case ToolType.Bucket:
                        EditorGUIUtility.AddCursorRect(new Rect(0, 0, position.width, position.height),
                            MouseCursor.Arrow);
                        break;
                    case ToolType.Eyedropper:
                        EditorGUIUtility.AddCursorRect(new Rect(0, 0, position.width, position.height),
                            MouseCursor.Arrow);
                        break;
                    default:
                        EditorGUIUtility.AddCursorRect(new Rect(0, 0, position.width, position.height),
                            MouseCursor.Arrow);
                        break;
                }
            }
        }

        private bool IsMouseInCanvas(Vector2 mousePos, Rect canvasArea)
        {
            return canvasArea.Contains(mousePos);
        }

        // 在 MapEditorWindow 的 HandleEvents 方法中添加快捷键：

        private void HandleEvents()
        {
            Event e = Event.current;

            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.B:
                        SetCurrentTool(ToolType.Pencil);
                        e.Use();
                        break;
                    case KeyCode.G:
                        SetCurrentTool(ToolType.Bucket);
                        e.Use();
                        break;
                    case KeyCode.E:
                        SetCurrentTool(ToolType.Eraser);
                        e.Use();
                        break;
                    case KeyCode.I:
                        SetCurrentTool(ToolType.Eyedropper);
                        e.Use();
                        break;
                    case KeyCode.Z:
                        if (e.control)
                        {
                            OnUndo();
                            e.Use();
                        }

                        break;
                    case KeyCode.Y:
                        if (e.control)
                        {
                            OnRedo();
                            e.Use();
                        }

                        break;
                    case KeyCode.A:
                        if (e.control)
                        {
                            OnSelectAll();
                            e.Use();
                        }
                        break;
                    case KeyCode.D:
                        if (e.control)
                        {
                            OnClearSelection();
                            e.Use();
                        }
                        break;
                    case KeyCode.Delete:
                        if (hasSelection)
                        {
                            DeleteSelectionOptimized();
                            e.Use();
                        }
                        break;
                    case KeyCode.F11:
                        ToggleFullScreen();
                        e.Use();
                        break;
                    case KeyCode.F1:
                        ToggleGrid();
                        e.Use();
                        break;
                    case KeyCode.F2:
                        ToggleLayersPanel();
                        e.Use();
                        break;
                    case KeyCode.F3:
                        ToggleBackground();
                        e.Use();
                        break;
                    case KeyCode.Equals: // +键
                    case KeyCode.Plus:
                        if (e.control)
                        {
                            SetZoomLevel(zoomLevel * 1.2f, Event.current.mousePosition);
                            e.Use();
                        }

                        break;
                    case KeyCode.Minus: // -键
                        if (e.control)
                        {
                            SetZoomLevel(zoomLevel / 1.2f, Event.current.mousePosition);
                            e.Use();
                        }

                        break;

                    case KeyCode.Alpha0: // 0键 - 适应窗口
                        if (e.control)
                        {
                            ZoomToFit();
                            e.Use();
                        }

                        break;

                    case KeyCode.Alpha1: // 1键 - 实际大小
                        if (e.control)
                        {
                            ZoomToActualSize();
                            e.Use();
                        }

                        break;
                }
            }
        }

// 添加全屏切换方法
        private void ToggleFullScreen()
        {
            if (AreAllPanelsVisible())
            {
                HideAllPanels();
            }
            else
            {
                ShowAllPanels();
            }
        }

        // 公共方法
        public void SetCurrentTool(ToolType toolType)
        {
            if (tools.ContainsKey(toolType))
            {
                currentTool = tools[toolType];
                Repaint();
            }
        }

        public void SetCurrentColor(Color color)
        {
            currentColor = color;
            Repaint();
        }

        public void SetBrushSize(int size)
        {
            brushSize = Mathf.Clamp(size, 1, 50);
        }

        // 工具访问器
        public MapDataAsset GetCurrentMapData() => currentMapData;
        public Color GetCurrentColor() => currentColor;
        public int GetBrushSize() => brushSize;
        public float GetFillTolerance() => fillTolerance;
        public BaseEditorTool GetCurrentTool() => currentTool;

        // 菜单操作
        private void OnNewMap()
        {
            MapDataAsset newMap = CreateInstance<MapDataAsset>();
            newMap.width = 4096;
            newMap.height = 2048;
            newMap.name = "NewMap";

            string path = EditorUtility.SaveFilePanel("Create New Map", "Assets", "NewMap", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = FileUtil.GetProjectRelativePath(path);
                AssetDatabase.CreateAsset(newMap, path);
                AssetDatabase.SaveAssets();

                currentMapData = newMap;
                Repaint();
            }
        }

        private void OnOpenMap()
        {
            string path = EditorUtility.OpenFilePanel("Open Map", "Assets", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = FileUtil.GetProjectRelativePath(path);
                MapDataAsset mapData = AssetDatabase.LoadAssetAtPath<MapDataAsset>(path);
                if (mapData != null)
                {
                    currentMapData = mapData;
                    Repaint();
                }
            }
        }

        private void OnSaveMap()
        {
            if (currentMapData != null)
            {
                EditorUtility.SetDirty(currentMapData);
                AssetDatabase.SaveAssets();
            }
        }

        private void OnSaveAs()
        {
            if (currentMapData != null)
            {
                string path = EditorUtility.SaveFilePanel("Save Map As", "Assets", currentMapData.name, "asset");
                if (!string.IsNullOrEmpty(path))
                {
                    path = FileUtil.GetProjectRelativePath(path);
                    AssetDatabase.CreateAsset(Instantiate(currentMapData), path);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void OnImportPNG()
        {
            // 实现PNG导入逻辑
        }

        private void OnExportPNG()
        {
            if (currentMapData != null && currentMapData.GetColorMapTexture() != null)
            {
                string path = EditorUtility.SaveFilePanel("Export PNG", "", currentMapData.name, "png");
                if (!string.IsNullOrEmpty(path))
                {
                    byte[] pngData = currentMapData.GetColorMapTexture().EncodeToPNG();
                    System.IO.File.WriteAllBytes(path, pngData);
                    AssetDatabase.Refresh();
                }
            }
        }

        public void RunErrorCheck()
        {
            // 实现错误检查逻辑
            EditorUtility.DisplayDialog("Error Check", "Error check completed!", "OK");
        }

        // 在 MapEditorWindow 类中添加这些方法：

        /// <summary>
        /// 切换网格可见性
        /// </summary>
        public void ToggleGrid()
        {
            showGrid = !showGrid;
            Debug.Log($"Grid visibility: {showGrid}");
            Repaint();
        }

        /// <summary>
        /// 设置网格可见性
        /// </summary>
        public void SetGridVisible(bool visible)
        {
            showGrid = visible;
            Repaint();
        }

        /// <summary>
        /// 切换图层面板可见性
        /// </summary>
        public void ToggleLayersPanel()
        {
            showLayersPanel = !showLayersPanel;
            Debug.Log($"Layers panel visibility: {showLayersPanel}");
            Repaint();
        }

        /// <summary>
        /// 切换背景可见性
        /// </summary>
        public void ToggleBackground()
        {
            showBackground = !showBackground;
            Debug.Log($"Background visibility: {showBackground}");
            Repaint();
        }

        /// <summary>
        /// 切换块标签可见性
        /// </summary>
        public void ToggleBlockLabels()
        {
            showBlockLabels = !showBlockLabels;
            Debug.Log($"Block labels visibility: {showBlockLabels}");
            Repaint();
        }

        /// <summary>
        /// 切换错误覆盖层可见性
        /// </summary>
        public void ToggleErrorOverlay()
        {
            showErrorOverlay = !showErrorOverlay;
            Debug.Log($"Error overlay visibility: {showErrorOverlay}");
            Repaint();
        }

        /// <summary>
        /// 切换属性面板可见性
        /// </summary>
        public void TogglePropertiesPanel()
        {
            showPropertiesPanel = !showPropertiesPanel;
            Debug.Log($"Properties panel visibility: {showPropertiesPanel}");
            Repaint();
        }

        /// <summary>
        /// 切换颜色选择器面板可见性
        /// </summary>
        public void ToggleColorPickerPanel()
        {
            showColorPickerPanel = !showColorPickerPanel;
            Debug.Log($"Color picker panel visibility: {showColorPickerPanel}");
            Repaint();
        }

        /// <summary>
        /// 显示所有面板
        /// </summary>
        public void ShowAllPanels()
        {
            showLayersPanel = true;
            showPropertiesPanel = true;
            showColorPickerPanel = true;
            Repaint();
        }

        /// <summary>
        /// 隐藏所有面板（全屏画布模式）
        /// </summary>
        public void HideAllPanels()
        {
            showLayersPanel = false;
            showPropertiesPanel = false;
            showColorPickerPanel = false;
            Repaint();
        }

        // 添加这些辅助方法到 MapEditorWindow 类：

        /// <summary>
        /// 检查是否所有面板都可见
        /// </summary>
        public bool AreAllPanelsVisible()
        {
            return showLayersPanel && showPropertiesPanel && showColorPickerPanel;
        }

        /// <summary>
        /// 获取网格可见性状态
        /// </summary>
        public bool IsGridVisible()
        {
            return showGrid;
        }

        /// <summary>
        /// 获取背景可见性状态
        /// </summary>
        public bool IsBackgroundVisible()
        {
            return showBackground;
        }

        /// <summary>
        /// 获取图层面板可见性状态
        /// </summary>
        public bool IsLayersPanelVisible()
        {
            return showLayersPanel;
        }

        /// <summary>
        /// 检查地图坐标是否有效
        /// </summary>
        /// <param name="mapPos">地图坐标</param>
        /// <returns>是否有效</returns>
        public bool IsMapPositionValid(Vector2Int mapPos)
        {
            if (currentMapData == null)
                return false;

            return mapPos.x >= 0 && mapPos.x < currentMapData.width &&
                   mapPos.y >= 0 && mapPos.y < currentMapData.height;
        }

        /// <summary>
        /// 检查地图坐标是否有效
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>是否有效</returns>
        public bool IsMapPositionValid(int x, int y)
        {
            if (currentMapData == null)
                return false;

            return x >= 0 && x < currentMapData.width &&
                   y >= 0 && y < currentMapData.height;
        }

        /// <summary>
        /// 获取当前缩放级别
        /// </summary>
        public float GetZoomLevel()
        {
            return zoomLevel;
        }

        /// <summary>
        /// 设置缩放级别
        /// </summary>
        public void SetZoomLevel(float newZoomLevel, Vector2? zoomCenter = null)
        {
            float oldZoom = zoomLevel;
            zoomLevel = Mathf.Clamp(newZoomLevel, 0.1f, 10f);

            // 如果有指定缩放中心，调整平移以保持该点位置不变
            if (zoomCenter.HasValue && !Mathf.Approximately(oldZoom, zoomLevel))
            {
                Rect drawArea = CalculateDrawArea(new Rect(0, 0, position.width, position.height));
                Vector2 mapPos = ScreenToMapPosition(zoomCenter.Value, drawArea);

                // 重新计算绘制区域并调整平移
                Rect newDrawArea = CalculateDrawArea(new Rect(0, 0, position.width, position.height));
                Vector2 newScreenPos = MapToScreenPosition(mapPos, newDrawArea);

                Vector2 delta = newScreenPos - zoomCenter.Value;
                panOffset -= delta;

                ClampPanOffset();
            }

            Repaint();
        }

        /// <summary>
        /// 缩放到适应窗口
        /// </summary>
        public void ZoomToFit()
        {
            if (currentMapData == null) return;

            Rect canvasArea = CalculateCanvasArea();
            float mapAspect = (float)currentMapData.width / currentMapData.height;
            float canvasAspect = canvasArea.width / canvasArea.height;

            if (canvasAspect > mapAspect)
            {
                // 基于高度计算缩放
                zoomLevel = (canvasArea.height * 0.95f) /
                            (currentMapData.height * (canvasArea.height / currentMapData.height));
            }
            else
            {
                // 基于宽度计算缩放
                zoomLevel = (canvasArea.width * 0.95f) /
                            (currentMapData.width * (canvasArea.width / currentMapData.width));
            }

            panOffset = Vector2.zero;
            Repaint();
        }

        /// <summary>
        /// 缩放到实际大小
        /// </summary>
        public void ZoomToActualSize()
        {
            if (currentMapData == null) return;

            // 计算使地图以实际像素大小显示的缩放级别
            float fitZoomX = position.width * 0.9f / currentMapData.width;
            float fitZoomY = position.height * 0.9f / currentMapData.height;
            zoomLevel = Mathf.Min(fitZoomX, fitZoomY, 1.0f); // 最大不超过100%

            panOffset = Vector2.zero;
            Repaint();
        }

        /// <summary>
        /// 绘制边界和调试信息
        /// </summary>
        private void DrawBoundsAndDebugInfo(Rect drawArea, Rect canvasArea)
        {
            Handles.BeginGUI();

            // 绘制画布边界 - 使用 DrawWireCube 替代 DrawSolidRectangle
            Handles.color = new Color(1, 0.5f, 0, 0.3f);
            Handles.DrawWireCube(canvasArea.center, new Vector3(canvasArea.width, canvasArea.height, 0));

            // 绘制绘制区域边界
            Handles.color = Color.green;
            Handles.DrawWireCube(drawArea.center, new Vector3(drawArea.width, drawArea.height, 0));

            // 如果需要填充背景，使用 EditorGUI.DrawRect
            EditorGUI.DrawRect(new Rect(canvasArea.x, canvasArea.y, canvasArea.width, 20), new Color(0, 0, 0, 0.5f));

            // 绘制边界状态
            string boundsInfo = GetBoundsInfo(drawArea, canvasArea);
            GUIStyle boundsStyle = new GUIStyle(EditorStyles.whiteLabel);
            boundsStyle.normal.textColor = Color.yellow;
            boundsStyle.fontSize = 9;

            Rect boundsRect = new Rect(canvasArea.x + 10, canvasArea.y + 10, 200, 60);
            EditorGUI.DrawRect(boundsRect, new Color(0, 0, 0, 0.8f));
            GUI.Label(boundsRect, boundsInfo, boundsStyle);

            Handles.EndGUI();
        }

        /// <summary>
        /// 获取边界信息
        /// </summary>
        private string GetBoundsInfo(Rect drawArea, Rect canvasArea)
        {
            string boundsState = "";

            if (drawArea.width <= canvasArea.width && drawArea.height <= canvasArea.height)
            {
                boundsState = "FITTED - No panning needed";
            }
            else
            {
                boundsState = "PANNING ENABLED\n";

                if (drawArea.x > canvasArea.x) boundsState += "Left Bound ✓\n";
                else boundsState += "Left Bound ✗\n";

                if (drawArea.xMax < canvasArea.xMax) boundsState += "Right Bound ✓\n";
                else boundsState += "Right Bound ✗\n";

                if (drawArea.y > canvasArea.y) boundsState += "Top Bound ✓\n";
                else boundsState += "Top Bound ✗\n";

                if (drawArea.yMax < canvasArea.yMax) boundsState += "Bottom Bound ✓";
                else boundsState += "Bottom Bound ✗";
            }

            return $"Zoom: {zoomLevel:F2}x\nPan: ({panOffset.x:F0}, {panOffset.y:F0})\n{boundsState}";
        }
        
        // 在 MapEditorWindow 类中添加：

        /// <summary>
        /// 获取当前选择的 Block ID
        /// </summary>
        public int GetCurrentBlockId()
        {
            // 这里可以根据当前颜色自动查找对应的 Block ID
            // 或者让用户手动选择
    
            Color currentColor = GetCurrentColor();
            if (currentMapData != null)
            {
                int blockId = currentMapData.FindBlockIdByColor(currentColor);
                if (blockId != 0)
                {
                    return blockId;
                }
            }
    
            // 如果没有找到对应的 Block ID，返回默认值或创建新的
            return 1; // 默认 Block ID
        }

        /// <summary>
        /// 设置当前 Block ID
        /// </summary>
        public void SetCurrentBlockId(int blockId)
        {
            // 这里可以更新UI显示当前选择的Block
            // 例如在属性面板中显示当前Block信息
        }

        /// <summary>
        /// 获取当前颜色对应的 Block 信息
        /// </summary>
        public ColorBlock? GetCurrentColorBlock()
        {
            int blockId = GetCurrentBlockId();
            if (currentMapData != null)
            {
                return currentMapData.GetColorBlock(blockId);
            }
            return null;
        }
        
        private void DrawUndoRedoStatus()
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(!undoRedoManager.CanUndo());
                if (GUILayout.Button("Undo", GUILayout.Width(60)))
                {
                    OnUndo();
                }
                EditorGUI.EndDisabledGroup();
        
                EditorGUI.BeginDisabledGroup(!undoRedoManager.CanRedo());
                if (GUILayout.Button("Redo", GUILayout.Width(60)))
                {
                    OnRedo();
                }
                EditorGUI.EndDisabledGroup();
        
                EditorGUILayout.LabelField(undoRedoManager.GetUndoDescription(), EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}