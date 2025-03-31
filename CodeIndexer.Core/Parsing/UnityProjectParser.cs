using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CodeIndexer.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeIndexer.Core.Parsing
{
    /// <summary>
    /// Unity工程解析器，负责解析Unity工程文件并提取代码元素
    /// </summary>
    public class UnityProjectParser
    {
        private readonly ILogger _logger;
        private readonly CodeParser _codeParser;
        private readonly CecilAssemblyParser _assemblyParser;

        public UnityProjectParser(ILogger logger = null)
        {
            _logger = logger;
            _codeParser = new CodeParser(logger);
            _assemblyParser = new CecilAssemblyParser(logger);
        }

        /// <summary>
        /// 解析Unity工程目录
        /// </summary>
        /// <param name="projectPath">Unity工程根目录路径</param>
        /// <returns>解析出的代码元素集合</returns>
        public async Task<CodeDatabase> ParseUnityProjectAsync(string projectPath)
        {
            _logger?.LogInformation($"开始解析Unity工程: {projectPath}");

            if (!Directory.Exists(projectPath))
            {
                throw new DirectoryNotFoundException($"Unity工程目录不存在: {projectPath}");
            }

            var codeDatabase = new CodeDatabase();

            // 解析C#脚本文件
            await ParseScriptsAsync(projectPath, codeDatabase);

            // 解析程序集文件
            await ParseAssembliesAsync(projectPath, codeDatabase);
            
            _logger?.LogInformation($"Unity工程解析完成，共提取 {codeDatabase.GetAllElements().Count} 个代码元素");
            return codeDatabase;
        }

        /// <summary>
        /// 解析Unity工程中的C#脚本文件
        /// </summary>
        private async Task ParseScriptsAsync(string projectPath, CodeDatabase codeDatabase)
        {
            _logger?.LogInformation("开始解析Unity工程中的C#脚本文件");

            // 查找Scripts目录和Assets目录下的所有C#脚本
            var scriptsDirectories = new List<string>();
            
            // 常见的脚本目录
            var commonScriptPaths = new[]
            {
                Path.Combine(projectPath, "Assets", "Scripts"),
                Path.Combine(projectPath, "Assets")
            };

            foreach (var path in commonScriptPaths)
            {
                if (Directory.Exists(path))
                {
                    scriptsDirectories.Add(path);
                }
            }

            // 解析每个脚本目录
            foreach (var scriptDir in scriptsDirectories)
            {
                try
                {
                    var scriptDatabase = await _codeParser.ParseDirectoryAsync(scriptDir);
                    codeDatabase.AddElements(scriptDatabase.GetAllElements());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析脚本目录时出错: {scriptDir}");
                }
            }

            _logger?.LogInformation($"C#脚本文件解析完成，共提取 {codeDatabase.GetAllElements().Count} 个代码元素");
        }

        /// <summary>
        /// 解析Unity工程中的程序集文件
        /// </summary>
        private async Task ParseAssembliesAsync(string projectPath, CodeDatabase codeDatabase)
        {
            _logger?.LogInformation("开始解析Unity工程中的程序集文件");

            // 查找Library目录下的所有DLL文件
            var libraryPath = Path.Combine(projectPath, "Library");
            var managedPath = Path.Combine(libraryPath, "ScriptAssemblies");
            
            // 常见的Unity程序集目录
            var assemblyDirectories = new List<string>();
            
            if (Directory.Exists(managedPath))
            {
                assemblyDirectories.Add(managedPath);
            }
            
            // 查找Packages目录下的程序集
            var packagesPath = Path.Combine(projectPath, "Packages");
            if (Directory.Exists(packagesPath))
            {
                assemblyDirectories.Add(packagesPath);
            }

            // 解析每个程序集目录
            foreach (var assemblyDir in assemblyDirectories)
            {
                try
                {
                    var assemblyDatabase = await _assemblyParser.ParseDirectoryAsync(assemblyDir);
                    codeDatabase.AddElements(assemblyDatabase.GetAllElements());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析程序集目录时出错: {assemblyDir}");
                }
            }

            // 解析Unity引擎程序集
            await ParseUnityEngineAssembliesAsync(projectPath, codeDatabase);

            _logger?.LogInformation($"程序集文件解析完成，共提取 {codeDatabase.GetAllElements().Count} 个代码元素");
        }

        /// <summary>
        /// 解析Unity引擎程序集
        /// </summary>
        private async Task ParseUnityEngineAssembliesAsync(string projectPath, CodeDatabase codeDatabase)
        {
            // 查找Unity安装目录下的引擎程序集
            // 注意：这里需要根据实际情况调整Unity安装路径
            var unityEditorPath = FindUnityEditorPath(projectPath);
            if (string.IsNullOrEmpty(unityEditorPath))
            {
                _logger?.LogWarning("未找到Unity编辑器路径，跳过引擎程序集解析");
                return;
            }

            var managedPath = Path.Combine(unityEditorPath, "Data", "Managed");
            if (!Directory.Exists(managedPath))
            {
                _logger?.LogWarning($"Unity引擎程序集目录不存在: {managedPath}");
                return;
            }

            try
            {
                // 只解析核心Unity引擎程序集
                var coreAssemblies = new[]
                {
                    Path.Combine(managedPath, "UnityEngine.dll"),
                    Path.Combine(managedPath, "UnityEngine.CoreModule.dll"),
                    Path.Combine(managedPath, "UnityEditor.dll")
                };

                foreach (var assemblyPath in coreAssemblies)
                {
                    if (File.Exists(assemblyPath))
                    {
                        try
                        {
                            var elements = await _assemblyParser.ParseAssemblyAsync(assemblyPath);
                            codeDatabase.AddElements(elements);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"解析Unity引擎程序集时出错: {assemblyPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"解析Unity引擎程序集目录时出错: {managedPath}");
            }
        }

        /// <summary>
        /// 查找Unity编辑器路径
        /// </summary>
        private string FindUnityEditorPath(string projectPath)
        {
            // 尝试从ProjectSettings/ProjectVersion.txt文件中获取Unity版本
            var projectVersionPath = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
            if (!File.Exists(projectVersionPath))
            {
                return string.Empty;
            }

            try
            {
                var versionContent = File.ReadAllText(projectVersionPath);
                var versionLine = versionContent.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("m_EditorVersion:"));
                
                if (string.IsNullOrEmpty(versionLine))
                {
                    return string.Empty;
                }

                var version = versionLine.Substring("m_EditorVersion:".Length).Trim();
                
                // 根据操作系统查找Unity安装路径
                if (OperatingSystem.IsMacOS())
                {
                    // macOS上的Unity安装路径
                    return $"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents";
                }
                else if (OperatingSystem.IsWindows())
                {
                    // Windows上的Unity安装路径
                    return $"C:\\Program Files\\Unity\\Hub\\Editor\\{version}";
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Linux上的Unity安装路径
                    return $"/opt/unity/hub/editor/{version}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"读取Unity版本信息时出错: {projectVersionPath}");
            }

            return string.Empty;
        }


        /// <summary>
        /// 计算字符串的哈希值
        /// </summary>
        private string ComputeHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}