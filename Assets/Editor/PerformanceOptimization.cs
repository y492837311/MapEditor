using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public static class PerformanceOptimization
    {
        private static bool useFastRendering = true;
        private static int textureUpdateThreshold = 1000; // 像素数量阈值
        private static bool enableCaching = true;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // 注册性能设置窗口
            // UnityEditor.EditorApplication.delayCall += ShowPerformanceSettingsIfNeeded;
        }

        public static void OptimizeForLargeTextures(Texture2D texture)
        {
            if (texture == null) return;

            // 设置纹理参数以优化大尺寸纹理性能
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 0;
            texture.mipMapBias = 0;
            
            // 建议压缩格式以减少内存使用
            if (EditorUtility.DisplayDialog("Texture Compression", 
                "Would you like to compress the texture to reduce memory usage?\nThis may slightly reduce quality.", 
                "Compress", "Keep Original"))
            {
                // 这里可以实现纹理压缩逻辑
            }
        }

        public static void ShowPerformanceSettings()
        {
            PerformanceSettingsWindow.ShowWindow();
        }

        public static bool ShouldUseFastRendering()
        {
            return useFastRendering;
        }

        public static bool ShouldBatchTextureUpdates(int pixelCount)
        {
            return pixelCount > textureUpdateThreshold;
        }

        public static bool IsCachingEnabled()
        {
            return enableCaching;
        }
    }

    public class PerformanceSettingsWindow : EditorWindow
    {
        private bool useFastRendering;
        private int textureUpdateThreshold;
        private bool enableCaching;
        private bool enableGPUAcceleration;

        public static void ShowWindow()
        {
            var window = GetWindow<PerformanceSettingsWindow>("Performance Settings");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            useFastRendering = true;
            textureUpdateThreshold = 1000;
            enableCaching = true;
            enableGPUAcceleration = SystemInfo.supportsComputeShaders;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Map Editor Performance Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Adjust these settings to optimize performance for large maps (4096x2048).", MessageType.Info);
            EditorGUILayout.Space();

            // 快速渲染
            useFastRendering = EditorGUILayout.Toggle("Fast Rendering", useFastRendering);
            EditorGUILayout.HelpBox("Use simplified rendering for better performance. May reduce visual quality.", MessageType.None);

            EditorGUILayout.Space();

            // 纹理更新阈值
            textureUpdateThreshold = EditorGUILayout.IntField("Texture Update Threshold", textureUpdateThreshold);
            EditorGUILayout.HelpBox("Number of pixels changed before forcing texture update. Higher values = better performance but delayed updates.", MessageType.None);

            EditorGUILayout.Space();

            // 缓存
            enableCaching = EditorGUILayout.Toggle("Enable Caching", enableCaching);
            EditorGUILayout.HelpBox("Cache frequently used operations for better performance.", MessageType.None);

            EditorGUILayout.Space();

            // GPU加速
            EditorGUI.BeginDisabledGroup(!SystemInfo.supportsComputeShaders);
            enableGPUAcceleration = EditorGUILayout.Toggle("GPU Acceleration", enableGPUAcceleration);
            if (!SystemInfo.supportsComputeShaders)
            {
                EditorGUILayout.HelpBox("GPU acceleration requires Compute Shader support.", MessageType.Warning);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            // 应用按钮
            if (GUILayout.Button("Apply Settings", GUILayout.Height(30)))
            {
                ApplySettings();
                Close();
            }

            EditorGUILayout.Space();

            // 性能统计
            DrawPerformanceStats();
        }

        private void ApplySettings()
        {
            // 这里应用性能设置
            PerformanceOptimizationSettings.ApplySettings(
                useFastRendering,
                textureUpdateThreshold,
                enableCaching,
                enableGPUAcceleration
            );

            Debug.Log("Performance settings applied");
        }

        private void DrawPerformanceStats()
        {
            EditorGUILayout.LabelField("Performance Statistics", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("HelpBox");
            {
                EditorGUILayout.LabelField($"System Memory: {SystemInfo.systemMemorySize}MB");
                EditorGUILayout.LabelField($"Graphics Memory: {SystemInfo.graphicsMemorySize}MB");
                EditorGUILayout.LabelField($"Compute Shader Support: {SystemInfo.supportsComputeShaders}");
                EditorGUILayout.LabelField($"Max Texture Size: {SystemInfo.maxTextureSize}");
            }
            EditorGUILayout.EndVertical();
        }
    }

    public static class PerformanceOptimizationSettings
    {
        public static void ApplySettings(bool fastRendering, int updateThreshold, bool caching, bool gpuAcceleration)
        {
            // 实现性能设置应用逻辑
            PlayerPrefs.SetInt("MapEditor_FastRendering", fastRendering ? 1 : 0);
            PlayerPrefs.SetInt("MapEditor_UpdateThreshold", updateThreshold);
            PlayerPrefs.SetInt("MapEditor_Caching", caching ? 1 : 0);
            PlayerPrefs.SetInt("MapEditor_GPUAcceleration", gpuAcceleration ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void LoadSettings()
        {
            // 实现性能设置加载逻辑
        }
    }
}