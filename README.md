# C#代码索引系统

这是一个强大的C#代码索引系统，能够解析C#代码库并建立索引，支持高性能的模糊查询和精确查询功能。系统可以索引命名空间、类、接口、结构体、枚举以及各种成员（方法、属性、字段等），并提供REST API接口供客户端调用。

## 功能特点

- **代码解析**：使用Roslyn（Microsoft.CodeAnalysis）解析C#代码，提取代码结构和元素信息
- **索引建立**：使用Lucene.Net建立全文索引，支持高效的查询和搜索
- **查询功能**：
  - 按名称模糊查询
  - 按全名精确查询
  - 按元素类型查询
  - 按父元素查询子元素
  - 高级组合查询
- **代码重构支持**：通过维护元素的哈希值和版本号，支持检测代码变更
- **REST API**：提供RESTful API接口，方便集成到各种开发环境
- **客户端示例**：包含控制台客户端示例，展示如何使用API

## 项目结构

- **CodeIndexer.Core**：核心库，包含代码解析、索引和查询功能
- **CodeIndexer.Service**：Web API服务，提供REST接口
- **CodeIndexer.Client**：客户端示例，展示如何使用API

## 快速开始

### 构建项目

```bash
dotnet build
```

### 启动服务

```bash
cd CodeIndexer.Service
dotnet run
```

服务将在 http://localhost:5000 启动，可以通过 http://localhost:5000/swagger 访问API文档。

### 使用客户端

```bash
cd CodeIndexer.Client
dotnet run
```

按照控制台提示操作，可以索引代码目录并执行各种查询。

## API接口

### 索引代码目录

```
POST /api/codeindex/index
```

请求体：
```json
{
  "directoryPath": "C:\\Projects\\MyProject"
}
```

### 按名称模糊查询

```
GET /api/codeindex/search/name/{namePattern}?maxResults=100
```

### 按全名精确查询

```
GET /api/codeindex/search/fullname/{fullName}
```

### 按元素类型查询

```
GET /api/codeindex/search/type/{elementType}?maxResults=1000
```

### 按父元素查询子元素

```
GET /api/codeindex/search/parent/{parentId}?maxResults=1000
```

### 高级查询

```
POST /api/codeindex/search/advanced?maxResults=100
```

请求体：
```json
{
  "namePattern": "Get*",
  "elementType": "Method",
  "accessModifier": "public",
  "parentId": "some-parent-id",
  "returnType": "string"
}
```

### 获取元素详情

```
GET /api/codeindex/element/{id}
```

## 技术栈

- **.NET 6**：跨平台的高性能框架
- **Roslyn**：微软的C#编译器平台，用于代码解析
- **Lucene.Net**：高性能的全文搜索引擎
- **ASP.NET Core**：用于构建Web API

## 扩展和定制

系统设计为可扩展的，可以根据需求进行定制：

- **支持更多语言**：可以扩展代码解析器，支持更多编程语言
- **自定义索引字段**：可以根据需求添加更多索引字段
- **集成到IDE**：可以开发IDE插件，集成到Visual Studio或VS Code
- **分布式部署**：可以将索引服务部署为分布式系统，支持更大规模的代码库

## 处理代码重构和命名变更

系统通过以下机制处理代码重构和命名变更：

1. **内容哈希**：每个代码元素都有一个内容哈希值，用于检测变更
2. **版本控制**：元素有版本号，可以跟踪变更历史
3. **引用关系**：维护元素之间的引用关系，当元素变更时可以更新相关引用
4. **增量更新**：支持增量更新索引，只处理变更的文件

## 许可证

MIT