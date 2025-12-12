using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
// Migrated from Newtonsoft.Json to SimpleJson
using UnityEditor;
using UnityEngine;
using UniMcp.Models;

namespace UniMcp.Tools
{
    /// <summary>
    /// Handles Python script execution including validation and running Python code.
    /// ?????: python_runner
    /// </summary>
    [ToolName("python_runner", "Dev Tools")]
    public class PythonRunner : StateMethodBase
    {
        // Python execution tracking
        private class PythonOperation
        {
            public string PythonCode { get; set; }
            public string ScriptName { get; set; }
            public List<PythonResult> Results { get; set; } = new List<PythonResult>();
        }

        private class PythonResult
        {
            public string Operation { get; set; }
            public bool Success { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
            public double Duration { get; set; }
            public int ExitCode { get; set; }
        }

        private object validationResult;
        private object executionResult;

        /// <summary>
        /// ??????????????
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                // ????
                new MethodStr("action", "????", false)
                    .SetEnumValues("execute", "validate", "install_package", "create"),
                
                // Python??
                new MethodStr("code", "Python??????")
                    .AddExamples("print('Hello World')", "import os; print(os.getcwd())"),
                
                // ????
                new MethodStr("description", "??????")
                    .AddExamples("????", "????"),
                
                // ????
                new MethodStr("script_path", "Python??????")
                    .AddExamples("Assets/Scripts/test.py", "D:/Scripts/process.py"),
                
                // ????
                new MethodStr("script_name", "????")
                    .AddExamples("script.py", "test.py"),
                
                // Python?????
                new MethodStr("python_path", "Python?????")
                    .AddExamples("python", "python3"),
                
                // ????
                new MethodStr("working_directory", "????")
                    .AddExamples("Assets/Scripts", "D:/Projects"),
                
                // ????
                new MethodInt("timeout", "????(?)")
                    .SetRange(1, 3600)
                    .AddExample("300"),
                
                // ????
                new MethodBool("cleanup", "?????????"),
                
                // ?????
                new MethodStr("packages", "????Python?")
                    .AddExamples("numpy,pandas", "requests"),
                
                // ????
                new MethodStr("requirements_file", "requirements.txt????")
                    .AddExample("Assets/Scripts/requirements.txt"),
                
                // ????
                new MethodStr("virtual_env", "??????")
                    .AddExample("D:/venv/myproject"),
                
                // ????
                new MethodBool("refresh_project", "?????Unity??")
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
                    .Leaf("execute", HandleExecutePython)
                    .Leaf("validate", HandleValidatePython)
                    .Leaf("install_package", HandleInstallPackage)
                    .Leaf("create", HandleCreateScript)
                    .DefaultLeaf(HandleExecutePython)
                .Build();
        }

        // --- Python???????? ---

        /// <summary>
        /// ????Python??
        /// </summary>
        private object HandleExecutePython(StateTreeContext ctx)
        {
            McpLogger.Log("[PythonRunner] Executing Python code");
            // ?Python??????????(90?)
            return ctx.AsyncReturn(ExecutePythonCoroutine(ctx.JsonData), 90f);
        }

        /// <summary>
        /// ????Python????
        /// </summary>
        private object HandleValidatePython(StateTreeContext ctx)
        {
            McpLogger.Log("[PythonRunner] Validating Python code or script");
            // ?Python??????????(30?)
            return ctx.AsyncReturn(ValidatePythonCoroutine(ctx.JsonData), 30f);
        }

        /// <summary>
        /// ????Python???
        /// </summary>
        private object HandleInstallPackage(StateTreeContext ctx)
        {
            McpLogger.Log("[PythonRunner] Installing Python packages");
            // ?Python?????????(180?)
            return ctx.AsyncReturn(InstallPackageCoroutine(ctx.JsonData), 180f);
        }

        /// <summary>
        /// ????Python????
        /// </summary>
        private object HandleCreateScript(StateTreeContext ctx)
        {
            McpLogger.Log("[PythonRunner] Creating Python script");
            // ?Python??????????(30?)
            return ctx.AsyncReturn(CreateScriptCoroutine(ctx.JsonData), 30f);
        }

        // --- ?????? ---

