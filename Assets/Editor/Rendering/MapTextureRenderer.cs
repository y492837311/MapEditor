using UnityEngine;
using UnityEditor;

namespace MapEditor
{
    public class MapTextureRenderer
    {
        private Material visualizationMaterial;
        private ComputeShader computeShader;
        private RenderTexture previewTexture;
        private bool useGPUAcceleration = true;

        public MapTextureRenderer()
        {
            InitializeMaterials();
            InitializeComputeShader();
        }

        private void InitializeMaterials()
        {
            // 创建可视化材质
            Shader shader = Shader.Find("Hidden/MapEditorVisualization");
            if (shader != null)
            {
                visualizationMaterial = new Material(shader);
            }
            else
            {
                Debug.LogWarning("MapEditorVisualization shader not found. Using default shader.");
                visualizationMaterial = new Material(Shader.Find("Unlit/Texture"));
            }
        }

        private void InitializeComputeShader()
        {
            computeShader = Resources.Load<ComputeShader>("MapEditorCompute");
            if (computeShader == null)
            {
                Debug.LogWarning("MapEditorCompute compute shader not found. GPU acceleration disabled.");
                useGPUAcceleration = false;
            }
        }

        /// <summary>
        /// 渲染地图到目标纹理
        /// </summary>
        public void RenderMap(MapDataAsset mapData, RenderTexture target, Texture2D background = null)
        {
            if (mapData == null) return;

            // 获取渲染纹理
            Texture2D colorMapTexture = mapData.GetRenderTexture();
            if (colorMapTexture == null) return;

            if (useGPUAcceleration && SystemInfo.supportsComputeShaders)
            {
                RenderWithComputeShader(colorMapTexture, target, background);
            }
            else
            {
                RenderWithMaterial(colorMapTexture, target, background);
            }
        }

        private void RenderWithComputeShader(Texture2D colorMap, RenderTexture target, Texture2D background)
        {
            // 由于团结引擎1.6的Compute Shader支持可能有限，这里提供备选方案
            RenderWithMaterial(colorMap, target, background);
        }

        private void RenderWithMaterial(Texture2D colorMap, RenderTexture target, Texture2D background)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            GL.Clear(true, true, new Color(0, 0, 0, 0));

            if (visualizationMaterial != null)
            {
                visualizationMaterial.SetTexture("_ColorMap", colorMap);
                visualizationMaterial.SetTexture("_Background", background);
                visualizationMaterial.SetFloat("_ShowBackground", background != null ? 1.0f : 0.0f);

                // 使用GL立即绘制
                GL.PushMatrix();
                GL.LoadOrtho();

                visualizationMaterial.SetPass(0);

                GL.Begin(GL.QUADS);
                GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
                GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
                GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
                GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
                GL.End();

                GL.PopMatrix();
            }

            RenderTexture.active = previous;
        }

        /// <summary>
        /// 更新材质属性
        /// </summary>
        public void UpdateMaterialProperties(float zoomLevel, bool showGrid, bool showErrors)
        {
            if (visualizationMaterial != null)
            {
                visualizationMaterial.SetFloat("_ZoomLevel", zoomLevel);
                visualizationMaterial.SetFloat("_ShowGrid", showGrid ? 1.0f : 0.0f);
                visualizationMaterial.SetFloat("_ShowErrors", showErrors ? 1.0f : 0.0f);
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            if (visualizationMaterial != null)
            {
                Object.DestroyImmediate(visualizationMaterial);
            }

            if (previewTexture != null)
            {
                previewTexture.Release();
                Object.DestroyImmediate(previewTexture);
            }
        }

        /// <summary>
        /// 创建预览纹理
        /// </summary>
        public RenderTexture CreatePreviewTexture(int width, int height)
        {
            if (previewTexture != null)
            {
                previewTexture.Release();
                Object.DestroyImmediate(previewTexture);
            }

            previewTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            previewTexture.filterMode = FilterMode.Point;
            previewTexture.wrapMode = TextureWrapMode.Clamp;
            previewTexture.Create();

            return previewTexture;
        }

        /// <summary>
        /// 获取预览纹理
        /// </summary>
        public RenderTexture GetPreviewTexture()
        {
            return previewTexture;
        }
    }
}