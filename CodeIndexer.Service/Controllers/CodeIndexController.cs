using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeIndexer.Core.Indexing;
using CodeIndexer.Core.Models;
using CodeIndexer.Core.Parsing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CodeIndexer.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CodeIndexController : ControllerBase
    {
        private readonly ILogger<CodeIndexController> _logger;
        private readonly CodeParser _codeParser;
        private readonly CecilAssemblyParser _assemblyParser;
        private readonly UnityProjectParser _unityProjectParser;
        private readonly Core.Indexing.CodeIndexer _codeIndexer;
        private readonly string _indexPath;
        private CodeDatabase? _codeDatabase;

        public CodeIndexController(ILogger<CodeIndexController> logger)
        {
            _logger = logger;
            _indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodeIndex");
            _codeParser = new CodeParser(logger);
            _assemblyParser = new CecilAssemblyParser(logger);
            _unityProjectParser = new UnityProjectParser(logger);
            _codeIndexer = new Core.Indexing.CodeIndexer(_indexPath, logger);
        }

        /// <summary>
        /// 索引代码目录
        /// </summary>
        /// <param name="request">索引请求</param>
        /// <returns>索引结果</returns>
        [HttpPost("index")]
        public async Task<ActionResult<IndexResult>> IndexDirectory([FromBody] IndexRequest request)
        {
            if (string.IsNullOrEmpty(request.DirectoryPath))
            {
                return BadRequest("目录路径不能为空");
            }

            try
            {
                _logger.LogInformation($"开始索引目录: {request.DirectoryPath}");

                // 解析代码
                _codeDatabase = await _codeParser.ParseDirectoryAsync(request.DirectoryPath);

                // 建立索引
                _codeIndexer.BuildIndex(_codeDatabase);

                var result = new IndexResult
                {
                    Success = true,
                    Message = $"成功索引目录: {request.DirectoryPath}",
                    ElementCount = _codeDatabase.Count
                };

                _logger.LogInformation($"索引完成，共索引 {_codeDatabase.Count} 个代码元素");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"索引目录时出错: {request.DirectoryPath}");
                return StatusCode(500, $"索引出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 索引DLL文件目录
        /// </summary>
        /// <param name="request">索引请求</param>
        /// <returns>索引结果</returns>
        [HttpPost("index/dll")]
        public async Task<ActionResult<IndexResult>> IndexDllDirectory([FromBody] IndexRequest request)
        {
            if (string.IsNullOrEmpty(request.DirectoryPath))
            {
                return BadRequest("目录路径不能为空");
            }

            try
            {
                _logger.LogInformation($"开始索引DLL目录: {request.DirectoryPath}");

                // 解析DLL
                _codeDatabase = await _assemblyParser.ParseDirectoryAsync(request.DirectoryPath);

                // 建立索引
                _codeIndexer.BuildIndex(_codeDatabase);

                var result = new IndexResult
                {
                    Success = true,
                    Message = $"成功索引DLL目录: {request.DirectoryPath}",
                    ElementCount = _codeDatabase.Count
                };

                _logger.LogInformation($"索引完成，共索引 {_codeDatabase.Count} 个代码元素");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"索引DLL目录时出错: {request.DirectoryPath}");
                return StatusCode(500, $"索引出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 索引Unity工程
        /// </summary>
        /// <param name="request">索引请求</param>
        /// <returns>索引结果</returns>
        [HttpPost("index/unity")]
        public async Task<ActionResult<IndexResult>> IndexUnityProject([FromBody] IndexRequest request)
        {
            if (string.IsNullOrEmpty(request.DirectoryPath))
            {
                return BadRequest("Unity工程路径不能为空");
            }

            try
            {
                _logger.LogInformation($"开始索引Unity工程: {request.DirectoryPath}");

                // 解析Unity工程
                _codeDatabase = await _unityProjectParser.ParseUnityProjectAsync(request.DirectoryPath);

                // 建立索引
                _codeIndexer.BuildIndex(_codeDatabase);

                var result = new IndexResult
                {
                    Success = true,
                    Message = $"成功索引Unity工程: {request.DirectoryPath}",
                    ElementCount = _codeDatabase.Count
                };

                _logger.LogInformation($"索引完成，共索引 {_codeDatabase.Count} 个代码元素");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"索引Unity工程时出错: {request.DirectoryPath}");
                return StatusCode(500, $"索引出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 按名称模式查询
        /// </summary>
        /// <param name="namePattern">名称模式</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>查询结果</returns>
        [HttpGet("search/name/{namePattern}")]
        public ActionResult<SearchResult> SearchByNamePattern(string namePattern, [FromQuery] int maxResults = 100)
        {
            try
            {
                var elements = _codeIndexer.SearchByNamePattern(namePattern, maxResults);
                var result = new SearchResult
                {
                    TotalCount = elements.Count,
                    Elements = elements
                };

                _logger.LogInformation($"按名称模式查询: {namePattern}，找到 {elements.Count} 个结果");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"按名称模式查询时出错: {namePattern}");
                return StatusCode(500, $"查询出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 按全名精确查询
        /// </summary>
        /// <param name="fullName">全名</param>
        /// <returns>查询结果</returns>
        [HttpGet("search/fullname/{fullName}")]
        public ActionResult<SearchResult> SearchByFullName(string fullName)
        {
            try
            {
                var element = _codeIndexer.SearchByFullName(fullName);
                var result = new SearchResult
                {
                    TotalCount = element != null ? 1 : 0,
                    Elements = element != null ? new List<CodeElementIndex> { element } : new List<CodeElementIndex>()
                };

                _logger.LogInformation($"按全名查询: {fullName}，找到 {result.TotalCount} 个结果");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"按全名查询时出错: {fullName}");
                return StatusCode(500, $"查询出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 按元素类型查询
        /// </summary>
        /// <param name="elementType">元素类型</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>查询结果</returns>
        [HttpGet("search/type/{elementType}")]
        public ActionResult<SearchResult> SearchByElementType(ElementType elementType, [FromQuery] int maxResults = 1000)
        {
            try
            {
                var elements = _codeIndexer.SearchByElementType(elementType, maxResults);
                var result = new SearchResult
                {
                    TotalCount = elements.Count,
                    Elements = elements
                };

                _logger.LogInformation($"按元素类型查询: {elementType}，找到 {elements.Count} 个结果");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"按元素类型查询时出错: {elementType}");
                return StatusCode(500, $"查询出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 按父元素ID查询子元素
        /// </summary>
        /// <param name="parentId">父元素ID</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>查询结果</returns>
        [HttpGet("search/parent/{parentId}")]
        public ActionResult<SearchResult> SearchByParentId(string parentId, [FromQuery] int maxResults = 1000)
        {
            try
            {
                var elements = _codeIndexer.SearchByParentId(parentId, maxResults);
                var result = new SearchResult
                {
                    TotalCount = elements.Count,
                    Elements = elements
                };

                _logger.LogInformation($"按父元素ID查询: {parentId}，找到 {elements.Count} 个结果");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"按父元素ID查询时出错: {parentId}");
                return StatusCode(500, $"查询出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 高级查询
        /// </summary>
        /// <param name="query">查询条件</param>
        /// <param name="maxResults">最大结果数</param>
        /// <returns>查询结果</returns>
        [HttpPost("search/advanced")]
        public ActionResult<SearchResult> AdvancedSearch([FromBody] CodeElementQuery query, [FromQuery] int maxResults = 100)
        {
            try
            {
                var elements = _codeIndexer.AdvancedSearch(query, maxResults);
                var result = new SearchResult
                {
                    TotalCount = elements.Count,
                    Elements = elements
                };

                _logger.LogInformation($"高级查询，找到 {elements.Count} 个结果");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "高级查询时出错");
                return StatusCode(500, $"查询出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取元素详情
        /// </summary>
        /// <param name="id">元素ID</param>
        /// <returns>元素详情</returns>
        [HttpGet("element/{id}")]
        public ActionResult<CodeElement> GetElementById(string id)
        {
            if (_codeDatabase == null)
            {
                return BadRequest("代码数据库未初始化，请先索引代码目录");
            }

            try
            {
                var element = _codeDatabase.GetElementById(id);
                if (element == null)
                {
                    return NotFound($"未找到ID为 {id} 的元素");
                }

                _logger.LogInformation($"获取元素详情: {id}");

                return Ok(element);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取元素详情时出错: {id}");
                return StatusCode(500, $"获取元素详情出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 索引请求
    /// </summary>
    public class IndexRequest
    {
        /// <summary>
        /// 代码目录路径
        /// </summary>
        public string DirectoryPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// 索引结果
    /// </summary>
    public class IndexResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 索引的元素数量
        /// </summary>
        public int ElementCount { get; set; }
    }

    /// <summary>
    /// 搜索结果
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// 总结果数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 元素列表
        /// </summary>
        public List<CodeElementIndex> Elements { get; set; } = new List<CodeElementIndex>();
    }
}