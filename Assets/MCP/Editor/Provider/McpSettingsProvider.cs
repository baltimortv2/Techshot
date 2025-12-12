using UnityEngine;
using UnityEditor;

namespace UniMcp.Gui
{
    /// <summary>
    /// MCP设置提供器，用于在Unity的ProjectSettings窗口中显示MCP设置
    /// </summary>
    public static class McpSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateMcpSettingsProvider()
        {
            var provider = new SettingsProvider("Project/MCP", SettingsScope.Project)
            {
                label = "MCP",
                guiHandler = (searchContext) =>
                {
                    DrawMcpSettings();
                },
                keywords = new[] { "MCP", "Settings", "Configuration", "Debug", "Bridge", "Server" }
            };

            return provider;
        }

        private static void DrawMcpSettings()
        {
            var settings = McpSettings.Instance;

            EditorGUILayout.LabelField("MCP (Model Context Protocol)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "MCP is a powerful Unity extension tool providing intelligent UI generation, code management and project optimization. " +
                "Through deep AI integration, MCP helps developers quickly create high-quality Unity projects.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // 绘制完整的MCP管理GUI
            McpServiceGUI.DrawGUI();

            // 自动保存
            if (GUI.changed)
            {
                settings.SaveSettings();
            }
        }
    }
}
