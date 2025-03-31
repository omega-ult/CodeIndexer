using System;
using System.Collections.Generic;

namespace CodeIndexer.Core.Models
{
    /// <summary>
    /// 表示代码中的成员元素（方法、属性、字段等）
    /// </summary>
    public class MemberElement : CodeElement
    {
        /// <summary>
        /// 成员的返回类型或字段/属性类型
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 成员的参数列表（适用于方法、构造函数等）
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

        /// <summary>
        /// 成员是否为虚拟的
        /// </summary>
        public bool IsVirtual { get; set; }

        /// <summary>
        /// 成员是否为抽象的
        /// </summary>
        public bool IsAbstract { get; set; }

        /// <summary>
        /// 成员是否为静态的
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// 成员是否为重写的
        /// </summary>
        public bool IsOverride { get; set; }

        /// <summary>
        /// 成员是否为异步的
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// 成员是否为扩展方法
        /// </summary>
        public bool IsExtension { get; set; }

        /// <summary>
        /// 成员的泛型参数列表
        /// </summary>
        public List<string> GenericParameters { get; set; } = new List<string>();

        /// <summary>
        /// 成员的泛型约束条件
        /// </summary>
        public Dictionary<string, List<string>> GenericConstraints { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// 元素类型（方法、属性、字段等）
        /// </summary>
        public override ElementType ElementType { get; set; }

        public string ReturnType { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="elementType">元素类型</param>
        public MemberElement(ElementType elementType)
        {
            if (elementType != ElementType.Method && 
                elementType != ElementType.Property && 
                elementType != ElementType.Field && 
                elementType != ElementType.Event && 
                elementType != ElementType.Constructor && 
                elementType != ElementType.Destructor && 
                elementType != ElementType.Indexer && 
                elementType != ElementType.Operator && 
                elementType != ElementType.EnumMember)
            {
                throw new ArgumentException("元素类型必须是方法、属性、字段、事件、构造函数、析构函数、索引器、运算符或枚举成员", nameof(elementType));
            }
            
            ElementType = elementType;
        }
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    public class ParameterInfo
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 参数类型
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 参数是否有默认值
        /// </summary>
        public bool HasDefaultValue { get; set; }

        /// <summary>
        /// 参数的默认值（如果有）
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// 参数修饰符（ref, out, in, params等）
        /// </summary>
        public string Modifier { get; set; } = string.Empty;
    }
}