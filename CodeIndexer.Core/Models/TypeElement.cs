using System.Collections.Generic;

namespace CodeIndexer.Core.Models
{
    /// <summary>
    /// 表示代码中的类型元素（类、接口、结构体等）
    /// </summary>
    public class TypeElement : CodeElement
    {
        /// <summary>
        /// 类型的基类ID
        /// </summary>
        public string? BaseTypeId { get; set; }

        /// <summary>
        /// 类型实现的接口ID列表
        /// </summary>
        public List<string> ImplementedInterfaceIds { get; set; } = new List<string>();

        /// <summary>
        /// 类型中包含的成员ID列表（方法、属性、字段等）
        /// </summary>
        public List<string> MemberIds { get; set; } = new List<string>();

        /// <summary>
        /// 类型中包含的嵌套类型ID列表
        /// </summary>
        public List<string> NestedTypeIds { get; set; } = new List<string>();

        /// <summary>
        /// 类型的泛型参数列表
        /// </summary>
        public List<string> GenericParameters { get; set; } = new List<string>();

        /// <summary>
        /// 类型的约束条件
        /// </summary>
        public Dictionary<string, List<string>> GenericConstraints { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// 类型是否为抽象类
        /// </summary>
        public bool IsAbstract { get; set; }

        /// <summary>
        /// 类型是否为静态类
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// 类型是否为密封类
        /// </summary>
        public bool IsSealed { get; set; }

        /// <summary>
        /// 类型是否为部分类
        /// </summary>
        public bool IsPartial { get; set; }

        /// <summary>
        /// 元素类型（类、接口、结构体等）
        /// </summary>
        public override ElementType ElementType { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="elementType">元素类型</param>
        public TypeElement(ElementType elementType)
        {
            if (elementType != ElementType.Class && 
                elementType != ElementType.Interface && 
                elementType != ElementType.Struct && 
                elementType != ElementType.Enum && 
                elementType != ElementType.Delegate)
            {
                throw new ArgumentException("元素类型必须是类、接口、结构体、枚举或委托", nameof(elementType));
            }
            
            ElementType = elementType;
        }
    }
}