        /// <summary>
        /// ????Python????
        /// </summary>
        private IEnumerator ExecutePythonCoroutine(JsonClass args)
        {
            string tempFilePath = null;
            bool isTemporaryFile = false;

            try
            {
                string pythonCode = args["code"]?.Value;
                string scriptPath = args["script_path"]?.Value;

                // ????:???? code ? script_path ??
                if (string.IsNullOrEmpty(pythonCode) && string.IsNullOrEmpty(scriptPath))
                {
                    yield return Response.Error("Either 'code' or 'script_path' parameter is required");
                    yield break;
                }

                // ???????????,???? script_path
                if (!string.IsNullOrEmpty(pythonCode) && !string.IsNullOrEmpty(scriptPath))
                {
                    McpLogger.Log("[PythonRunner] Both code and script_path provided, using script_path");
                    pythonCode = null; // ??code,????script_path
                }

                string scriptName = args["script_name"]?.Value;
                if (string.IsNullOrEmpty(scriptName)) scriptName = "script.py";
                string pythonPath = args["python_path"]?.Value;
                if (string.IsNullOrEmpty(pythonPath)) pythonPath = "python";
                string workingDirectory = args["working_directory"]?.Value;
                if (string.IsNullOrEmpty(workingDirectory)) workingDirectory = System.Environment.CurrentDirectory;
                int timeout = args["timeout"].AsIntDefault(300);
                bool cleanup = args["cleanup"].AsBoolDefault(true);
                bool refreshProject = args["refresh_project"].AsBoolDefault(false);
                string virtualEnv = args["virtual_env"]?.Value;

                if (!string.IsNullOrEmpty(scriptPath))
                {
                    // ??1: ?????????
                    if (!File.Exists(scriptPath))
                    {
                        yield return Response.Error($"Script file not found: {scriptPath}");
                        yield break;
                    }

                    McpLogger.Log($"[PythonRunner] Executing existing Python script: {scriptPath}");

                    // ????????
                    yield return ExecutePythonScript(scriptPath, pythonPath, workingDirectory, timeout, virtualEnv, (result) =>
                    {
                        if (result != null)
                        {
                            executionResult = Response.Success("Python script execution completed", new
                            {
                                operation = "execute",
                                script_path = scriptPath,
                                success = result.Success,
                                output = result.Output,
                                error = result.Error,
                                exit_code = result.ExitCode,
                                duration = result.Duration,
                                project_refreshed = refreshProject
                            });

                            // ?????????????
                            if (refreshProject && result.Success)
                            {
                                EditorApplication.delayCall += () =>
                                {
                                    McpLogger.Log("[PythonRunner] Refreshing Unity project...");
                                    AssetDatabase.Refresh();
                                };
                            }
                        }
                        else
                        {
                            executionResult = Response.Error("Python script execution failed - no result returned");
                        }
                    });

                    yield return executionResult;
                }
                else
                {
                    // ??2: ?code?????????
                    McpLogger.Log($"[PythonRunner] Executing Python code as script: {scriptName}");

                    // ??????Python
                    isTemporaryFile = true;
                    yield return ExecutePythonCoroutineInternal(pythonCode, scriptName, pythonPath, workingDirectory, timeout, virtualEnv, refreshProject,
                        (tFilePath) => { tempFilePath = tFilePath; });
                    yield return executionResult;
                }
            }
            finally
            {
                // ???????????
                if (isTemporaryFile && !string.IsNullOrEmpty(tempFilePath))
                {
                    EditorApplication.delayCall += () => CleanupTempFiles(tempFilePath);
                }
            }
        }

