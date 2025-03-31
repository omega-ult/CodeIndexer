using System;
using System.Collections.Generic;

namespace CodeIndexer.Core.Models
{
    /// <summary>
    /// 代码元素的基类，表示代码库中的任何可索引元素
    /// </summary>
    public abstract class CodeElement
    {
        /// <summary>
        /// 元素的唯一标识符
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 元素的名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 元素的完全限定名称（包含命名空间和父类等）
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// 元素在源代码中的位置信息
        /// </summary>
        public SourceLocation Location { get; set; } = new SourceLocation();

        /// <summary>
        /// 元素的文档注释
        /// </summary>
        public string Documentation { get; set; } = string.Empty;

        /// <summary>
        /// 元素的访问修饰符（public, private, protected等）
        /// </summary>
        public string AccessModifier { get; set; } = string.Empty;

        /// <summary>
        /// 元素的修饰符（static, abstract, virtual等）
        /// </summary>
        public List<string> Modifiers { get; set; } = new List<string>();

        /// <summary>
        /// 元素的类型（命名空间、类、方法、属性等）
        /// </summary>
        public abstract ElementType ElementType { get; set; }

        /// <summary>
        /// 元素的哈希值，用于检测变更
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// 元素的版本号，用于跟踪变更历史
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 元素的父元素ID
        /// </summary>
        public string? ParentId { get; set; }
    }

    /// <summary>
    /// 源代码位置信息
    /// </summary>
    public class SourceLocation
    {
        /// <summary>
        /// 源文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 起始行号
        /// </summary>
        public int StartLine { get; set; }

        /// <summary>
        /// 起始列号
        /// </summary>
        public int StartColumn { get; set; }

        /// <summary>
        /// 结束行号
        /// </summary>
        public int EndLine { get; set; }

        /// <summary>
        /// 结束列号
        /// </summary>
        public int EndColumn { get; set; }
    }

    /// <summary>
    /// 用于Unity特有文件的代码元素实现
    /// </summary>
    public class UnityFileElement : CodeElement
    {
        public override ElementType ElementType { get; set; }

        public UnityFileElement(ElementType elementType)
        {
            ElementType = elementType;
        }
    }
    public enum ElementType
    {
        Namespace,
        Class,
        Interface,
        Struct,
        Enum,
        Method,
        Property,
        Field,
        Event,
        Delegate,
        EnumMember,
        Constructor,
        Destructor,
        Indexer,
        Operator,
        Other // 用于表示Unity特有文件类型如场景、预制体等
    }
}