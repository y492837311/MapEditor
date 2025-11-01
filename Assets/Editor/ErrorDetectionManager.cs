using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MapEditor
{
    public class ErrorDetectionManager
    {
        private MapEditorWindow editorWindow;
        private List<MapError> detectedErrors = new();
        private bool errorsVisible = true;

        public ErrorDetectionManager(MapEditorWindow window)
        {
            editorWindow = window;
        }

        public void RunErrorCheck()
        {
            detectedErrors.Clear();
            
            var mapData = editorWindow.GetCurrentMapData();
            if (mapData == null || mapData.GetColorMapTexture() == null) return;

            EditorUtility.DisplayProgressBar("Error Check", "Analyzing map...", 0f);

            try
            {
                Color32[] pixels = mapData.GetColorMapTexture().GetPixels32();
                
                // 检测孤立像素
                DetectIsolatedPixels(pixels, mapData.width, mapData.height);
                
                // 检测三色交点
                DetectThreeColorIntersections(pixels, mapData.width, mapData.height);
                
                // 检测单像素线
                DetectSinglePixelLines(pixels, mapData.width, mapData.height);
                
                // 检测颜色冲突
                DetectColorConflicts(pixels, mapData);
                
                // 检测特殊地形规则
                DetectSpecialTerrainRules(pixels, mapData.width, mapData.height);

                EditorUtility.ClearProgressBar();
                
                Debug.Log($"Error check completed. Found {detectedErrors.Count} issues.");
                
                if (detectedErrors.Count > 0)
                {
                    ShowErrorReport();
                }
                else
                {
                    EditorUtility.DisplayDialog("Error Check", "No errors found!", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DetectIsolatedPixels(Color32[] pixels, int width, int height)
        {
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    if (pixels[index].a > 0) // 非透明像素
                    {
                        if (IsPixelIsolated(pixels, x, y, width, height))
                        {
                            detectedErrors.Add(new MapError
                            {
                                type = ErrorType.IsolatedPixel,
                                position = new Vector2Int(x, y),
                                message = $"Isolated pixel at ({x}, {y})"
                            });
                        }
                    }
                }
            }
        }

        private bool IsPixelIsolated(Color32[] pixels, int x, int y, int width, int height)
        {
            Color32 centerColor = pixels[y * width + x];
            
            // 检查8个相邻像素
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        int neighborIndex = ny * width + nx;
                        if (pixels[neighborIndex].a > 0 && ColorsEqual(pixels[neighborIndex], centerColor))
                        {
                            return false; // 找到相同颜色的邻居
                        }
                    }
                }
            }
            
            return true; // 没有找到相同颜色的邻居
        }

        private void DetectThreeColorIntersections(Color32[] pixels, int width, int height)
        {
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    if (pixels[index].a > 0)
                    {
                        if (IsThreeColorIntersection(pixels, x, y, width, height))
                        {
                            detectedErrors.Add(new MapError
                            {
                                type = ErrorType.ThreeColorIntersection,
                                position = new Vector2Int(x, y),
                                message = $"Three-color intersection at ({x}, {y})"
                            });
                        }
                    }
                }
            }
        }

        private bool IsThreeColorIntersection(Color32[] pixels, int x, int y, int width, int height)
        {
            var uniqueColors = new HashSet<Color32>();
            
            // 检查4个主要方向的邻居
            Vector2Int[] directions = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            foreach (var dir in directions)
            {
                int nx = x + dir.x;
                int ny = y + dir.y;
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    Color32 neighborColor = pixels[ny * width + nx];
                    if (neighborColor.a > 0)
                    {
                        uniqueColors.Add(neighborColor);
                    }
                }
            }

            return uniqueColors.Count >= 3;
        }

        private void DetectSinglePixelLines(Color32[] pixels, int width, int height)
        {
            // 实现单像素线检测逻辑
            // 这需要检测宽度只有1像素的连接区域
        }

        private void DetectColorConflicts(Color32[] pixels, MapDataAsset mapData)
        {
            // 检测颜色冲突：相同颜色对应多个block ID
            var colorToBlockMap = new Dictionary<Color32, int>();
            
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0)
                {
                    int blockId = FindBlockIdForColor(mapData, pixels[i]);
                    if (blockId != 0)
                    {
                        if (colorToBlockMap.ContainsKey(pixels[i]))
                        {
                            if (colorToBlockMap[pixels[i]] != blockId)
                            {
                                int x = i % mapData.width;
                                int y = i / mapData.width;
                                
                                detectedErrors.Add(new MapError
                                {
                                    type = ErrorType.ColorConflict,
                                    position = new Vector2Int(x, y),
                                    message = $"Color conflict at ({x}, {y}): Same color used for multiple blocks"
                                });
                            }
                        }
                        else
                        {
                            colorToBlockMap[pixels[i]] = blockId;
                        }
                    }
                }
            }
        }

        private void DetectSpecialTerrainRules(Color32[] pixels, int width, int height)
        {
            // 实现特殊地形规则检测
            // 例如：关隘必须与三个普通地块相邻等
        }

        private int FindBlockIdForColor(MapDataAsset mapData, Color32 color)
        {
            foreach (var block in mapData.colorBlocks)
            {
                if (ColorsEqual(block.color, color))
                {
                    return block.id;
                }
            }
            return 0;
        }

        private bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private void ShowErrorReport()
        {
            ErrorReportWindow.ShowWindow(detectedErrors, editorWindow);
        }

        public void DrawErrorOverlay(Rect canvasArea)
        {
            if (!errorsVisible || detectedErrors.Count == 0) return;

            Handles.BeginGUI();

            foreach (var error in detectedErrors)
            {
                DrawErrorMarker(canvasArea, error);
            }

            Handles.EndGUI();
        }

        private void DrawErrorMarker(Rect canvasArea, MapError error)
        {
            Vector2 screenPos = editorWindow.ScreenToMapPosition(
                new Vector2(error.position.x, error.position.y), canvasArea);

            Color errorColor = GetErrorColor(error.type);
            Handles.color = errorColor;

            // 绘制错误标记
            float markerSize = 8f;
            Handles.DrawSolidDisc(screenPos, Vector3.forward, markerSize);

            // 绘制轮廓
            Handles.color = Color.white;
            Handles.DrawWireDisc(screenPos, Vector3.forward, markerSize);
        }

        private Color GetErrorColor(ErrorType type)
        {
            switch (type)
            {
                case ErrorType.IsolatedPixel: return Color.red;
                case ErrorType.ThreeColorIntersection: return Color.yellow;
                case ErrorType.SinglePixelLine: return Color.cyan;
                case ErrorType.ColorConflict: return Color.magenta;
                default: return Color.white;
            }
        }

        public void ClearErrors()
        {
            detectedErrors.Clear();
        }

        public void ToggleErrorsVisible()
        {
            errorsVisible = !errorsVisible;
        }
    }

    public struct MapError
    {
        public ErrorType type;
        public Vector2Int position;
        public string message;
    }

    public class ErrorReportWindow : EditorWindow
    {
        private List<MapError> errors;
        private MapEditorWindow editorWindow;
        private Vector2 scrollPosition;

        public static void ShowWindow(List<MapError> errorList, MapEditorWindow editor)
        {
            var window = CreateInstance<ErrorReportWindow>();
            window.errors = errorList;
            window.editorWindow = editor;
            window.titleContent = new GUIContent("Map Error Report");
            window.position = new Rect(100, 100, 500, 600);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField($"Found {errors.Count} Errors", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                {
                    foreach (var error in errors)
                    {
                        DrawErrorItem(error);
                    }
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Select All Errors"))
                    {
                        // 实现选择所有错误位置的功能
                    }
                    
                    if (GUILayout.Button("Clear Report"))
                    {
                        Close();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawErrorItem(MapError error)
        {
            EditorGUILayout.BeginVertical("HelpBox");
            {
                EditorGUILayout.BeginHorizontal();
                {
                    // 错误类型颜色指示
                    Rect colorRect = GUILayoutUtility.GetRect(20, 20);
                    EditorGUI.DrawRect(colorRect, GetErrorColor(error.type));
                    
                    EditorGUILayout.BeginVertical();
                    {
                        EditorGUILayout.LabelField(error.message, EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField($"Position: ({error.position.x}, {error.position.y})", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Go To", GUILayout.Width(60)))
                    {
                        // 跳转到错误位置
                        editorWindow.Focus();
                        // 这里可以实现跳转到具体位置的逻辑
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private Color GetErrorColor(ErrorType type)
        {
            switch (type)
            {
                case ErrorType.IsolatedPixel: return Color.red;
                case ErrorType.ThreeColorIntersection: return Color.yellow;
                case ErrorType.SinglePixelLine: return Color.cyan;
                case ErrorType.ColorConflict: return Color.magenta;
                default: return Color.white;
            }
        }
    }
}