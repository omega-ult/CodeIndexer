using System.Collections.Generic;

namespace CodeIndexer.Core.Models
{
    /// <summary>
    /// 表示代码中的命名空间
    /// </summary>
    public class NamespaceElement : CodeElement
    {
        /// <summary>
        /// 命名空间中包含的类型（类、接口、结构体等）的ID列表
        /// </summary>
        public List<string> TypeIds { get; set; } = new List<string>();

        /// <summary>
        /// 命名空间中包含的子命名空间的ID列表
        /// </summary>
        public List<string> ChildNamespaceIds { get; set; } = new List<string>();

        /// <summary>
        /// 元素类型为命名空间
        /// </summary>
        public override ElementType ElementType
        {
            get => ElementType.Namespace;
            set => throw new NotImplementedException();
        }
    }
}