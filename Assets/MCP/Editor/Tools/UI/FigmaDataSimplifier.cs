using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// Migrated from Newtonsoft.Json to SimpleJson
// Migrated from Newtonsoft.Json to SimpleJson

namespace UniMcp.Tools
{
    /// <summary>
    /// Figma????? - ????Figma???????AI???token?????
    /// </summary>
    public static class FigmaDataSimplifier
    {
        /// ??Figma????,???????????
        /// </summary>
        /// <param name="figmaNode">??Figma????</param>
        /// <param name="maxDepth">????,?????</param>
        /// <returns>????????(JsonNode??)</returns>
        public static JsonNode SimplifyNode(JsonNode figmaNode, int maxDepth = -1)
        {
            var result = SimplifyNodeInternal(figmaNode, maxDepth, null, null);

            // ??Figma???,???????
            return result;
        }

        /// <summary>
        /// ??????,?????????
        /// </summary>
        private static JsonNode SimplifyNodeInternal(JsonNode figmaNode, int maxDepth, JsonNode parentNode, JsonNode parentFigmaNode)
        {
            if (figmaNode == null || maxDepth == 0)
                return null;

            // ???????,????null,?????
            bool visible = figmaNode["visible"].AsBoolDefault(true);
            if (!visible)
                return null;

            // ??????JsonClass??????????
            var simplified = new JsonClass();

            // ??????
            if (!figmaNode["id"].IsNull())
                simplified["id"] = figmaNode["id"];
            if (!figmaNode["name"].IsNull())
                simplified["name"] = figmaNode["name"];
            if (!figmaNode["type"].IsNull())
                simplified["type"] = figmaNode["type"];
            // visible?????,??????????????

            // ???????????(??Figma???)
            var absoluteBoundingBox = figmaNode["absoluteBoundingBox"];
            if (absoluteBoundingBox != null)
            {
                float figmaX = absoluteBoundingBox["x"].AsFloatDefault(0);
                float figmaY = absoluteBoundingBox["y"].AsFloatDefault(0);
                float width = absoluteBoundingBox["width"].AsFloatDefault(0);
                float height = absoluteBoundingBox["height"].AsFloatDefault(0);

                // ??Figma?????(?????)
                var posArray = new JsonArray();
                posArray.Add(new JsonData((float)Math.Round(figmaX, 2)));
                posArray.Add(new JsonData((float)Math.Round(figmaY, 2)));
                simplified["pos"] = posArray;

                var sizeArray = new JsonArray();
                sizeArray.Add(new JsonData((float)Math.Round(width, 2)));
                sizeArray.Add(new JsonData((float)Math.Round(height, 2)));
                simplified["size"] = sizeArray;
            }

            // ?????????
            ExtractTextInfo(figmaNode, simplified);

            // ??????
            ExtractStyleInfo(figmaNode, simplified);

            // ??????
            ExtractLayoutInfo(figmaNode, simplified);

            // ??????????
            if (HasImageRef(figmaNode))
                simplified["hasImage"] = true;

            // ???????????(????)
            if (IsDownloadableNode(figmaNode))
                simplified["hasEffect"] = true;

            // ???????
            var children = figmaNode["children"];
            if (children != null && children.type == JsonNodeType.Array)
            {
                var simplifiedChildren = new JsonArray();
                var nextDepth = maxDepth > 0 ? maxDepth - 1 : -1; // ??maxDepth?-1??????

                foreach (JsonNode child in children.Childs) // ???????
                {
                    var simplifiedChild = SimplifyNodeInternal(child, nextDepth, simplified, figmaNode);
                    if (simplifiedChild != null)
                    {
                        simplifiedChildren.Add(simplifiedChild);
                    }
                }

                if (simplifiedChildren.Count > 0)
                {
                    simplified["children"] = simplifiedChildren;
                }
            }

            // ??????????absolutePos?size,?????UGUI??

            return simplified;
        }

