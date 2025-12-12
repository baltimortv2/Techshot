using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.Compilation;
using UnityEngine;
using UniMcp.Models;

namespace UniMcp.Tools
{
    /// <summary>
    /// Handles Unity Package Manager operations including adding, removing, listing, and searching packages.
    /// ?????: manage_package
    /// </summary>
    [ToolName("manage_package", "System")]
    public class Package : StateMethodBase
    {
        // Results storage for async operations
        private object operationResult;

        // ???????????????
        private static Dictionary<string, PackageOperationInfo> _pendingOperations = new Dictionary<string, PackageOperationInfo>();
        private static bool _isCompilationListenerRegistered = false;

        // ?????
        private class PackageOperationInfo
        {
            public string OperationType { get; set; }
            public string PackageName { get; set; }
            public DateTime StartTime { get; set; }
            public int TimeoutSeconds { get; set; }
            public string Status { get; set; } = "pending";
        }

        static Package()
        {
            RegisterCompilationEvents();
        }

        /// <summary>
        /// ????????
        /// </summary>
        private static void RegisterCompilationEvents()
        {
            if (!_isCompilationListenerRegistered)
            {
                CompilationPipeline.compilationFinished += OnCompilationFinished;
                _isCompilationListenerRegistered = true;
            }
        }

        /// <summary>
        /// ????????
        /// </summary>
        private static void OnCompilationFinished(object obj)
        {
            McpLogger.Log("[ManagePackage] ????,?????????...");

            // ?????????
            var completedOperations = new List<string>();
            foreach (var kvp in _pendingOperations)
            {
                var operationInfo = kvp.Value;
                var elapsed = (DateTime.Now - operationInfo.StartTime).TotalSeconds;

                if (elapsed > operationInfo.TimeoutSeconds)
                {
                    McpLogger.LogWarning($"[ManagePackage] ?????: {operationInfo.PackageName} ({operationInfo.OperationType})");
                    completedOperations.Add(kvp.Key);
                }
                else
                {
                    // ?????????
                    CheckPackageOperationStatus(kvp.Key, operationInfo);
                }
            }

            // ???????
            foreach (var key in completedOperations)
            {
                _pendingOperations.Remove(key);
            }
        }

        /// <summary>
        /// ???????
        /// </summary>
        private static void CheckPackageOperationStatus(string operationId, PackageOperationInfo operationInfo)
        {
            // ????????????????
            // ??Unity Package Manager???????,????????????
            McpLogger.Log($"[ManagePackage] ???????: {operationInfo.PackageName} ({operationInfo.OperationType})");
        }

        /// <summary>
        /// ?????
        /// </summary>
        private static string RegisterPackageOperation(string operationType, string packageName, int timeoutSeconds)
        {
            string operationId = $"{operationType}_{packageName}_{DateTime.Now.Ticks}";
            _pendingOperations[operationId] = new PackageOperationInfo
            {
                OperationType = operationType,
                PackageName = packageName,
                StartTime = DateTime.Now,
                TimeoutSeconds = timeoutSeconds
            };

            McpLogger.Log($"[ManagePackage] ?????: {operationId}");
            return operationId;
        }

        /// <summary>
        /// ?????????
        /// </summary>
        private object CheckPendingOperationsStatus()
        {
            var operations = _pendingOperations.Values.Select(op => new
            {
                operation_type = op.OperationType,
                package_name = op.PackageName,
                start_time = op.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                elapsed_seconds = (DateTime.Now - op.StartTime).TotalSeconds,
                timeout_seconds = op.TimeoutSeconds,
                status = op.Status
            }).ToArray();

            return Response.Success(
                $"??? {operations.Length} ????????",
                new
                {
                    operation = "status",
                    pending_operations_count = operations.Length,
                    operations = operations
                }
            );
        }

        /// <summary>
        /// ??????????????
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                // ????
                new MethodStr("action", "????", false)
                    .SetEnumValues("add", "remove", "list", "search", "refresh", "resolve", "status", "restore_auto_refresh")
                    .AddExamples("add", "list"),
                
                // ?????
                new MethodStr("source", "?????")
                    .SetEnumValues("registry", "github", "disk")
                    .AddExamples("registry", "github"),
                
                // ???
                new MethodStr("package_name", "???")
                    .AddExamples("com.unity.textmeshpro", "com.unity.cinemachine"),
                
                // ??????
                new MethodStr("package_identifier", "??????")
                    .AddExamples("com.unity.textmeshpro@3.0.6", "com.unity.cinemachine@2.8.9"),
                
