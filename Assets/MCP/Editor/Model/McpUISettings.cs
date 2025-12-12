using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UniMcp
{
    [System.Serializable]
    public class McpUISettings
    {
        /// <summary>
        /// 当前选择的UI类型
        /// </summary>
        public UIType selectedUIType
        {
            get { return _selectedUIType; }
            set { _selectedUIType = value; }
        }
        [SerializeField] private UIType _selectedUIType = UIType.UGUI;
        /// <summary>
        /// 通用精灵文件夹
        /// </summary>  
        [SerializeField] private List<string> _commonSpriteFolders = new List<string>();
        /// <summary>
        /// 通用纹理文件夹
        /// </summary>
        [SerializeField] private List<string> _commonTextureFolders = new List<string>();
        /// <summary>
        /// 通用字体文件夹
        /// </summary>
        [SerializeField] private List<string> _commonFontFolders = new List<string>();

        /// <summary>
        /// 通用精灵文件夹
        /// </summary>
        public List<string> commonSpriteFolders
        {
            get { return _commonSpriteFolders; }
            set { _commonSpriteFolders = value; }
        }
        /// <summary>
        /// 通用纹理文件夹
        /// </summary>
        public List<string> commonTextureFolders
        {
            get { return _commonTextureFolders; }
            set { _commonTextureFolders = value; }
        }
        /// <summary>
        /// 通用字体文件夹
        /// </summary>
        public List<string> commonFontFolders
        {
            get { return _commonFontFolders; }
            set { _commonFontFolders = value; }
        }
        /// <summary>
        /// 所有UI类型的数据
        /// </summary>
        public Dictionary<UIType, UITypeData> uiTypeDataDict
        {
            get
            {
                if (_uiTypeDataDict == null)
                    InitializeUITypeData();
                return _uiTypeDataDict;
            }
        }
        [System.NonSerialized] private Dictionary<UIType, UITypeData> _uiTypeDataDict;

        /// <summary>
        /// 序列化的UI类型数据列表（用于Unity序列化）
        /// </summary>
        [SerializeField] private List<UITypeDataSerializable> _serializedUITypeData = new List<UITypeDataSerializable>();

        /// <summary>
        /// UI构建步骤（返回当前UI类型的步骤）
        /// </summary>
        public List<string> ui_build_steps
        {
            get
            {
                return GetCurrentUITypeData().buildSteps;
            }
            set
            {
                GetCurrentUITypeData().buildSteps = value;
            }
        }

        /// <summary>
        /// UI构建环境（返回当前UI类型的环境）
        /// </summary>
        public List<string> ui_build_enviroments
        {
            get
            {
                return GetCurrentUITypeData().buildEnvironments;
            }
            set
            {
                GetCurrentUITypeData().buildEnvironments = value;
            }
        }

        /// <summary>
        /// 初始化UI类型数据
        /// </summary>
        private void InitializeUITypeData()
        {
            _uiTypeDataDict = new Dictionary<UIType, UITypeData>();

            // 从序列化数据中恢复
            foreach (var serializedData in _serializedUITypeData)
            {
                _uiTypeDataDict[serializedData.uiType] = serializedData.ToUITypeData();
            }

            // 确保所有UI类型都有数据
            foreach (UIType uiType in System.Enum.GetValues(typeof(UIType)))
            {
                if (!_uiTypeDataDict.ContainsKey(uiType))
                {
                    _uiTypeDataDict[uiType] = CreateDefaultUITypeData(uiType);
                }
            }
        }

        /// <summary>
        /// 获取当前UI类型的数据
        /// </summary>
        private UITypeData GetCurrentUITypeData()
        {
            if (!uiTypeDataDict.ContainsKey(selectedUIType))
            {
                uiTypeDataDict[selectedUIType] = CreateDefaultUITypeData(selectedUIType);
            }
            return uiTypeDataDict[selectedUIType];
        }

        /// <summary>
        /// 序列化UI类型数据
        /// </summary>
        public void SerializeUITypeData()
        {
            _serializedUITypeData.Clear();
            if (_uiTypeDataDict != null)
            {
                foreach (var kvp in _uiTypeDataDict)
                {
                    _serializedUITypeData.Add(new UITypeDataSerializable(kvp.Key, kvp.Value));
                }
            }
        }

        /// <summary>
        /// 创建默认的UI类型数据
        /// </summary>
        private UITypeData CreateDefaultUITypeData(UIType uiType)
        {
            var data = new UITypeData(uiType.ToString());
            data.buildSteps = GetDefaultBuildSteps(uiType);
            data.buildEnvironments = GetDefaultBuildEnvironments(uiType);
            return data;
        }

        /// <summary>
        /// 获取默认的UI构建步骤
        /// </summary>
        // 自动生成的默认构建步骤 - UGUI (2025-10-20 10:30:59)
        // 自动生成的默认构建步骤 - UGUI (2025-10-22 11:18:48)
        // 自动生成的默认构建步骤 - UGUI (2025-10-23 10:25:28)
        // 自动生成的默认构建步骤 - UGUI (2025-10-23 10:32:12)
        // 自动生成的默认构建步骤 - UGUI (2025-10-23 15:47:14)
        public static List<string> GetDefaultBuildSteps()
        {
            return new List<string>
            {
                "Use figma_manage to download page node info",
                "Get figma to UGUI conversion rules via figma_manage",
                "Design UGUI hierarchy based on page info and preview",
                "Analyze hierarchy, maximize interactive components, merge non-interactive ones",
                "Create Canvas and root container with proper size",
                "Match Game window size with UI size",
                "Create required UI components step by step",
                "Check for missing components, fix and re-check until complete",
                "Adjust component hierarchy as needed",
                "Record UI component names and node IDs to rule file via ui_rule_manage",
                "Configure component parameters",
                "Use ugui_layout mcp tool for interface layout adjustment",
                "Set full-screen stretch if root is full-screen size",
                "Record changes to rule file via ui_rule_manage",
                "For components with effects, download images directly instead of parsing children",
                "Load downloaded images to UI components via mcp",
                "Record image info to rule file via ui_rule_manage",
                "Optimize screen adaptation using ugui_layout anchor_preset",
                "Take Game window screenshot and use figma_manage for preview",
                "Analyze UI fidelity and adjust if needed"
            };
        }

        /// <summary>
        /// 根据UI类型获取默认的UI构建步骤
        /// </summary>
        public static List<string> GetDefaultBuildSteps(UIType uiType)
        {
            switch (uiType)
            {
                case UIType.UGUI:
                    return new List<string>
                    {
                        "Review unity-mcp tool usage",
                        "Use figma_manage to download and analyze design structure",
                        "Create Canvas and root container with proper size",
                        "Match Game window size with UI size",
                        "Create required UI components based on design",
                        "Adjust component hierarchy as needed",
                        "Record UI component names and node IDs to rule file",
                        "Configure component properties",
                        "Use ugui_layout mcp tool for interface layout adjustment",
                        "Optimize screen adaptation",
                        "Record changes to rule file",
                        "Download required image resources",
                        "Record image info to rule file",
                        "Load downloaded images to UI components via mcp"
                    };

                case UIType.UIToolkit:
                    return new List<string>
                    {
                        "Review unity-mcp tool usage",
                        "Analyze UI Toolkit design requirements",
                        "Create UI Document and root VisualElement",
                        "Design USS style file",
                        "Create UXML structure file",
                        "Configure UI Builder layout",
                        "Bind C# script logic",
                        "Handle events and interactions",
                        "Optimize responsive layout",
                        "Test different resolution adaptation"
                    };

                case UIType.NGUI:
                    return new List<string>
                    {
                        "Review unity-mcp tool usage",
                        "Create NGUI Root and Camera",
                        "Setup UI Atlas texture",
                        "Create NGUI panels and components",
                        "Configure anchors and layout",
                        "Handle NGUI event system",
                        "Optimize Draw Calls",
                        "Configure fonts and localization"
                    };

                case UIType.FairyGUI:
                    return new List<string>
                    {
                        "Review unity-mcp tool usage",
                        "Import FairyGUI editor resources",
                        "Create FairyGUI package and components",
                        "Setup UI adaptation rules",
                        "Configure animations and transitions",
                        "Bind code logic",
                        "Optimize performance and memory",
                        "Test multi-platform compatibility"
                    };

                case UIType.Custom:
                default:
                    return new List<string>
                    {
                        "Analyze custom UI system requirements",
                        "Design UI architecture",
                        "Implement core UI components",
                        "Configure render pipeline",
                        "Handle input and events",
                        "Optimize performance",
                        "Test and debug"
                    };
            }
        }

        /// <summary>
        /// 获取默认的UI环境说明
        /// </summary>
        public static List<string> GetDefaultBuildEnvironments()
        {
            return GetDefaultBuildEnvironments(UIType.UGUI);
        }

        /// <summary>
        /// 根据UI类型获取默认的UI环境说明
        /// </summary>
        public static List<string> GetDefaultBuildEnvironments(UIType uiType)
        {
            switch (uiType)
            {
                case UIType.UGUI:
                    return new List<string>
                    {
                        "Based on UGUI system",
                        "TMP font support",
                        "Text components must use TMP",
                        "Coordinate system unified to Unity (center origin)",
                        "Round corner images can use ProceduralUIImage instead of downloading"
                    };

                case UIType.UIToolkit:
                    return new List<string>
                    {
                        "Based on UI Toolkit system",
                        "Uses USS stylesheets",
                        "UXML files define structure",
                        "Flexbox layout support",
                        "Responsive design priority",
                        "Vector graphics support",
                        "Modern web standards compatible"
                    };

                case UIType.NGUI:
                    return new List<string>
                    {
                        "Based on NGUI system",
                        "Atlas texture management",
                        "BMFont support",
                        "Draw Call optimization important",
                        "Anchor-based layout",
                        "Independent event system",
                        "Mobile platform optimized"
                    };

                case UIType.FairyGUI:
                    return new List<string>
                    {
                        "Based on FairyGUI editor",
                        "Visual UI design",
                        "Component-based development",
                        "Rich animation support",
                        "Multi-resolution adaptation",
                        "Complex interaction support",
                        "Cross-platform compatible"
                    };

                case UIType.Custom:
                default:
                    return new List<string>
                    {
                        "Custom UI system",
                        "Project-specific customization",
                        "Extensible architecture design",
                        "Performance optimization priority",
                        "Flexible render pipeline"
                    };
            }
        }
    }

    /// <summary>
    /// UI类型数据
    /// </summary>
    [System.Serializable]
    public class UITypeData
    {
        public string typeName;
        public List<string> buildSteps;
        public List<string> buildEnvironments;

        public UITypeData(string name)
        {
            typeName = name;
            buildSteps = new List<string>();
            buildEnvironments = new List<string>();
        }
    }

    /// <summary>
    /// 可序列化的UI类型数据（用于Unity序列化）
    /// </summary>
    [System.Serializable]
    public class UITypeDataSerializable
    {
        public UIType uiType;
        public string typeName;
        public List<string> buildSteps;
        public List<string> buildEnvironments;

        public UITypeDataSerializable(UIType type, UITypeData data)
        {
            uiType = type;
            typeName = data.typeName;
            buildSteps = new List<string>(data.buildSteps);
            buildEnvironments = new List<string>(data.buildEnvironments);
        }

        public UITypeData ToUITypeData()
        {
            var data = new UITypeData(typeName);
            data.buildSteps = new List<string>(buildSteps);
            data.buildEnvironments = new List<string>(buildEnvironments);
            return data;
        }
    }

}