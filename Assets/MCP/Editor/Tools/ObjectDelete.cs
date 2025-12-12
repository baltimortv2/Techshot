using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
// Migrated from Newtonsoft.Json to SimpleJson
using UnityEditor;
using UnityEngine;
using UniMcp.Models; // For Response class

namespace UniMcp.Tools
{
    /// <summary>
    /// Handles UnityEngine.Object deletion operations using dual state tree architecture with interactive confirmation.
    /// Supports GameObjects, assets, and other Unity objects.
    /// Target tree: IObjectSelector handles target location
    /// Action tree: 'confirm' parameter determines confirmation behavior:
    ///   - confirm=true: Always shows confirmation dialog before deletion
    ///   - confirm=false/unset: Asset deletion requires confirmation, scene object deletion is direct
    /// Uses coroutines with EditorUtility.DisplayDialog for interactive user confirmation.
    /// ?????: object_delete
    /// </summary>
    [ToolName("object_delete", "Resources")]
    public class ObjectDelete : DualStateMethodBase
    {
        private IObjectSelector objectSelector;

        public ObjectDelete()
        {
            objectSelector = new ObjectSelector<UnityEngine.Object>();
        }

        /// <summary>
        /// ??????????????
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                // ?????? - ??????
                new MethodStr("path", "????????", false)
                    .AddExamples("Main Camera", "UI/Canvas/Button"),
                
                // ?????? - ??ID
                new MethodStr("instance_id", "????ID")
                    .AddExample("-2524"),
                
