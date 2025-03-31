using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeIndexer.Core.Models;
using Microsoft.Extensions.Logging;
using ParameterInfo = CodeIndexer.Core.Models.ParameterInfo;

namespace CodeIndexer.Core.Parsing
{
    /// <summary>
    /// 程序集解析器，负责解析DLL文件并提取代码元素
    /// </summary>
    public class AssemblyParser
    {
        private readonly ILogger<AssemblyParser>? _logger;

        public AssemblyParser(ILogger<AssemblyParser>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解析指定目录下的所有DLL文件
        /// </summary>
        /// <param name="directoryPath">DLL目录路径</param>
        /// <returns>解析出的代码元素集合</returns>
        public async Task<CodeDatabase> ParseDirectoryAsync(string directoryPath)
        {
            _logger?.LogInformation($"开始解析DLL目录: {directoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directoryPath}");
            }

            var codeDatabase = new CodeDatabase();
            var dllFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

            _logger?.LogInformation($"找到 {dllFiles.Length} 个DLL文件");

            foreach (var filePath in dllFiles)
            {
                try
                {
                    var fileElements = await ParseAssemblyAsync(filePath);
                    codeDatabase.AddElements(fileElements);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析DLL文件 {filePath} 时出错");
                }
            }

            _logger?.LogInformation($"解析完成，共提取 {codeDatabase.GetAllElements().Count} 个代码元素");
            return codeDatabase;
        }

        /// <summary>
        /// 解析单个DLL文件
        /// </summary>
        /// <param name="filePath">DLL文件路径</param>
        /// <returns>解析出的代码元素集合</returns>
        public async Task<List<CodeElement>> ParseAssemblyAsync(string filePath)
        {
            _logger?.LogDebug($"开始解析DLL文件: {filePath}");

            var elements = new List<CodeElement>();
            
            // 使用反射加载程序集
            try
            {
                // 使用Assembly.LoadFrom而不是Assembly.LoadFile，以便能够加载依赖项
                var assembly = Assembly.LoadFrom(filePath);
                
                // 解析所有类型
                foreach (var type in assembly.GetExportedTypes())
                {
                    try
                    {
                        // 解析命名空间
                        var namespaceName = type.Namespace ?? string.Empty;
                        var namespaceElement = elements.FirstOrDefault(e => 
                            e.ElementType == ElementType.Namespace && e.Name == namespaceName) as NamespaceElement;
                        
                        if (namespaceElement == null && !string.IsNullOrEmpty(namespaceName))
                        {
                            namespaceElement = new NamespaceElement
                            {
                                Name = namespaceName,
                                FullName = namespaceName,
                                Location = new SourceLocation
                                {
                                    FilePath = filePath
                                },
                                ContentHash = ComputeHash(namespaceName)
                            };
                            elements.Add(namespaceElement);
                        }

                        // 解析类型
                        var typeElement = CreateTypeElement(type, filePath, namespaceElement?.Id);
                        elements.Add(typeElement);
                        
                        if (namespaceElement != null)
                        {
                            namespaceElement.TypeIds.Add(typeElement.Id);
                        }

                        // 解析成员
                        ParseMembers(type, typeElement, elements);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"解析类型 {type.FullName} 时出错");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"加载程序集 {filePath} 时出错");
                throw;
            }

            return elements;
        }

        /// <summary>
        /// 创建类型元素
        /// </summary>
        private TypeElement CreateTypeElement(Type type, string filePath, string? parentId)
        {
            var elementType = DetermineElementType(type);
            var typeElement = new TypeElement(elementType)
            {
                Name = type.Name,
                FullName = type.FullName ?? type.Name,
                Location = new SourceLocation
                {
                    FilePath = filePath
                },
                ParentId = parentId,
                IsAbstract = type.IsAbstract && !type.IsInterface,
                IsStatic = type.IsAbstract && type.IsSealed,
                IsSealed = type.IsSealed && !type.IsAbstract,
                AccessModifier = DetermineAccessModifier(type),
                ContentHash = ComputeHash(type.FullName ?? type.Name)
            };

            // 添加泛型参数
            if (type.IsGenericType)
            {
                foreach (var genericArg in type.GetGenericArguments())
                {
                    typeElement.GenericParameters.Add(genericArg.Name);
                }
            }

            // 添加基类
            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                typeElement.BaseTypeId = type.BaseType.FullName;
            }

            // 添加接口
            foreach (var interfaceType in type.GetInterfaces())
            {
                typeElement.ImplementedInterfaceIds.Add(interfaceType.FullName ?? interfaceType.Name);
            }

            return typeElement;
        }

        /// <summary>
        /// 解析类型的成员
        /// </summary>
        private void ParseMembers(Type type, TypeElement typeElement, List<CodeElement> elements)
        {
            // 解析方法
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                try
                {
                    if (method.IsSpecialName) // 跳过属性的访问器方法
                        continue;

                    var memberElement = new MemberElement(ElementType.Method)
                    {
                        Name = method.Name,
                        FullName = $"{typeElement.FullName}.{method.Name}",
                        Location = typeElement.Location, // 使用类型的位置，因为DLL中没有具体行号
                        ParentId = typeElement.Id,
                        AccessModifier = DetermineAccessModifier(method),
                        ReturnType = method.ReturnType.Name,
                        ContentHash = ComputeHash(method.ToString() ?? "")
                    };

                    // 添加方法修饰符
                    if (method.IsStatic) memberElement.Modifiers.Add("static");
                    if (method.IsAbstract) memberElement.Modifiers.Add("abstract");
                    if (method.IsVirtual) memberElement.Modifiers.Add("virtual");
                    if (method.IsFinal) memberElement.Modifiers.Add("sealed");

                    // 添加参数
                    foreach (var param in method.GetParameters())
                    {
                        memberElement.Parameters.Add(new ParameterInfo
                        {
                            Name = param.Name ?? "",
                            Type = param.ParameterType.Name
                        });
                    }

                    elements.Add(memberElement);
                    typeElement.MemberIds.Add(memberElement.Id);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析方法 {method.Name} 时出错");
                }
            }