        /// <summary>
        /// ??????
        /// </summary>
        private static void ExtractTextInfo(JsonNode node, JsonNode simplified)
        {
            // ????
            if (!node["characters"].IsNull())
            {
                simplified["text"] = node["characters"];
            }
            // ????
            var style = node["style"];
            if (!node["style"].IsNull() && style != null && style.type == JsonNodeType.Object)
            {
                var textStyle = new JsonClass();
                textStyle["fontFamily"] = style["fontFamily"];
                textStyle["fontWeight"] = style["fontWeight"];
                textStyle["fontSize"] = new JsonData((float)Math.Round(style["fontSize"].AsFloatDefault(0), 2));
                textStyle["textAlign"] = style["textAlignHorizontal"];
                textStyle["lineHeight"] = new JsonData((float)Math.Round(style["lineHeightPx"].AsFloatDefault(0), 2));

                simplified["textStyle"] = textStyle;
            }
        }

        /// <summary>
        /// ??????
        /// </summary>
        private static void ExtractStyleInfo(JsonNode node, JsonNode simplified)
        {
            // ?????????
            var fills = node["fills"];
            if (!node["fills"].IsNull() && fills != null && fills.type == JsonNodeType.Array && fills.Count > 0)
            {
                simplified["fills"] = ExtractFillsInfo(fills);

                // ??????:??????????????
                var fillsArray0 = simplified["fills"] as JsonArray;
                if (fillsArray0 != null && fillsArray0.Count > 0)
                {
                    var firstVisibleFill = fillsArray0.Childs.FirstOrDefault(f => f["visible"].AsBoolDefault(true));
                    if (firstVisibleFill != null && !firstVisibleFill["color"].IsNull())
                    {
                        simplified["backgroundColor"] = firstVisibleFill["color"];
                    }
                }
            }

            // ????
            var textStyle = simplified["textStyle"] as JsonClass;
            if (textStyle != null && fills != null && fills.type == JsonNodeType.Array && fills.Count > 0)
            {
                var firstFill = fills.Childs.FirstOrDefault();
                if (firstFill != null && firstFill.type == JsonNodeType.Object)
                {
                    simplified["textColor"] = ExtractColor(firstFill);
                }
            }

            // ??
            var cornerRadius = (float)Math.Round(node["cornerRadius"].AsFloatDefault(0));
            if (cornerRadius > 0)
            {
                simplified["cornerRadius"] = cornerRadius;
            }

            // ???? - ??????????
            var fillsArray = simplified["fills"] as JsonArray;
            if (fillsArray != null && fillsArray.Count > 0)
            {
                var imageFill = fillsArray.Childs.FirstOrDefault(f =>
                    f["type"].Value == "IMAGE" &&
                    !f["imageRef"].IsNull() &&
                    !string.IsNullOrEmpty(f["imageRef"].Value));

                if (imageFill != null)
                {
                    simplified["imageRef"] = imageFill["imageRef"];
                }
            }
        }

        /// <summary>
        /// ???????????
        /// </summary>
        private static JsonArray ExtractFillsInfo(JsonNode fills)
        {
            var fillInfos = new JsonArray();

            if (fills == null || fills.type != JsonNodeType.Array)
                return fillInfos;

            foreach (JsonNode fill in fills.Childs)
            {
                if (fill == null)
                    continue;

                var fillInfo = new JsonClass();
                if (!fill["type"].IsNull())
                    fillInfo["type"] = fill["type"];
                fillInfo["visible"] = new JsonData(fill["visible"].AsBoolDefault(true));
                fillInfo["opacity"] = new JsonData((float)Math.Round(fill["opacity"].AsFloatDefault(1.0f), 2));
                if (!fill["blendMode"].IsNull())
                    fillInfo["blendMode"] = fill["blendMode"];

                // ????????????
                string fillType = fillInfo["type"].Value;
                switch (fillType)
                {
                    case "SOLID":
                        fillInfo["color"] = ExtractColor(fill);
                        break;

                    case "IMAGE":
                        if (!fill["imageRef"].IsNull())
                            fillInfo["imageRef"] = fill["imageRef"];
                        break;

                    case "GRADIENT_LINEAR":
                    case "GRADIENT_RADIAL":
                    case "GRADIENT_ANGULAR":
                        fillInfo["gradient"] = ExtractGradientInfo(fill);
                        break;
                }

                fillInfos.Add(fillInfo);
            }

            return fillInfos;
        }

