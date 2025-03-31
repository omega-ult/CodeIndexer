using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeIndexer.Core.Models;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Logging;
using Directory = System.IO.Directory;

namespace CodeIndexer.Core.Indexing
{
    /// <summary>
    /// 代码索引器，负责建立和查询代码元素的索引
    /// </summary>
    public class CodeIndexer : IDisposable
    {
        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

        private readonly string _indexPath;
        private readonly ILogger _logger;
        private readonly FSDirectory _directory;
        private readonly Analyzer _analyzer;
        private readonly IndexWriter _writer;
        private readonly SearcherManager _searcherManager;

        // 字段名称常量
        private const string FieldId = "id";
        private const string FieldName = "name";
        private const string FieldFullName = "fullName";
        private const string FieldType = "type";
        private const string FieldElementType = "elementType";
        private const string FieldParentId = "parentId";
        private const string FieldDocumentation = "documentation";
        private const string FieldAccessModifier = "accessModifier";
        private const string FieldModifiers = "modifiers";
        private const string FieldParameters = "parameters";
        private const string FieldReturnType = "returnType";
        private const string FieldFilePath = "filePath";
        private const string FieldLineNumber = "lineNumber";
        private const string FieldContentHash = "contentHash";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="indexPath">索引存储路径</param>
        /// <param name="logger">日志记录器</param>
        public CodeIndexer(string indexPath, ILogger logger = null)
        {
            _indexPath = indexPath;
            _logger = logger;

            // 创建索引目录
            Directory.CreateDirectory(indexPath);
            _directory = FSDirectory.Open(new DirectoryInfo(indexPath));

            // 创建分析器和索引写入器
            _analyzer = new StandardAnalyzer(AppLuceneVersion);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };
            _writer = new IndexWriter(_directory, indexConfig);

            // 创建搜索管理器
            _searcherManager = new SearcherManager(_writer, true, null);

            _logger?.LogInformation($"代码索引器初始化完成，索引路径: {indexPath}");
        }

