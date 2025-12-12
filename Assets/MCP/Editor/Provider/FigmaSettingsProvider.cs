using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UniMcp;

namespace UniMcp.Gui
{
    /// <summary>
    /// Figma设置提供器，用于在Unity的ProjectSettings窗口中显示Figma相关设置
    /// </summary>
    public class FigmaSettingsProvider
    {
        private static Vector2 scrollPosition;
        private static bool apiSettingsFoldout = true;
        private static bool downloadSettingsFoldout = true;
        private static bool aiPromptFoldout = true;
        private static bool engineEffectsFoldout = true;
        private static bool helpInfoFoldout = false;

        [SettingsProvider]
        public static SettingsProvider CreateFigmaSettingsProvider()
        {
            var provider = new SettingsProvider("Project/MCP/Figma", SettingsScope.Project)
            {
                label = "Figma",
                guiHandler = (searchContext) =>
                {
                    DrawFigmaSettings();
                },
                keywords = new[] { "Figma", "Design", "Token", "Download", "Images", "API", "File" }
            };

            return provider;
        }

        private static void DrawFigmaSettings()
        {
            var settings = McpSettings.Instance;
            if (settings.figmaSettings == null)
                settings.figmaSettings = new FigmaSettings();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Figma简介
            EditorGUILayout.LabelField("Figma Integration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure Figma integration settings including access token and download options. " +
                "These settings affect how design resources are fetched from Figma.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // API设置
            apiSettingsFoldout = EditorGUILayout.Foldout(apiSettingsFoldout, "API Settings", true, EditorStyles.foldoutHeader);

            if (apiSettingsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                string token = settings.figmaSettings.figma_access_token;
                token = EditorGUILayout.PasswordField(
                    "Figma Access Token",
                    token);
                settings.figmaSettings.figma_access_token = token;
                EditorGUILayout.LabelField("💾", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "Token is saved in local editor settings and won't be committed to version control.",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 下载设置
            downloadSettingsFoldout = EditorGUILayout.Foldout(downloadSettingsFoldout, "Download Settings", true, EditorStyles.foldoutHeader);

            if (downloadSettingsFoldout)
            {
                EditorGUI.indentLevel++;

                settings.figmaSettings.default_download_path = EditorGUILayout.TextField(
                    "Default Download Path",
                    settings.figmaSettings.default_download_path);

                settings.figmaSettings.figma_assets_path = EditorGUILayout.TextField(
                    "Figma Assets Path",
                    settings.figmaSettings.figma_assets_path);

                settings.figmaSettings.figma_preview_path = EditorGUILayout.TextField(
                    "Preview Image Path",
                    settings.figmaSettings.figma_preview_path);

                settings.figmaSettings.auto_download_images = EditorGUILayout.Toggle(
                    "Auto Download Images",
                    settings.figmaSettings.auto_download_images);

                settings.figmaSettings.image_scale = EditorGUILayout.FloatField(
                    "Image Scale",
                    settings.figmaSettings.image_scale);

                settings.figmaSettings.preview_max_size = EditorGUILayout.IntSlider(
                    "Max Preview Size",
                    settings.figmaSettings.preview_max_size,
                    50, 600);

                settings.figmaSettings.auto_convert_to_sprite = EditorGUILayout.Toggle(
                    "Auto Convert to Sprite",
                    settings.figmaSettings.auto_convert_to_sprite);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // AI转换提示词
            aiPromptFoldout = EditorGUILayout.Foldout(aiPromptFoldout, "AI Prompts", true, EditorStyles.foldoutHeader);

            if (aiPromptFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Configure AI prompts for Figma to Unity conversion, guiding coordinate transformation and layout.",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // 添加UI类型选择
                EditorGUILayout.LabelField("UI Framework:", EditorStyles.boldLabel);

                // 使用EnumPopup绘制UI类型选择器
                settings.figmaSettings.selectedUIType = (UIType)EditorGUILayout.EnumPopup(
                    "Select Framework",
                    settings.figmaSettings.selectedUIType);

                EditorGUILayout.Space(5);

                // 显示多行文本编辑器
                EditorGUILayout.LabelField(string.Format("Prompt Content ({0}):", settings.figmaSettings.selectedUIType.ToString()), EditorStyles.boldLabel);

                // 创建一个滚动视图来显示多行文本
                GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    richText = false
                };

                // 根据选择的UI类型显示对应的提示词
                string currentPrompt = settings.figmaSettings.GetPromptForUIType(settings.figmaSettings.selectedUIType, false);
                string newPrompt = EditorGUILayout.TextArea(
                    currentPrompt,
                    textAreaStyle,
                    GUILayout.MinHeight(300),
                    GUILayout.MaxHeight(600));

                // 如果提示词被修改，更新对应UI类型的提示词
                if (newPrompt != currentPrompt)
                {
                    settings.figmaSettings.SetPromptForUIType(settings.figmaSettings.selectedUIType, newPrompt);
                }

                EditorGUILayout.Space(5);

                // 重置按钮
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(string.Format("Reset {0} Prompt to Default", settings.figmaSettings.selectedUIType.ToString()), GUILayout.Width(200)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Reset",
                        string.Format("Reset {0} AI prompt to default?\nCustom content will be lost.", settings.figmaSettings.selectedUIType.ToString()),
                        "OK", "Cancel"))
                    {
                        // 重置当前选择的UI类型的提示词为默认值
                        settings.figmaSettings.SetPromptForUIType(settings.figmaSettings.selectedUIType, settings.figmaSettings.GetDefaultPrompt());
                        GUI.changed = true;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 引擎支持效果设置
            engineEffectsFoldout = EditorGUILayout.Foldout(engineEffectsFoldout, "Engine Effects", true, EditorStyles.foldoutHeader);

            if (engineEffectsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Configure Unity engine support for UI effects. Enable to avoid downloading effects that can be achieved with native Unity components.",
                    MessageType.Info);

                // 初始化engineSupportEffect如果为null
                if (settings.figmaSettings.engineSupportEffect == null)
                    settings.figmaSettings.engineSupportEffect = new FigmaSettings.EngineSupportEffect();

                // 圆角支持
                EditorGUILayout.BeginHorizontal();
                settings.figmaSettings.engineSupportEffect.roundCorner = EditorGUILayout.Toggle(
                    "Round Corner (ProceduralUIImage)",
                    settings.figmaSettings.engineSupportEffect.roundCorner,
                    GUILayout.Width(200));

                if (settings.figmaSettings.engineSupportEffect.roundCorner)
                {
                    settings.figmaSettings.engineSupportEffect.roundCornerPrompt = EditorGUILayout.TextField(
                        settings.figmaSettings.engineSupportEffect.roundCornerPrompt);
                }
                EditorGUILayout.EndHorizontal();

                // 描边支持
                EditorGUILayout.BeginHorizontal();
                settings.figmaSettings.engineSupportEffect.outLineImg = EditorGUILayout.Toggle(
                    "Outline (Outline Component)",
                    settings.figmaSettings.engineSupportEffect.outLineImg,
                    GUILayout.Width(200));

                if (settings.figmaSettings.engineSupportEffect.outLineImg)
                {
                    settings.figmaSettings.engineSupportEffect.outLinePrompt = EditorGUILayout.TextField(
                        settings.figmaSettings.engineSupportEffect.outLinePrompt);
                }
                EditorGUILayout.EndHorizontal();

                // 渐变支持
                EditorGUILayout.BeginHorizontal();
                settings.figmaSettings.engineSupportEffect.gradientImg = EditorGUILayout.Toggle(
                    "Gradient (UI Gradient)",
                    settings.figmaSettings.engineSupportEffect.gradientImg,
                    GUILayout.Width(200));

                if (settings.figmaSettings.engineSupportEffect.gradientImg)
                {
                    settings.figmaSettings.engineSupportEffect.gradientPrompt = EditorGUILayout.TextField(
                        settings.figmaSettings.engineSupportEffect.gradientPrompt);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 帮助信息
            helpInfoFoldout = EditorGUILayout.Foldout(helpInfoFoldout, "Instructions", true, EditorStyles.foldoutHeader);

            if (helpInfoFoldout)
            {
                EditorGUI.indentLevel++;

                // API Settings description
                EditorGUILayout.LabelField("API Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• Access Token: Generate personal access token in Figma for API access\n" +
                    "• How to get: Login Figma → Settings → Personal access tokens → Generate new token\n" +
                    "• Security: Token saved in local EditorPrefs, won't be committed to Git",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // Download Settings description
                EditorGUILayout.LabelField("Download Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• Download Path: Local save location for images and resources\n" +
                    "• Assets Path: Save location for Figma node data\n" +
                    "• Preview Path: Save location for preview images\n" +
                    "• Scale: Controls image resolution (2.0 recommended for HD)\n" +
                    "• Max Preview Size: Maximum preview image size (pixels)\n" +
                    "• Auto Convert to Sprite: Auto-set downloaded images to Sprite format",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // AI Prompts description
                EditorGUILayout.LabelField("AI Prompts", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• Purpose: Guide AI for precise Figma to Unity conversion\n" +
                    "• Content: Coordinate formulas, layout rules, conversion requirements\n" +
                    "• Customize: Modify prompts based on project needs\n" +
                    "• Reset: Click 'Reset to Default' button to restore default prompts\n" +
                    "• Tip: Keep defaults initially, adjust based on results",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // Engine Effects description
                EditorGUILayout.LabelField("Engine Effects", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• Round Corner: Uses ProceduralUIImage instead of downloading images\n" +
                    "• Outline: Uses Outline component instead of downloading images\n" +
                    "• Gradient: Uses UI Gradient component instead of downloading images\n" +
                    "• Benefits: Reduces resources, improves performance, runtime adjustable",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // Workflow description
                EditorGUILayout.LabelField("Workflow", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "1. Configure Figma access token\n" +
                    "2. Set download path and scale\n" +
                    "3. Configure AI prompts if needed (optional)\n" +
                    "4. Enable engine effects as needed\n" +
                    "5. Use figma_manage in MCP to download design resources\n" +
                    "6. Use AI and prompts for precise UI layout conversion\n" +
                    "7. Auto-create Unity UI components via UI generation tools",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();

            // 自动保存
            if (GUI.changed)
            {
                settings.SaveSettings();
            }
        }
    }
}