                // ???????
                new MethodBool("confirm", "???????:true=????,false/???=????(?3???,>3????)")
            };
        }

        /// <summary>
        /// ?????????(??IObjectSelector)
        /// </summary>
        protected override StateTree CreateTargetTree()
        {
            return objectSelector.BuildStateTree();
        }

        /// <summary>
        /// ?????????
        /// </summary>
        protected override StateTree CreateActionTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("confirm")
                    .Leaf("true", (Func<StateTreeContext, object>)HandleConfirmedDeleteAction) // ????
                    .Leaf("false", (Func<StateTreeContext, object>)HandleUnconfirmedDeleteAction) // ?????
                    .DefaultLeaf((Func<StateTreeContext, object>)HandleUnconfirmedDeleteAction) // ????????
                .Build();
        }

        /// <summary>
        /// ???????????????(?????????)
        /// </summary>
        private IEnumerator HandleConfirmedDeleteActionAsync(StateTreeContext ctx)
        {
            UnityEngine.Object target = ExtractTargetFromContext(ctx);
            if (target == null)
            {
                yield return Response.Error("No target Object found for deletion.");
                yield break;
            }

            // ???????????
            bool isAssetDeletion = IsAssetDeletion(ctx);
            if (!isAssetDeletion)
            {
                // ??????,????Object
                var result = DeleteSingleObject(target);
                yield return result;
                yield break;
            }

            // ???????????
            string confirmationMessage = $"Are you sure you want to delete the asset '{target.name}' ({target.GetType().Name})?\n\nThis action cannot be undone.";

            bool confirmed = EditorUtility.DisplayDialog(
                "Confirm Asset Deletion",
                confirmationMessage,
                "Delete Asset",
                "Cancel"
            );

            if (!confirmed)
            {
                McpLogger.Log($"[ObjectDelete] User cancelled asset deletion for Object '{target.name}'");
                yield return Response.Success($"Asset deletion cancelled by user. Object '{target.name}' was not deleted.", new { cancelled = true, target_name = target.name });
                yield break;
            }

            McpLogger.Log($"[ObjectDelete] User confirmed asset deletion for Object '{target.name}'");

            // ?????????
            var deleteResult = DeleteSingleObject(target);
            yield return deleteResult;
        }

        /// <summary>
        /// ?????????????
        /// </summary>
        private object HandleConfirmedDeleteAction(StateTreeContext ctx)
        {
            // ?????????????(30?)
            return ctx.AsyncReturn(HandleConfirmedDeleteActionAsync(ctx), 30f);
        }

        /// <summary>
        /// ??????????????,??????????????????
        /// </summary>
        private IEnumerator HandleUnconfirmedDeleteActionAsync(StateTreeContext ctx)
        {
            UnityEngine.Object target = ExtractTargetFromContext(ctx);
            if (target == null)
            {
                yield return Response.Error("No target Object found for deletion.");
                yield break;
            }

            // ????????(??GameObject??)
            if (target is GameObject gameObject)
            {
                object redirectResult = CheckPrefabRedirection(gameObject);
                if (redirectResult != null)
                {
                    yield return redirectResult;
                    yield break;
                }
            }

            // ?????????
            bool isAssetDeletion = IsAssetDeletion(ctx);
            if (!isAssetDeletion)
            {
                // ??Object??,????????
                McpLogger.Log($"[ObjectDelete] Direct deletion of {target.GetType().Name} '{target.name}' without confirmation");
                var result = DeleteSingleObject(target);
                yield return result;
                yield break;
            }

            // ??????????
            McpLogger.Log($"[ObjectDelete] Asset deletion detected for '{target.name}', showing confirmation dialog");

            string confirmationMessage = $"You are about to delete the asset '{target.name}' ({target.GetType().Name}).\n\nThis action cannot be undone. Continue?";

            bool confirmed = EditorUtility.DisplayDialog(
                "Confirm Asset Deletion",
                confirmationMessage,
                "Delete Asset",
                "Cancel"
            );

            if (!confirmed)
            {
                McpLogger.Log($"[ObjectDelete] User cancelled asset deletion for '{target.name}'");
                yield return Response.Success($"Asset deletion cancelled by user. Object '{target.name}' was not deleted.", new { cancelled = true, target_name = target.name });
                yield break;
            }

            McpLogger.Log($"[ObjectDelete] User confirmed asset deletion for '{target.name}'");

            // ?????????
            var deleteResult = DeleteSingleObject(target);
            yield return deleteResult;
        }

        /// <summary>
        /// ??????????
        /// </summary>
        private object HandleUnconfirmedDeleteAction(StateTreeContext ctx)
        {
            // ?????????????(30?)
            return ctx.AsyncReturn(HandleUnconfirmedDeleteActionAsync(ctx), 30f);
        }

        /// <summary>
        /// ?????????????UnityEngine.Object
        /// </summary>
        private UnityEngine.Object ExtractTargetFromContext(StateTreeContext context)
        {
            // ????ObjectReferences??(???????)
            if (context.TryGetObjectReference("_resolved_targets", out object targetsObj))
            {
                if (targetsObj is UnityEngine.Object singleObject)
                {
                    return singleObject;
                }
                else if (targetsObj is UnityEngine.Object[] objectArray && objectArray.Length > 0)
                {
                    return objectArray[0]; // ?????
                }
                else if (targetsObj is System.Collections.IList list && list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        if (item is UnityEngine.Object obj)
                            return obj; // ????????UnityEngine.Object
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// ???????????
        /// </summary>
        private bool IsAssetDeletion(StateTreeContext context)
        {
            // ??path???????????
            if (context.TryGetValue("path", out object pathObj) && pathObj != null)
            {
                string path = pathObj.ToString();
                // ?????Assets/??,????????
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// ??????????
        /// </summary>
        private object CheckPrefabRedirection(GameObject target)
        {
            if (target == null)
                return null;

            // ??????????,??????????,????manage_asset??
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // ???????,??????
                return null;
            }

            return null; // ??????
        }

        /// <summary>
        /// ????UnityEngine.Object
        /// </summary>
        private object DeleteSingleObject(UnityEngine.Object targetObject)
        {
            if (targetObject == null)
            {
                return Response.Error("Target Object is null.");
            }

            string objectName = targetObject.name;
            int objectId = targetObject.GetInstanceID();
            string objectType = targetObject.GetType().Name;

            try
            {
                // ?????????
                string assetPath = AssetDatabase.GetAssetPath(targetObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // ??????
                    bool success = AssetDatabase.DeleteAsset(assetPath);
                    if (success)
                    {
                        var deletedObject = new { name = objectName, instanceID = objectId, type = objectType, assetPath = assetPath };
                        return Response.Success($"{objectType} asset '{objectName}' deleted successfully.", deletedObject);
                    }
                    else
                    {
                        return Response.Error($"Failed to delete {objectType} asset '{objectName}' at path: {assetPath}");
                    }
                }
                else
                {
                    // ??????
                    Undo.DestroyObjectImmediate(targetObject);
                    var deletedObject = new { name = objectName, instanceID = objectId, type = objectType };
                    return Response.Success($"{objectType} '{objectName}' deleted successfully.", deletedObject);
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to delete {objectType} '{objectName}': {e.Message}");
            }
        }



    }
}