        /// <summary>
        /// ??Python????
        /// </summary>
        private IEnumerator ValidatePythonCoroutine(JsonClass args)
        {
            string tempFilePath = null;
            bool isTemporaryFile = false;

            try
            {
                string pythonCode = args["code"]?.Value;
                string scriptPath = args["script_path"]?.Value;

                // ????:???? code ? script_path ??
                if (string.IsNullOrEmpty(pythonCode) && string.IsNullOrEmpty(scriptPath))
                {
                    yield return Response.Error("Either 'code' or 'script_path' parameter is required");
                    yield break;
                }

                // ???????????,???? script_path
                if (!string.IsNullOrEmpty(pythonCode) && !string.IsNullOrEmpty(scriptPath))
                {
                    McpLogger.Log("[PythonRunner] Both code and script_path provided, using script_path for validation");
                    pythonCode = null; // ??code,????script_path
                }

                string scriptName = args["script_name"]?.Value;
                if (string.IsNullOrEmpty(scriptName)) scriptName = "script.py";
                string pythonPath = args["python_path"]?.Value;
                if (string.IsNullOrEmpty(pythonPath)) pythonPath = "python";
                string virtualEnv = args["virtual_env"]?.Value;

                string targetScriptPath;

                if (!string.IsNullOrEmpty(scriptPath))
                {
                    // ??1: ?????????
                    if (!File.Exists(scriptPath))
                    {
                        yield return Response.Error($"Script file not found: {scriptPath}");
                        yield break;
                    }

                    McpLogger.Log($"[PythonRunner] Validating existing Python script: {scriptPath}");
                    targetScriptPath = scriptPath;
                }
                else
                {
                    // ??2: ?code?????????
                    McpLogger.Log($"[PythonRunner] Validating Python code as script: {scriptName}");

                    // ??????
                    var tempDir = Path.Combine(Application.temporaryCachePath, "PythonRunner");
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    var timestamp = DateTime.Now.Ticks;
                    var randomId = UnityEngine.Random.Range(1000, 9999);
                    var tempFileName = $"{Path.GetFileNameWithoutExtension(scriptName)}_{timestamp}_{randomId}.py";
                    tempFilePath = Path.Combine(tempDir, tempFileName);
                    targetScriptPath = tempFilePath;
                    isTemporaryFile = true;

                    File.WriteAllText(tempFilePath, pythonCode, Encoding.UTF8);
                }

                // ??Python??
                yield return ValidatePythonSyntax(targetScriptPath, pythonPath, virtualEnv, (success, output, error) =>
                {
                    if (success)
                    {
                        validationResult = Response.Success("Python script syntax is valid", new
                        {
                            operation = "validate",
                            script_path = !string.IsNullOrEmpty(scriptPath) ? scriptPath : scriptName,
                            python_path = pythonPath,
                            is_temporary_file = isTemporaryFile,
                            temp_file = isTemporaryFile ? tempFilePath : null
                        });
                    }
                    else
                    {
                        validationResult = Response.Error("Python script syntax validation failed", new
                        {
                            operation = "validate",
                            script_path = !string.IsNullOrEmpty(scriptPath) ? scriptPath : scriptName,
                            error = error,
                            output = output,
                            is_temporary_file = isTemporaryFile
                        });
                    }
                });

                yield return validationResult;
            }
            finally
            {
                // ???????????
                if (isTemporaryFile && !string.IsNullOrEmpty(tempFilePath))
                {
                    EditorApplication.delayCall += () => CleanupTempFiles(tempFilePath);
                }
            }
        }

        /// <summary>
        /// ??Python???
        /// </summary>
        private IEnumerator InstallPackageCoroutine(JsonClass args)
        {
            var packages = args["packages"];
            string requirementsFile = args["requirements_file"]?.Value;
            string pythonPath = args["python_path"]?.Value;
            if (string.IsNullOrEmpty(pythonPath)) pythonPath = "python";
            string virtualEnv = args["virtual_env"]?.Value;
            int timeout = args["timeout"].AsIntDefault(60);

            if (packages == null && string.IsNullOrEmpty(requirementsFile))
            {
                yield return Response.Error("Either 'packages' or 'requirements_file' parameter is required");
                yield break;
            }

            McpLogger.Log("[PythonRunner] Installing Python packages");

            object installResult = null;

            if (!string.IsNullOrEmpty(requirementsFile))
            {
                // ??requirements.txt???
                yield return InstallFromRequirements(requirementsFile, pythonPath, virtualEnv, timeout, (result) =>
                {
                    installResult = result;
                });
            }
            else
            {
                // ??????
                var packageList = new List<string>();
                if (packages.type == JsonNodeType.Array)
                {
                    var packagesArray = packages as JsonArray;
                    if (packagesArray != null)
                    {
                        packageList.AddRange(packagesArray.ToStringList());
                    }
                }
                else
                {
                    packageList.AddRange(packages.Value.Split(',').Select(p => p.Trim()));
                }

                yield return InstallPackages(packageList.ToArray(), pythonPath, virtualEnv, timeout, (result) =>
                {
                    installResult = result;
                });
            }

            yield return installResult;
        }