        /// <summary>
        /// 为代码数据库建立索引
        /// </summary>
        /// <param name="codeDatabase">代码数据库</param>
        public void BuildIndex(CodeDatabase codeDatabase)
        {
            _logger?.LogInformation("开始建立索引...");

            try
            {
                // 清空现有索引
                _writer.DeleteAll();

                // 索引所有代码元素
                var elements = codeDatabase.GetAllElements();
                foreach (var element in elements)
                {
                    IndexElement(element);
                }

                // 提交更改
                _writer.Commit();
                _searcherManager.MaybeRefresh();

                _logger?.LogInformation($"索引建立完成，共索引 {elements.Count} 个代码元素");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "建立索引时出错");
                throw;
            }
        }

        /// <summary>
        /// 更新索引中的代码元素
        /// </summary>
        /// <param name="element">代码元素</param>
        public void UpdateElement(CodeElement element)
        {
            try
            {
                // 删除旧的索引
                _writer.DeleteDocuments(new Term(FieldId, element.Id));

                // 添加新的索引
                IndexElement(element);

                // 提交更改
                _writer.Commit();
                _searcherManager.MaybeRefresh();

                _logger?.LogDebug($"更新元素索引: {element.FullName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"更新元素索引时出错: {element.FullName}");
                throw;
            }
        }

        /// <summary>
        /// 删除索引中的代码元素
        /// </summary>
        /// <param name="elementId">代码元素ID</param>
        public void DeleteElement(string elementId)
        {
            try
            {
                _writer.DeleteDocuments(new Term(FieldId, elementId));
                _writer.Commit();
                _searcherManager.MaybeRefresh();

                _logger?.LogDebug($"删除元素索引: {elementId}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"删除元素索引时出错: {elementId}");
                throw;
            }
        }

        /// <summary>
        /// 根据名称模糊查询代码元素
        /// </summary>
        /// <param name="namePattern">名称模式</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>匹配的代码元素索引列表</returns>
        public List<CodeElementIndex> SearchByNamePattern(string namePattern, int maxResults = 100)
        {
            _logger?.LogDebug($"执行名称模糊查询: {namePattern}");

            try
            {
                _searcherManager.MaybeRefresh();
                var searcher = _searcherManager.Acquire();

                try
                {
                    // 创建模糊查询
                    var queryParser = new QueryParser(AppLuceneVersion, FieldName, _analyzer);
                    queryParser.DefaultOperator = QueryParser.AND_OPERATOR;
                    var query = queryParser.Parse(namePattern + "*"); // 使用通配符进行前缀匹配

                    // 执行查询
                    var topDocs = searcher.Search(query, maxResults);
                    return ConvertToCodeElementIndexes(searcher, topDocs);
                }
                finally
                {
                    _searcherManager.Release(searcher);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"名称模糊查询时出错: {namePattern}");
                return new List<CodeElementIndex>();
            }
        }

        /// <summary>
        /// 根据全名精确查询代码元素
        /// </summary>
        /// <param name="fullName">全名</param>
        /// <returns>匹配的代码元素索引，如果不存在则返回null</returns>
        public CodeElementIndex? SearchByFullName(string fullName)
        {
            _logger?.LogDebug($"执行全名精确查询: {fullName}");

            try
            {
                _searcherManager.MaybeRefresh();
                var searcher = _searcherManager.Acquire();

                try
                {
                    // 创建精确查询
                    var query = new TermQuery(new Term(FieldFullName, fullName));

                    // 执行查询
                    var topDocs = searcher.Search(query, 1);
                    if (topDocs.TotalHits > 0)
                    {
                        var doc = searcher.Doc(topDocs.ScoreDocs[0].Doc);
                        return CreateCodeElementIndex(doc);
                    }

                    return null;
                }
                finally
                {
                    _searcherManager.Release(searcher);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"全名精确查询时出错: {fullName}");
                return null;
            }
        }

        /// <summary>
        /// 根据元素类型查询代码元素
        /// </summary>
        /// <param name="elementType">元素类型</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>匹配的代码元素索引列表</returns>
        public List<CodeElementIndex> SearchByElementType(ElementType elementType, int maxResults = 1000)
        {
            _logger?.LogDebug($"执行元素类型查询: {elementType}");

            try
            {
                _searcherManager.MaybeRefresh();
                var searcher = _searcherManager.Acquire();

                try
                {
                    // 创建类型查询
                    var query = new TermQuery(new Term(FieldElementType, elementType.ToString()));

                    // 执行查询
                    var topDocs = searcher.Search(query, maxResults);
                    return ConvertToCodeElementIndexes(searcher, topDocs);
                }
                finally
                {
                    _searcherManager.Release(searcher);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"元素类型查询时出错: {elementType}");
                return new List<CodeElementIndex>();
            }
        }

        /// <summary>
        /// 根据父元素ID查询子元素
        /// </summary>
        /// <param name="parentId">父元素ID</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>匹配的代码元素索引列表</returns>
        public List<CodeElementIndex> SearchByParentId(string parentId, int maxResults = 1000)
        {
            _logger?.LogDebug($"执行父元素查询: {parentId}");

            try
            {
                _searcherManager.MaybeRefresh();
                var searcher = _searcherManager.Acquire();

                try
                {
                    // 创建父元素查询
                    var query = new TermQuery(new Term(FieldParentId, parentId));

                    // 执行查询
                    var topDocs = searcher.Search(query, maxResults);
                    return ConvertToCodeElementIndexes(searcher, topDocs);
                }
                finally
                {
                    _searcherManager.Release(searcher);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"父元素查询时出错: {parentId}");
                return new List<CodeElementIndex>();
            }
        }

        /// <summary>
        /// 高级查询，支持组合多个条件
        /// </summary>
        /// <param name="query">查询条件</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>匹配的代码元素索引列表</returns>
        public List<CodeElementIndex> AdvancedSearch(CodeElementQuery query, int maxResults = 100)
        {
            _logger?.LogDebug("执行高级查询");

            try
            {
                _searcherManager.MaybeRefresh();
                var searcher = _searcherManager.Acquire();

                try
                {
                    // 创建布尔查询
                    var booleanQuery = new BooleanQuery();

                    // 添加名称条件
                    if (!string.IsNullOrEmpty(query.NamePattern))
                    {
                        var queryParser = new QueryParser(AppLuceneVersion, FieldName, _analyzer);
                        var nameQuery = queryParser.Parse(query.NamePattern + "*");
                        booleanQuery.Add(nameQuery, Occur.MUST);
                    }

                    // 添加元素类型条件
                    if (query.ElementType.HasValue)
                    {
                        var typeQuery = new TermQuery(new Term(FieldElementType, query.ElementType.Value.ToString()));
                        booleanQuery.Add(typeQuery, Occur.MUST);
                    }

                    // 添加访问修饰符条件
                    if (!string.IsNullOrEmpty(query.AccessModifier))
                    {
                        var accessQuery = new TermQuery(new Term(FieldAccessModifier, query.AccessModifier));
                        booleanQuery.Add(accessQuery, Occur.MUST);
                    }

                    // 添加父元素条件
                    if (!string.IsNullOrEmpty(query.ParentId))
                    {
                        var parentQuery = new TermQuery(new Term(FieldParentId, query.ParentId));
                        booleanQuery.Add(parentQuery, Occur.MUST);
                    }

                    // 添加返回类型条件
                    if (!string.IsNullOrEmpty(query.ReturnType))
                    {
                        var returnTypeQuery = new TermQuery(new Term(FieldReturnType, query.ReturnType));
                        booleanQuery.Add(returnTypeQuery, Occur.MUST);
                    }

                    // 执行查询
                    var topDocs = searcher.Search(booleanQuery, maxResults);
                    return ConvertToCodeElementIndexes(searcher, topDocs);
                }
                finally
                {
                    _searcherManager.Release(searcher);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "高级查询时出错");
                return new List<CodeElementIndex>();
            }
        }

        /// <summary>
        /// 为代码元素建立索引
        /// </summary>
        private void IndexElement(CodeElement element)
        {
            var doc = new Document();

            // 添加基本字段
            doc.Add(new StringField(FieldId, element.Id, Field.Store.YES));
            doc.Add(new TextField(FieldName, element.Name, Field.Store.YES));
            doc.Add(new StringField(FieldFullName, element.FullName, Field.Store.YES));
            doc.Add(new StringField(FieldElementType, element.ElementType.ToString(), Field.Store.YES));
            doc.Add(new StringField(FieldContentHash, element.ContentHash, Field.Store.YES));

            // 添加父元素ID
            if (element.ParentId != null)
            {
                doc.Add(new StringField(FieldParentId, element.ParentId, Field.Store.YES));
            }

            // 添加访问修饰符
            if (!string.IsNullOrEmpty(element.AccessModifier))
            {
                doc.Add(new StringField(FieldAccessModifier, element.AccessModifier, Field.Store.YES));
            }

            // 添加修饰符
            if (element.Modifiers.Any())
            {
                doc.Add(new TextField(FieldModifiers, string.Join(" ", element.Modifiers), Field.Store.YES));
            }

            // 添加文档注释
            if (!string.IsNullOrEmpty(element.Documentation))
            {
                doc.Add(new TextField(FieldDocumentation, element.Documentation, Field.Store.YES));
            }

            // 添加位置信息
            doc.Add(new StringField(FieldFilePath, element.Location.FilePath, Field.Store.YES));
            doc.Add(new Int32Field(FieldLineNumber, element.Location.StartLine, Field.Store.YES));

            // 根据元素类型添加特定字段
            if (element is MemberElement memberElement)
            {
                // 添加返回类型或成员类型
                doc.Add(new StringField(FieldReturnType, memberElement.Type, Field.Store.YES));

                // 添加参数信息（如果是方法或构造函数）
                if (memberElement.Parameters.Any())
                {
                    var parametersText = string.Join(" ", memberElement.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    doc.Add(new TextField(FieldParameters, parametersText, Field.Store.YES));
                }
            }

            // 添加到索引
            _writer.AddDocument(doc);
        }

        /// <summary>
        /// 将Lucene文档转换为代码元素索引
        /// </summary>
        private CodeElementIndex CreateCodeElementIndex(Document doc)
        {
            return new CodeElementIndex
            {
                Id = doc.Get(FieldId),
                Name = doc.Get(FieldName),
                FullName = doc.Get(FieldFullName),
                ElementType = Enum.Parse<ElementType>(doc.Get(FieldElementType)),
                ParentId = doc.Get(FieldParentId),
                AccessModifier = doc.Get(FieldAccessModifier),
                FilePath = doc.Get(FieldFilePath),
                LineNumber = int.Parse(doc.Get(FieldLineNumber)),
                ReturnType = doc.Get(FieldReturnType),
                ContentHash = doc.Get(FieldContentHash)
            };
        }

        /// <summary>
        /// 将Lucene搜索结果转换为代码元素索引列表
        /// </summary>
        private List<CodeElementIndex> ConvertToCodeElementIndexes(IndexSearcher searcher, TopDocs topDocs)
        {
            var results = new List<CodeElementIndex>();

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = searcher.Doc(scoreDoc.Doc);
                results.Add(CreateCodeElementIndex(doc));
            }

            return results;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _searcherManager?.Dispose();
            _writer?.Dispose();
            _analyzer?.Dispose();
            _directory?.Dispose();

            _logger?.LogInformation("代码索引器已释放资源");
        }
    }

    /// <summary>
    /// 代码元素索引，表示索引中的代码元素信息
    /// </summary>
    public class CodeElementIndex
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 元素名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 元素全名
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// 元素类型
        /// </summary>
        public ElementType ElementType { get; set; }

        /// <summary>
        /// 父元素ID
        /// </summary>
        public string? ParentId { get; set; }

        /// <summary>
        /// 访问修饰符
        /// </summary>
        public string? AccessModifier { get; set; }

        /// <summary>
        /// 源文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 行号
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// 返回类型或成员类型
        /// </summary>
        public string? ReturnType { get; set; }

        /// <summary>
        /// 内容哈希值
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// 代码元素查询条件
    /// </summary>
    public class CodeElementQuery
    {
        /// <summary>
        /// 名称模式
        /// </summary>
        public string? NamePattern { get; set; }

        /// <summary>
        /// 元素类型
        /// </summary>
        public ElementType? ElementType { get; set; }

        /// <summary>
        /// 访问修饰符
        /// </summary>
        public string? AccessModifier { get; set; }

        /// <summary>
        /// 父元素ID
        /// </summary>
        public string? ParentId { get; set; }

        /// <summary>
        /// 返回类型或成员类型
        /// </summary>
        public string? ReturnType { get; set; }
    }
}