        /// <summary>
        /// ??????
        /// </summary>
        private static JsonClass ExtractGradientInfo(JsonNode fill)
        {
            var gradientInfo = new JsonClass();
            if (!fill["type"].IsNull())
                gradientInfo["type"] = fill["type"];

            // ???????
            var gradientStops = fill["gradientStops"];
            if (!fill["gradientStops"].IsNull() && gradientStops != null && gradientStops.type == JsonNodeType.Array)
            {
                var stopsArray = new JsonArray();
                foreach (JsonNode stop in gradientStops.Childs)
                {
                    if (stop != null)
                    {
                        var gradientStop = new JsonClass();
                        gradientStop["position"] = stop["position"];
                        gradientStop["color"] = ExtractColor(stop);
                        stopsArray.Add(gradientStop);
                    }
                }
                gradientInfo["gradientStops"] = stopsArray;
            }

            // ????????
            var gradientHandlePositions = fill["gradientHandlePositions"];
            if (!fill["gradientHandlePositions"].IsNull() && gradientHandlePositions != null && gradientHandlePositions.type == JsonNodeType.Array)
            {
                var positionsArray = new JsonArray();
                foreach (JsonNode position in gradientHandlePositions.Childs)
                {
                    if (position != null && position.Count >= 2)
                    {
                        var posArray = new JsonArray();
                        posArray.Add(new JsonData((float)Math.Round(position[0].AsFloatDefault(0), 2)));
                        posArray.Add(new JsonData((float)Math.Round(position[1].AsFloatDefault(0), 2)));
                        positionsArray.Add(posArray);
                    }
                }
                gradientInfo["gradientHandlePositions"] = positionsArray;
            }

            return gradientInfo;
        }

        /// <summary>
        /// ??????
        /// </summary>
        private static JsonNode ExtractColor(JsonNode fill)
        {
            if (fill == null || fill.type != JsonNodeType.Object) return new JsonData("");

            var color = fill["color"];
            if (!fill["color"].IsNull() && color != null && color.type == JsonNodeType.Object)
            {
                // ???????,???????
                int r = Mathf.RoundToInt(color["r"].AsFloatDefault(0) * 255);
                int g = Mathf.RoundToInt(color["g"].AsFloatDefault(0) * 255);
                int b = Mathf.RoundToInt(color["b"].AsFloatDefault(0) * 255);
                int a = Mathf.RoundToInt(color["a"].AsFloatDefault(1) * 255);
                return new JsonData($"#{r:X2}{g:X2}{b:X2}{a:X2}");
            }

            return new JsonData("");
        }

        /// <summary>
        /// ??????
        /// </summary>
        private static void ExtractLayoutInfo(JsonNode node, JsonNode simplified)
        {
            var layoutMode = !node["layoutMode"].IsNull() ? node["layoutMode"].Value : "";
            if (!string.IsNullOrEmpty(layoutMode))
            {
                var layout = new JsonClass();
                layout["layoutMode"] = layoutMode;

                string alignItems = "";
                if (!node["primaryAxisAlignItems"].IsNull())
                    alignItems = node["primaryAxisAlignItems"].Value;
                else if (!node["counterAxisAlignItems"].IsNull())
                    alignItems = node["counterAxisAlignItems"].Value;

                layout["alignItems"] = alignItems;
                layout["itemSpacing"] = (float)Math.Round(node["itemSpacing"].AsFloatDefault(0), 2);

                simplified["layout"] = layout;

                // ???
                var paddingLeft = (float)Math.Round(node["paddingLeft"].AsFloatDefault(0), 2);
                var paddingTop = (float)Math.Round(node["paddingTop"].AsFloatDefault(0), 2);
                var paddingRight = (float)Math.Round(node["paddingRight"].AsFloatDefault(0), 2);
                var paddingBottom = (float)Math.Round(node["paddingBottom"].AsFloatDefault(0), 2);

                if (paddingLeft > 0 || paddingTop > 0 || paddingRight > 0 || paddingBottom > 0)
                {
                    layout = simplified["layout"] as JsonClass;
                    if (layout != null)
                    {
                        var paddingArray = new JsonArray();
                        paddingArray.Add(new JsonData(paddingLeft));
                        paddingArray.Add(new JsonData(paddingTop));
                        paddingArray.Add(new JsonData(paddingRight));
                        paddingArray.Add(new JsonData(paddingBottom));
                        layout["padding"] = paddingArray;
                    }
                }
            }
        }


