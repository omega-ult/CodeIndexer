using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CodeIndexer.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace CodeIndexer.Core.Parsing
{
    /// <summary>
    /// C#代码解析器，负责解析代码文件并提取代码元素
    /// </summary>
    public class CodeParser
    {
        private readonly ILogger _logger;

        public CodeParser(ILogger logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解析指定目录下的所有C#代码文件
        /// </summary>
        /// <param name="directoryPath">代码目录路径</param>
        /// <returns>解析出的代码元素集合</returns>
        public async Task<CodeDatabase> ParseDirectoryAsync(string directoryPath)
        {
            _logger?.LogInformation($"开始解析目录: {directoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directoryPath}");
            }

            var codeDatabase = new CodeDatabase();
            var csharpFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);

            _logger?.LogInformation($"找到 {csharpFiles.Length} 个C#文件");

            foreach (var filePath in csharpFiles)
            {
                try
                {
                    var fileElements = await ParseFileAsync(filePath);
                    codeDatabase.AddElements(fileElements);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"解析文件 {filePath} 时出错");
                }
            }

            _logger?.LogInformation($"解析完成，共提取 {codeDatabase.GetAllElements().Count} 个代码元素");
            return codeDatabase;
        }

        /// <summary>
        /// 解析单个C#代码文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>解析出的代码元素集合</returns>
        public async Task<List<CodeElement>> ParseFileAsync(string filePath)
        {
            _logger?.LogDebug($"开始解析文件: {filePath}");

            var elements = new List<CodeElement>();
            var sourceText = await File.ReadAllTextAsync(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = await syntaxTree.GetRootAsync();
            var compilation = CSharpCompilation.Create("CodeAnalysis")
                .AddSyntaxTrees(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // 解析命名空间
            var namespaceDeclarations = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
            foreach (var namespaceDeclaration in namespaceDeclarations)
            {
                var namespaceElement = ParseNamespace(namespaceDeclaration, semanticModel, filePath);
                elements.Add(namespaceElement);

                // 解析命名空间中的类型
                var typeDeclarations = namespaceDeclaration.DescendantNodes()
                    .Where(n => n is ClassDeclarationSyntax || n is InterfaceDeclarationSyntax || 
                                n is StructDeclarationSyntax || n is EnumDeclarationSyntax);

                foreach (var typeDeclaration in typeDeclarations)
                {
                    var typeElement = ParseType(typeDeclaration, semanticModel, filePath, namespaceElement.Id);
                    if (typeElement != null)
                    {
                        elements.Add(typeElement);
                        namespaceElement.TypeIds.Add(typeElement.Id);

                        // 解析类型中的成员
                        var memberElements = ParseMembers(typeDeclaration, semanticModel, filePath, typeElement.Id);
                        elements.AddRange(memberElements);
                        typeElement.MemberIds.AddRange(memberElements.Select(m => m.Id));
                    }
                }
            }

            // 处理文件级别的类型声明（C# 10+）
            var fileTypeDeclarations = root.DescendantNodes()
                .Where(n => n.Parent is CompilationUnitSyntax && 
                           (n is ClassDeclarationSyntax || n is InterfaceDeclarationSyntax || 
                            n is StructDeclarationSyntax || n is EnumDeclarationSyntax));

            foreach (var typeDeclaration in fileTypeDeclarations)
            {
                var typeElement = ParseType(typeDeclaration, semanticModel, filePath, null);
                if (typeElement != null)
                {
                    elements.Add(typeElement);

                    // 解析类型中的成员
                    var memberElements = ParseMembers(typeDeclaration, semanticModel, filePath, typeElement.Id);
                    elements.AddRange(memberElements);
                    typeElement.MemberIds.AddRange(memberElements.Select(m => m.Id));
                }
            }

            _logger?.LogDebug($"文件 {filePath} 解析完成，提取 {elements.Count} 个代码元素");
            return elements;
        }

        /// <summary>
        /// 解析命名空间
        /// </summary>
        private NamespaceElement ParseNamespace(NamespaceDeclarationSyntax namespaceDeclaration, SemanticModel semanticModel, string filePath)
        {
            var namespaceSymbol = semanticModel.GetDeclaredSymbol(namespaceDeclaration);
            var namespaceElement = new NamespaceElement
            {
                Name = namespaceDeclaration.Name.ToString(),
                FullName = namespaceSymbol?.ToDisplayString() ?? namespaceDeclaration.Name.ToString(),
                Location = GetSourceLocation(namespaceDeclaration.GetLocation(), filePath),
                ContentHash = ComputeHash(namespaceDeclaration.ToString())
            };

            return namespaceElement;
        }

        /// <summary>
        /// 解析类型（类、接口、结构体、枚举）
        /// </summary>
        private TypeElement? ParseType(SyntaxNode typeDeclaration, SemanticModel semanticModel, string filePath, string? parentId)
        {
            ElementType elementType;
            INamedTypeSymbol? typeSymbol = null;

            if (typeDeclaration is ClassDeclarationSyntax)
            {
                elementType = ElementType.Class;
                typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration as ClassDeclarationSyntax);
            }
            else if (typeDeclaration is InterfaceDeclarationSyntax)
            {
                elementType = ElementType.Interface;
                typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration as InterfaceDeclarationSyntax);
            }
            else if (typeDeclaration is StructDeclarationSyntax)
            {
                elementType = ElementType.Struct;
                typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration as StructDeclarationSyntax);
            }
            else if (typeDeclaration is EnumDeclarationSyntax)
            {
                elementType = ElementType.Enum;
                typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration as EnumDeclarationSyntax);
            }
            else
            {
                return null;
            }

            var typeElement = new TypeElement(elementType)
            {
                Name = GetTypeName(typeDeclaration),
                FullName = typeSymbol?.ToDisplayString() ?? GetTypeName(typeDeclaration),
                Location = GetSourceLocation(typeDeclaration.GetLocation(), filePath),
                ParentId = parentId,
                ContentHash = ComputeHash(typeDeclaration.ToString()),
                AccessModifier = GetAccessModifier(typeDeclaration),
                Modifiers = GetModifiers(typeDeclaration),
                Documentation = GetDocumentation(typeDeclaration)
            };

            if (typeSymbol != null)
            {
                // 设置类型特性
                if (typeElement.ElementType == ElementType.Class)
                {
                    typeElement.IsAbstract = typeSymbol.IsAbstract && !typeSymbol.IsStatic;
                    typeElement.IsStatic = typeSymbol.IsStatic;
                    typeElement.IsSealed = typeSymbol.IsSealed && !typeSymbol.IsStatic;
                }

                // 设置基类
                if (typeSymbol.BaseType != null && typeSymbol.BaseType.Name != "Object" && typeSymbol.BaseType.Name != "ValueType")
                {
                    // 注意：这里只存储基类的全名，实际的引用关系需要在后续处理中建立
                    typeElement.BaseTypeId = typeSymbol.BaseType.ToDisplayString();
                }

                // 设置实现的接口
                foreach (var interfaceSymbol in typeSymbol.Interfaces)
                {
                    // 同样，这里只存储接口的全名
                    typeElement.ImplementedInterfaceIds.Add(interfaceSymbol.ToDisplayString());
                }

                // 设置泛型参数
                foreach (var typeParameter in typeSymbol.TypeParameters)
                {
                    typeElement.GenericParameters.Add(typeParameter.Name);

                    // 设置泛型约束
                    var constraints = new List<string>();
                    if (typeParameter.HasReferenceTypeConstraint)
                        constraints.Add("class");
                    if (typeParameter.HasValueTypeConstraint)
                        constraints.Add("struct");
                    if (typeParameter.HasConstructorConstraint)
                        constraints.Add("new()");

                    foreach (var constraintType in typeParameter.ConstraintTypes)
                    {
                        constraints.Add(constraintType.ToDisplayString());
                    }

                    if (constraints.Any())
                    {
                        typeElement.GenericConstraints[typeParameter.Name] = constraints;
                    }
                }
            }

            // 检查是否为部分类
            if (typeDeclaration is ClassDeclarationSyntax classDeclaration)
            {
                typeElement.IsPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            }

            return typeElement;
        }

        /// <summary>
        /// 解析类型成员（方法、属性、字段等）
        /// </summary>
        private List<MemberElement> ParseMembers(SyntaxNode typeDeclaration, SemanticModel semanticModel, string filePath, string parentId)
        {
            var members = new List<MemberElement>();

            // 解析方法
            var methodDeclarations = typeDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodDeclaration in methodDeclarations)
            {
                var methodElement = ParseMethod(methodDeclaration, semanticModel, filePath, parentId);
                members.Add(methodElement);
            }

            // 解析属性
            var propertyDeclarations = typeDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            foreach (var propertyDeclaration in propertyDeclarations)
            {
                var propertyElement = ParseProperty(propertyDeclaration, semanticModel, filePath, parentId);
                members.Add(propertyElement);
            }

            // 解析字段
            var fieldDeclarations = typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var fieldDeclaration in fieldDeclarations)
            {
                var fieldElements = ParseField(fieldDeclaration, semanticModel, filePath, parentId);
                members.AddRange(fieldElements);
            }

            // 解析构造函数
            var constructorDeclarations = typeDeclaration.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
            foreach (var constructorDeclaration in constructorDeclarations)
            {
                var constructorElement = ParseConstructor(constructorDeclaration, semanticModel, filePath, parentId);
                members.Add(constructorElement);
            }

            // 解析事件
            var eventDeclarations = typeDeclaration.DescendantNodes().OfType<EventDeclarationSyntax>();
            foreach (var eventDeclaration in eventDeclarations)
            {
                var eventElement = ParseEvent(eventDeclaration, semanticModel, filePath, parentId);
                members.Add(eventElement);
            }

            // 解析枚举成员
            if (typeDeclaration is EnumDeclarationSyntax enumDeclaration)
            {
                var enumMembers = ParseEnumMembers(enumDeclaration, semanticModel, filePath, parentId);
                members.AddRange(enumMembers);
            }

            return members;
        }

        /// <summary>
        /// 解析方法
        /// </summary>
        private MemberElement ParseMethod(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, string filePath, string parentId)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
            var methodElement = new MemberElement(ElementType.Method)
            {
                Name = methodDeclaration.Identifier.Text,
                FullName = methodSymbol?.ToDisplayString() ?? methodDeclaration.Identifier.Text,
                Type = methodDeclaration.ReturnType.ToString(),
                Location = GetSourceLocation(methodDeclaration.GetLocation(), filePath),
                ParentId = parentId,
                ContentHash = ComputeHash(methodDeclaration.ToString()),
                AccessModifier = GetAccessModifier(methodDeclaration),
                Modifiers = GetModifiers(methodDeclaration),
                Documentation = GetDocumentation(methodDeclaration),
                IsVirtual = methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)),
                IsAbstract = methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
                IsStatic = methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                IsOverride = methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)),
                IsAsync = methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword))
            };

            // 解析参数
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                var parameterInfo = new ParameterInfo
                {
                    Name = parameter.Identifier.Text,
                    Type = parameter.Type?.ToString() ?? "var",
                    HasDefaultValue = parameter.Default != null,
                    DefaultValue = parameter.Default?.Value.ToString()
                };

                // 设置参数修饰符
                if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword)))
                    parameterInfo.Modifier = "ref";
                else if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword)))
                    parameterInfo.Modifier = "out";
                else if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.InKeyword)))
                    parameterInfo.Modifier = "in";
                else if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.ParamsKeyword)))
                    parameterInfo.Modifier = "params";

                methodElement.Parameters.Add(parameterInfo);
            }

            // 解析泛型参数
            if (methodDeclaration.TypeParameterList != null)
            {
                foreach (var typeParameter in methodDeclaration.TypeParameterList.Parameters)
                {
                    methodElement.GenericParameters.Add(typeParameter.Identifier.Text);
                }
            }

            // 检查是否为扩展方法
            if (methodSymbol != null && methodSymbol.IsExtensionMethod)
            {
                methodElement.IsExtension = true;
            }

            return methodElement;
        }

        /// <summary>
        /// 解析属性
        /// </summary>
        private MemberElement ParseProperty(PropertyDeclarationSyntax propertyDeclaration, SemanticModel semanticModel, string filePath, string parentId)
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration);
            var propertyElement = new MemberElement(ElementType.Property)
            {
                Name = propertyDeclaration.Identifier.Text,
                FullName = propertySymbol?.ToDisplayString() ?? propertyDeclaration.Identifier.Text,
                Type = propertyDeclaration.Type.ToString(),
                Location = GetSourceLocation(propertyDeclaration.GetLocation(), filePath),
                ParentId = parentId,
                ContentHash = ComputeHash(propertyDeclaration.ToString()),
                AccessModifier = GetAccessModifier(propertyDeclaration),
                Modifiers = GetModifiers(propertyDeclaration),
                Documentation = GetDocumentation(propertyDeclaration),
                IsVirtual = propertyDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)),
                IsAbstract = propertyDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
                IsStatic = propertyDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                IsOverride = propertyDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            };

            return propertyElement;
        }

        /// <summary>
        /// 解析字段
        /// </summary>
        private List<MemberElement> ParseField(FieldDeclarationSyntax fieldDeclaration, SemanticModel semanticModel, string filePath, string parentId)
        {
            var fieldElements = new List<MemberElement>();
            var fieldType = fieldDeclaration.Declaration.Type.ToString();
            var accessModifier = GetAccessModifier(fieldDeclaration);
            var modifiers = GetModifiers(fieldDeclaration);
            var documentation = GetDocumentation(fieldDeclaration);
            var isStatic = fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

            // 一个字段声明可能包含多个变量
            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                var fieldElement = new MemberElement(ElementType.Field)
                {
                    Name = variable.Identifier.Text,
                    FullName = fieldSymbol?.ToDisplayString() ?? variable.Identifier.Text,
                    Type = fieldType,
                    Location = GetSourceLocation(variable.GetLocation(), filePath),
                    ParentId = parentId,
                    ContentHash = ComputeHash(variable.ToString()),
                    AccessModifier = accessModifier,
                    Modifiers = modifiers,
                    Documentation = documentation,
                    IsStatic = isStatic
                };

                fieldElements.Add(fieldElement);
            }

            return fieldElements;
        }

        /// <summary>
        /// 解析构造函数
        /// </summary>
        private MemberElement ParseConstructor(ConstructorDeclarationSyntax constructorDeclaration, SemanticModel semanticModel, string filePath, string parentId)
        {
            var constructorSymbol = semanticModel.GetDeclaredSymbol(constructorDeclaration);
            var constructorElement = new MemberElement(ElementType.Constructor)
            {
                Name = constructorDeclaration.Identifier.Text,
                FullName = constructorSymbol?.ToDisplayString() ?? constructorDeclaration.Identifier.Text,
                Location = GetSourceLocation(constructorDeclaration.GetLocation(), filePath),
                ParentId = parentId,
                ContentHash = ComputeHash(constructorDeclaration.ToString()),
                AccessModifier = GetAccessModifier(constructorDeclaration),
                Modifiers = GetModifiers(constructorDeclaration),
                Documentation = GetDocumentation(constructorDeclaration),
                IsStatic = constructorDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
            };

            // 解析参数
            foreach (var parameter in constructorDeclaration.ParameterList.Parameters)
            {
                var parameterInfo = new ParameterInfo
                {
                    Name = parameter.Identifier.Text,
                    Type = parameter.Type?.ToString() ?? "var",
                    HasDefaultValue = parameter.Default != null,
                    DefaultValue = parameter.Default?.Value.ToString()
                };

                // 设置参数修饰符
                if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword)))
                    parameterInfo.Modifier = "ref";
                else if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword)))
                    parameterInfo.Modifier = "out";
                else if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.InKeyword)))
                    parameterInfo.Modifier = "in";
                else if (parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.ParamsKeyword)))
                    parameterInfo.Modifier = "params";

                constructorElement.Parameters.Add(parameterInfo);
            }

            return constructorElement;
        }

        /// <summary>
        /// 解析事件
        /// </summary>
        private MemberElement ParseEvent(EventDeclarationSyntax eventDeclaration, SemanticModel semanticModel, string filePath, string parentId)
        {
            var eventSymbol = semanticModel.GetDeclaredSymbol(eventDeclaration);
            var eventElement = new MemberElement(ElementType.Event)
            {
                Name = eventDeclaration.Identifier.Text,
                FullName = eventSymbol?.ToDisplayString() ?? eventDeclaration.Identifier.Text,
                Type = eventDeclaration.Type.ToString(),
                Location = GetSourceLocation(eventDeclaration.GetLocation(), filePath),
                ParentId = parentId,
                ContentHash = ComputeHash(eventDeclaration.ToString()),
                AccessModifier = GetAccessModifier(eventDeclaration),
                Modifiers = GetModifiers(eventDeclaration),
                Documentation = GetDocumentation(eventDeclaration),
                IsVirtual = eventDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)),
                IsAbstract = eventDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
                IsStatic = eventDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                IsOverride = eventDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            };

            return eventElement;
        }

        /// <summary>
        /// 解析枚举成员
        /// </summary>
        private List<MemberElement> ParseEnumMembers(EnumDeclarationSyntax enumDeclaration, SemanticModel semanticModel, string filePath, string parentId)
        {
            var enumMembers = new List<MemberElement>();

            foreach (var member in enumDeclaration.Members)
            {
                var enumMemberSymbol = semanticModel.GetDeclaredSymbol(member);
                var enumMemberElement = new MemberElement(ElementType.EnumMember)
                {
                    Name = member.Identifier.Text,
                    FullName = enumMemberSymbol?.ToDisplayString() ?? member.Identifier.Text,
                    Location = GetSourceLocation(member.GetLocation(), filePath),
                    ParentId = parentId,
                    ContentHash = ComputeHash(member.ToString()),
                    Documentation = GetDocumentation(member),
                    AccessModifier = "public" // 枚举成员总是公共的
                };

                enumMembers.Add(enumMemberElement);
            }

            return enumMembers;
        }

        /// <summary>
        /// 获取源代码位置信息
        /// </summary>
        private SourceLocation GetSourceLocation(Location location, string filePath)
        {
            var lineSpan = location.GetLineSpan();
            return new SourceLocation
            {
                FilePath = filePath,
                StartLine = lineSpan.StartLinePosition.Line + 1, // 转换为1-based
                StartColumn = lineSpan.StartLinePosition.Character + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character + 1
            };
        }

        /// <summary>
        /// 获取类型名称
        /// </summary>
        private string GetTypeName(SyntaxNode typeDeclaration)
        {
            if (typeDeclaration is ClassDeclarationSyntax classDeclaration)
                return classDeclaration.Identifier.Text;
            else if (typeDeclaration is InterfaceDeclarationSyntax interfaceDeclaration)
                return interfaceDeclaration.Identifier.Text;
            else if (typeDeclaration is StructDeclarationSyntax structDeclaration)
                return structDeclaration.Identifier.Text;
            else if (typeDeclaration is EnumDeclarationSyntax enumDeclaration)
                return enumDeclaration.Identifier.Text;
            else
                return string.Empty;
        }

        /// <summary>
        /// 获取访问修饰符
        /// </summary>
        private string GetAccessModifier(SyntaxNode node)
        {
            if (node is MemberDeclarationSyntax memberDeclaration)
            {
                if (memberDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                    return "public";
                else if (memberDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                    return "private";
                else if (memberDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
                {
                    if (memberDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
                        return "protected internal";
                    else
                        return "protected";
                }
                else if (memberDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
                    return "internal";
                else
                    return "private"; // 默认为私有
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取修饰符列表
        /// </summary>
        private List<string> GetModifiers(SyntaxNode node)
        {
            var modifiers = new List<string>();

            if (node is MemberDeclarationSyntax memberDeclaration)
            {
                foreach (var modifier in memberDeclaration.Modifiers)
                {
                    // 排除访问修饰符，因为它们已经在AccessModifier属性中
                    if (!modifier.IsKind(SyntaxKind.PublicKeyword) &&
                        !modifier.IsKind(SyntaxKind.PrivateKeyword) &&
                        !modifier.IsKind(SyntaxKind.ProtectedKeyword) &&
                        !modifier.IsKind(SyntaxKind.InternalKeyword))
                    {
                        modifiers.Add(modifier.Text);
                    }
                }
            }

            return modifiers;
        }

        /// <summary>
        /// 获取文档注释
        /// </summary>
        private string GetDocumentation(SyntaxNode node)
        {
            var trivia = node.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                    t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                return trivia.ToString().Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// 计算内容的哈希值，用于检测变更
        /// </summary>
        private string ComputeHash(string content)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}