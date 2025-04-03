using System;
using System.IO;
using System.Threading.Tasks;
using CodeIndexer.Core.Models;
using CodeIndexer.Core.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeIndexer.Tests.Parsing
{
    [TestClass]
    public class CodeParserTests
    {
        private readonly ILogger _logger;
        private readonly string _testDataDirectory;

        public CodeParserTests()
        {
            _logger = NullLogger.Instance;
            // 创建测试数据目录在临时文件夹中
            _testDataDirectory = Path.Combine(Path.GetTempPath(), "CodeIndexerTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // 测试完成后清理测试数据目录
            if (Directory.Exists(_testDataDirectory))
            {
                Directory.Delete(_testDataDirectory, true);
            }
        }

        [TestMethod]
        public async Task ParseDirectoryAsync_EmptyDirectory_ReturnsEmptyDatabase()
        {
            // Arrange
            var parser = new CodeParser(_logger);

            // Act
            var result = await parser.ParseDirectoryAsync(_testDataDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Namespaces.Count);
            Assert.AreEqual(0, result.Types.Count);
            Assert.AreEqual(0, result.Members.Count);
        }

        [TestMethod]
        public async Task ParseDirectoryAsync_SingleClassFile_ParsesCorrectly()
        {
            // Arrange
            var parser = new CodeParser(_logger);
            var classContent = @"using System;

namespace TestNamespace
{
    public class TestClass
    {
        public string TestProperty { get; set; }

        public void TestMethod()
        {
            Console.WriteLine(""Test"");
        }
    }
}";

            var filePath = Path.Combine(_testDataDirectory, "TestClass.cs");
            File.WriteAllText(filePath, classContent);

            // Act
            var result = await parser.ParseDirectoryAsync(_testDataDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Namespaces.Count);
            Assert.IsTrue(result.Namespaces.ContainsKey("TestNamespace"));

            var ns = result.Namespaces["TestNamespace"];
            Assert.AreEqual("TestNamespace", ns.Name);
            Assert.AreEqual(1, ns.TypeIds.Count);

            // 验证类型
            Assert.AreEqual(1, result.Types.Count);
            var typeKey = result.Types.Keys.First();
            var testClass = result.Types[typeKey];
            Assert.AreEqual("TestClass", testClass.Name);
            Assert.AreEqual("TestNamespace.TestClass", testClass.FullName);
            Assert.AreEqual(ElementType.Class, testClass.ElementType);
            Assert.AreEqual("public", testClass.AccessModifier);
            Assert.AreEqual(2, testClass.MemberIds.Count); // 属性和方法

            // 验证成员
            Assert.AreEqual(2, result.Members.Count);
            var propertyExists = result.Members.Values.Any(m => 
                m.Name == "TestProperty" && 
                m.ElementType == ElementType.Property);
            var methodExists = result.Members.Values.Any(m => 
                m.Name == "TestMethod" && 
                m.ElementType == ElementType.Method);

            Assert.IsTrue(propertyExists, "TestProperty not found");
            Assert.IsTrue(methodExists, "TestMethod not found");
        }

        [TestMethod]
        [ExpectedException(typeof(DirectoryNotFoundException))]
        public async Task ParseDirectoryAsync_NonExistentDirectory_ThrowsException()
        {
            // Arrange
            var parser = new CodeParser(_logger);
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Act & Assert
            await parser.ParseDirectoryAsync(nonExistentPath);
        }
    }
}