        /// <summary>
        /// ????????
        /// </summary>
        /// <param name="figmaNodes">????????</param>
        /// <param name="maxDepth">????,?????</param>
        /// <param name="useComponentPfb">?????????,??????</param>
        /// <returns>????????(JsonNode, ???????)</returns>
        public static JsonNode SimplifyNodes(JsonClass figmaNodes, int maxDepth = -1, bool useComponentPfb = false)
        {
            var result = new JsonClass();

            if (figmaNodes == null) return result;

            foreach (KeyValuePair<string, JsonNode> kvp in figmaNodes.AsEnumerable())
            {
                var nodeData = kvp.Value["document"];
                if (nodeData != null)
                {
                    var simplified = SimplifyNode(nodeData, maxDepth);
                    if (simplified != null)
                    {
                        // ????? components
                        var componentsData = kvp.Value["components"];
                        if (componentsData != null && componentsData is JsonClass)
                        {
                            var componentsList = ExtractComponentIds(componentsData);
                            if (componentsList.Count > 0)
                            {
                                if (useComponentPfb)
                                {
                                    // ???????:??ID?????????
                                    var componentsDict = new JsonClass();
                                    foreach (var componentId in componentsList)
                                    {
                                        // ??????,?????????????
                                        string prefabPath = Models.ComponentDefineObject.GetPrefabPathById(componentId);
                                        if (!string.IsNullOrEmpty(prefabPath))
                                        {
                                            componentsDict[componentId] = prefabPath;
                                        }
                                    }
                                    simplified["components"] = componentsDict;
                                }
                                else
                                {
                                    // ????:????ID??
                                    var componentsArray = new JsonArray();
                                    foreach (var componentId in componentsList)
                                    {
                                        componentsArray.Add(componentId);
                                    }
                                    simplified["components"] = componentsArray;
                                }
                            }
                        }

                        result[kvp.Key] = simplified;
                    }
                }
            }

            // ????????????,??????
            if (useComponentPfb)
            {
                ProcessNodesForComponentPrefabs(result);
            }

            return result;
        }

        /// <summary>
        /// ????????????????
        /// </summary>
        /// <param name="nodesData">????</param>
        private static void ProcessNodesForComponentPrefabs(JsonClass nodesData)
        {
            if (nodesData == null) return;

            // ??????
            foreach (KeyValuePair<string, JsonNode> kvp in nodesData.AsEnumerable())
            {
                ProcessNodeForComponentPrefabs(kvp.Value);
            }
        }