        /// <summary>
        /// ??Python????
        /// </summary>
        private IEnumerator CreateScriptCoroutine(JsonClass args)
        {
            object result = null;

            string pythonCode = args["code"]?.Value;
            string scriptPath = args["script_path"]?.Value;
            string scriptName = args["script_name"]?.Value;
            if (string.IsNullOrEmpty(scriptName)) scriptName = "script.py";

            // ????:???? code
            if (string.IsNullOrEmpty(pythonCode))
            {
                result = Response.Error("'code' parameter is required for create action");
                yield return result;
                yield break;
            }

            try
            {
                // ????????
                string targetPath;
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    // ????? script_path,???
                    targetPath = scriptPath;
                }
                else
                {
                    // ????? Python ??
                    string pythonDir = Path.Combine(System.Environment.CurrentDirectory, "Python");
                    if (!Directory.Exists(pythonDir))
                    {
                        Directory.CreateDirectory(pythonDir);
                        McpLogger.Log($"[PythonRunner] Created Python directory: {pythonDir}");
                    }
                    targetPath = Path.Combine(pythonDir, scriptName);
                }

                // ?? targetPath ???,??????????
                if (Directory.Exists(targetPath))
                {
                    targetPath = Path.Combine(targetPath, scriptName);
                }

                // ??????
                string directory = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    McpLogger.Log($"[PythonRunner] Created directory: {directory}");
                }

                McpLogger.Log($"[PythonRunner] Creating Python script: {targetPath}");

                // ??????
                File.WriteAllText(targetPath, pythonCode, Encoding.UTF8);

                result = Response.Success("Python script created successfully", new
                {
                    operation = "create",
                    script_path = targetPath,
                    script_name = Path.GetFileName(targetPath),
                    directory = directory,
                    file_size = new FileInfo(targetPath).Length
                });
            }
            catch (Exception ex)
            {
                LogError($"[PythonRunner] Failed to create script: {ex.Message}");
                result = Response.Error($"Failed to create Python script: {ex.Message}");
            }

