
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MapEditor
{
    /// <summary>
    /// 地图编辑器场景设置工具
    /// </summary>
    public class MapEditorSceneSetup : EditorWindow
    {
        private static string projectPath = "Assets/Scenes/MapEditorScene.unity";
        
        [MenuItem("Tools/Map Editor/Create Map Editor Scene")]
        public static void CreateScene()
        {
            // 确保必要的目录存在
            if (!Directory.Exists("Assets/Scenes"))
            {
                Directory.CreateDirectory("Assets/Scenes");
            }
            
            if (!Directory.Exists("Assets/Scripts"))
            {
                Directory.CreateDirectory("Assets/Scripts");
            }
            
            if (!Directory.Exists("Assets/Shaders"))
            {
                Directory.CreateDirectory("Assets/Shaders");
            }
            
            // 创建场景
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            
            // 创建主控制器对象
            GameObject controllerObj = new GameObject("MapEditorController");
            MapEditorController controller = controllerObj.AddComponent<MapEditorController>();
            MapDataManager dataManager = controllerObj.AddComponent<MapDataManager>();
            MapDataProcessor dataProcessor = controllerObj.AddComponent<MapDataProcessor>();
            MapEditorUI uiManager = controllerObj.AddComponent<MapEditorUI>();
            AdvancedFeatures advancedFeatures = controllerObj.AddComponent<AdvancedFeatures>();
            MapComputeProcessor computeProcessor = controllerObj.AddComponent<MapComputeProcessor>();
            
            // 设置引用关系
            controller.mapDataManager = dataManager;
            controller.mapDataProcessor = dataProcessor;
            controller.mapEditorUI = uiManager;
            controller.advancedFeatures = advancedFeatures;
            
            uiManager.mapDataManager = dataManager;
            uiManager.mapDataProcessor = dataProcessor;
            
            advancedFeatures.mapDataManager = dataManager;
            advancedFeatures.mapDataProcessor = dataProcessor;
            advancedFeatures.mapEditorUI = uiManager;
            
            // 创建UI Canvas
            GameObject canvasObj = new GameObject("Canvas", typeof(RectTransform));
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // 创建地图显示区域
            GameObject mapDisplay = new GameObject("MapDisplay");
            mapDisplay.transform.SetParent(canvasObj.transform);
            RawImage rawImage = mapDisplay.AddComponent<RawImage>();
            RectTransform mapRect = mapDisplay.GetComponent<RectTransform>();
            mapRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.anchoredPosition = Vector2.zero;
            mapRect.sizeDelta = new Vector2(512, 512);
            
            // 创建工具面板
            GameObject toolsPanel = new GameObject("ToolsPanel", typeof(RectTransform));
            toolsPanel.transform.SetParent(canvasObj.transform);
            RectTransform toolsRect = toolsPanel.GetComponent<RectTransform>();
            toolsRect.anchorMin = new Vector2(0, 1);
            toolsRect.anchorMax = new Vector2(0, 1);
            toolsRect.anchoredPosition = new Vector2(100, -100);
            toolsRect.sizeDelta = new Vector2(180, 200);
            
            // 添加布局组件
            toolsPanel.AddComponent<VerticalLayoutGroup>();
            ContentSizeFitter sizeFitter = toolsPanel.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // 创建工具按钮
            CreateToolButton(toolsPanel, "PencilButton", "铅笔", () => uiManager.SelectTool(MapEditorUI.ToolType.Pencil));
            CreateToolButton(toolsPanel, "EraserButton", "橡皮擦", () => uiManager.SelectTool(MapEditorUI.ToolType.Eraser));
            CreateToolButton(toolsPanel, "BucketButton", "油漆桶", () => uiManager.SelectTool(MapEditorUI.ToolType.Bucket));
            CreateToolButton(toolsPanel, "PickerButton", "取色器", () => uiManager.SelectTool(MapEditorUI.ToolType.Picker));
            CreateToolButton(toolsPanel, "PaletteButton", "调色盘", () => uiManager.SelectTool(MapEditorUI.ToolType.Palette));
            
            // 创建画笔大小滑块
            GameObject brushSliderObj = new GameObject("BrushSizeSlider");
            brushSliderObj.transform.SetParent(toolsPanel.transform);
            Slider brushSlider = brushSliderObj.AddComponent<Slider>();
            brushSlider.minValue = 1;
            brushSlider.maxValue = 20;
            brushSlider.wholeNumbers = true;
            brushSlider.value = 3;
            
            // 添加滑块的子对象（背景、填充、手柄）
            GameObject background = new GameObject("Background");
            background.transform.SetParent(brushSliderObj.transform);
            background.AddComponent<Image>();
            
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(brushSliderObj.transform);
            
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform);
            fill.AddComponent<Image>();
            
            GameObject handleSlideArea = new GameObject("Handle Slide Area");
            handleSlideArea.transform.SetParent(brushSliderObj.transform);
            
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleSlideArea.transform);
            handle.AddComponent<Image>();
            
            // 创建状态面板
            GameObject statusPanel = new GameObject("StatusPanel", typeof(RectTransform));
            statusPanel.transform.SetParent(canvasObj.transform);
            RectTransform statusRect = statusPanel.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(1, 0);
            statusRect.anchoredPosition = new Vector2(0, 20);
            statusRect.sizeDelta = new Vector2(-20, 30);
            
            // 创建坐标显示文本
            GameObject coordText = new GameObject("CoordinateText", typeof(RectTransform));
            coordText.transform.SetParent(statusPanel.transform);
            Text coordComponent = coordText.AddComponent<Text>();
            coordComponent.text = "坐标: (0, 0)";
            coordText.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            coordText.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            coordText.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            coordText.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);
            
            // 创建状态显示文本
            GameObject statusText = new GameObject("StatusText", typeof(RectTransform));
            statusText.transform.SetParent(statusPanel.transform);
            Text statusComponent = statusText.AddComponent<Text>();
            statusComponent.text = "工具: 铅笔";
            statusText.GetComponent<RectTransform>().anchorMin = new Vector2(1, 1);
            statusText.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            statusText.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            statusText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 30);
            
            // 设置UI引用
            uiManager.mainCamera = Camera.main;
            uiManager.canvasRect = canvasObj.GetComponent<RectTransform>();
            uiManager.mapDisplay = rawImage;
            uiManager.pencilButton = GameObject.Find("PencilButton").GetComponent<Button>();
            uiManager.eraserButton = GameObject.Find("EraserButton").GetComponent<Button>();
            uiManager.bucketButton = GameObject.Find("BucketButton").GetComponent<Button>();
            uiManager.pickerButton = GameObject.Find("PickerButton").GetComponent<Button>();
            uiManager.paletteButton = GameObject.Find("PaletteButton").GetComponent<Button>();
            uiManager.brushSizeSlider = brushSlider;
            uiManager.coordinateText = coordComponent;
            uiManager.statusText = statusComponent;
            
            // 设置高级功能UI引用
            advancedFeatures.errorPanel = new GameObject("ErrorPanel", typeof(RectTransform)).GetComponent<RectTransform>().gameObject;
            advancedFeatures.errorListText = new GameObject("ErrorListText", typeof(RectTransform)).AddComponent<Text>();
            
            // 保存场景
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(), projectPath);
            
            Debug.Log("地图编辑器场景创建完成: " + projectPath);
            
            // 显示成功消息
            EditorUtility.DisplayDialog("成功", "地图编辑器场景已创建完成！", "确定");
        }
        
        private static void CreateToolButton(GameObject parent, string name, string text, UnityAction onClick)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent.transform);
            
            Button button = buttonObj.AddComponent<Button>();
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = Color.white;
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);
            Text textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.black;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            button.onClick.AddListener(onClick);
        }
    }
}