        /// <summary>
        /// ??????????????????
        /// </summary>
        /// <param name="node">????</param>
        /// <returns>????????</returns>
        private static bool ProcessNodeForComponentPrefabs(JsonNode node)
        {
            if (node == null || node.type != JsonNodeType.Object) return false;

            // ???????????
            bool isComponentInstance = !node["componentId"].IsNull() && !string.IsNullOrEmpty(node["componentId"].Value);
            string componentId = isComponentInstance ? node["componentId"].Value : null;

            // ???????????????????
            string prefabPath = "";
            var components = node["components"] as JsonClass;
            if (isComponentInstance && components != null && !components[componentId].IsNull())
            {
                prefabPath = components[componentId].Value;
            }

            // ????????,??????????
            if (isComponentInstance && !string.IsNullOrEmpty(prefabPath))
            {
                // ?????
                if (!node["children"].IsNull())
                {
                    node.Remove("children");
                }

                // ??????
                node["desc"] = new JsonData($"???????: {prefabPath}");

                return true; // ??????????
            }

            // ???????
            var children = node["children"];
            if (!node["children"].IsNull() && children != null && children.type == JsonNodeType.Array)
            {
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    if (ProcessNodeForComponentPrefabs(children[i]))
                    {
                        // ?????????,?????????
                        // ????,??????????
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// ????ID??
        /// </summary>
        /// <param name="componentsData">??????</param>
        /// <returns>??ID??</returns>
        private static List<string> ExtractComponentIds(JsonNode componentsData)
        {
            var componentIds = new List<string>();

            if (componentsData == null || componentsData.type != JsonNodeType.Object)
                return componentIds;

            foreach (string key in ((JsonClass)componentsData).GetKeys())
            {
                // key ????ID
                componentIds.Add(key);
            }

            return componentIds;
        }


        #region ??????

        /// <summary>
        /// ??????,???????????
        /// </summary>
        private static bool IsDownloadableNode(JsonNode node)
        {
            if (node == null) return false;

            string nodeType = node["type"]?.Value;
            // ?????visible,??????????????????

            // 1. ?????????
            if (HasImageRef(node))
            {
                return true;
            }

            // 2. Vector????(????)
            if (nodeType == "VECTOR" || nodeType == "BOOLEAN_OPERATION")
            {
                return true;
            }

            // 3. ????????????
            if (HasComplexFills(node))
            {
                return true;
            }

            // 4. ??????
            if (HasStrokes(node))
            {
                return true;
            }

            // 5. ??????(??????)
            if (HasEffects(node))
            {
                return true;
            }

            // 6. ????
            if (nodeType == "ELLIPSE")
            {
                return true;
            }

            // 7. ??????
            if (nodeType == "RECTANGLE" && HasRoundedCorners(node))
            {
                return true;
            }

            // 8. ???Frame(???????????)
            if (nodeType == "FRAME" && IsComplexFrame(node))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// ????????????
        /// </summary>
        private static bool HasImageRef(JsonNode node)
        {
            var fills = node["fills"];
            if (fills != null)
            {
                foreach (JsonNode fill in fills.Childs)
                {
                    if (fill["type"]?.Value == "IMAGE" && fill["imageRef"] != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// ?????????(??????)
        /// </summary>
        private static bool HasComplexFills(JsonNode node)
        {
            var fills = node["fills"];
            if (fills != null)
            {
                foreach (JsonNode fill in fills.Childs)
                {
                    string fillType = fill["type"]?.Value;
                    if (fillType == "GRADIENT_LINEAR" ||
                        fillType == "GRADIENT_RADIAL" ||
                        fillType == "GRADIENT_ANGULAR" ||
                        fillType == "IMAGE")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// ???????
        /// </summary>
        private static bool HasStrokes(JsonNode node)
        {
            var strokes = node["strokes"];
            return strokes != null && strokes.Count > 0;
        }

        /// <summary>
        /// ???????
        /// </summary>
        private static bool HasEffects(JsonNode node)
        {
            var effects = node["effects"];
            return effects != null && effects.Count > 0;
        }

        /// <summary>
        /// ???????
        /// </summary>
        private static bool HasRoundedCorners(JsonNode node)
        {
            var cornerRadius = node["cornerRadius"];
            if (cornerRadius != null)
            {
                float radius = cornerRadius.AsFloat;
                return radius > 0;
            }

            var rectangleCornerRadii = node["rectangleCornerRadii"];
            if (rectangleCornerRadii != null)
            {
                foreach (JsonNode radius in rectangleCornerRadii.Childs)
                {
                    if (radius.AsFloat > 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// ???????Frame
        /// </summary>
        private static bool IsComplexFrame(JsonNode node)
        {
            var children = node["children"];
            if (children == null || children.Count == 0)
                return false;

            // ??Frame?????????????????????,?????Frame
            if (HasComplexFills(node) || HasEffects(node) || HasStrokes(node))
                return true;

            // ?????????????
            int childCount = children.Count;
            if (childCount > 3) // ??3?????????
                return true;

            return false;
        }
        #endregion
    }
}
