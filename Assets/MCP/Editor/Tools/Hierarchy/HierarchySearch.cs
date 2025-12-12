using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
// Migrated from Newtonsoft.Json to SimpleJson
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniMcp.Models; // For Response class

namespace UniMcp.Tools
{
    /// <summary>
    /// Handles GameObject search and find operations in the scene hierarchy.
    /// ?????: hierarchy_search
    /// </summary>
    [ToolName("hierarchy_search", "Hierarchy")]
    public class HierarchySearch : StateMethodBase
    {
        /// <summary>
        /// ??????????????
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                // ???? - ??
                new MethodStr("search_type", "????", false)
                    .SetEnumValues("by_name", "by_id", "by_tag", "by_layer", "by_component", "by_query")
                    .AddExamples("by_name", "by_tag"),
                
                // ????
                new MethodStr("query", "????(???ID??????,?????*)", false)
                    .AddExamples("Player*", "UI/Canvas"),
                
                // ????
                new MethodBool("select_many", "?????????"),
                
                // ??????
                new MethodBool("include_hierarchy", "??????????????????"),
                
                // ???????
                new MethodBool("include_inactive", "?????????"),
                
                // ???????
                new MethodBool("use_regex", "?????????")
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("search_type")
                    .Leaf("by_name", HandleSearchByName)
                    .Leaf("by_id", HandleSearchById)
                    .Leaf("by_tag", HandleSearchByTag)
                    .Leaf("by_layer", HandleSearchByLayer)
                    .Leaf("by_component", HandleSearchByComponent)
                    .Leaf("by_query", HandleSearchByquery)
                .DefaultLeaf(HandleDefaultSearch) // ????????
                .Build();
        }

        /// <summary>
        /// ???????? - ?????search_type????????
        /// </summary>
        private object HandleDefaultSearch(JsonClass args)
        {
            string query = args["query"]?.Value;

            // ???query??,?????????
            if (!string.IsNullOrEmpty(query))
            {
                return HandleSearchByName(args);
            }

            return Response.Error("Either 'search_type' must be specified or 'query' must be provided for default name search.");
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// ?????GameObject
        /// </summary>
        private object HandleSearchByName(JsonClass args)
        {
            string query = args["query"]?.Value;
            if (string.IsNullOrEmpty(query))
            {
                return Response.Error("query is required for by_name search.");
            }

            bool findAll = args["select_many"].AsBoolDefault(false);
            bool includeHierarchy = args["include_hierarchy"].AsBoolDefault(false);
            bool searchInInactive = args["include_inactive"].AsBoolDefault(false);

            List<GameObject> foundObjects = new List<GameObject>();

            // ?????? - ??Unity??API
            GameObject exactMatch = GameObject.Find(query);
            if (exactMatch != null && (searchInInactive || exactMatch.activeInHierarchy))
            {
                foundObjects.Add(exactMatch);
            }

            if (findAll || foundObjects.Count == 0)
            {
                // ?????????GameObject
                GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

                // ?????????
                bool hasWildcards = query.Contains('*');
                Regex regex = null;

                if (hasWildcards)
                {
                    // ????????????
                    string regexPattern = ConvertWildcardToRegex(query);
                    try
                    {
                        regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                    }
                    catch (ArgumentException)
                    {
                        // ?????????,???????
                        hasWildcards = false;
                    }
                }

                foreach (GameObject go in allObjects)
                {
                    bool nameMatches;

                    if (hasWildcards && regex != null)
                    {
                        nameMatches = regex.IsMatch(go.name);
                    }
                    else
                    {
                        nameMatches = go.name.Contains(query, StringComparison.OrdinalIgnoreCase);
                    }

                    if (nameMatches)
                    {
                        if (foundObjects.Contains(go))
                            continue;
                        foundObjects.Add(go);
                    }
                }
            }

            return CreateHierarchySearchResult(foundObjects, "name", includeHierarchy);
        }

        /// <summary>
        /// ?ID??GameObject
        /// </summary>
        private object HandleSearchById(JsonClass args)
        {
            string query = args["query"]?.Value;
            bool searchInInactive = args["include_inactive"].AsBoolDefault(false);