            yield return result;
        }

        /// <summary>
        /// ??Python???????
        /// </summary>
        private IEnumerator ExecutePythonCoroutineInternal(string pythonCode, string scriptName, string pythonPath,
            string workingDirectory, int timeout, string virtualEnv, bool refreshProject = false, System.Action<string> onTempFileCreated = null)
        {
            executionResult = null;

            // ????Python??
            var tempDir = Path.Combine(Application.temporaryCachePath, "PythonRunner");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var timestamp = DateTime.Now.Ticks;
            var randomId = UnityEngine.Random.Range(1000, 9999);
            var tempFileName = $"{Path.GetFileNameWithoutExtension(scriptName)}_{timestamp}_{randomId}.py";
            var tempFilePath = Path.Combine(tempDir, tempFileName);

            // ?????????????
            bool fileCreated = false;
            try
            {
                // ??Base64?????????????,???????
                string encodingSolutionCode = @"#!/usr/bin/env python
# -*- coding: utf-8 -*-
import sys
import io
import base64
import json
import os
import builtins

# ?????????????
os.environ['PYTHONUNBUFFERED'] = '1'

# Unity MCP Python Runner ??????
class UnicodeOutput:
    def __init__(self, original_stream):
        self.original_stream = original_stream
        
    def write(self, text):
        if text and isinstance(text, str):
            try:
                # ???????ASCII??,??Base64??
                if any(ord(c) > 127 for c in text):
                    encoded_text = base64.b64encode(text.encode('utf-8')).decode('ascii')
                    self.original_stream.write(f'[UNITY_MCP_B64:{encoded_text}]')
                else:
                    self.original_stream.write(text)
                self.original_stream.flush()  # ????
            except:
                try:
                    self.original_stream.write(text.encode('ascii', errors='replace').decode('ascii'))
                    self.original_stream.flush()  # ????
                except:
                    pass
    
    def flush(self):
        try:
            self.original_stream.flush()
        except:
            pass
    
    def __getattr__(self, name):
        return getattr(self.original_stream, name)

# ??????
try:
    sys.stdout = UnicodeOutput(sys.stdout)
    sys.stderr = UnicodeOutput(sys.stderr)
except:
    pass

# ??print????????
original_print = builtins.print
def unity_print(*args, **kwargs):
    kwargs.setdefault('flush', True)  # ??????
    return original_print(*args, **kwargs)

builtins.print = unity_print

";
                string finalCode = encodingSolutionCode + pythonCode;
                File.WriteAllText(tempFilePath, finalCode, Encoding.UTF8);
                McpLogger.Log($"[PythonRunner] ??????: {tempFilePath}");

                onTempFileCreated?.Invoke(tempFilePath);
                fileCreated = true;
            }
            catch (Exception e)
            {
                LogError($"[PythonRunner] Failed to create temporary file: {e.Message}");
                executionResult = Response.Error($"Failed to create temporary file: {e.Message}");
            }

            if (!fileCreated)
            {
                yield return executionResult;
                yield break;
            }

            // ??Python??
            yield return ExecutePythonScript(tempFilePath, pythonPath, workingDirectory, timeout, virtualEnv, (result) =>
            {
                if (result != null)
                {
                    executionResult = Response.Success("Python script execution completed", new
                    {
                        operation = "execute",
                        script_name = scriptName,
                        success = result.Success,
                        output = result.Output,
                        error = result.Error,
                        exit_code = result.ExitCode,
                        duration = result.Duration,
                        temp_file = tempFilePath,
                        project_refreshed = refreshProject
                    });

                    // ?????????????
                    if (refreshProject && result.Success)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            McpLogger.Log("[PythonRunner] Refreshing Unity project...");
                            AssetDatabase.Refresh();
                        };
                    }
                }
                else
                {
                    executionResult = Response.Error("Python script execution failed - no result returned");
                }
            });

            yield return executionResult;
        }

        /// <summary>
        /// ??????Python??
        /// </summary>
        private string FindPythonExecutable()
        {
            // Windows??Python??
            string[] windowsPaths = new[]
            {
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python39\python.exe",
                @"C:\Python38\python.exe",
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python312\python.exe"),
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python311\python.exe"),
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310\python.exe"),
            };

            // ????????
            foreach (var path in windowsPaths)
            {
                if (File.Exists(path))
                {
                    McpLogger.Log($"[PythonRunner] Found Python at: {path}");
                    return path;
                }
            }

            // ????where????(Windows)
            try
            {
                var whereProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "python",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                whereProcess.Start();
                string output = whereProcess.StandardOutput.ReadToEnd().Trim();
                whereProcess.WaitForExit();

                if (whereProcess.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    string firstPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    if (File.Exists(firstPath))
                    {
                        McpLogger.Log($"[PythonRunner] Found Python via 'where': {firstPath}");
                        return firstPath;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[PythonRunner] Failed to run 'where python': {ex.Message}");
            }

            // ????python,??????PATH??
            LogWarning("[PythonRunner] Could not find Python executable, using 'python' as default");
            return "python";
        }

        /// <summary>
        /// ??Python??
        /// </summary>
        private IEnumerator ExecutePythonScript(string scriptPath, string pythonPath, string workingDirectory,
            int timeout, string virtualEnv, System.Action<PythonResult> callback)
        {
            var result = new PythonResult { Operation = "execute" };
            var startTime = DateTime.Now;
            bool processStarted = false;
            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;

            // ??Python??
            string pythonExecutable = pythonPath;

            // ??pythonPath???????,??????
            if (string.IsNullOrEmpty(pythonExecutable) || pythonExecutable == "python")
            {
                pythonExecutable = FindPythonExecutable();
                McpLogger.Log($"[PythonRunner] Auto-detected Python: {pythonExecutable}");
            }

            if (!string.IsNullOrEmpty(virtualEnv))
            {
                // ?????????,????????Python
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python"); // Linux/Mac
                }
            }

            // ??Python??
            if (string.IsNullOrEmpty(pythonExecutable))
            {
                result.Success = false;
                result.Error = "Failed to start Python process: Cannot start process because a file name has not been provided.";
                result.ExitCode = -1;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;
                callback?.Invoke(result);
                yield break;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u \"{scriptPath}\"", // ??-u????Python????
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // ??Python???????UTF-8???????
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // ??Python??
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            McpLogger.Log($"[PythonRunner] ????: {pythonExecutable} \"{scriptPath}\"");
            McpLogger.Log($"[PythonRunner] ????: {workingDirectory}");

            // ?????????????
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);

                            // ?????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ??????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);

                            // ???????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ????????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
            }
            catch (Exception ex)
            {
                LogError($"[PythonRunner] Failed to start Python process: {ex.Message}");
                result.Success = false;
                result.Error = $"Failed to start Python process: {ex.Message}";
                result.ExitCode = -1;
                result.Duration = (DateTime.Now - startTime).TotalMilliseconds;
                callback(result);
                yield break;
            }

            if (processStarted && process != null)
            {
                // ????????????
                float elapsedTime = 0f;
                const float checkInterval = 0.05f; // ??????(50ms)

                while (!process.HasExited && elapsedTime < timeout)
                {
                    yield return new WaitForSeconds(checkInterval);
                    elapsedTime += checkInterval;

                    // ????????????(????)
                    if (elapsedTime % 1.0f < checkInterval) // ??????
                    {
                        var currentOutput = outputBuilder?.ToString() ?? "";
                        var currentError = errorBuilder?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(currentOutput) || !string.IsNullOrEmpty(currentError))
                        {
                            McpLogger.Log($"[PythonRunner] ????? ({elapsedTime:F1}s), ?????: {currentOutput.Length} ??");
                        }
                    }
                }

                if (!process.HasExited)
                {
                    // ??????,??????????
                    var partialOutput = outputBuilder?.ToString() ?? "";
                    var partialError = errorBuilder?.ToString() ?? "";

                    LogWarning($"[PythonRunner] Python script execution timeout after {timeout} seconds");
                    LogWarning($"[PythonRunner] ????????: {partialOutput.Length} ??, ??: {partialError.Length} ??");

                    try
                    {
                        process.Kill();
                        // ??????????????
                    }
                    catch { }
                    yield return new WaitForSeconds(0.2f);
                    result.Success = false;
                    result.Error = $"Script execution timeout after {timeout} seconds. Partial output captured: {partialOutput.Length} chars.";
                    result.ExitCode = -1;
                    result.Output = partialOutput; // ??????????
                }
                else
                {
                    try
                    {
                        process.WaitForExit(1000); // ????1????????
                        result.Success = process.ExitCode == 0;
                        result.Output = outputBuilder?.ToString() ?? "";
                        result.Error = errorBuilder?.ToString() ?? "";
                        result.ExitCode = process.ExitCode;

                        McpLogger.Log($"[PythonRunner] Python script completed with exit code: {process.ExitCode}");
                        if (!string.IsNullOrEmpty(result.Output))
                            McpLogger.Log($"[PythonRunner] Output ({result.Output.Length} chars):\n{result.Output}");
                        if (!string.IsNullOrEmpty(result.Error))
                            LogWarning($"[PythonRunner] Error ({result.Error.Length} chars):\n{result.Error}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"[PythonRunner] Error reading process result: {ex.Message}");
                        result.Success = false;
                        result.Error = $"Error reading process result: {ex.Message}";
                        result.ExitCode = -1;
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }

            result.Duration = (DateTime.Now - startTime).TotalMilliseconds;
            callback(result);
        }

        /// <summary>
        /// ??Python??
        /// </summary>
        private IEnumerator ValidatePythonSyntax(string scriptPath, string pythonPath, string virtualEnv,
            System.Action<bool, string, string> callback)
        {
            string pythonExecutable = pythonPath;

            // ??pythonPath???????,??????
            if (string.IsNullOrEmpty(pythonExecutable) || pythonExecutable == "python")
            {
                pythonExecutable = FindPythonExecutable();
                McpLogger.Log($"[PythonRunner] Auto-detected Python for validation: {pythonExecutable}");
            }

            if (!string.IsNullOrEmpty(virtualEnv))
            {
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python");
                }
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u -m py_compile \"{scriptPath}\"", // ??-u??
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // ??Python???????UTF-8???????
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // ??Python??
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            McpLogger.Log($"[PythonRunner] ????: {pythonExecutable} -m py_compile \"{scriptPath}\"");

            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;
            bool processStarted = false;

            // ?????????????
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);

                            // ?????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ??????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);

                            // ???????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ????????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
                else
                {
                    callback(false, "", "Failed to start Python process");
                    yield break;
                }
            }
            catch (Exception ex)
            {
                callback(false, "", $"Failed to validate syntax: {ex.Message}");
                yield break;
            }

            if (processStarted && process != null)
            {
                // ??????
                float elapsedTime = 0f;
                while (!process.HasExited && elapsedTime < 10f)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsedTime += 0.1f;
                }

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    callback(false, "", "Syntax validation timeout");
                }
                else
                {
                    try
                    {
                        process.WaitForExit();
                        bool success = process.ExitCode == 0;
                        callback(success, outputBuilder?.ToString() ?? "", errorBuilder?.ToString() ?? "");
                    }
                    catch (Exception ex)
                    {
                        callback(false, "", $"Error reading validation result: {ex.Message}");
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// ??Python?
        /// </summary>
        private IEnumerator InstallPackages(string[] packages, string pythonPath, string virtualEnv, int timeout,
            System.Action<object> callback)
        {
            string pythonExecutable = pythonPath;

            // ??pythonPath???????,??????
            if (string.IsNullOrEmpty(pythonExecutable) || pythonExecutable == "python")
            {
                pythonExecutable = FindPythonExecutable();
                McpLogger.Log($"[PythonRunner] Auto-detected Python for package install: {pythonExecutable}");
            }

            if (!string.IsNullOrEmpty(virtualEnv))
            {
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python");
                }
            }

            var packageList = string.Join(" ", packages);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u -m pip install {packageList}", // ??-u??
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // ??Python???????UTF-8???????
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // ??Python??
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            McpLogger.Log($"[PythonRunner] ???: {pythonExecutable} -m pip install {packageList}");

            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;
            bool processStarted = false;

            // ?????????????
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);

                            // ?????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ??????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);

                            // ???????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ????????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
                else
                {
                    callback(Response.Error("Failed to start pip process"));
                    yield break;
                }
            }
            catch (Exception ex)
            {
                callback(Response.Error($"Failed to install packages: {ex.Message}"));
                yield break;
            }

            if (processStarted && process != null)
            {
                // ??????
                float elapsedTime = 0f;
                while (!process.HasExited && elapsedTime < timeout)
                {
                    yield return new WaitForSeconds(0.5f);
                    elapsedTime += 0.5f;
                }

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    callback(Response.Error("Package installation timeout", new
                    {
                        operation = "install_package",
                        packages = packages,
                        timeout = timeout
                    }));
                }
                else
                {
                    try
                    {
                        process.WaitForExit();
                        bool success = process.ExitCode == 0;

                        if (success)
                        {
                            callback(Response.Success("Python packages installed successfully", new
                            {
                                operation = "install_package",
                                packages = packages,
                                output = outputBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                        else
                        {
                            callback(Response.Error("Package installation failed", new
                            {
                                operation = "install_package",
                                packages = packages,
                                error = errorBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        callback(Response.Error($"Error reading installation result: {ex.Message}"));
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// ?requirements?????
        /// </summary>
        private IEnumerator InstallFromRequirements(string requirementsFile, string pythonPath, string virtualEnv,
            int timeout, System.Action<object> callback)
        {
            if (!File.Exists(requirementsFile))
            {
                callback(Response.Error($"Requirements file not found: {requirementsFile}"));
                yield break;
            }

            string pythonExecutable = pythonPath;

            // ??pythonPath???????,??????
            if (string.IsNullOrEmpty(pythonExecutable) || pythonExecutable == "python")
            {
                pythonExecutable = FindPythonExecutable();
                McpLogger.Log($"[PythonRunner] Auto-detected Python for requirements install: {pythonExecutable}");
            }

            if (!string.IsNullOrEmpty(virtualEnv))
            {
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python");
                }
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u -m pip install -r \"{requirementsFile}\"", // ??-u??
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // ??Python???????UTF-8???????
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // ??Python??
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            McpLogger.Log($"[PythonRunner] ?requirements??: {pythonExecutable} -m pip install -r \"{requirementsFile}\"");

            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;
            bool processStarted = false;

            // ?????????????
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);

                            // ?????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ??????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // ????????
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);

                            // ???????Unity??? - ?????????
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] ????????: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
                else
                {
                    callback(Response.Error("Failed to start pip process"));
                    yield break;
                }
            }
            catch (Exception ex)
            {
                callback(Response.Error($"Failed to install from requirements: {ex.Message}"));
                yield break;
            }

            if (processStarted && process != null)
            {
                // ??????
                float elapsedTime = 0f;
                while (!process.HasExited && elapsedTime < timeout)
                {
                    yield return new WaitForSeconds(0.5f);
                    elapsedTime += 0.5f;
                }

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    callback(Response.Error("Requirements installation timeout", new
                    {
                        operation = "install_package",
                        requirements_file = requirementsFile,
                        timeout = timeout
                    }));
                }
                else
                {
                    try
                    {
                        process.WaitForExit();
                        bool success = process.ExitCode == 0;

                        if (success)
                        {
                            callback(Response.Success("Requirements installed successfully", new
                            {
                                operation = "install_package",
                                requirements_file = requirementsFile,
                                output = outputBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                        else
                        {
                            callback(Response.Error("Requirements installation failed", new
                            {
                                operation = "install_package",
                                requirements_file = requirementsFile,
                                error = errorBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        callback(Response.Error($"Error reading requirements installation result: {ex.Message}"));
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// ????????
        /// </summary>
        private string FixEncodingIssues(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            try
            {
                // ????????Base64?????
                string decodedInput = DecodeBase64Content(input);
                if (decodedInput != input)
                {
                    return decodedInput; // ???????Base64??,????
                }

                // ?????????????
                bool hasBadChars = input.Any(c => c == '?' || (c >= 0xFFFD && c <= 0xFFFE));

                if (hasBadChars || HasSuspiciousCharacterPattern(input))
                {
                    // ??1: ?????????UTF-8,???????????????
                    try
                    {
                        // ????????(???GBK/GB2312)
                        var systemEncoding = Encoding.Default;
                        // ?????????????
                        byte[] systemBytes = systemEncoding.GetBytes(input);
                        // ?UTF-8????
                        string utf8Result = Encoding.UTF8.GetString(systemBytes);

                        if (IsValidChineseText(utf8Result))
                        {
                            return utf8Result;
                        }
                    }
                    catch { }

                    // ??2: ??ISO-8859-1?UTF-8???
                    try
                    {
                        byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(input);
                        string utf8Result = Encoding.UTF8.GetString(bytes);

                        if (IsValidChineseText(utf8Result))
                        {
                            return utf8Result;
                        }
                    }
                    catch { }
                }

                return input;
            }
            catch
            {
                return input;
            }
        }

        /// <summary>
        /// ??Base64?????
        /// </summary>
        private string DecodeBase64Content(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            try
            {
                // ??Base64????
                const string prefix = "[UNITY_MCP_B64:";
                const string suffix = "]";

                string result = input;
                int startIndex = 0;

                while (true)
                {
                    int prefixIndex = result.IndexOf(prefix, startIndex);
                    if (prefixIndex == -1)
                        break;

                    int suffixIndex = result.IndexOf(suffix, prefixIndex + prefix.Length);
                    if (suffixIndex == -1)
                        break;

                    // ??Base64????
                    string base64Content = result.Substring(prefixIndex + prefix.Length, suffixIndex - prefixIndex - prefix.Length);

                    try
                    {
                        // ??Base64
                        byte[] bytes = Convert.FromBase64String(base64Content);
                        string decodedText = Encoding.UTF8.GetString(bytes);

                        // ?????????????
                        string originalTag = prefix + base64Content + suffix;
                        result = result.Replace(originalTag, decodedText);
                    }
                    catch
                    {
                        // ????,??????
                        startIndex = suffixIndex + suffix.Length;
                    }
                }

                return result;
            }
            catch
            {
                return input;
            }
        }

        /// <summary>
        /// ????????????(???????)
        /// </summary>
        private bool HasSuspiciousCharacterPattern(string input)
        {
            // ????????ASCII??,?????????
            return input.Any(c => c >= 128 && c <= 255);
        }

        /// <summary>
        /// ???????????????
        /// </summary>
        private bool IsValidChineseText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // ??????????
            bool hasChinese = text.Any(c => c >= 0x4E00 && c <= 0x9FFF);

            // ???????????????
            bool hasValidChars = !text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

            return hasChinese && hasValidChars;
        }

        /// <summary>
        /// ??????
        /// </summary>
        private void CleanupTempFiles(string tempFilePath)
        {
            CleanupSingleFile(tempFilePath);

            // ?????.pyc??
            var pycPath = tempFilePath + "c";
            CleanupSingleFile(pycPath);

            // ??__pycache__??
            var directory = Path.GetDirectoryName(tempFilePath);
            var pycacheDir = Path.Combine(directory, "__pycache__");
            if (Directory.Exists(pycacheDir))
            {
                try
                {
                    Directory.Delete(pycacheDir, true);
                }
                catch (Exception ex)
                {
                    LogWarning($"[PythonRunner] Failed to clean __pycache__ directory: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ??????,?????
        /// </summary>
        private void CleanupSingleFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    File.Delete(filePath);
                    McpLogger.Log($"[PythonRunner] Cleaned temporary file: {filePath}");
                    return;
                }
                catch (IOException ex)
                {
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        McpLogger.Log($"[PythonRunner] Failed to clean file, retry {retryCount}/{maxRetries}: {filePath}");
                        System.Threading.Thread.Sleep(100 * retryCount);
                    }
                    else
                    {
                        LogWarning($"[PythonRunner] Unable to clean temporary file: {filePath}, error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[PythonRunner] Unexpected error occurred while cleaning file: {filePath}, error: {ex.Message}");
                    break;
                }
            }
        }
    }
}

