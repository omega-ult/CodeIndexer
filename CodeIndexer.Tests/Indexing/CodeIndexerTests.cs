using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeIndexer.Core.Indexing;
using CodeIndexer.Core.Models;
using CodeIndexer.Core.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeIndexer.Tests.Indexing
{
    [TestClass]
    public class CodeIndexerTests
    {
        private readonly ILogger _logger;
        private readonly string _testDataDirectory;
        private readonly string _testIndexDirectory;
        private CodeDatabase _testDatabase;

        public CodeIndexerTests()
        {
            _logger = NullLogger.Instance;
            // 创建测试数据和索引目录
            var testRootDir = Path.Combine(Path.GetTempPath(), "CodeIndexerTests", Guid.NewGuid().ToString());
            _testDataDirectory = Path.Combine(testRootDir, "Data");
            _testIndexDirectory = Path.Combine(testRootDir, "Index");
            
            Directory.CreateDirectory(_testDataDirectory);
            Directory.CreateDirectory(_testIndexDirectory);

            // 创建测试数据库
            _testDatabase = CreateTestDatabase();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // 测试完成后清理测试目录
            var testRootDir = Directory.GetParent(_testDataDirectory).FullName;
            if (Directory.Exists(testRootDir))
            {
                Directory.Delete(testRootDir, true);
            }
        }

        [TestMethod]
        public void BuildIndex_ValidDatabase_CreatesIndex()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);

            // Act
            indexer.BuildIndex(_testDatabase);

            // Assert - 验证索引目录中有文件生成
            Assert.IsTrue(Directory.Exists(_testIndexDirectory));
            Assert.IsTrue(Directory.GetFiles(_testIndexDirectory, "*", SearchOption.AllDirectories).Length > 0);
        }

        [TestMethod]
        public void SearchByName_ExistingName_ReturnsResults()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);

            // Act
            var results = indexer.SearchByName("Test", 10);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.Any(r => r.Name.Contains("Test")));
        }

        [TestMethod]
        public void SearchByName_NonExistingName_ReturnsEmptyList()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);

            // Act
            var results = indexer.SearchByName("NonExistingElement", 10);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void SearchByFullName_ExistingFullName_ReturnsResult()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);
            const string fullName = "TestNamespace.TestClass";

            // Act
            var results = indexer.SearchByFullName(fullName);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(fullName, results[0].FullName);
        }

        [TestMethod]
        public void SearchByElementType_Class_ReturnsOnlyClasses()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);

            // Act
            var results = indexer.SearchByElementType(ElementType.Class, 100);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => r.ElementType == ElementType.Class));
        }

        [TestMethod]
        public void SearchByParentId_ValidParentId_ReturnsChildElements()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);
            
            // 获取TestClass的ID
            var classElement = _testDatabase.Types.Values.First(t => t.Name == "TestClass");

            // Act
            var results = indexer.SearchByParentId(classElement.Id, 100);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count); // TestProperty 和 TestMethod
        }

        [TestMethod]
        public void AdvancedSearch_MultipleConditions_ReturnsFilteredResults()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);

            var query = new CodeElementQuery
            {
                NamePattern = "Test",
                ElementType = ElementType.Method,
                AccessModifier = "public"
            };

            // Act
            var results = indexer.AdvancedSearch(query, 100);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => 
                r.Name.Contains("Test") && 
                r.ElementType == ElementType.Method && 
                r.AccessModifier == "public"));
        }

        [TestMethod]
        public void GetElementById_ExistingId_ReturnsElement()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);
            
            // 获取一个已知元素的ID
            var knownElement = _testDatabase.Types.Values.First();

            // Act
            var result = indexer.GetElementById(knownElement.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(knownElement.Id, result.Id);
            Assert.AreEqual(knownElement.Name, result.Name);
        }

        [TestMethod]
        public void GetElementById_NonExistingId_ReturnsNull()
        {
            // Arrange
            using var indexer = new Core.Indexing.CodeIndexer(_testIndexDirectory, _logger);
            indexer.BuildIndex(_testDatabase);

            // Act
            var result = indexer.GetElementById("non-existing-id");

            // Assert
            Assert.IsNull(result);
        }

        /// <summary>
        /// 创建测试用的代码数据库
        /// </summary>
        private CodeDatabase CreateTestDatabase()
        {
            var database = new CodeDatabase();

            // 创建命名空间
            var ns = new NamespaceElement
            {
                Id = Guid.NewGuid().ToString(),
                Name = "TestNamespace",
                FullName = "TestNamespace"
            };

            // 创建类
            var testClass = new TypeElement
            {
                Id = Guid.NewGuid().ToString(),
                Name = "TestClass",
                FullName = "TestNamespace.TestClass",
                ElementType = ElementType.Class,
                AccessModifier = "public",
                ParentId = ns.Id,
                Location = new SourceLocation
                {
                    FilePath = "TestClass.cs",
                    StartLine = 5,
                    EndLine = 14
                }
            };

            // 创建属性
            var property = new MemberElement
            {
                Id = Guid.NewGuid().ToString(),
                Name = "TestProperty",
                FullName = "TestNamespace.TestClass.TestProperty",
                ElementType = ElementType.Property,
                AccessModifier = "public",
                ParentId = testClass.Id,
                Type = "string",
                Location = new SourceLocation
                {
                    FilePath = "TestClass.cs",
                    StartLine = 7,
                    EndLine = 7
                }
            };

            // 创建方法
            var method = new MemberElement
            {
                Id = Guid.NewGuid().ToString(),
                Name = "TestMethod",
                FullName = "TestNamespace.TestClass.TestMethod",
                ElementType = ElementType.Method,
                AccessModifier = "public",
                ParentId = testClass.Id,
                Type = "void",
                Location = new SourceLocation
                {
                    FilePath = "TestClass.cs",
                    StartLine = 9,
                    EndLine = 12
                }
            };

            // 添加到数据库
            database.Namespaces.Add(ns.FullName, ns);
            database.Types.Add(testClass.Id, testClass);
            database.Members.Add(property.Id, property);
            database.Members.Add(method.Id, method);

            // 建立关系
            ns.TypeIds.Add(testClass.Id);
            testClass.MemberIds.Add(property.Id);
            testClass.MemberIds.Add(method.Id);

            return database;
        }
    }
}