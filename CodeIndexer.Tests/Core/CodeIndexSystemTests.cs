using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeIndexer.Core.Indexing;
using CodeIndexer.Core.Models;
using CodeIndexer.Core.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeIndexer.Tests.Core
{
    [TestClass]
    public class CodeIndexSystemTests
    {
        private readonly ILogger _logger;
        private readonly string _testRootDirectory;
        private readonly string _testSourceDirectory;
        private readonly string _testIndexDirectory;

        public CodeIndexSystemTests()
        {
            _logger = NullLogger.Instance;
            // 创建测试目录结构
            _testRootDirectory = Path.Combine(Path.GetTempPath(), "CodeIndexSystemTests", Guid.NewGuid().ToString());
            _testSourceDirectory = Path.Combine(_testRootDirectory, "Source");
            _testIndexDirectory = Path.Combine(_testRootDirectory, "Index");
            
            Directory.CreateDirectory(_testSourceDirectory);
            Directory.CreateDirectory(_testIndexDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // 测试完成后清理测试目录
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, true);
            }
        }

        [TestMethod]
        public async Task IndexAndSearch_ComplexProject_CorrectResults()
        {
            // Arrange - 创建一个包含多种C#元素的复杂项目
            CreateComplexTestProject();

            // Act - 执行完整的索引和搜索流程
            // 1. 解析代码
            var parser = new CodeParser(_logger);
            var codeDatabase = await parser.ParseDirectoryAsync(_testSourceDirectory);
            
            // 2. 建立索引
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(codeDatabase);
            
            // 3. 执行各种高级查询
            // 3.1 查询所有公共方法
            var publicMethodsQuery = new CodeElementQuery
            {
                ElementType = ElementType.Method,
                AccessModifier = "public"
            };
            var publicMethods = indexer.AdvancedSearch(publicMethodsQuery, 100);
            
            // 3.2 查询所有私有属性
            var privatePropertiesQuery = new CodeElementQuery
            {
                ElementType = ElementType.Property,
                AccessModifier = "private"
            };
            var privateProperties = indexer.AdvancedSearch(privatePropertiesQuery, 100);
            
            // 3.3 查询特定返回类型的方法
            var stringMethodsQuery = new CodeElementQuery
            {
                ElementType = ElementType.Method,
                ReturnType = "string"
            };
            var stringMethods = indexer.AdvancedSearch(stringMethodsQuery, 100);

            // Assert
            // 验证解析结果
            Assert.IsNotNull(codeDatabase);
            Assert.IsTrue(codeDatabase.Namespaces.Count > 0);
            Assert.IsTrue(codeDatabase.Types.Count > 0);
            Assert.IsTrue(codeDatabase.Members.Count > 0);
            
            // 验证高级查询结果
            Assert.IsTrue(publicMethods.Count > 0);
            Assert.IsTrue(privateProperties.Count > 0);
            Assert.IsTrue(stringMethods.Count > 0);
            
            // 验证所有公共方法确实是公共的
            Assert.IsTrue(publicMethods.All(m => m.AccessModifier == "public"));
            
            // 验证所有私有属性确实是私有的
            Assert.IsTrue(privateProperties.All(p => p.AccessModifier == "private"));
            
            // 验证所有string返回类型的方法
            foreach (var method in stringMethods)
            {
                var fullMethod = indexer.GetElementById(method.Id) as MemberElement;
                Assert.IsNotNull(fullMethod);
                Assert.AreEqual("string", fullMethod.Type);
            }
        }

        [TestMethod]
        public async Task IndexAndSearch_DocumentationComments_CorrectlyIndexed()
        {
            // Arrange - 创建带有文档注释的测试代码
            CreateDocumentedTestCode();

            // Act
            var parser = new CodeParser(_logger);
            var codeDatabase = await parser.ParseDirectoryAsync(_testSourceDirectory);
            
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(codeDatabase);
            
            // 查找带有文档注释的类
            var classResults = indexer.SearchByFullName("DocumentedNamespace.DocumentedClass");
            var methodResults = indexer.SearchByName("DocumentedMethod", 10);

            // Assert
            Assert.AreEqual(1, classResults.Count);
            Assert.AreEqual(1, methodResults.Count);
            
            // 获取详细信息验证文档注释
            var documentedClass = indexer.GetElementById(classResults[0].Id);
            var documentedMethod = indexer.GetElementById(methodResults[0].Id);
            
            Assert.IsNotNull(documentedClass);
            Assert.IsNotNull(documentedMethod);
            
            // 验证文档注释被正确解析和索引
            Assert.IsTrue(!string.IsNullOrEmpty(documentedClass.Documentation));
            Assert.IsTrue(!string.IsNullOrEmpty(documentedMethod.Documentation));
            Assert.IsTrue(documentedClass.Documentation.Contains("测试文档注释"));
            Assert.IsTrue(documentedMethod.Documentation.Contains("带参数的文档化方法"));
        }

        [TestMethod]
        public async Task IndexAndSearch_NestedTypes_CorrectlyIndexed()
        {
            // Arrange - 创建带有嵌套类型的测试代码
            CreateNestedTypesTestCode();

            // Act
            var parser = new CodeParser(_logger);
            var codeDatabase = await parser.ParseDirectoryAsync(_testSourceDirectory);
            
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(codeDatabase);
            
            // 查找外部类和嵌套类
            var outerClassResults = indexer.SearchByFullName("NestedNamespace.OuterClass");
            var nestedClassResults = indexer.SearchByName("NestedClass", 10);
            var nestedEnumResults = indexer.SearchByName("NestedEnum", 10);

            // Assert
            Assert.AreEqual(1, outerClassResults.Count);
            Assert.IsTrue(nestedClassResults.Count > 0);
            Assert.IsTrue(nestedEnumResults.Count > 0);
            
            // 获取外部类详细信息
            var outerClass = indexer.GetElementById(outerClassResults[0].Id) as TypeElement;
            Assert.IsNotNull(outerClass);
            
            // 验证嵌套类型的父子关系
            var nestedClass = indexer.GetElementById(nestedClassResults[0].Id) as TypeElement;
            Assert.IsNotNull(nestedClass);
            Assert.AreEqual(outerClass.Id, nestedClass.ParentId);
        }

        #region 辅助方法

        /// <summary>
        /// 创建复杂的测试项目，包含各种C#元素和关系
        /// </summary>
        private void CreateComplexTestProject()
        {
            // 创建一个包含多种访问修饰符和返回类型的类
            var complexClassContent = @"using System;
using System.Collections.Generic;

namespace ComplexProject
{
    /// <summary>
    /// 复杂测试类
    /// </summary>
    public class ComplexClass
    {
        // 私有字段
        private int _privateField;
        
        // 私有属性
        private string _privateProperty { get; set; }
        
        // 公共属性
        public int PublicProperty { get; set; }
        
        // 受保护的属性
        protected bool ProtectedProperty { get; set; }
        
        // 内部属性
        internal DateTime InternalProperty { get; set; }
        
        // 构造函数
        public ComplexClass()
        {
            _privateField = 0;
            _privateProperty = string.Empty;
        }
        
        // 公共方法，返回void
        public void PublicVoidMethod()
        {
            Console.WriteLine(""Public void method"");
        }
        
        // 公共方法，返回string
        public string PublicStringMethod()
        {
            return ""Public string method"";
        }
        
        // 私有方法，返回int
        private int PrivateIntMethod()
        {
            return _privateField;
        }
        
        // 受保护的方法，返回string
        protected string ProtectedStringMethod()
        {
            return ""Protected string method"";
        }
        
        // 内部方法，返回bool
        internal bool InternalBoolMethod()
        {
            return true;
        }
        
        // 静态方法
        public static void StaticMethod()
        {
            Console.WriteLine(""Static method"");
        }
    }
}";

            // 创建一个接口
            var interfaceContent = @"namespace ComplexProject
{
    /// <summary>
    /// 复杂接口
    /// </summary>
    public interface IComplexInterface
    {
        string InterfaceProperty { get; set; }
        
        void InterfaceMethod();
        
        int CalculateValue(int input);
    }
}";

            // 创建一个实现接口的类
            var implementationContent = @"namespace ComplexProject
{
    /// <summary>
    /// 接口实现类
    /// </summary>
    public class ImplementationClass : IComplexInterface
    {
        public string InterfaceProperty { get; set; }
        
        public void InterfaceMethod()
        {
            // 实现
        }
        
        public int CalculateValue(int input)
        {
            return input * 2;
        }
    }
}";

            // 写入文件
            File.WriteAllText(Path.Combine(_testSourceDirectory, "ComplexClass.cs"), complexClassContent);
            File.WriteAllText(Path.Combine(_testSourceDirectory, "IComplexInterface.cs"), interfaceContent);
            File.WriteAllText(Path.Combine(_testSourceDirectory, "ImplementationClass.cs"), implementationContent);
        }

        /// <summary>
        /// 创建带有详细文档注释的测试代码
        /// </summary>
        private void CreateDocumentedTestCode()
        {
            var documentedCodeContent = @"using System;

namespace DocumentedNamespace
{
    /// <summary>
    /// 这是一个测试文档注释的类
    /// </summary>
    /// <remarks>
    /// 这个类用于演示XML文档注释如何被解析和索引
    /// </remarks>
    public class DocumentedClass
    {
        /// <summary>
        /// 文档化的属性
        /// </summary>
        /// <value>属性的值描述</value>
        public string DocumentedProperty { get; set; }
        
        /// <summary>
        /// 带参数的文档化方法
        /// </summary>
        /// <param name=""input"">输入参数</param>
        /// <returns>返回处理后的字符串</returns>
        /// <exception cref=""ArgumentNullException"">当输入为null时抛出</exception>
        public string DocumentedMethod(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
                
            return $""Processed: {input}"";
        }
    }
}";

            File.WriteAllText(Path.Combine(_testSourceDirectory, "DocumentedClass.cs"), documentedCodeContent);
        }

        /// <summary>
        /// 创建带有嵌套类型的测试代码
        /// </summary>
        private void CreateNestedTypesTestCode()
        {
            var nestedTypesContent = @"namespace NestedNamespace
{
    /// <summary>
    /// 外部类
    /// </summary>
    public class OuterClass
    {
        /// <summary>
        /// 嵌套类
        /// </summary>
        public class NestedClass
        {
            public string NestedProperty { get; set; }
            
            public void NestedMethod()
            {
                // 实现
            }
        }
        
        /// <summary>
        /// 嵌套枚举
        /// </summary>
        public enum NestedEnum
        {
            First,
            Second,
            Third
        }
        
        // 外部类的成员
        public NestedClass CreateNested()
        {
            return new NestedClass();
        }
        
        public NestedEnum DefaultEnum { get; } = NestedEnum.First;
    }
}";

            File.WriteAllText(Path.Combine(_testSourceDirectory, "NestedTypes.cs"), nestedTypesContent);
        }

        #endregion
    }
}