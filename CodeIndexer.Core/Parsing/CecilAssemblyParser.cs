using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CodeIndexer.Core.Models;
using Microsoft.Extensions.Logging;
using Mono.Cecil;

namespace CodeIndexer.Core.Parsing
{
    /// <summary>
    /// 使用Mono.Cecil库的高级程序集解析器，提供更详细的DLL解析功能
    /// </summary>
    public class CecilAssemblyParser
    {
        private readonly ILogger _logger;

        public CecilAssemblyParser(ILogger logger = null)
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
            _logger?.LogInformation($"开始使用Cecil解析DLL目录: {directoryPath}");

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
        /// 使用Mono.Cecil解析单个DLL文件
        /// </summary>
        /// <param name="filePath">DLL文件路径</param>
        /// <returns>解析出的代码元素集合</returns>
        public async Task<List<CodeElement>> ParseAssemblyAsync(string filePath)
        {
            _logger?.LogDebug($"开始使用Cecil解析DLL文件: {filePath}");

            var elements = new List<CodeElement>();
            
            // 使用Cecil加载程序集
            try
            {
                var readerParameters = new ReaderParameters { ReadSymbols = false };
                var assembly = AssemblyDefinition.ReadAssembly(filePath, readerParameters);
                
                // 解析所有模块
                foreach (var module in assembly.Modules)
                {
                    // 解析所有类型
                    foreach (var type in module.Types)
                    {
                        try
                        {
                            // 跳过<Module>类型
                            if (type.Name == "<Module>")
                                continue;
                                
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
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"加载程序集 {filePath} 时出错");
                throw;
            }

            return await Task.FromResult(elements);
        }

        /// <summary>
        /// 创建类型元素
        /// </summary>
        private TypeElement CreateTypeElement(TypeDefinition type, string filePath, string? parentId)
        {
            var elementType = DetermineElementType(type);
            var typeElement = new TypeElement(elementType)
            {
                Name = type.Name,
                FullName = type.FullName,
                Location = new SourceLocation
                {
                    FilePath = filePath
                },
                ParentId = parentId,
                IsAbstract = type.IsAbstract && !type.IsInterface,
                IsStatic = type.IsAbstract && type.IsSealed,
                IsSealed = type.IsSealed && !type.IsAbstract,
                IsPartial = false, // Cecil不直接支持部分类检测
                AccessModifier = DetermineAccessModifier(type),
                ContentHash = ComputeHash(type.FullName)
            };

            // 添加泛型参数
            if (type.HasGenericParameters)
            {
                foreach (var genericParam in type.GenericParameters)
                {
                    typeElement.GenericParameters.Add(genericParam.Name);
                    
                    // 添加泛型约束
                    if (genericParam.HasConstraints)
                    {
                        var constraints = new List<string>();
                        foreach (var constraint in genericParam.Constraints)
                        {
                            constraints.Add(constraint.ConstraintType.FullName);
                        }
                        typeElement.GenericConstraints[genericParam.Name] = constraints;
                    }
                }
            }

            // 添加基类
            if (type.BaseType != null && type.BaseType.FullName != "System.Object")
            {
                typeElement.BaseTypeId = type.BaseType.FullName;
            }

            // 添加接口
            if (type.HasInterfaces)
            {
                foreach (var interfaceType in type.Interfaces)
                {
                    typeElement.ImplementedInterfaceIds.Add(interfaceType.InterfaceType.FullName);
                }
            }

            // 检查Unity特有属性
            if (IsUnityType(type))
            {
                typeElement.Modifiers.Add("UnityType");
            }

            return typeElement;
        }

        /// <summary>
        /// 解析类型的成员
        /// </summary>
        private void ParseMembers(TypeDefinition type, TypeElement typeElement, List<CodeElement> elements)
        {
            // 解析方法
            if (type.HasMethods)
            {
                foreach (var method in type.Methods)
                {
                    try
                    {
                        // 跳过属性的访问器方法和构造函数
                        if (method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")))
                            continue;

                        var memberElement = new MemberElement(ElementType.Method)
                        {
                            Name = method.Name,
                            FullName = $"{typeElement.FullName}.{method.Name}",
                            Location = typeElement.Location, // 使用类型的位置，因为DLL中没有具体行号
                            ParentId = typeElement.Id,
                            AccessModifier = DetermineAccessModifier(method),
                            ReturnType = method.ReturnType.FullName,
                            ContentHash = ComputeHash(method.FullName)
                        };

                        // 添加方法修饰符
                        if (method.IsStatic) memberElement.Modifiers.Add("static");
                        if (method.IsAbstract) memberElement.Modifiers.Add("abstract");
                        if (method.IsVirtual) memberElement.Modifiers.Add("virtual");
                        if (method.IsFinal) memberElement.Modifiers.Add("sealed");

                        // 检查Unity特有方法
                        if (IsUnityMethod(method))
                        {
                            memberElement.Modifiers.Add("UnityCallback");
                        }

                        // 添加参数
                        if (method.HasParameters)
                        {
                            foreach (var param in method.Parameters)
                            {
                                memberElement.Parameters.Add(new ParameterInfo
                                {
                                    Name = param.Name,
                                    Type = param.ParameterType.FullName
                                });
                            }
                        }

                        elements.Add(memberElement);
                        typeElement.MemberIds.Add(memberElement.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"解析方法 {method.Name} 时出错");
                    }
                }
            }

            // 解析属性
            if (type.HasProperties)
            {
                foreach (var property in type.Properties)
                {
                    try
                    {
                        var memberElement = new MemberElement(ElementType.Property)
                        {
                            Name = property.Name,
                            FullName = $"{typeElement.FullName}.{property.Name}",
                            Location = typeElement.Location,
                            ParentId = typeElement.Id,
                            AccessModifier = DeterminePropertyAccessModifier(property),
                            ReturnType = property.PropertyType.FullName,
                            ContentHash = ComputeHash(property.FullName)
                        };

                        // 检查Unity特有属性
                        if (HasUnityAttribute(property))
                        {
                            memberElement.Modifiers.Add("UnitySerializedField");
                        }

                        elements.Add(memberElement);
                        typeElement.MemberIds.Add(memberElement.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"解析属性 {property.Name} 时出错");
                    }
                }
            }

            // 解析字段
            if (type.HasFields)
            {
                foreach (var field in type.Fields)
                {
                    try
                    {
                        // 跳过编译器生成的后备字段
                        if (field.Name.StartsWith("<") && field.Name.Contains(">"))
                            continue;

                        var memberElement = new MemberElement(ElementType.Field)
                        {
                            Name = field.Name,
                            FullName = $"{typeElement.FullName}.{field.Name}",
                            Location = typeElement.Location,
                            ParentId = typeElement.Id,
                            AccessModifier = DetermineAccessModifier(field),
                            ReturnType = field.FieldType.FullName,
                            ContentHash = ComputeHash(field.FullName)
                        };

                        // 添加字段修饰符
                        if (field.IsStatic) memberElement.Modifiers.Add("static");
                        if (field.IsInitOnly) memberElement.Modifiers.Add("readonly");
                        if (field.IsLiteral) memberElement.Modifiers.Add("const");

                        // 检查Unity特有字段
                        if (HasUnityAttribute(field))
                        {
                            memberElement.Modifiers.Add("UnitySerializedField");
                        }

                        elements.Add(memberElement);
                        typeElement.MemberIds.Add(memberElement.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"解析字段 {field.Name} 时出错");
                    }
                }
            }

            // 解析事件
            if (type.HasEvents)
            {
                foreach (var evt in type.Events)
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
                            ReturnType = evt.EventType.FullName,
                            ContentHash = ComputeHash(evt.FullName)
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
        }

        /// <summary>
        /// 确定类型的元素类型
        /// </summary>
        private ElementType DetermineElementType(TypeDefinition type)
        {
            if (type.IsClass)
            {
                if (type.BaseType?.FullName == "System.MulticastDelegate" || 
                    type.BaseType?.FullName == "System.Delegate")
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
        /// 确定类型的访问修饰符
        /// </summary>
        private string DetermineAccessModifier(TypeDefinition type)
        {
            if (type.IsNestedPublic || type.IsPublic)
                return "public";
            if (type.IsNestedPrivate)
                return "private";
            if (type.IsNestedFamily)
                return "protected";
            if (type.IsNestedAssembly)
                return "internal";
            if (type.IsNestedFamilyOrAssembly)
                return "protected internal";
            if (type.IsNestedFamilyAndAssembly)
                return "private protected";
            return "internal"; // 默认为internal
        }

        /// <summary>
        /// 确定方法的访问修饰符
        /// </summary>
        private string DetermineAccessModifier(MethodDefinition method)
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

        /// <summary>
        /// 确定属性的访问修饰符
        /// </summary>
        private string DeterminePropertyAccessModifier(PropertyDefinition property)
        {
            var getMethod = property.GetMethod;
            var setMethod = property.SetMethod;

            if (getMethod != null && setMethod != null)
            {
                // 使用最宽松的访问级别
                var getAccess = DetermineAccessModifier(getMethod);
                var setAccess = DetermineAccessModifier(setMethod);
                return GetMostPermissiveAccessModifier(getAccess, setAccess);
            }
            else if (getMethod != null)
            {
                return DetermineAccessModifier(getMethod);
            }
            else if (setMethod != null)
            {
                return DetermineAccessModifier(setMethod);
            }

            return "private"; // 默认为private
        }

        /// <summary>
        /// 确定字段的访问修饰符
        /// </summary>
        private string DetermineAccessModifier(FieldDefinition field)
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

        /// <summary>
        /// 确定事件的访问修饰符
        /// </summary>
        private string DetermineAccessModifier(EventDefinition evt)
        {
            if (evt.AddMethod != null)
                return DetermineAccessModifier(evt.AddMethod);
            return "private"; // 默认为private
        }

        /// <summary>
        /// 获取最宽松的访问修饰符
        /// </summary>
        private string GetMostPermissiveAccessModifier(string modifier1, string modifier2)
        {
            var modifierRank = new Dictionary<string, int>
            {
                { "public", 5 },
                { "protected internal", 4 },
                { "internal", 3 },
                { "protected", 2 },
                { "private protected", 1 },
                { "private", 0 }
            };

            if (modifierRank.TryGetValue(modifier1, out int rank1) && 
                modifierRank.TryGetValue(modifier2, out int rank2))
            {
                return rank1 >= rank2 ? modifier1 : modifier2;
            }

            return "private"; // 默认为private
        }

        /// <summary>
        /// 检查是否为Unity类型
        /// </summary>
        private bool IsUnityType(TypeDefinition type)
        {
            // 检查是否继承自UnityEngine.Object
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.FullName == "UnityEngine.Object" || 
                    baseType.FullName == "UnityEngine.MonoBehaviour" || 
                    baseType.FullName == "UnityEngine.ScriptableObject")
                {
                    return true;
                }
                
                try
                {
                    if (baseType.Resolve() != null)
                    {
                        baseType = baseType.Resolve().BaseType;
                    }
                    else
                    {
                        break;
                    }
                }
                catch
                {
                    break;
                }
            }

            // 检查是否有Unity特有属性
            return type.CustomAttributes.Any(attr => 
                attr.AttributeType.FullName.StartsWith("UnityEngine.") || 
                attr.AttributeType.FullName.StartsWith("UnityEditor."));
        }

        /// <summary>
        /// 检查是否为Unity生命周期方法
        /// </summary>
        private bool IsUnityMethod(MethodDefinition method)
        {
            // Unity常见的生命周期方法
            var unityLifecycleMethods = new HashSet<string>
            {
                "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
                "OnEnable", "OnDisable", "OnDestroy", "OnApplicationQuit",
                "OnApplicationPause", "OnApplicationFocus",
                "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit",
                "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
                "OnMouseDown", "OnMouseUp", "OnMouseEnter", "OnMouseExit", "OnMouseOver",
                "OnDrawGizmos", "OnDrawGizmosSelected",
                "OnAnimatorMove", "OnAnimatorIK",
                "OnValidate", "Reset"
            };

            // 检查方法名是否为Unity生命周期方法
            if (unityLifecycleMethods.Contains(method.Name))
            {
                return true;
            }

            // 检查是否有Unity特有属性
            return method.CustomAttributes.Any(attr => 
                attr.AttributeType.FullName.StartsWith("UnityEngine.") || 
                attr.AttributeType.FullName.StartsWith("UnityEditor."));
        }

        /// <summary>
        /// 检查是否有Unity特有属性
        /// </summary>
        private bool HasUnityAttribute(IMemberDefinition member)
        {
            return member.CustomAttributes.Any(attr => 
                attr.AttributeType.FullName == "UnityEngine.SerializeField" ||
                attr.AttributeType.FullName == "UnityEngine.HideInInspector" ||
                attr.AttributeType.FullName.StartsWith("UnityEngine.") ||
                attr.AttributeType.FullName.StartsWith("UnityEditor."));
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