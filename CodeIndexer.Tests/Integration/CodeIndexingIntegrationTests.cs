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

namespace CodeIndexer.Tests.Integration
{
    [TestClass]
    public class CodeIndexingIntegrationTests
    {
        private readonly ILogger _logger;
        private readonly string _testRootDirectory;
        private readonly string _testSourceDirectory;
        private readonly string _testIndexDirectory;

        public CodeIndexingIntegrationTests()
        {
            _logger = NullLogger.Instance;
            // 创建测试目录结构
            _testRootDirectory = Path.Combine(Path.GetTempPath(), "CodeIndexerIntegrationTests", Guid.NewGuid().ToString());
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
        public async Task FullIndexingProcess_SimpleProject_SuccessfullyIndexed()
        {
            // Arrange - 创建测试代码文件
            CreateTestCodeFiles();

            // Act - 执行完整的索引流程
            // 1. 解析代码
            var parser = new CodeParser(_logger);
            var codeDatabase = await parser.ParseDirectoryAsync(_testSourceDirectory);
            
            // 2. 建立索引
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(codeDatabase);
            
            // 3. 执行各种查询
            var classResults = indexer.SearchByElementType(ElementType.Class, 100);
            var methodResults = indexer.SearchByElementType(ElementType.Method, 100);
            var propertyResults = indexer.SearchByElementType(ElementType.Property, 100);
            var nameResults = indexer.SearchByName("Test", 100);

            // Assert
            // 验证解析结果
            Assert.IsNotNull(codeDatabase);
            Assert.IsTrue(codeDatabase.Namespaces.Count > 0);
            Assert.IsTrue(codeDatabase.Types.Count > 0);
            Assert.IsTrue(codeDatabase.Members.Count > 0);
            
            // 验证索引查询结果
            Assert.IsTrue(classResults.Count > 0);
            Assert.IsTrue(methodResults.Count > 0);
            Assert.IsTrue(propertyResults.Count > 0);
            Assert.IsTrue(nameResults.Count > 0);
            
            // 验证具体元素
            Assert.IsTrue(classResults.Any(c => c.Name == "TestClass"));
            Assert.IsTrue(methodResults.Any(m => m.Name == "TestMethod"));
            Assert.IsTrue(propertyResults.Any(p => p.Name == "TestProperty"));
        }

        [TestMethod]
        public async Task FullIndexingProcess_MultipleFiles_CorrectRelationshipsPreserved()
        {
            // Arrange - 创建多个相关联的测试代码文件
            CreateMultiFileTestProject();

            // Act - 执行完整的索引流程
            var parser = new CodeParser(_logger);
            var codeDatabase = await parser.ParseDirectoryAsync(_testSourceDirectory);
            
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(codeDatabase);
            
            // 查找基类和派生类
            var baseClassResults = indexer.SearchByFullName("TestProject.BaseClass");
            var derivedClassResults = indexer.SearchByFullName("TestProject.DerivedClass");
            
            // 查找接口和实现类
            var interfaceResults = indexer.SearchByFullName("TestProject.ITestInterface");
            var implementationResults = indexer.SearchByFullName("TestProject.ImplementationClass");

            // Assert
            Assert.IsNotNull(codeDatabase);
            
            // 验证基类和派生类关系
            Assert.AreEqual(1, baseClassResults.Count);
            Assert.AreEqual(1, derivedClassResults.Count);
            
            // 验证接口和实现类
            Assert.AreEqual(1, interfaceResults.Count);
            Assert.AreEqual(1, implementationResults.Count);
            
            // 获取详细信息验证关系
            var baseClass = indexer.GetElementById(baseClassResults[0].Id);
            var derivedClass = indexer.GetElementById(derivedClassResults[0].Id);
            var testInterface = indexer.GetElementById(interfaceResults[0].Id);
            var implClass = indexer.GetElementById(implementationResults[0].Id);
            
            Assert.IsNotNull(baseClass);
            Assert.IsNotNull(derivedClass);
            Assert.IsNotNull(testInterface);
            Assert.IsNotNull(implClass);
            
            // 验证继承关系
            var derivedClassElement = derivedClass as TypeElement;
            Assert.IsNotNull(derivedClassElement);
            Assert.IsNotNull(derivedClassElement.BaseTypeId);
            
            // 验证接口实现关系
            var implClassElement = implClass as TypeElement;
            Assert.IsNotNull(implClassElement);
            Assert.IsTrue(implClassElement.ImplementedInterfaceIds.Count > 0);
        }

        [TestMethod]
        public async Task FullIndexingProcess_EmptyDirectory_CreatesEmptyIndex()
        {
            // Arrange - 使用空目录
            // 不创建任何文件

            // Act
            var parser = new CodeParser(_logger);
            var codeDatabase = await parser.ParseDirectoryAsync(_testSourceDirectory);
            
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(codeDatabase);
            
            var results = indexer.SearchByName("*", 100);

            // Assert
            Assert.IsNotNull(codeDatabase);
            Assert.AreEqual(0, codeDatabase.Namespaces.Count);
            Assert.AreEqual(0, codeDatabase.Types.Count);
            Assert.AreEqual(0, codeDatabase.Members.Count);
            Assert.AreEqual(0, results.Count);
        }

        #region 辅助方法

        /// <summary>
        /// 创建简单的测试代码文件
        /// </summary>
        private void CreateTestCodeFiles()
        {
            var classContent = @"using System;

namespace TestProject
{
    /// <summary>
    /// 测试类
    /// </summary>
    public class TestClass
    {
        /// <summary>
        /// 测试属性
        /// </summary>
        public string TestProperty { get; set; }

        /// <summary>
        /// 测试方法
        /// </summary>
        public void TestMethod()
        {
            Console.WriteLine(""Test"");
        }

        /// <summary>
        /// 带参数的测试方法
        /// </summary>
        public int CalculateSum(int a, int b)
        {
            return a + b;
        }
    }
}";

            var filePath = Path.Combine(_testSourceDirectory, "TestClass.cs");
            File.WriteAllText(filePath, classContent);
        }

        /// <summary>
        /// 创建多文件测试项目，包含继承和接口实现关系
        /// </summary>
        private void CreateMultiFileTestProject()
        {
            // 创建基类
            var baseClassContent = @"namespace TestProject
{
    /// <summary>
    /// 基类
    /// </summary>
    public abstract class BaseClass
    {
        /// <summary>
        /// 基类属性
        /// </summary>
        public string BaseProperty { get; set; }

        /// <summary>
        /// 基类方法
        /// </summary>
        public virtual void BaseMethod()
        {
        }
    }
}";

            // 创建派生类
            var derivedClassContent = @"namespace TestProject
{
    /// <summary>
    /// 派生类
    /// </summary>
    public class DerivedClass : BaseClass
    {
        /// <summary>
        /// 派生类属性
        /// </summary>
        public int DerivedProperty { get; set; }

        /// <summary>
        /// 重写基类方法
        /// </summary>
        public override void BaseMethod()
        {
            // 重写实现
        }
    }
}";

            // 创建接口
            var interfaceContent = @"namespace TestProject
{
    /// <summary>
    /// 测试接口
    /// </summary>
    public interface ITestInterface
    {
        /// <summary>
        /// 接口方法
        /// </summary>
        void InterfaceMethod();

        /// <summary>
        /// 接口属性
        /// </summary>
        string InterfaceProperty { get; set; }
    }
}";

            // 创建接口实现类
            var implementationContent = @"namespace TestProject
{
    /// <summary>
    /// 接口实现类
    /// </summary>
    public class ImplementationClass : ITestInterface
    {
        /// <summary>
        /// 实现接口属性
        /// </summary>
        public string InterfaceProperty { get; set; }

        /// <summary>
        /// 实现接口方法
        /// </summary>
        public void InterfaceMethod()
        {
            // 实现
        }
    }
}";

            // 写入文件
            File.WriteAllText(Path.Combine(_testSourceDirectory, "BaseClass.cs"), baseClassContent);
            File.WriteAllText(Path.Combine(_testSourceDirectory, "DerivedClass.cs"), derivedClassContent);
            File.WriteAllText(Path.Combine(_testSourceDirectory, "ITestInterface.cs"), interfaceContent);
            File.WriteAllText(Path.Combine(_testSourceDirectory, "ImplementationClass.cs"), implementationContent);
        }

        #endregion
    }
}