            if (string.IsNullOrEmpty(query))
            {
                return Response.Error("query is required for by_id search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // ????ID???
            if (int.TryParse(query, out int instanceId))
            {
                GameObject found = EditorUtility.EntityIdToObject(instanceId) as GameObject;
                if (found != null && (searchInInactive || found.activeInHierarchy))
                {
                    foundObjects.Add(found);
                }
            }

            return CreateSearchResult(foundObjects, "ID");
        }

        /// <summary>
        /// ?????GameObject
        /// </summary>
        private object HandleSearchByTag(JsonClass args)
        {
            string searchTerm = args["query"]?.Value;
            bool findAll = args["select_many"].AsBoolDefault(false);
            bool searchInInactive = args["include_inactive"].AsBoolDefault(false);

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_tag search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // ??Unity???FindGameObjectsWithTag??
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(searchTerm);
            foundObjects.AddRange(taggedObjects);

            if (searchInInactive)
            {
                // ??????? - ???????
                GameObject[] allObjects = GetAllGameObjectsInActiveScene(true);
                foreach (GameObject go in allObjects)
                {
                    if (!go.activeInHierarchy && go.CompareTag(searchTerm))
                    {
                        foundObjects.Add(go);
                    }
                }
            }

            return CreateSearchResult(foundObjects, "tag");
        }

        /// <summary>
        /// ?????GameObject
        /// </summary>
        private object HandleSearchByLayer(JsonClass args)
        {
            string searchTerm = args["query"]?.Value;
            bool findAll = args["select_many"].AsBoolDefault(false);
            bool searchInInactive = args["include_inactive"].AsBoolDefault(false);

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_layer search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // ??????
            int layerIndex = LayerMask.NameToLayer(searchTerm);
            if (layerIndex == -1)
            {
                return Response.Error($"Layer '{searchTerm}' not found.");
            }

            // ???????GameObject
            GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

            foreach (GameObject go in allObjects)
            {
                if (go.layer == layerIndex)
                {
                    foundObjects.Add(go);
                }
            }

            return CreateSearchResult(foundObjects, "layer");
        }

        /// <summary>
        /// ?????GameObject
        /// </summary>
        private object HandleSearchByComponent(JsonClass args)
        {
            string searchTerm = args["query"]?.Value;
            bool findAll = args["select_many"].AsBoolDefault(false);
            bool searchInInactive = args["include_inactive"].AsBoolDefault(false);

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_component search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // ????????
            Type componentType = GetComponentType(searchTerm);
            if (componentType == null)
            {
                return Response.Error($"Component type '{searchTerm}' not found.");
            }

            // ??????????????GameObject
            GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

            foreach (GameObject go in allObjects)
            {
                if (go.GetComponent(componentType) != null)
                {
                    foundObjects.Add(go);
                }
            }

            return CreateSearchResult(foundObjects, "component");
        }

        /// <summary>
        /// ???????GameObject
        /// </summary>
        private object HandleSearchByquery(JsonClass args)
        {
            string searchTerm = args["query"]?.Value;
            bool findAll = args["select_many"].AsBoolDefault(false);
            bool includeHierarchy = args["include_hierarchy"].AsBoolDefault(false);
            bool searchInInactive = args["include_inactive"].AsBoolDefault(false);
            bool useRegex = args["use_regex"].AsBoolDefault(false);

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_query search.");
            }

            // ?????????(t:TypeName ??)
            bool isTypeSearch = false;
            string typeName = null;
            if (searchTerm.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
            {
                isTypeSearch = true;
                typeName = searchTerm.Substring(2).Trim();
                if (string.IsNullOrEmpty(typeName))
                {
                    return Response.Error("Type name is required after 't:' prefix.");
                }
            }

            // ??????:??????????????
            Regex regex = null;
            bool isPatternMatch = false;

            if (!isTypeSearch)
            {
                // ?????????
                bool hasWildcards = searchTerm.Contains('*');

                if (useRegex)
                {
                    // ?????????
                    try
                    {
                        regex = new Regex(searchTerm, RegexOptions.IgnoreCase);
                        isPatternMatch = true;
                    }
                    catch (ArgumentException ex)
                    {
                        return Response.Error($"Invalid regular expression: {ex.Message}");
                    }
                }
                else if (hasWildcards)
                {
                    // ????????????
                    string regexPattern = ConvertWildcardToRegex(searchTerm);
                    try
                    {
                        regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                        isPatternMatch = true;
                    }
                    catch (ArgumentException ex)
                    {
                        return Response.Error($"Invalid wildcard pattern: {ex.Message}");
                    }
                }
            }

            List<GameObject> foundObjects = new List<GameObject>();
            HashSet<GameObject> uniqueObjects = new HashSet<GameObject>(); // ????

            // ???????,????FindObjectsOfType
            if (isTypeSearch)
            {
                Type queryType = GetComponentType(typeName);
                if (queryType == null)
                {
                    return Response.Error($"Component type '{typeName}' not found.");
                }

                GameObject[] sceneObjects = GetAllGameObjectsInActiveScene(searchInInactive);

                foreach (GameObject go in sceneObjects)
                {
                    if (go.GetComponent(queryType) != null)
                    {
                        if (uniqueObjects.Add(go))
                        {
                            foundObjects.Add(go);
                        }
                    }
                }

                return CreateSearchResult(foundObjects, "type");
            }

            // ?????????GameObject
            GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

            foreach (GameObject go in allObjects)
            {
                bool matches = false;

                // 1. ??????
                if (isPatternMatch && regex != null)
                {
                    if (regex.IsMatch(go.name))
                    {
                        matches = true;
                    }
                }
                else
                {
                    if (go.name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                    }
                }

                // 2. ??????
                if (!matches)
                {
                    if (isPatternMatch && regex != null)
                    {
                        if (regex.IsMatch(go.tag))
                        {
                            matches = true;
                        }
                    }
                    else
                    {
                        // ???????,??????????
                        try
                        {
                            if (go.CompareTag(searchTerm) || go.tag.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            {
                                matches = true;
                            }
                        }
                        catch (UnityException)
                        {
                            // ?????,??????
                        }
                    }
                }

                // 3. ??????
                if (!matches)
                {
                    string layerName = LayerMask.LayerToName(go.layer);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        if (isPatternMatch && regex != null)
                        {
                            if (regex.IsMatch(layerName))
                            {
                                matches = true;
                            }
                        }
                        else
                        {
                            if (layerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            {
                                matches = true;
                            }
                        }
                    }
                }

                // 4. ??????
                if (!matches)
                {
                    Component[] components = go.GetComponents<Component>();
                    foreach (Component component in components)
                    {
                        if (component != null)
                        {
                            string componentTypeName = component.GetType().Name;
                            if (isPatternMatch && regex != null)
                            {
                                if (regex.IsMatch(componentTypeName))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                            else
                            {
                                if (componentTypeName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 5. ?????????(????)
                if (!matches)
                {
                    Transform[] children = go.GetComponentsInChildren<Transform>();
                    foreach (Transform child in children)
                    {
                        if (child != go.transform)
                        {
                            if (isPatternMatch && regex != null)
                            {
                                if (regex.IsMatch(child.name))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                            else
                            {
                                if (child.name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (matches && uniqueObjects.Add(go))
                {
                    foundObjects.Add(go);
                }
            }

            return CreateHierarchySearchResult(foundObjects, "term", includeHierarchy);
        }

        // --- Helper Methods ---

        /// <summary>
        /// ????????????GameObject
        /// </summary>
        private GameObject[] GetAllGameObjectsInActiveScene(bool includeInactive)
        {
            List<GameObject> allObjects = new List<GameObject>();

            // ????????????
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return allObjects.ToArray();
            }

            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            foreach (GameObject rootObj in rootObjects)
            {
                if (includeInactive)
                {
                    // ???????,???????(??????)
                    Transform[] allTransforms = rootObj.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allTransforms)
                    {
                        allObjects.Add(t.gameObject);
                    }
                }
                else
                {
                    // ???????
                    if (rootObj.activeInHierarchy)
                    {
                        Transform[] activeTransforms = rootObj.GetComponentsInChildren<Transform>(false);
                        foreach (Transform t in activeTransforms)
                        {
                            allObjects.Add(t.gameObject);
                        }
                    }
                }
            }

            return allObjects.ToArray();
        }

        /// <summary>
        /// ??????
        /// </summary>
        private object CreateSearchResult(List<GameObject> foundObjects, string searchType)
        {
            // ??????
            var results = foundObjects.Select(go => Json.FromObject(GameObjectUtils.GetGameObjectData(go))).ToList();

            // ??????
            string message;
            if (results.Count == 0)
            {
                message = $"No GameObjects found using search method: {searchType}.";
            }
            else
            {
                message = $"Found {results.Count} GameObjects using {searchType}.";
            }

            // ??????,?????????????????
            var response = new JsonClass
            {
                ["success"] = true,
                ["message"] = message,
                ["data"] = Json.FromObject(results),
                ["exec_time_ms"] = 1.00,
                ["mode"] = "Async mode"
            };

            return response;
        }

        /// <summary>
        /// ???????????????
        /// </summary>
        private object CreateHierarchySearchResult(List<GameObject> foundObjects, string searchType, bool includeHierarchy)
        {
            // ?????? - ??JObject???????
            var results = new List<JsonClass>();

            if (includeHierarchy)
            {
                // ?????????
                foreach (var go in foundObjects)
                {
                    var hierarchyData = GetCompleteHierarchyData(go);
                    results.Add(hierarchyData);
                }
            }
            else
            {
                // ?????GameObject??
                foreach (var go in foundObjects)
                {
                    results.Add(GameObjectUtils.GetGameObjectData(go));
                }
            }

            // ????
            string message;
            if (results.Count == 0)
            {
                message = $"No GameObjects found using search method: {searchType}.";
            }
            else
            {
                string hierarchyInfo = includeHierarchy ? " with complete hierarchy" : "";
                message = $"Found {results.Count} GameObjects using {searchType}{hierarchyInfo}.";
            }

            // ??????,???????
            var response = new JsonClass
            {
                ["success"] = true,
                ["message"] = message,
                ["data"] = Json.FromObject(results),
                ["exec_time_ms"] = 1.00,
                ["mode"] = "Async mode"
            };

            return response;
        }

        /// <summary>
        /// ??GameObject???????(????????????)
        /// </summary>
        private JsonClass GetCompleteHierarchyData(GameObject go)
        {
            if (go == null)
                return null;

            // ?????????YAML??
            var baseYaml = GameObjectUtils.GetGameObjectDataYaml(go);

            // ????JSONClass
            JsonClass result = new JsonClass();
            result["yaml"] = baseYaml;

            // ??????????????
            if (go.transform.childCount > 0)
            {
                JsonArray childrenArray = new JsonArray();
                foreach (Transform child in go.transform)
                {
                    if (child != null && child.gameObject != null)
                    {
                        JsonClass childData = GetCompleteHierarchyData(child.gameObject);
                        if (childData != null)
                        {
                            childrenArray.Add(childData);
                        }
                    }
                }

                if (childrenArray.Count > 0)
                {
                    result["children"] = childrenArray;
                }
            }

            return result;
        }

        /// <summary>
        /// ??????
        /// </summary>
        private Type GetComponentType(string typeName)
        {
            // ??????Unity??????
            string[] commonNamespaces = {
                "UnityEngine",
                "UnityEngine.UI",
                "UnityEngine.EventSystems",
                "UnityEditor"
            };

            foreach (string ns in commonNamespaces)
            {
                Type type = Type.GetType($"{ns}.{typeName}");
                if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                    return type;
            }

            // ????????
            Type directType = Type.GetType(typeName);
            if (directType != null && typeof(UnityEngine.Object).IsAssignableFrom(directType))
                return directType;

            // ???????????????
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // ???????
                    Type type = assembly.GetType(typeName);
                    if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                        return type;

                    // ???????,??????
                    foreach (var t in assembly.GetTypes())
                    {
                        if ((t.Name == typeName || t.FullName == typeName) &&
                            typeof(UnityEngine.Object).IsAssignableFrom(t))
                            return t;
                    }
                }
                catch
                {
                    // ??????????
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// ??????????????
        /// </summary>
        /// <param name="wildcardPattern">?????*???</param>
        /// <returns>????????</returns>
        private string ConvertWildcardToRegex(string wildcardPattern)
        {
            if (string.IsNullOrEmpty(wildcardPattern))
                return string.Empty;

            // ?????????????,??????*
            string escaped = Regex.Escape(wildcardPattern);

            // ?????\*???.*(??????)
            string regexPattern = escaped.Replace("\\*", ".*");

            // ???????????
            return $"^{regexPattern}$";
        }
    }
}

