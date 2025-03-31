# DLL和Unity工程索引功能

本文档介绍了C#代码索引系统对DLL文件和Unity工程的索引支持。

## DLL索引功能

### 功能概述

系统现已支持对.NET程序集（DLL文件）的索引，可以提取程序集中的类型信息和成员定义，包括：

- 命名空间
- 类、接口、结构体、枚举、委托
- 方法、属性、字段、事件
- 泛型参数和约束
- 访问修饰符和其他修饰符

### 使用方法

#### 通过API索引DLL

```http
POST /api/CodeIndex/index/dll
Content-Type: application/json

{
  "DirectoryPath": "C:\Path\To\DLLs"
}
```

系统将扫描指定目录下的所有DLL文件，并建立索引。

## Unity工程索引功能

### 功能概述

系统现已支持对Unity工程的索引，包括：

- C#脚本文件
- 程序集（DLL）文件
- Unity特有文件（场景、预制体等）
- Unity引擎类型和生命周期方法的识别

### Unity特有功能

- **MonoBehaviour识别**：自动识别继承自MonoBehaviour的类
- **生命周期方法识别**：识别Unity特有的生命周期方法（如Awake、Start、Update等）
- **序列化字段识别**：识别带有[SerializeField]等特性的字段和属性
- **场景和预制体索引**：支持索引Unity场景和预制体文件

### 使用方法

#### 通过API索引Unity工程

```http
POST /api/CodeIndex/index/unity
Content-Type: application/json

{
  "DirectoryPath": "C:\Path\To\UnityProject"
}
```

系统将扫描指定的Unity工程目录，并建立索引。

## 查询示例

索引完成后，可以使用现有的查询API查询DLL和Unity工程中的代码元素：

### 查询Unity生命周期方法

```http
GET /api/CodeIndex/search/name/Update?maxResults=100
```

### 查询MonoBehaviour子类

```http
GET /api/CodeIndex/search/base/UnityEngine.MonoBehaviour?maxResults=100
```

### 查询序列化字段

```http
GET /api/CodeIndex/search/modifier/UnitySerializedField?maxResults=100
```

## 技术实现

### DLL解析

系统使用以下技术解析DLL文件：

- **System.Reflection**：用于基本的程序集反射
- **Mono.Cecil**：用于更详细的程序集分析，支持无需加载程序集到应用程序域

### Unity工程解析

系统使用以下策略解析Unity工程：

1. 解析C#脚本文件（使用Roslyn）
2. 解析程序集文件（使用Mono.Cecil）
3. 解析Unity特有文件（场景、预制体等）
4. 识别Unity特有类型和方法

## 注意事项

- 索引DLL文件时，系统不会加载程序集到应用程序域，因此不会执行任何代码
- 索引Unity工程时，系统会尝试查找Unity编辑器路径，以便解析Unity引擎程序集
- 对于大型Unity工程，索引过程可能需要较长时间