                // ???
                new MethodStr("version", "???")
                    .AddExamples("3.0.6", "2.8.9"),
                
                // GitHub??URL
                new MethodStr("repository_url", "GitHub??URL")
                    .AddExamples("https://github.com/Unity-Technologies/UnityCsReference.git", "https://github.com/user/repo.git"),
                
                // GitHub??
                new MethodStr("branch", "GitHub???")
                    .AddExamples("main", "develop")
                    .SetDefault("main"),
                
                // ???
                new MethodStr("path", "???")
                    .AddExamples("Packages/MyPackage", "D:/LocalPackages/MyPackage"),
                
                // ?????
                new MethodStr("search_keywords", "?????")
                    .AddExamples("unity", "cinemachine")
                    .SetDefault(""),
                
                // ??????
                new MethodBool("include_dependencies", "??????")
                    .SetDefault(false),
                
                // ?????
                new MethodStr("scope", "?????")
                    .AddExamples("com.unity", "com.mycompany")
                    .SetDefault(""),
                
                // ????
                new MethodInt("timeout", "????(?)")
                    .SetRange(10, 300)
                    .AddExample("60")
                    .SetDefault(60),
                
                // ??????
                new MethodBool("disable_auto_refresh", "?????????")
                    .SetDefault(false)
            };
        }

        /// <summary>
        /// ?????
        /// </summary>
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Node("add", "source")
                            .Leaf("registry", HandleAddFromRegistry)
                            .Leaf("github", HandleAddFromGitHub)
                            .Leaf("disk", HandleAddFromDisk)
                            .DefaultLeaf(HandleAddFromRegistry)
                        .Up()
                    .Leaf("remove", HandleRemovePackage)
                    .Leaf("list", HandleListPackages)
                    .Leaf("search", HandleSearchPackages)
                    .Leaf("refresh", HandleRefreshPackages)
                    .Leaf("resolve", HandleResolvePackages)
                    .Leaf("status", HandleCheckOperationStatus)
                    .Leaf("restore_auto_refresh", HandleRestoreAutoRefresh)
                .Build();
        }

        // --- ????????? ---

        /// <summary>
        /// ???Registry?????
        /// </summary>
        private object HandleAddFromRegistry(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing add package from registry");
            // ????????????(180?)
            return ctx.AsyncReturn(ExecuteAddFromRegistryAsync(ctx), 180f);
        }

        /// <summary>
        /// ???GitHub?????
        /// </summary>
        private object HandleAddFromGitHub(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing add package from GitHub");
            // ????????????(180?)
            return ctx.AsyncReturn(ExecuteAddFromGitHubAsync(ctx), 180f);
        }

        /// <summary>
        /// ??????????
        /// </summary>
        private object HandleAddFromDisk(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing add package from disk");
            // ????????????(120?)
            return ctx.AsyncReturn(ExecuteAddFromDiskAsync(ctx), 120f);
        }

        /// <summary>
        /// ???????
        /// </summary>
        private object HandleRemovePackage(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing remove package operation");
            // ????????????(120?)
            return ctx.AsyncReturn(ExecuteRemovePackageAsync(ctx), 120f);
        }

        /// <summary>
        /// ???????
        /// </summary>
        private object HandleListPackages(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing list packages operation");
            // ????????????(60?)
            return ctx.AsyncReturn(ExecuteListPackagesAsync(ctx), 60f);
        }

        /// <summary>
        /// ???????
        /// </summary>
        private object HandleSearchPackages(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing search packages operation");
            // ????????????(120?)
            return ctx.AsyncReturn(ExecuteSearchPackagesAsync(ctx), 120f);
        }

        /// <summary>
        /// ???????
        /// </summary>
        private object HandleRefreshPackages(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing refresh packages operation");
            // ????????????(120?)
            return ctx.AsyncReturn(ExecuteRefreshPackagesAsync(ctx), 120f);
        }

        /// <summary>
        /// ?????????
        /// </summary>
        private object HandleResolvePackages(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Executing resolve packages operation");
            // ????????????(120?)
            return ctx.AsyncReturn(ExecuteResolvePackagesAsync(ctx), 120f);
        }

        /// <summary>
        /// ????????
        /// </summary>
        private object HandleCheckOperationStatus(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Checking operation status");
            return CheckPendingOperationsStatus();
        }

        /// <summary>
        /// ????????
        /// </summary>
        private object HandleRestoreAutoRefresh(StateTreeContext ctx)
        {
            McpLogger.Log("[ManagePackage] Restoring auto refresh settings");
            try
            {
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();

                McpLogger.Log("[ManagePackage] ??????????");

                return Response.Success(
                    "????????????",
                    new
                    {
                        operation = "restore_auto_refresh",
                        auto_refresh_enabled = true,
                        assembly_reload_unlocked = true,
                        message = "Unity???????????????"
                    }
                );
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] ????????: {e.Message}");
                return Response.Error($"Failed to restore auto refresh: {e.Message}");
            }
        }

        // --- ???????? ---

        /// <summary>
        /// ?Registry????????
        /// </summary>
        private IEnumerator ExecuteAddFromRegistryAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;
            AddRequest request = null;
            bool failed = false;

            // ??????????
            string packageName = ctx.JsonData["package_name"]?.Value;
            int timeout = ctx.JsonData["timeout"].AsIntDefault(60);
            bool disableAutoRefresh = ctx.JsonData["disable_auto_refresh"].AsBoolDefault(false);

            // ???????
            bool wasAutoRefreshDisabled = false;
            if (disableAutoRefresh)
            {
                McpLogger.Log($"[ManagePackage] ?????????: {packageName}");
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                wasAutoRefreshDisabled = true;
            }

            try
            {
                request = AddFromRegistry(ctx.JsonData);
                if (request == null)
                {
                    operationResult = Response.Error("Failed to create registry package add request");
                    failed = true;
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] Failed to add package from registry: {e.Message}");
                operationResult = Response.Error($"Failed to add package from registry: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                // ??????,??????
                if (wasAutoRefreshDisabled)
                {
                    AssetDatabase.AllowAutoRefresh();
                    EditorApplication.UnlockReloadAssemblies();
                    McpLogger.Log($"[ManagePackage] ???????????");
                }
                yield return operationResult;
                yield break;
            }

            // ???????? - ??AddRequest???????????
            McpLogger.Log($"[ManagePackage] ?????????: {packageName}");
            yield return WaitForRequestOnlyAsync(request, "add", timeout, packageName, wasAutoRefreshDisabled);
            yield return operationResult;
        }

        /// <summary>
        /// ?GitHub????????
        /// </summary>
        private IEnumerator ExecuteAddFromGitHubAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;
            AddRequest request = null;
            bool failed = false;

            try
            {
                request = AddFromGitHub(ctx.JsonData);
                if (request == null)
                {
                    operationResult = Response.Error("Failed to create GitHub package add request");
                    failed = true;
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] Failed to add package from GitHub: {e.Message}");
                operationResult = Response.Error($"Failed to add package from GitHub: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "add", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// ???????????
        /// </summary>
        private IEnumerator ExecuteAddFromDiskAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;
            AddRequest request = null;
            bool failed = false;

            try
            {
                request = AddFromDisk(ctx.JsonData);
                if (request == null)
                {
                    operationResult = Response.Error("Failed to create disk package add request");
                    failed = true;
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] Failed to add package from disk: {e.Message}");
                operationResult = Response.Error($"Failed to add package from disk: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "add", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// ??????????
        /// </summary>
        private IEnumerator ExecuteRemovePackageAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;
            RemoveRequest request = null;
            bool failed = false;

            // ??????????  
            int timeout = ctx.JsonData["timeout"].AsIntDefault(60);
            bool disableAutoRefresh = ctx.JsonData["disable_auto_refresh"].AsBoolDefault(false);

            string packageName = ctx.JsonData["package_name"]?.Value ?? ctx.JsonData["package_identifier"]?.Value;

            // ???????
            bool wasAutoRefreshDisabled = false;
            if (disableAutoRefresh)
            {
                McpLogger.Log($"[ManagePackage] ?????????: {packageName}");
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                wasAutoRefreshDisabled = true;
            }

            try
            {
                if (string.IsNullOrEmpty(packageName))
                {
                    operationResult = Response.Error("package_name or package_identifier parameter is required");
                    failed = true;
                }
                else
                {
                    McpLogger.Log($"[ManagePackage] ???: {packageName}");
                    request = Client.Remove(packageName);
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] ?????: {e.Message}");
                operationResult = Response.Error($"Failed to remove package: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                // ??????,??????
                if (wasAutoRefreshDisabled)
                {
                    AssetDatabase.AllowAutoRefresh();
                    EditorApplication.UnlockReloadAssemblies();
                    McpLogger.Log($"[ManagePackage] ???????????");
                }
                yield return operationResult;
                yield break;
            }

            // ???????? - ??RemoveRequest???????????
            McpLogger.Log($"[ManagePackage] ?????????: {packageName}");
            yield return WaitForRequestOnlyAsync(request, "remove", timeout, packageName, wasAutoRefreshDisabled);
            yield return operationResult;
        }

        /// <summary>
        /// ??????????
        /// </summary>
        private IEnumerator ExecuteListPackagesAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;
            ListRequest request = null;
            bool failed = false;

            try
            {
                bool includeIndirect = ctx.JsonData["include_dependencies"].AsBoolDefault(false);
                McpLogger.Log($"[ManagePackage] ??? (??????: {includeIndirect})");

                request = Client.List(includeIndirect);
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] ?????: {e.Message}");
                operationResult = Response.Error($"Failed to list packages: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "list", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// ??????????
        /// </summary>
        private IEnumerator ExecuteSearchPackagesAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;
            SearchRequest request = null;
            bool failed = false;

            try
            {
                string keywords = ctx.JsonData["search_keywords"]?.Value;

                if (string.IsNullOrEmpty(keywords))
                {
                    // ???????,?????
                    McpLogger.Log("[ManagePackage] ?????");
                    request = Client.SearchAll();
                }
                else
                {
                    // ?????
                    McpLogger.Log($"[ManagePackage] ???: {keywords}");
                    request = Client.Search(keywords);
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] ?????: {e.Message}");
                operationResult = Response.Error($"Failed to search packages: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "search", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// ??????????
        /// </summary>
        private IEnumerator ExecuteRefreshPackagesAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;

            try
            {
                McpLogger.Log("[ManagePackage] ?????");
                Client.Resolve();

                operationResult = Response.Success("Package list refresh operation started", new { operation = "refresh" });
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] ?????: {e.Message}");
                operationResult = Response.Error($"Failed to refresh packages: {e.Message}");
            }

            yield return operationResult;
        }

        /// <summary>
        /// ????????????
        /// </summary>
        private IEnumerator ExecuteResolvePackagesAsync(StateTreeContext ctx)
        {
            // ?????????,???try-catch???yield return
            operationResult = null;

            try
            {
                McpLogger.Log("[ManagePackage] ?????");
                Client.Resolve();

                operationResult = Response.Success("Package dependency resolution operation started", new { operation = "resolve" });
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] ???????: {e.Message}");
                operationResult = Response.Error($"Failed to resolve package dependencies: {e.Message}");
            }

            yield return operationResult;
        }

        /// <summary>
        /// ?????????
        /// </summary>
        private IEnumerator MonitorOperationAsync(Request request, string operationType, JsonClass args)
        {
            int timeout = args["timeout"].AsIntDefault(60); // ???????60?
            float elapsedTime = 0f;

            McpLogger.Log($"[ManagePackage] ???? {operationType} ??,????: {timeout}?");

            while (!request.IsCompleted && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;

                if (Mathf.FloorToInt(elapsedTime) != Mathf.FloorToInt(elapsedTime - 0.1f))
                {
                    McpLogger.Log($"[ManagePackage] {operationType} ?????... ???: {elapsedTime:F1}s");
                }
            }

            if (elapsedTime >= timeout)
            {
                operationResult = Response.Error($"Operation timeout ({timeout} seconds)");
            }
            else if (request.Status == StatusCode.Success)
            {
                operationResult = ProcessSuccessfulOperationResult(request, operationType);
            }
            else if (request.Status == StatusCode.Failure)
            {
                operationResult = Response.Error($"Operation failed: {request.Error?.message ?? "Unknown error"}");
            }
            else
            {
                operationResult = Response.Error($"Unknown operation status: {request.Status}");
            }

            McpLogger.Log($"[ManagePackage] {operationType} ????");
        }

        /// <summary>
        /// ????????????(????????)
        /// </summary>
        private IEnumerator WaitForRequestOnlyAsync(Request request, string operationType, int timeout, string packageName, bool wasAutoRefreshDisabled = false)
        {
            float elapsedTime = 0f;

            McpLogger.Log($"[ManagePackage] ???? {operationType} ????: {packageName},????: {timeout}?");

            while (!request.IsCompleted && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;

                if (Mathf.FloorToInt(elapsedTime) != Mathf.FloorToInt(elapsedTime - 0.1f))
                {
                    McpLogger.Log($"[ManagePackage] ?? {operationType} ?????... ???: {elapsedTime:F1}s");
                }
            }

            // ??????
            if (elapsedTime >= timeout)
            {
                operationResult = Response.Error($"Request timeout ({timeout} seconds)");
                LogWarning($"[ManagePackage] {operationType} ????: {packageName}");
            }
            else if (request.Status == StatusCode.Success)
            {
                // ??????,??????,????????
                McpLogger.Log($"[ManagePackage] {operationType} ??????,??????: {packageName}");

                // ???????????????
                var result = ProcessSuccessfulOperationResult(request, operationType);
                if (wasAutoRefreshDisabled && result is object resultObj)
                {
                    // ??????,????????
                    var enhancedResult = AddRefreshControlInfo(resultObj, wasAutoRefreshDisabled, packageName);
                    operationResult = enhancedResult;
                }
                else
                {
                    operationResult = result;
                }
            }
            else if (request.Status == StatusCode.Failure)
            {
                operationResult = Response.Error($"Request failed: {request.Error?.message ?? "Unknown error"}");
                LogError($"[ManagePackage] {operationType} ????: {packageName}, ??: {request.Error?.message}");
            }
            else
            {
                operationResult = Response.Error($"Unknown request status: {request.Status}");
                LogWarning($"[ManagePackage] {operationType} ??????: {packageName}, ??: {request.Status}");
            }

            // ??????
            if (wasAutoRefreshDisabled)
            {
                McpLogger.Log($"[ManagePackage] ????,??????????????????");
                McpLogger.Log($"[ManagePackage] ??AssetDatabase.AllowAutoRefresh()?EditorApplication.UnlockReloadAssemblies()???????");
                McpLogger.Log($"[ManagePackage] ???AssetDatabase.Refresh()?????");
            }

            McpLogger.Log($"[ManagePackage] {operationType} ??????: {packageName}");
        }

        /// <summary>
        /// ?????????????
        /// </summary>
        private object AddRefreshControlInfo(object originalResult, bool wasAutoRefreshDisabled, string packageName)
        {
            try
            {
                // ????????
                var resultType = originalResult.GetType();
                var properties = resultType.GetProperties();
                var messageProperty = properties.FirstOrDefault(p => p.Name.Equals("message", StringComparison.OrdinalIgnoreCase));
                var dataProperty = properties.FirstOrDefault(p => p.Name.Equals("data", StringComparison.OrdinalIgnoreCase));
                var successProperty = properties.FirstOrDefault(p => p.Name.Equals("success", StringComparison.OrdinalIgnoreCase));

                string message = messageProperty?.GetValue(originalResult)?.ToString() ?? "Operation completed";
                object data = dataProperty?.GetValue(originalResult);
                bool success = (bool)(successProperty?.GetValue(originalResult) ?? true);

                // ?????????
                var enhancedData = new Dictionary<string, object>();

                // ??????
                if (data != null)
                {
                    var dataType = data.GetType();
                    foreach (var prop in dataType.GetProperties())
                    {
                        enhancedData[prop.Name] = prop.GetValue(data);
                    }
                }

                // ????????
                enhancedData["auto_refresh_disabled"] = wasAutoRefreshDisabled;
                enhancedData["refresh_control"] = new
                {
                    message = "???????????",
                    instructions = new[]
                    {
                        "???????,???: AssetDatabase.AllowAutoRefresh() ? EditorApplication.UnlockReloadAssemblies()",
                        "?????,???: AssetDatabase.Refresh()",
                        "??????????????????"
                    },
                    current_state = "assemblies_locked"
                };

                return Response.Success(
                    $"{message} (??????????)",
                    enhancedData
                );
            }
            catch (Exception ex)
            {
                LogWarning($"[ManagePackage] ????????: {ex.Message}");
                return originalResult;
            }
        }




        // --- ?????????? ---

        /// <summary>
        /// ?Unity Registry???
        /// </summary>
        private AddRequest AddFromRegistry(JsonClass args)
        {
            string packageName = args["package_name"]?.Value;
            if (string.IsNullOrEmpty(packageName))
            {
                throw new ArgumentException("package_name ??????(registry?)");
            }

            string version = args["version"]?.Value;
            string packageIdentifier = packageName;

            if (!string.IsNullOrEmpty(version))
            {
                packageIdentifier = $"{packageName}@{version}";
            }

            McpLogger.Log($"[ManagePackage] ?Registry???: {packageIdentifier}");
            return Client.Add(packageIdentifier);
        }

        /// <summary>
        /// ?GitHub???
        /// </summary>
        private AddRequest AddFromGitHub(JsonClass args)
        {
            string repositoryUrl = args["repository_url"]?.Value;
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                throw new ArgumentException("repository_url ??????(github?)");
            }

            string branch = args["branch"]?.Value;
            string path = args["path"]?.Value;

            // ??.git??
            if (repositoryUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                repositoryUrl = repositoryUrl.Substring(0, repositoryUrl.Length - 4);
            }

            // ????
            if (!string.IsNullOrEmpty(branch))
            {
                repositoryUrl += "#" + branch;
            }

            // ????
            if (!string.IsNullOrEmpty(path))
            {
                if (!string.IsNullOrEmpty(branch))
                {
                    repositoryUrl += "/" + path;
                }
                else
                {
                    repositoryUrl += "#" + path;
                }
            }

            McpLogger.Log($"[ManagePackage] ?GitHub???: {repositoryUrl}");
            return Client.Add(repositoryUrl);
        }

        /// <summary>
        /// ??????
        /// </summary>
        private AddRequest AddFromDisk(JsonClass args)
        {
            string path = args["path"]?.Value;
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path ??????(disk?)");
            }

            string packageUrl = $"file:{path}";
            McpLogger.Log($"[ManagePackage] ??????: {packageUrl}");
            return Client.Add(packageUrl);
        }

        // --- ???????? ---

        /// <summary>
        /// ?????????(????)
        /// </summary>
        private object ProcessSuccessfulOperationResult(Request request, string operationType)
        {
            switch (operationType)
            {
                case "add":
                    return ProcessAddResult(request as AddRequest);
                case "remove":
                    return ProcessRemoveResult(request as RemoveRequest);
                case "list":
                    return ProcessListResult(request as ListRequest);
                case "search":
                    return ProcessSearchResult(request as SearchRequest);
                default:
                    return Response.Success($"{operationType} operation completed");
            }
        }

        /// <summary>
        /// ????????
        /// </summary>
        private object ProcessAddResult(AddRequest request)
        {
            var result = request.Result;
            if (result != null)
            {
                return Response.Success(
                    $"?????: {result.displayName} ({result.name}) ?? {result.version}",
                    new
                    {
                        operation = "add",
                        package_info = new
                        {
                            name = result.name,
                            display_name = result.displayName,
                            version = result.version,
                            description = result.description,
                            // status = result.status.ToString(), // Removed deprecated API
                            source = result.source.ToString()
                        }
                    }
                );
            }

            return Response.Success("Package add operation completed, but no package information returned");
        }

        /// <summary>
        /// ????????
        /// </summary>
        private object ProcessRemoveResult(RemoveRequest request)
        {
            return Response.Success(
                "???????",
                new
                {
                    operation = "remove"
                }
            );
        }

        /// <summary>
        /// ????????
        /// </summary>
        private object ProcessListResult(ListRequest request)
        {
            var packages = request.Result;
            if (packages != null)
            {
                var packageList = packages.Select(pkg => new
                {
                    name = pkg.name,
                    display_name = pkg.displayName,
                    version = pkg.version,
                    description = pkg.description,
                    // status = pkg.status.ToString(), // Removed deprecated API
                    source = pkg.source.ToString(),
                    package_id = pkg.packageId,
                    resolved_path = pkg.resolvedPath
                }).ToArray();

                return Response.Success(
                    $"?? {packageList.Length} ??",
                    new
                    {
                        operation = "list",
                        package_count = packageList.Length,
                        packages = packageList
                    }
                );
            }

            return Response.Success("Package list operation completed, but no package information returned");
        }

        /// <summary>
        /// ????????
        /// </summary>
        private object ProcessSearchResult(SearchRequest request)
        {
            var searchResult = request.Result;
            if (searchResult != null)
            {
                var packages = searchResult.Select(pkg => new
                {
                    name = pkg.name,
                    display_name = pkg.displayName,
                    version = pkg.version,
                    description = pkg.description,
                    source = pkg.source.ToString(),
                    package_id = pkg.packageId
                }).ToArray();

                return Response.Success(
                    $"??? {packages.Length} ??",
                    new
                    {
                        operation = "search",
                        package_count = packages.Length,
                        packages = packages
                    }
                );
            }

            return Response.Success("Package search operation completed, but no search results returned");
        }
    }
}
