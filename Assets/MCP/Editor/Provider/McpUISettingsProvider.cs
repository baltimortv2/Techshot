using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UniMcp.Models;

namespace UniMcp.Gui
{
    /// <summary>
    /// MCP UI设置提供器，用于在Unity的ProjectSettings窗口中显示UI相关设置
    /// </summary>
    [System.Serializable]
    public class McpUISettingsProvider
    {
        private static Vector2 scrollPosition;
        private static ReorderableList buildStepsList;
        private static ReorderableList preferredComponentsList;
        private static ReorderableList commonSpriteFoldersList;
        private static ReorderableList commonTextureFoldersList;
        private static ReorderableList commonFontFoldersList;
        private static UIType currentUIType = UIType.UGUI;


        [SettingsProvider]
        public static SettingsProvider CreateMcpUISettingsProvider()
        {
            var provider = new SettingsProvider("Project/MCP/UI-Prompts", SettingsScope.Project)
            {
                label = "UI-Prompts",
                guiHandler = (searchContext) =>
                {
                    DrawMcpUISettings();
                },
                keywords = new[] { "UI", "UI", "Generation", "Rules", "Figma", "Canvas", "Button", "Text", "Image" }
            };

            return provider;
        }

        private static void DrawMcpUISettings()
        {
            var settings = McpSettings.Instance;
            if (settings.uiSettings == null)
                settings.uiSettings = new McpUISettings();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // UI简介
            EditorGUILayout.LabelField("UI Generation Rules", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure automatic UI generation rules and preferences. " +
                "These settings affect default behavior and structure of UI components generated via MCP tools.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // UI类型选择器
            EditorGUILayout.LabelField("UI Type Selection", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var newUIType = (UIType)EditorGUILayout.EnumPopup("Current UI Type", settings.uiSettings.selectedUIType);
            if (EditorGUI.EndChangeCheck())
            {
                // 保存当前数据
                settings.uiSettings.SerializeUITypeData();

                // 切换UI类型
                settings.uiSettings.selectedUIType = newUIType;
                currentUIType = newUIType;

                // 重置列表以刷新显示
                buildStepsList = null;
                preferredComponentsList = null;
                commonSpriteFoldersList = null;
                commonTextureFoldersList = null;
                commonFontFoldersList = null;

                settings.SaveSettings();
            }

            EditorGUILayout.HelpBox($"Selected: {settings.uiSettings.selectedUIType} - Each UI type has independent build steps and environment config", MessageType.Info);
            EditorGUILayout.Space(10);

            // 初始化ReorderableList
            if (buildStepsList == null)
            {
                buildStepsList = new ReorderableList(settings.uiSettings.ui_build_steps, typeof(string), true, true, true, true);
                buildStepsList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "");

                    // 添加写入按钮
                    Rect writeButtonRect = new Rect(rect.width - 125, rect.y, 60, rect.height);
                    if (GUI.Button(writeButtonRect, "Write"))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Write", "Write current UI build steps to code as default values?", "确定", "取消"))
                        {
                            WriteDefaultBuildStepsToCode(settings.uiSettings.ui_build_steps);
                        }
                    }

                    // 添加重置按钮
                    Rect resetButtonRect = new Rect(rect.width - 60, rect.y, 60, rect.height);
                    if (GUI.Button(resetButtonRect, "Reset"))
                    {
                        if (EditorUtility.DisplayDialog("确认重置", $"确定要重置{settings.uiSettings.selectedUIType}的UI构建步骤为默认值吗？", "确定", "取消"))
                        {
                            settings.uiSettings.ui_build_steps = McpUISettings.GetDefaultBuildSteps(settings.uiSettings.selectedUIType);
                            settings.SaveSettings();
                        }
                    }
                };
                buildStepsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    settings.uiSettings.ui_build_steps[index] = EditorGUI.TextField(rect, settings.uiSettings.ui_build_steps[index]);
                };
                buildStepsList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.ui_build_steps.Add("New step?");
                };
            }

            if (preferredComponentsList == null)
            {
                preferredComponentsList = new ReorderableList(settings.uiSettings.ui_build_enviroments, typeof(string), true, true, true, true);
                preferredComponentsList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "");

                    // 添加写入按钮
                    Rect writeButtonRect = new Rect(rect.width - 125, rect.y, 60, rect.height);
                    if (GUI.Button(writeButtonRect, "Write"))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Write", "确定要将当前UI环境说明写入到代码中作为默认值吗？", "确定", "取消"))
                        {
                            WriteDefaultBuildEnvironmentsToCode(settings.uiSettings.ui_build_enviroments);
                        }
                    }

                    // 添加重置按钮
                    Rect resetButtonRect = new Rect(rect.width - 60, rect.y, 60, rect.height);
                    if (GUI.Button(resetButtonRect, "Reset"))
                    {
                        if (EditorUtility.DisplayDialog("确认重置", $"确定要重置{settings.uiSettings.selectedUIType}的UI环境说明为默认值吗？", "确定", "取消"))
                        {
                            settings.uiSettings.ui_build_enviroments = McpUISettings.GetDefaultBuildEnvironments(settings.uiSettings.selectedUIType);
                            settings.SaveSettings();
                        }
                    }
                };
                preferredComponentsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (index > settings.uiSettings.ui_build_enviroments.Count)
                    {
                        settings.uiSettings.ui_build_enviroments.Add("");
                    }
                    settings.uiSettings.ui_build_enviroments[index] = EditorGUI.TextField(rect, settings.uiSettings.ui_build_enviroments[index]);
                };
                preferredComponentsList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.ui_build_enviroments.Add("");
                };
            }

            // 初始化通用资源文件夹列表
            InitializeCommonFoldersList(settings);

            // 绘制UI构建步骤列表
            EditorGUILayout.LabelField($"UI Build Steps ({settings.uiSettings.selectedUIType})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Define {settings.uiSettings.selectedUIType} UI generation workflow steps, executed in order.", MessageType.Info);
            buildStepsList.DoLayoutList();

            EditorGUILayout.Space(10);

            // 绘制偏好组件列表
            EditorGUILayout.LabelField($"UI Environment ({settings.uiSettings.selectedUIType})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Configure {settings.uiSettings.selectedUIType} UI generation environment and constraints.", MessageType.Info);
            preferredComponentsList.DoLayoutList();

            EditorGUILayout.Space(20);

            // 绘制通用资源文件夹列表
            EditorGUILayout.LabelField("Resource Folders Config", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configure common resource folder paths for auto-discovery and loading.", MessageType.Info);

            EditorGUILayout.Space(5);

            // 绘制通用精灵文件夹列表
            EditorGUILayout.LabelField("Sprite Folders", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configure sprite resource folders for auto-discovery.", MessageType.Info);
            commonSpriteFoldersList.DoLayoutList();

            EditorGUILayout.Space(5);

            // 绘制通用纹理文件夹列表
            EditorGUILayout.LabelField("Texture Folders", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configure texture resource folders for auto-discovery.", MessageType.Info);
            commonTextureFoldersList.DoLayoutList();

            EditorGUILayout.Space(5);

            // 绘制通用字体文件夹列表
            EditorGUILayout.LabelField("Font Folders", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configure font resource folders for auto-discovery.", MessageType.Info);
            commonFontFoldersList.DoLayoutList();

            EditorGUILayout.EndScrollView();

            // 自动保存
            if (GUI.changed)
            {
                // 序列化UI类型数据
                settings.uiSettings.SerializeUITypeData();
                settings.SaveSettings();
            }
        }

        /// <summary>
        /// 初始化通用资源文件夹列表
        /// </summary>
        private static void InitializeCommonFoldersList(McpSettings settings)
        {
            // 初始化通用精灵文件夹列表
            if (commonSpriteFoldersList == null)
            {
                commonSpriteFoldersList = new ReorderableList(settings.uiSettings.commonSpriteFolders, typeof(string), true, true, true, true);
                commonSpriteFoldersList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Sprite Folder Paths");
                };
                commonSpriteFoldersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    // 创建文件夹选择按钮
                    Rect folderButtonRect = new Rect(rect.x + rect.width - 60, rect.y, 60, rect.height);
                    Rect textFieldRect = new Rect(rect.x, rect.y, rect.width - 65, rect.height);

                    settings.uiSettings.commonSpriteFolders[index] = EditorGUI.TextField(textFieldRect, settings.uiSettings.commonSpriteFolders[index]);

                    if (GUI.Button(folderButtonRect, "Browse..."))
                    {
                        string currentPath = settings.uiSettings.commonSpriteFolders[index];
                        string initialPath = string.IsNullOrEmpty(currentPath) ? "Assets" : currentPath;
                        string selectedPath = EditorUtility.OpenFolderPanel("Select Sprite Folder", initialPath, "");

                        if (!string.IsNullOrEmpty(selectedPath))
                        {
                            // 转换为相对于Assets的路径
                            if (selectedPath.StartsWith(Application.dataPath))
                            {
                                selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                            }
                            settings.uiSettings.commonSpriteFolders[index] = selectedPath;
                        }
                    }
                };
                commonSpriteFoldersList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.commonSpriteFolders.Add("Assets/Sprites");
                };
            }

            // 初始化通用纹理文件夹列表
            if (commonTextureFoldersList == null)
            {
                commonTextureFoldersList = new ReorderableList(settings.uiSettings.commonTextureFolders, typeof(string), true, true, true, true);
                commonTextureFoldersList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Texture Folder Paths");
                };
                commonTextureFoldersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    // 创建文件夹选择按钮
                    Rect folderButtonRect = new Rect(rect.x + rect.width - 60, rect.y, 60, rect.height);
                    Rect textFieldRect = new Rect(rect.x, rect.y, rect.width - 65, rect.height);

                    settings.uiSettings.commonTextureFolders[index] = EditorGUI.TextField(textFieldRect, settings.uiSettings.commonTextureFolders[index]);

                    if (GUI.Button(folderButtonRect, "Browse..."))
                    {
                        string currentPath = settings.uiSettings.commonTextureFolders[index];
                        string initialPath = string.IsNullOrEmpty(currentPath) ? "Assets" : currentPath;
                        string selectedPath = EditorUtility.OpenFolderPanel("Select Texture Folder", initialPath, "");

                        if (!string.IsNullOrEmpty(selectedPath))
                        {
                            // 转换为相对于Assets的路径
                            if (selectedPath.StartsWith(Application.dataPath))
                            {
                                selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                            }
                            settings.uiSettings.commonTextureFolders[index] = selectedPath;
                        }
                    }
                };
                commonTextureFoldersList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.commonTextureFolders.Add("Assets/Textures");
                };
            }

            // 初始化通用字体文件夹列表
            if (commonFontFoldersList == null)
            {
                commonFontFoldersList = new ReorderableList(settings.uiSettings.commonFontFolders, typeof(string), true, true, true, true);
                commonFontFoldersList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Font Folder Paths");
                };
                commonFontFoldersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    // 创建文件夹选择按钮
                    Rect folderButtonRect = new Rect(rect.x + rect.width - 60, rect.y, 60, rect.height);
                    Rect textFieldRect = new Rect(rect.x, rect.y, rect.width - 65, rect.height);

                    settings.uiSettings.commonFontFolders[index] = EditorGUI.TextField(textFieldRect, settings.uiSettings.commonFontFolders[index]);

                    if (GUI.Button(folderButtonRect, "Browse..."))
                    {
                        string currentPath = settings.uiSettings.commonFontFolders[index];
                        string initialPath = string.IsNullOrEmpty(currentPath) ? "Assets" : currentPath;
                        string selectedPath = EditorUtility.OpenFolderPanel("Select Font Folder", initialPath, "");

                        if (!string.IsNullOrEmpty(selectedPath))
                        {
                            // 转换为相对于Assets的路径
                            if (selectedPath.StartsWith(Application.dataPath))
                            {
                                selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                            }
                            settings.uiSettings.commonFontFolders[index] = selectedPath;
                        }
                    }
                };
                commonFontFoldersList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.commonFontFolders.Add("Assets/Fonts");
                };
            }
        }

        /// <summary>
        /// 将当前UI构建步骤写入到代码中
        /// </summary>
        private static void WriteDefaultBuildStepsToCode(List<string> buildSteps)
        {
            try
            {
                // 通过GUID查找McpUISettings.cs文件
                string guid = "9f1fbf807c169a748a66f80287d6b872";
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new System.Exception($"无法通过GUID {guid} 找到McpUISettings.cs文件");
                }

                string filePath = System.IO.Path.GetFullPath(assetPath);
                string fileContent = System.IO.File.ReadAllText(filePath);

                // 获取当前UI类型
                var settings = McpSettings.Instance;
                var currentType = settings?.uiSettings?.selectedUIType ?? UIType.UGUI;

                // 构建新的GetDefaultBuildSteps方法代码
                var newMethodCode = GenerateGetDefaultBuildStepsCode(buildSteps, currentType);

                // 找到方法开始位置
                var methodStart = "public static List<string> GetDefaultBuildSteps()";
                int startIndex = fileContent.IndexOf(methodStart);
                if (startIndex == -1)
                {
                    throw new System.Exception("找不到GetDefaultBuildSteps方法");
                }

                // 找到方法体的开始大括号
                int braceStart = fileContent.IndexOf('{', startIndex);
                if (braceStart == -1)
                {
                    throw new System.Exception("找不到方法开始大括号");
                }

                // 计数大括号找到方法结束位置
                int braceCount = 0;
                int braceEnd = braceStart;
                for (int i = braceStart; i < fileContent.Length; i++)
                {
                    if (fileContent[i] == '{') braceCount++;
                    else if (fileContent[i] == '}') braceCount--;

                    if (braceCount == 0)
                    {
                        braceEnd = i;
                        break;
                    }
                }

                // 替换整个方法
                string beforeMethod = fileContent.Substring(0, startIndex);
                string afterMethod = fileContent.Substring(braceEnd + 1);
                fileContent = beforeMethod + newMethodCode + afterMethod;

                System.IO.File.WriteAllText(filePath, fileContent);
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("写入成功", "UI构建步骤已成功写入到代码中！", "确定");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("写入失败", $"写入过程中发生错误：{ex.Message}", "确定");
                Debug.LogException(new System.Exception($"写入UI构建步骤失败: {ex}", ex));
            }
        }

        /// <summary>
        /// 将当前UI环境说明写入到代码中
        /// </summary>
        private static void WriteDefaultBuildEnvironmentsToCode(List<string> environments)
        {
            try
            {
                // 通过GUID查找McpUISettings.cs文件
                string guid = "9f1fbf807c169a748a66f80287d6b872";
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new System.Exception($"无法通过GUID {guid} 找到McpUISettings.cs文件");
                }

                string filePath = System.IO.Path.GetFullPath(assetPath);
                string fileContent = System.IO.File.ReadAllText(filePath);

                // 获取当前UI类型
                var settings = McpSettings.Instance;
                var currentType = settings?.uiSettings?.selectedUIType ?? UIType.UGUI;

                // 构建新的GetDefaultBuildEnvironments方法代码
                var newMethodCode = GenerateGetDefaultBuildEnvironmentsCode(environments, currentType);

                // 找到方法开始位置
                var methodStart = "public static List<string> GetDefaultBuildEnvironments()";
                int startIndex = fileContent.IndexOf(methodStart);
                if (startIndex == -1)
                {
                    throw new System.Exception("找不到GetDefaultBuildEnvironments方法");
                }

                // 找到方法体的开始大括号
                int braceStart = fileContent.IndexOf('{', startIndex);
                if (braceStart == -1)
                {
                    throw new System.Exception("找不到方法开始大括号");
                }

                // 计数大括号找到方法结束位置
                int braceCount = 0;
                int braceEnd = braceStart;
                for (int i = braceStart; i < fileContent.Length; i++)
                {
                    if (fileContent[i] == '{') braceCount++;
                    else if (fileContent[i] == '}') braceCount--;

                    if (braceCount == 0)
                    {
                        braceEnd = i;
                        break;
                    }
                }

                // 替换整个方法
                string beforeMethod = fileContent.Substring(0, startIndex);
                string afterMethod = fileContent.Substring(braceEnd + 1);
                fileContent = beforeMethod + newMethodCode + afterMethod;

                System.IO.File.WriteAllText(filePath, fileContent);
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("写入成功", "UI环境说明已成功写入到代码中！", "确定");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("写入失败", $"写入过程中发生错误：{ex.Message}", "确定");
                Debug.LogError($"写入UI环境说明失败: {ex}");
            }
        }

        /// <summary>
        /// 生成GetDefaultBuildSteps方法的代码
        /// </summary>
        private static string GenerateGetDefaultBuildStepsCode(List<string> buildSteps, UIType uiType)
        {
            var code = new System.Text.StringBuilder();
            code.AppendLine($"// 自动生成的默认构建步骤 - {uiType} ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            code.AppendLine("        public static List<string> GetDefaultBuildSteps()");
            code.AppendLine("        {");
            code.AppendLine("            return new List<string>");
            code.AppendLine("            {");

            for (int i = 0; i < buildSteps.Count; i++)
            {
                var step = buildSteps[i].Replace("\"", "\\\""); // 转义双引号
                var comma = i < buildSteps.Count - 1 ? "," : "";
                code.AppendLine($"                \"{step}\"{comma}");
            }

            code.AppendLine("            };");
            code.Append("        }");

            return code.ToString();
        }

        /// <summary>
        /// 生成GetDefaultBuildEnvironments方法的代码
        /// </summary>
        private static string GenerateGetDefaultBuildEnvironmentsCode(List<string> environments, UIType uiType)
        {
            var code = new System.Text.StringBuilder();
            code.AppendLine($"// 自动生成的默认环境说明 - {uiType} ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            code.AppendLine("        public static List<string> GetDefaultBuildEnvironments()");
            code.AppendLine("        {");
            code.AppendLine("            return new List<string>");
            code.AppendLine("            {");

            for (int i = 0; i < environments.Count; i++)
            {
                var env = environments[i].Replace("\"", "\\\""); // 转义双引号
                var comma = i < environments.Count - 1 ? "," : "";
                code.AppendLine($"                \"{env}\"{comma}");
            }

            code.AppendLine("            };");
            code.Append("        }");

            return code.ToString();
        }
    }
}