            // 解析属性
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                try
                {
                    var memberElement = new MemberElement(ElementType.Property)
                    {
                        Name = property.Name,
                        FullName = $"{typeElement.FullName}.{property.Name}",
                        Location = typeElement.Location,
                        ParentId = typeElement.Id,
                        AccessModifier = DetermineAccessModifier(property),
                        ReturnType = property.PropertyType.Name,
                        ContentHash = ComputeHash(property.ToString() ?? "")
                    };

                    elements.Add(memberElement);
                    typeElement.MemberIds.Add(memberElement.Id);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析属性 {property.Name} 时出错");
                }
            }

            // 解析字段
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                try
                {
                    var memberElement = new MemberElement(ElementType.Field)
                    {
                        Name = field.Name,
                        FullName = $"{typeElement.FullName}.{field.Name}",
                        Location = typeElement.Location,
                        ParentId = typeElement.Id,
                        AccessModifier = DetermineAccessModifier(field),
                        ReturnType = field.FieldType.Name,
                        ContentHash = ComputeHash(field.ToString() ?? "")
                    };

                    // 添加字段修饰符
                    if (field.IsStatic) memberElement.Modifiers.Add("static");
                    if (field.IsInitOnly) memberElement.Modifiers.Add("readonly");
                    if (field.IsLiteral) memberElement.Modifiers.Add("const");

                    elements.Add(memberElement);
                    typeElement.MemberIds.Add(memberElement.Id);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析字段 {field.Name} 时出错");
                }
            }

            // 解析事件
            foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                try
                {
                    var memberElement = new MemberElement(ElementType.Event)
                    {
                        Name = evt.Name,
                        FullName = $"{typeElement.FullName}.{evt.Name}",
                        Location = typeElement.Location,
                        ParentId = typeElement.Id,
                        AccessModifier = DetermineAccessModifier(evt),
                        ReturnType = evt.EventHandlerType?.Name ?? "void",
                        ContentHash = ComputeHash(evt.ToString() ?? "")
                    };

                    elements.Add(memberElement);
                    typeElement.MemberIds.Add(memberElement.Id);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析事件 {evt.Name} 时出错");
                }
            }
        }

        /// <summary>
        /// 确定类型的元素类型
        /// </summary>
        private ElementType DetermineElementType(Type type)
        {
            if (type.IsClass)
            {
                if (type.BaseType == typeof(MulticastDelegate))
                    return ElementType.Delegate;
                return ElementType.Class;
            }
            else if (type.IsInterface)
                return ElementType.Interface;
            else if (type.IsEnum)
                return ElementType.Enum;
            else if (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
                return ElementType.Struct;
            else
                return ElementType.Class; // 默认为类
        }

        /// <summary>
        /// 确定成员的访问修饰符
        /// </summary>
        private string DetermineAccessModifier(MemberInfo member)
        {
            Type? type = member as Type;
            if (type != null)
            {
                if (type.IsNestedPublic || type.IsPublic)
                    return "public";
                if (type.IsNestedPrivate)
                    return "private";
                if (type.IsNestedFamily)
                    return "protected";
                if (type.IsNestedAssembly)
                    return "internal";
                if (type.IsNestedFamORAssem)
                    return "protected internal";
                if (type.IsNestedFamANDAssem)
                    return "private protected";
                return "internal"; // 默认为internal
            }

            MethodInfo? method = member as MethodInfo;
            if (method != null)
            {
                if (method.IsPublic)
                    return "public";
                if (method.IsPrivate)
                    return "private";
                if (method.IsFamily)
                    return "protected";
                if (method.IsAssembly)
                    return "internal";
                if (method.IsFamilyOrAssembly)
                    return "protected internal";
                if (method.IsFamilyAndAssembly)
                    return "private protected";
                return "private"; // 默认为private
            }

            PropertyInfo? property = member as PropertyInfo;
            if (property != null)
            {
                var accessor = property.GetMethod ?? property.SetMethod;
                if (accessor != null)
                    return DetermineAccessModifier(accessor);
                return "private"; // 默认为private
            }

            FieldInfo? field = member as FieldInfo;
            if (field != null)
            {
                if (field.IsPublic)
                    return "public";
                if (field.IsPrivate)
                    return "private";
                if (field.IsFamily)
                    return "protected";
                if (field.IsAssembly)
                    return "internal";
                if (field.IsFamilyOrAssembly)
                    return "protected internal";
                if (field.IsFamilyAndAssembly)
                    return "private protected";
                return "private"; // 默认为private
            }

            EventInfo? evt = member as EventInfo;
            if (evt != null)
            {
                var accessor = evt.AddMethod;
                if (accessor != null)
                    return DetermineAccessModifier(accessor);
                return "private"; // 默认为private
            }

            return "private"; // 默认为private
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