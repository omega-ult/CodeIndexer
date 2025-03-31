using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeIndexer.Core.Models
{
    /// <summary>
    /// 代码数据库，存储和管理所有解析出的代码元素
    /// </summary>
    public class CodeDatabase
    {
        // 存储所有代码元素的字典，键为元素ID
        private readonly Dictionary<string, CodeElement> _elements = new Dictionary<string, CodeElement>();

        // 按元素类型分类的索引
        private readonly Dictionary<ElementType, List<string>> _elementsByType = new Dictionary<ElementType, List<string>>();

        // 按名称索引的字典，键为元素名称（小写），值为元素ID列表
        private readonly Dictionary<string, List<string>> _elementsByName = new Dictionary<string, List<string>>();

        // 按全名索引的字典，键为元素全名（小写），值为元素ID
        private readonly Dictionary<string, string> _elementsByFullName = new Dictionary<string, string>();

        // 父子关系索引，键为父元素ID，值为子元素ID列表
        private readonly Dictionary<string, List<string>> _childrenByParent = new Dictionary<string, List<string>>();

        /// <summary>
        /// 添加一个代码元素到数据库
        /// </summary>
        /// <param name="element">代码元素</param>
        public void AddElement(CodeElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            // 添加到主字典
            _elements[element.Id] = element;

            // 添加到类型索引
            if (!_elementsByType.ContainsKey(element.ElementType))
                _elementsByType[element.ElementType] = new List<string>();
            _elementsByType[element.ElementType].Add(element.Id);

            // 添加到名称索引
            var nameLower = element.Name.ToLowerInvariant();
            if (!_elementsByName.ContainsKey(nameLower))
                _elementsByName[nameLower] = new List<string>();
            _elementsByName[nameLower].Add(element.Id);

            // 添加到全名索引
            var fullNameLower = element.FullName.ToLowerInvariant();
            _elementsByFullName[fullNameLower] = element.Id;

            // 添加到父子关系索引
            if (element.ParentId != null)
            {
                if (!_childrenByParent.ContainsKey(element.ParentId))
                    _childrenByParent[element.ParentId] = new List<string>();
                _childrenByParent[element.ParentId].Add(element.Id);
            }
        }

        /// <summary>
        /// 添加多个代码元素到数据库
        /// </summary>
        /// <param name="elements">代码元素集合</param>
        public void AddElements(IEnumerable<CodeElement> elements)
        {
            foreach (var element in elements)
            {
                AddElement(element);
            }
        }

        /// <summary>
        /// 根据ID获取代码元素
        /// </summary>
        /// <param name="id">元素ID</param>
        /// <returns>代码元素，如果不存在则返回null</returns>
        public CodeElement? GetElementById(string id)
        {
            return _elements.TryGetValue(id, out var element) ? element : null;
        }

        /// <summary>
        /// 根据全名获取代码元素
        /// </summary>
        /// <param name="fullName">元素全名</param>
        /// <returns>代码元素，如果不存在则返回null</returns>
        public CodeElement? GetElementByFullName(string fullName)
        {
            var fullNameLower = fullName.ToLowerInvariant();
            return _elementsByFullName.TryGetValue(fullNameLower, out var id) ? GetElementById(id) : null;
        }

        /// <summary>
        /// 根据名称查找代码元素
        /// </summary>
        /// <param name="name">元素名称</param>
        /// <returns>匹配的代码元素列表</returns>
        public List<CodeElement> FindElementsByName(string name)
        {
            var nameLower = name.ToLowerInvariant();
            if (_elementsByName.TryGetValue(nameLower, out var ids))
            {
                return ids.Select(id => _elements[id]).ToList();
            }
            return new List<CodeElement>();
        }

        /// <summary>
        /// 根据名称模糊查找代码元素
        /// </summary>
        /// <param name="namePattern">名称模式</param>
        /// <returns>匹配的代码元素列表</returns>
        public List<CodeElement> FindElementsByNamePattern(string namePattern)
        {
            var patternLower = namePattern.ToLowerInvariant();
            var matchingElements = new List<CodeElement>();

            foreach (var kvp in _elementsByName)
            {
                if (kvp.Key.Contains(patternLower))
                {
                    matchingElements.AddRange(kvp.Value.Select(id => _elements[id]));
                }
            }

            return matchingElements;
        }

        /// <summary>
        /// 根据元素类型获取代码元素
        /// </summary>
        /// <param name="elementType">元素类型</param>
        /// <returns>指定类型的代码元素列表</returns>
        public List<CodeElement> GetElementsByType(ElementType elementType)
        {
            if (_elementsByType.TryGetValue(elementType, out var ids))
            {
                return ids.Select(id => _elements[id]).ToList();
            }
            return new List<CodeElement>();
        }

        /// <summary>
        /// 获取指定父元素的所有子元素
        /// </summary>
        /// <param name="parentId">父元素ID</param>
        /// <returns>子元素列表</returns>
        public List<CodeElement> GetChildElements(string parentId)
        {
            if (_childrenByParent.TryGetValue(parentId, out var childIds))
            {
                return childIds.Select(id => _elements[id]).ToList();
            }
            return new List<CodeElement>();
        }

        /// <summary>
        /// 获取所有代码元素
        /// </summary>
        /// <returns>所有代码元素的列表</returns>
        public List<CodeElement> GetAllElements()
        {
            return _elements.Values.ToList();
        }

        /// <summary>
        /// 获取数据库中的元素数量
        /// </summary>
        public int Count => _elements.Count;

        /// <summary>
        /// 清空数据库
        /// </summary>
        public void Clear()
        {
            _elements.Clear();
            _elementsByType.Clear();
            _elementsByName.Clear();
            _elementsByFullName.Clear();
            _childrenByParent.Clear();
        }
    }
}