using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    /// Handles GameObject creation operations in the scene hierarchy.
    /// ?????: hierarchy_create
    /// ??: menu, primitive, prefab, empty, copy
    /// </summary>
    [ToolName("hierarchy_create", "Hierarchy")]
    public class HierarchyCreate : StateMethodBase
    {
        /// <summary>
        /// ??????????????
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                // GameObject?? - ??
                new MethodStr("name", "GameObject??", false)
                    .AddExamples("Player", "UI_Button"),
                
                // ?????? - ??
                new MethodStr("source", "????", false)
                    .SetEnumValues("menu", "primitive", "prefab", "empty", "copy"),
                
                // GameObject??
                new MethodStr("tag", "GameObject??")
                    .SetEnumValues("Untagged", "Player", "Enemy", "UI", "MainCamera", "EditorOnly")
                    .AddExamples("Untagged", "Player"),
                
                // GameObject??
                new MethodStr("layer", "GameObject???")
                    .AddExamples("Default", "UI"),
                
                // ?????
                new MethodStr("parent", "????????")
                    .AddExamples("Canvas", "UI/Panel"),
                
                // ???ID
                new MethodStr("parent_id", "?????ID")
                    .AddExample("-1234"),
                
                // ????
                new MethodVector("position", "???? [x, y, z]"),
                
                // ????
                new MethodVector("rotation", "???? [x, y, z]"),
                
                // ????
                new MethodVector("scale", "???? [x, y, z]"),
                
                // ????
                new MethodStr("primitive_type", "????")
                    .SetEnumValues("Cube", "Sphere", "Cylinder", "Capsule", "Plane", "Quad")
                    .AddExamples("Cube", "Sphere"),
                
                // ?????
                new MethodStr("prefab_path", "?????")
                    .AddExamples("Assets/Prefabs/Player.prefab", "Assets/UI/Button.prefab"),
                
                // ????
                new MethodStr("menu_path", "????")
                    .AddExamples("GameObject/3D Object/Cube", "GameObject/UI/Button"),
                
                // ?????
                new MethodStr("copy_source", "????GameObject??")
                    .AddExamples("Player", "Main Camera"),
                
                // ??????
                new MethodBool("save_as_prefab", "????????"),
                
                // ??????
                new MethodBool("set_active", "??????")
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("source")
                    .Leaf("menu", HandleCreateFromMenu)
                    .Branch("primitive")
                        .OptionalKey("primitive_type")
                            .Leaf("Cube", HandleCreateCube)
                            .Leaf("Sphere", HandleCreateSphere)
                            .Leaf("Cylinder", HandleCreateCylinder)
                            .Leaf("Capsule", HandleCreateCapsule)
                            .Leaf("Plane", HandleCreatePlane)
                            .Leaf("Quad", HandleCreateQuad)
                            .DefaultLeaf(HandleCreateFromPrimitive)
                        .Up()
                        .DefaultLeaf(HandleCreateFromPrimitive)
                    .Up()
                    .Leaf("prefab", HandleCreateFromPrefab)
                    .Leaf("empty", HandleCreateEmpty)
                    .Leaf("copy", HandleCreateFromCopy)
                .Build();
        }

        /// <summary>
        /// ???? 
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        private IEnumerator HandleCreateFromMenuAsync(StateTreeContext ctx)
        {
            string menuPath = ctx["menu_path"]?.ToString();
            if (string.IsNullOrEmpty(menuPath))
            {
                yield return Response.Error("'menu_path' parameter is required for menu creation.");
                yield break;
            }

            McpLogger.Log($"[HierarchyCreate] Creating GameObject source menu: '{menuPath}'");

            if (!menuPath.StartsWith("GameObject"))
            {
                yield return Response.Error("'menu_path' parameter must start with 'GameObject'");
                yield break;
            }

            // ??????????
            GameObject previousSelection = Selection.activeGameObject;
            int previousSelectionID = previousSelection != null ? previousSelection.GetEntityId() : 0;

            // ?????
            JsonClass menuResult = MenuUtils.TryExecuteMenuItem(menuPath);

            // ????????
            if (!menuResult["success"].AsBoolDefault(false))
            {
                McpLogger.Log($"[HierarchyCreate] Menu execution failed: {menuResult}");
                yield return menuResult;
                yield break;
            }

            // ????????????,????????????
            GameObject newObject = null;
            int maxRetries = 10;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                newObject = Selection.activeGameObject;

                // ??????????
                if (newObject != null &&
                    (previousSelection == null || newObject.GetEntityId() != previousSelectionID))
                {
                    McpLogger.Log($"[HierarchyCreate] Found newly created object: '{newObject.name}' (ID: {newObject.GetEntityId()}) after {retryCount} retries");
                    break;
                }

                retryCount++;
                if (retryCount < maxRetries)
                {
                    // ??????:??????yield return null,????
                    yield return null;
                }
            }

            // ????????,????
            if (newObject != null &&
                (previousSelection == null || newObject.GetEntityId() != previousSelectionID))
            {
                // ??????,?????????
                yield return null;

                McpLogger.Log($"[HierarchyCreate] Finalizing newly created object: '{newObject.name}' (ID: {newObject.GetEntityId()})");

                // ?????????????
                Selection.activeGameObject = null;

                // ????????
                EditorGUIUtility.editingTextField = false;
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.keyboardControl = 0;

                // ??ESC???
                Event escapeEvent = new Event();
                escapeEvent.type = EventType.KeyDown;
                escapeEvent.keyCode = KeyCode.Escape;
                EditorWindow.focusedWindow?.SendEvent(escapeEvent);

                yield return null; // ????

                // ??????(??????)
                var finalizeResult = FinalizeGameObjectCreation(ctx.JsonData, newObject, false);
                McpLogger.Log($"[HierarchyCreate] Finalization result: {finalizeResult}");
                yield return finalizeResult;
                yield break;
            }
            else
            {
                // ?????????,???????
                McpLogger.Log($"[HierarchyCreate] Menu executed but no new object was detected after {maxRetries} retries. Previous: {previousSelection?.name}, Current: {newObject?.name}");
                yield return Response.Success($"Menu item '{menuPath}' executed successfully, but no new GameObject was detected.");
                yield break;
            }
        }

        /// <summary>
        /// ???????GameObject???
        /// </summary>
        private object HandleCreateFromMenu(StateTreeContext ctx)
        {
            // ???????????????(60?)
            return ctx.AsyncReturn(HandleCreateFromMenuAsync(ctx), 60f);
        }

        /// <summary>
        /// ????????GameObject???
        /// </summary>
        private object HandleCreateFromPrefab(JsonClass args)
        {
            string prefabPath = args["prefab_path"]?.Value;
            if (string.IsNullOrEmpty(prefabPath))
            {
                return Response.Error("'prefab_path' parameter is required for prefab instantiation.");
            }

            McpLogger.Log($"[HierarchyCreate] Creating GameObject source prefab: '{prefabPath}'");
            return CreateGameObjectFromPrefab(args, prefabPath);
        }

        /// <summary>
        /// ?????????GameObject???
        /// </summary>
        private object HandleCreateFromPrimitive(JsonClass args)
        {
            string primitiveType = args["primitive_type"]?.Value;
            if (string.IsNullOrEmpty(primitiveType))
            {
                // ????Cube??????
                primitiveType = "Cube";
                McpLogger.Log("[HierarchyCreate] No primitive_type specified, using default: Cube");
            }

            McpLogger.Log($"[HierarchyCreate] Creating GameObject source primitive: '{primitiveType}'");
            return CreateGameObjectFromPrimitive(args, primitiveType);
        }

        /// <summary>
        /// ????Cube???
        /// </summary>
        private object HandleCreateCube(JsonClass args)
        {
            McpLogger.Log("[HierarchyCreate] Creating Cube primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Cube");
        }

        /// <summary>
        /// ????Sphere???
        /// </summary>
        private object HandleCreateSphere(JsonClass args)
        {
            McpLogger.Log("[HierarchyCreate] Creating Sphere primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Sphere");
        }

        /// <summary>
        /// ????Cylinder???
        /// </summary>
        private object HandleCreateCylinder(JsonClass args)
        {
            McpLogger.Log("[HierarchyCreate] Creating Cylinder primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Cylinder");
        }

        /// <summary>
        /// ????Capsule???
        /// </summary>
        private object HandleCreateCapsule(JsonClass args)
        {
            McpLogger.Log("[HierarchyCreate] Creating Capsule primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Capsule");
        }

        /// <summary>
        /// ????Plane???
        /// </summary>
        private object HandleCreatePlane(JsonClass args)
        {
            McpLogger.Log("[HierarchyCreate] Creating Plane primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Plane");
        }

        /// <summary>
        /// ????Quad???
        /// </summary>
        private object HandleCreateQuad(JsonClass args)
        {
            McpLogger.Log("[HierarchyCreate] Creating Quad primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Quad");
        }

        /// <summary>
        /// ?????GameObject???(??????,????UI/?UI??)
        /// </summary>
        private object HandleCreateEmpty(JsonClass args)
        {
            string name = args["name"]?.Value;
            if (string.IsNullOrEmpty(name))
            {
                name = "GameObject";
                McpLogger.Log("[HierarchyCreate] No name specified for empty GameObject, using default: 'GameObject'");
            }

            McpLogger.Log($"[HierarchyCreate] Creating empty GameObject: '{name}'");

            try
            {
                // ???????(?????)
                GameObjectUtils.PreselectParentIfSpecified(args, McpLogger.Log);

                // ?????
                GameObject parentObject = Selection.activeGameObject;

                // ????????RectTransform(UI??)
                bool parentIsUI = parentObject != null && parentObject.GetComponent<RectTransform>() != null;

                McpLogger.Log($"[HierarchyCreate] Parent is UI: {parentIsUI}, creating appropriate empty object");

                GameObject newGo = null;

                if (parentIsUI)
                {
                    // ?UI??????,????RectTransform
                    newGo = new GameObject(name, typeof(RectTransform));

                    // ?????(?????false????????)
                    if (parentObject != null)
                    {
                        newGo.transform.SetParent(parentObject.transform, false);
                    }

                    McpLogger.Log($"[HierarchyCreate] Created UI GameObject with RectTransform: '{newGo.name}'");
                }
                else
                {
                    // ????,????
                    newGo = new GameObject(name);

                    // ?????(???)
                    if (parentObject != null)
                    {
                        newGo.transform.SetParent(parentObject.transform, true);
                    }

                    McpLogger.Log($"[HierarchyCreate] Created standard GameObject: '{newGo.name}'");
                }

                // ??????
                Undo.RegisterCreatedObjectUndo(newGo, $"Create Empty GameObject '{newGo.name}'");

                McpLogger.Log($"[HierarchyCreate] Finalizing empty object: '{newGo.name}' (ID: {newGo.GetEntityId()})");

                // ??????(??????)
                return FinalizeGameObjectCreation(args, newGo, true);
            }
            catch (Exception e)
            {
                McpLogger.Log($"[HierarchyCreate] Failed to create empty GameObject '{name}': {e.Message}");
                return Response.Error($"Failed to create empty GameObject '{name}': {e.Message}");
            }
        }

        /// <summary>
        /// ????????????
        /// </summary>
        private object HandleCreateFromCopy(JsonClass args)
        {
            string copySource = args["copy_source"]?.Value;
            if (string.IsNullOrEmpty(copySource))
            {
                return Response.Error("'copy_source' parameter is required for copy creation.");
            }

            McpLogger.Log($"[HierarchyCreate] Copying GameObject from source: '{copySource}'");
            return CreateGameObjectFromCopy(args, copySource);
        }

        // --- Core Creation Methods ---

        /// <summary>
        /// ??????GameObject
        /// </summary>
        private object CreateGameObjectFromPrefab(JsonClass args, string prefabPath)
        {
            try
            {
                // ???????(?????)
                GameObjectUtils.PreselectParentIfSpecified(args, McpLogger.Log);

                // ???????????
                string resolvedPath = ResolvePrefabPath(prefabPath);
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    McpLogger.Log($"[HierarchyCreate] Prefab not found at path: '{prefabPath}'");
                    return Response.Error($"Prefab not found at path: '{prefabPath}'");
                }

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(resolvedPath);
                if (prefabAsset == null)
                {
                    McpLogger.Log($"[HierarchyCreate] Failed to load prefab asset at: '{resolvedPath}'");
                    return Response.Error($"Failed to load prefab asset at: '{resolvedPath}'");
                }

                // ??????
                GameObject newGo = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                if (newGo == null)
                {
                    McpLogger.Log($"[HierarchyCreate] Failed to instantiate prefab: '{resolvedPath}'");
                    return Response.Error($"Failed to instantiate prefab: '{resolvedPath}'");
                }

                // ??Unity???????
                //Thread.Sleep(10);

                // ????
                string name = args["name"]?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    newGo.name = name;
                }

                // ??????
                Undo.RegisterCreatedObjectUndo(newGo, $"Instantiate Prefab '{prefabAsset.name}' as '{newGo.name}'");
                McpLogger.Log($"[HierarchyCreate] Instantiated prefab '{prefabAsset.name}' source path '{resolvedPath}' as '{newGo.name}'");

                return FinalizeGameObjectCreation(args, newGo, false);
            }
            catch (Exception e)
            {
                McpLogger.Log($"[HierarchyCreate] Error instantiating prefab '{prefabPath}': {e.Message}");
                return Response.Error($"Error instantiating prefab '{prefabPath}': {e.Message}");
            }
        }

        /// <summary>
        /// ???????GameObject
        /// </summary>
        private object CreateGameObjectFromPrimitive(JsonClass args, string primitiveType)
        {
            try
            {
                // ???????(?????)
                GameObjectUtils.PreselectParentIfSpecified(args, McpLogger.Log);

                PrimitiveType type = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), primitiveType, true);
                GameObject newGo = GameObject.CreatePrimitive(type);

                // ??Unity???????
                //Thread.Sleep(10);

                // ????
                string name = args["name"]?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    newGo.name = name;
                }
                else
                {
                    McpLogger.Log("[HierarchyCreate] 'name' parameter is recommended when creating a primitive.");
                }

                // ??????
                Undo.RegisterCreatedObjectUndo(newGo, $"Create GameObject '{newGo.name}'");
                return FinalizeGameObjectCreation(args, newGo, true);
            }
            catch (ArgumentException)
            {
                McpLogger.Log($"[HierarchyCreate] Invalid primitive type: '{primitiveType}'. Valid types: {string.Join(", ", Enum.GetNames(typeof(PrimitiveType)))}");
                return Response.Error($"Invalid primitive type: '{primitiveType}'. Valid types: {string.Join(", ", Enum.GetNames(typeof(PrimitiveType)))}");
            }
            catch (Exception e)
            {
                McpLogger.Log($"[HierarchyCreate] Failed to create primitive '{primitiveType}': {e.Message}");
                return Response.Error($"Failed to create primitive '{primitiveType}': {e.Message}");
            }
        }


        /// <summary>
        /// ???GameObject???????
        /// </summary>
        private object CreateGameObjectFromCopy(JsonClass args, string copySource)
        {
            try
            {
                // ???????(?????)
                GameObjectUtils.PreselectParentIfSpecified(args, McpLogger.Log);

                // ?????
                GameObject sourceObject = GameObject.Find(copySource);
                if (sourceObject == null)
                {
                    // ????????,????????
                    GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                    sourceObject = allObjects.FirstOrDefault(go =>
                        go.name.Equals(copySource, StringComparison.OrdinalIgnoreCase));

                    if (sourceObject == null)
                    {
                        McpLogger.Log($"[HierarchyCreate] Source GameObject '{copySource}' not found in scene");
                        return Response.Error($"Source GameObject '{copySource}' not found in scene");
                    }
                }

                McpLogger.Log($"[HierarchyCreate] Found source object: '{sourceObject.name}' (ID: {sourceObject.GetEntityId()})");

                // ????
                GameObject newGo = UnityEngine.Object.Instantiate(sourceObject);

                if (newGo == null)
                {
                    McpLogger.Log($"[HierarchyCreate] Failed to instantiate copy of '{copySource}'");
                    return Response.Error($"Failed to instantiate copy of '{copySource}'");
                }

                // ??Unity???????
                //Thread.Sleep(10);

                // ????
                string name = args["name"]?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    newGo.name = name;
                }
                else
                {
                    // ?????????(Unity?????(Clone)??)
                    McpLogger.Log($"[HierarchyCreate] No name specified, copied object named: '{newGo.name}'");
                }

                // ??????
                Undo.RegisterCreatedObjectUndo(newGo, $"Copy GameObject '{sourceObject.name}' as '{newGo.name}'");

                McpLogger.Log($"[HierarchyCreate] Successfully copied '{sourceObject.name}' to '{newGo.name}' (ID: {newGo.GetEntityId()})");

                return FinalizeGameObjectCreation(args, newGo, true);
            }
            catch (Exception e)
            {
                McpLogger.Log($"[HierarchyCreate] Error copying GameObject '{copySource}': {e.Message}");
                return Response.Error($"Error copying GameObject '{copySource}': {e.Message}");
            }
        }

        /// <summary>
        /// ???????
        /// </summary>
        private string ResolvePrefabPath(string prefabPath)
        {
            // ????????????.prefab???,?????
            if (!prefabPath.Contains("/") && !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                string prefabNameOnly = prefabPath;
                McpLogger.Log($"[HierarchyCreate] Searching for prefab named: '{prefabNameOnly}'");

                string[] guids = AssetDatabase.FindAssets($"t:Prefab {prefabNameOnly}");
                if (guids.Length == 0)
                {
                    return null; // ???
                }
                else if (guids.Length > 1)
                {
                    string foundPaths = string.Join(", ", guids.Select(g => AssetDatabase.GUIDToAssetPath(g)));
                    McpLogger.Log($"[HierarchyCreate] Multiple prefabs found matching name '{prefabNameOnly}': {foundPaths}. Using first one.");
                }

                string resolvedPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                McpLogger.Log($"[HierarchyCreate] Found prefab at path: '{resolvedPath}'");
                return resolvedPath;
            }
            else if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // ????.prefab???
                McpLogger.Log($"[HierarchyCreate] Adding .prefab extension to path: '{prefabPath}'");
                return prefabPath + ".prefab";
            }

            return prefabPath;
        }

        /// <summary>
        /// ??GameObject???????
        /// </summary>
        private object FinalizeGameObjectCreation(JsonClass args, GameObject newGo, bool createdNewObject)
        {
            if (newGo == null)
            {
                return Response.Error("GameObject creation failed.");
            }

            try
            {
                McpLogger.Log($"[HierarchyCreate] Starting finalization for '{newGo.name}' (ID: {newGo.GetEntityId()})");

                // ??????????
                Undo.RecordObject(newGo.transform, "Set GameObject Transform");
                Undo.RecordObject(newGo, "Set GameObject Properties");

                // ??????(??????)
                GameObjectUtils.ApplyCommonGameObjectSettings(args, newGo, McpLogger.Log);

                McpLogger.Log($"[HierarchyCreate] Applied settings to '{newGo.name}' (ID: {newGo.GetEntityId()})");

                // ???????
                GameObject finalInstance = newGo;
                bool saveAsPrefab = args["save_as_prefab"].AsBoolDefault(false);

                if (createdNewObject && saveAsPrefab)
                {
                    finalInstance = HandlePrefabSaving(args, newGo);
                    if (finalInstance == null)
                    {
                        return Response.Error("Failed to save GameObject as prefab.");
                    }
                }

                // ????????????
                Selection.activeGameObject = null;

                // ?????????????
                EditorApplication.delayCall += () =>
                {
                    // ????????????
                    Selection.activeGameObject = null;
                    EditorGUIUtility.editingTextField = false;

                    // ??ESC????????
                    Event escapeEvent = new Event();
                    escapeEvent.type = EventType.KeyDown;
                    escapeEvent.keyCode = KeyCode.Escape;
                    EditorWindow.focusedWindow?.SendEvent(escapeEvent);

                    // ????????
                    GUIUtility.keyboardControl = 0;
                    EditorGUIUtility.keyboardControl = 0;

                    // ??????
                    EditorApplication.RepaintHierarchyWindow();
                    if (EditorWindow.focusedWindow != null)
                    {
                        EditorWindow.focusedWindow.Repaint();
                    }
                };

                McpLogger.Log($"[HierarchyCreate] Finalized '{finalInstance.name}' (ID: {finalInstance.GetEntityId()})");

                // ??????
                string successMessage = GenerateCreationSuccessMessage(args, finalInstance, createdNewObject, saveAsPrefab);
                return Response.Success(successMessage, GameObjectUtils.GetGameObjectData(finalInstance));
            }
            catch (Exception e)
            {
                LogError($"[HierarchyCreate] Error finalizing GameObject creation: {e.Message}");
                // ???????
                if (newGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(newGo);
                }
                return Response.Error($"Error finalizing GameObject creation: {e.Message}");
            }
        }



        /// <summary>
        /// ???????
        /// </summary>
        private GameObject HandlePrefabSaving(JsonClass args, GameObject newGo)
        {
            string prefabPath = args["prefab_path"]?.Value;
            if (string.IsNullOrEmpty(prefabPath))
            {
                McpLogger.Log("[HierarchyCreate] 'prefab_path' is required when 'save_as_prefab' is true.");
                return null;
            }

            string finalPrefabPath = prefabPath;
            if (!finalPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                McpLogger.Log($"[HierarchyCreate] Adding .prefab extension to save path: '{finalPrefabPath}'");
                finalPrefabPath += ".prefab";
            }

            try
            {
                // ??????
                string directoryPath = System.IO.Path.GetDirectoryName(finalPrefabPath);
                if (!string.IsNullOrEmpty(directoryPath) && !System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                    AssetDatabase.Refresh();
                    McpLogger.Log($"[HierarchyCreate] Created directory for prefab: {directoryPath}");

                    // ??Unity??????
                    //Thread.Sleep(50);
                }

                // ??????
                GameObject finalInstance = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    newGo,
                    finalPrefabPath,
                    InteractionMode.UserAction
                );

                if (finalInstance == null)
                {
                    UnityEngine.Object.DestroyImmediate(newGo);
                    return null;
                }

                // ?????????
                //Thread.Sleep(10);

                McpLogger.Log($"[HierarchyCreate] GameObject '{newGo.name}' saved as prefab to '{finalPrefabPath}' and instance connected.");
                return finalInstance;
            }
            catch (Exception e)
            {
                McpLogger.Log($"[HierarchyCreate] Error saving prefab '{finalPrefabPath}': {e.Message}");
                UnityEngine.Object.DestroyImmediate(newGo);
                return null;
            }
        }

        /// <summary>
        /// ????????
        /// </summary>
        private string GenerateCreationSuccessMessage(JsonClass args, GameObject finalInstance, bool createdNewObject, bool saveAsPrefab)
        {
            string messagePrefabPath = AssetDatabase.GetAssetPath(
                PrefabUtility.GetCorrespondingObjectFromSource(finalInstance) ?? (UnityEngine.Object)finalInstance
            );

            if (!createdNewObject && !string.IsNullOrEmpty(messagePrefabPath))
            {
                return $"Prefab '{messagePrefabPath}' instantiated successfully as '{finalInstance.name}'.";
            }
            else if (createdNewObject && saveAsPrefab && !string.IsNullOrEmpty(messagePrefabPath))
            {
                return $"GameObject '{finalInstance.name}' created and saved as prefab to '{messagePrefabPath}'.";
            }
            else
            {
                return $"GameObject '{finalInstance.name}' created successfully in scene.";
            }
        }
    }
}

