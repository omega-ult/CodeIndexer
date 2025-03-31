using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeIndexer.Core.Indexing;
using CodeIndexer.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CodeIndexer.Client
{
    class Program
    {
        private static HttpClient _httpClient = null!;
        private static readonly string BaseUrl = "http://localhost:5000/api/codeindex";

        static async Task Main(string[] args)
        {
            Console.WriteLine("C#代码索引客户端示例");
            Console.WriteLine("======================\n");

            // 配置HttpClient
            var services = new ServiceCollection();
            services.AddHttpClient("CodeIndexerClient", client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            _httpClient = httpClientFactory.CreateClient("CodeIndexerClient");

            bool exit = false;
            while (!exit)
            {
                Console.WriteLine("\n请选择操作：");
                Console.WriteLine("1. 索引代码目录");
                Console.WriteLine("2. 按名称模糊查询");
                Console.WriteLine("3. 按全名精确查询");
                Console.WriteLine("4. 按元素类型查询");
                Console.WriteLine("5. 按父元素查询子元素");
                Console.WriteLine("6. 高级查询");
                Console.WriteLine("7. 获取元素详情");
                Console.WriteLine("0. 退出");

                Console.Write("\n请输入选项: ");
                var choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            await IndexDirectoryAsync();
                            break;
                        case "2":
                            await SearchByNamePatternAsync();
                            break;
                        case "3":
                            await SearchByFullNameAsync();
                            break;
                        case "4":
                            await SearchByElementTypeAsync();
                            break;
                        case "5":
                            await SearchByParentIdAsync();
                            break;
                        case "6":
                            await AdvancedSearchAsync();
                            break;
                        case "7":
                            await GetElementByIdAsync();
                            break;
                        case "0":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("无效的选项，请重新输入");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n操作出错: {ex.Message}");
                }
            }

            Console.WriteLine("\n感谢使用C#代码索引客户端！");
        }

        /// <summary>
        /// 索引代码目录
        /// </summary>
        static async Task IndexDirectoryAsync()
        {
            Console.Write("\n请输入要索引的代码目录路径: ");
            var directoryPath = Console.ReadLine();

            if (string.IsNullOrEmpty(directoryPath))
            {
                Console.WriteLine("目录路径不能为空");
                return;
            }

            Console.WriteLine("\n正在索引代码目录，请稍候...");

            var request = new { DirectoryPath = directoryPath };
            var response = await _httpClient.PostAsJsonAsync("index", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<IndexResult>();
                Console.WriteLine($"\n索引成功: {result?.Message}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n索引失败: {error}");
            }
        }

        /// <summary>
        /// 按名称模糊查询
        /// </summary>
        static async Task SearchByNamePatternAsync()
        {
            Console.Write("\n请输入要查询的名称模式: ");
            var namePattern = Console.ReadLine();

            if (string.IsNullOrEmpty(namePattern))
            {
                Console.WriteLine("名称模式不能为空");
                return;
            }

            Console.Write("请输入最大结果数 (默认100): ");
            var maxResultsInput = Console.ReadLine();
            int maxResults = string.IsNullOrEmpty(maxResultsInput) ? 100 : int.Parse(maxResultsInput);

            Console.WriteLine("\n正在查询，请稍候...");

            var response = await _httpClient.GetAsync($"search/name/{namePattern}?maxResults={maxResults}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SearchResult>();
                DisplaySearchResult(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n查询失败: {error}");
            }
        }

        /// <summary>
        /// 按全名精确查询
        /// </summary>
        static async Task SearchByFullNameAsync()
        {
            Console.Write("\n请输入要查询的全名: ");
            var fullName = Console.ReadLine();

            if (string.IsNullOrEmpty(fullName))
            {
                Console.WriteLine("全名不能为空");
                return;
            }

            Console.WriteLine("\n正在查询，请稍候...");

            var response = await _httpClient.GetAsync($"search/fullname/{fullName}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SearchResult>();
                DisplaySearchResult(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n查询失败: {error}");
            }
        }

        /// <summary>
        /// 按元素类型查询
        /// </summary>
        static async Task SearchByElementTypeAsync()
        {
            Console.WriteLine("\n可用的元素类型:");
            Console.WriteLine("0. Namespace");
            Console.WriteLine("1. Class");
            Console.WriteLine("2. Interface");
            Console.WriteLine("3. Struct");
            Console.WriteLine("4. Enum");
            Console.WriteLine("5. Method");
            Console.WriteLine("6. Property");
            Console.WriteLine("7. Field");
            Console.WriteLine("8. Event");
            Console.WriteLine("9. Delegate");
            Console.WriteLine("10. EnumMember");
            Console.WriteLine("11. Constructor");
            Console.WriteLine("12. Destructor");
            Console.WriteLine("13. Indexer");
            Console.WriteLine("14. Operator");

            Console.Write("\n请选择元素类型 (0-14): ");
            var typeInput = Console.ReadLine();
            if (string.IsNullOrEmpty(typeInput) || !int.TryParse(typeInput, out int typeValue) || typeValue < 0 || typeValue > 14)
            {
                Console.WriteLine("无效的元素类型");
                return;
            }

            var elementType = (ElementType)typeValue;

            Console.Write("请输入最大结果数 (默认1000): ");
            var maxResultsInput = Console.ReadLine();
            int maxResults = string.IsNullOrEmpty(maxResultsInput) ? 1000 : int.Parse(maxResultsInput);

            Console.WriteLine("\n正在查询，请稍候...");

            var response = await _httpClient.GetAsync($"search/type/{elementType}?maxResults={maxResults}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SearchResult>();
                DisplaySearchResult(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n查询失败: {error}");
            }
        }

        /// <summary>
        /// 按父元素查询子元素
        /// </summary>
        static async Task SearchByParentIdAsync()
        {
            Console.Write("\n请输入父元素ID: ");
            var parentId = Console.ReadLine();

            if (string.IsNullOrEmpty(parentId))
            {
                Console.WriteLine("父元素ID不能为空");
                return;
            }

            Console.Write("请输入最大结果数 (默认1000): ");
            var maxResultsInput = Console.ReadLine();
            int maxResults = string.IsNullOrEmpty(maxResultsInput) ? 1000 : int.Parse(maxResultsInput);

            Console.WriteLine("\n正在查询，请稍候...");

            var response = await _httpClient.GetAsync($"search/parent/{parentId}?maxResults={maxResults}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SearchResult>();
                DisplaySearchResult(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n查询失败: {error}");
            }
        }

        /// <summary>
        /// 高级查询
        /// </summary>
        static async Task AdvancedSearchAsync()
        {
            var query = new CodeElementQuery();

            Console.Write("\n请输入名称模式 (可选): ");
            var namePattern = Console.ReadLine();
            if (!string.IsNullOrEmpty(namePattern))
            {
                query.NamePattern = namePattern;
            }

            Console.WriteLine("\n选择元素类型 (可选):");
            Console.WriteLine("0. Namespace");
            Console.WriteLine("1. Class");
            Console.WriteLine("2. Interface");
            Console.WriteLine("3. Struct");
            Console.WriteLine("4. Enum");
            Console.WriteLine("5. Method");
            Console.WriteLine("6. Property");
            Console.WriteLine("7. Field");
            Console.WriteLine("8. Event");
            Console.WriteLine("9. Delegate");
            Console.WriteLine("10. EnumMember");
            Console.WriteLine("11. Constructor");
            Console.WriteLine("12. Destructor");
            Console.WriteLine("13. Indexer");
            Console.WriteLine("14. Operator");
            Console.WriteLine("-1. 不限制类型");

            Console.Write("\n请选择元素类型 (-1 或 0-14): ");
            var typeInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(typeInput) && int.TryParse(typeInput, out int typeValue) && typeValue >= 0 && typeValue <= 14)
            {
                query.ElementType = (ElementType)typeValue;
            }

            Console.Write("\n请输入访问修饰符 (public, private, protected, internal, protected internal) (可选): ");
            var accessModifier = Console.ReadLine();
            if (!string.IsNullOrEmpty(accessModifier))
            {
                query.AccessModifier = accessModifier;
            }

            Console.Write("\n请输入父元素ID (可选): ");
            var parentId = Console.ReadLine();
            if (!string.IsNullOrEmpty(parentId))
            {
                query.ParentId = parentId;
            }

            Console.Write("\n请输入返回类型或成员类型 (可选): ");
            var returnType = Console.ReadLine();
            if (!string.IsNullOrEmpty(returnType))
            {
                query.ReturnType = returnType;
            }

            Console.Write("请输入最大结果数 (默认100): ");
            var maxResultsInput = Console.ReadLine();
            int maxResults = string.IsNullOrEmpty(maxResultsInput) ? 100 : int.Parse(maxResultsInput);

            Console.WriteLine("\n正在查询，请稍候...");

            var response = await _httpClient.PostAsJsonAsync($"search/advanced?maxResults={maxResults}", query);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SearchResult>();
                DisplaySearchResult(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n查询失败: {error}");
            }
        }

        /// <summary>
        /// 获取元素详情
        /// </summary>
        static async Task GetElementByIdAsync()
        {
            Console.Write("\n请输入元素ID: ");
            var elementId = Console.ReadLine();

            if (string.IsNullOrEmpty(elementId))
            {
                Console.WriteLine("元素ID不能为空");
                return;
            }

            Console.WriteLine("\n正在获取元素详情，请稍候...");

            var response = await _httpClient.GetAsync($"element/{elementId}");

            if (response.IsSuccessStatusCode)
            {
                var element = await response.Content.ReadFromJsonAsync<CodeElement>();
                DisplayElementDetails(element);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n获取元素详情失败: {error}");
            }
        }

        /// <summary>
        /// 显示搜索结果
        /// </summary>
        static void DisplaySearchResult(SearchResult? result)
        {
            if (result == null || result.Elements == null || result.Elements.Count == 0)
            {
                Console.WriteLine("\n未找到匹配的结果");
                return;
            }

            Console.WriteLine($"\n找到 {result.TotalCount} 个匹配的结果，显示 {result.Elements.Count} 个:");
            Console.WriteLine("ID\t类型\t名称\t位置");
            Console.WriteLine("----------------------------------------");

            foreach (var element in result.Elements)
            {
                Console.WriteLine($"{element.Id}\t{element.ElementType}\t{element.Name}\t{element.FilePath}:{element.LineNumber}");
            }
        }

        /// <summary>
        /// 显示元素详情
        /// </summary>
        static void DisplayElementDetails(CodeElement? element)
        {
            if (element == null)
            {
                Console.WriteLine("\n未找到元素");
                return;
            }

            Console.WriteLine("\n元素详情:");
            Console.WriteLine($"ID: {element.Id}");
            Console.WriteLine($"名称: {element.Name}");
            Console.WriteLine($"全名: {element.FullName}");
            Console.WriteLine($"类型: {element.ElementType}");
            Console.WriteLine($"访问修饰符: {element.AccessModifier}");
            Console.WriteLine($"修饰符: {string.Join(", ", element.Modifiers)}");
            Console.WriteLine($"位置: {element.Location.FilePath}:{element.Location.StartLine}");
            Console.WriteLine($"父元素ID: {element.ParentId ?? "无"}");

            if (!string.IsNullOrEmpty(element.Documentation))
            {
                Console.WriteLine("\n文档注释:");
                Console.WriteLine(element.Documentation);
            }

            if (element is MemberElement memberElement)
            {
                Console.WriteLine($"\n返回类型或成员类型: {memberElement.Type}");

                if (memberElement.Parameters.Count > 0)
                {
                    Console.WriteLine("\n参数列表:");
                    foreach (var param in memberElement.Parameters)
                    {
                        Console.WriteLine($"{param.Type} {param.Name}{(param.HasDefaultValue ? $" = {param.DefaultValue}" : "")}");
                    }
                }
            }
            else if (element is TypeElement typeElement)
            {
                if (typeElement.BaseTypeId != null)
                {
                    Console.WriteLine($"\n基类: {typeElement.BaseTypeId}");
                }

                if (typeElement.ImplementedInterfaceIds.Count > 0)
                {
                    Console.WriteLine("\n实现的接口:");
                    foreach (var interfaceId in typeElement.ImplementedInterfaceIds)
                    {
                        Console.WriteLine(interfaceId);
                    }
                }

                if (typeElement.MemberIds.Count > 0)
                {
                    Console.WriteLine($"\n成员数量: {typeElement.MemberIds.Count}");
                }
            }
            else if (element is NamespaceElement namespaceElement)
            {
                Console.WriteLine($"\n类型数量: {namespaceElement.TypeIds.Count}");
                Console.WriteLine($"子命名空间数量: {namespaceElement.ChildNamespaceIds.Count}");
            }
        }
    }

    /// <summary>
    /// 索引结果
    /// </summary>
    class IndexResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ElementCount { get; set; }
    }

    /// <summary>
    /// 搜索结果
    /// </summary>
    class SearchResult
    {
        public int TotalCount { get; set; }
        public List<CodeElementIndex> Elements { get; set; } = new List<CodeElementIndex>();
    }
}