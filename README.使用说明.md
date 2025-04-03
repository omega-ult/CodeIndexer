# C#代码索引系统使用说明

## 项目简介

C#代码索引系统是一个强大的代码分析工具，能够解析C#代码库、DLL文件和Unity工程，并建立索引，支持高性能的模糊查询和精确查询功能。系统由三个主要组件组成：

- **CodeIndexer.Core**：核心库，包含代码解析、索引和查询功能
- **CodeIndexer.Service**：Web API服务，提供REST接口
- **CodeIndexer.Client**：客户端示例，展示如何使用API

## 系统要求

- **.NET 6.0 SDK**（或更高版本）
- **Windows**、**macOS**或**Linux**操作系统

## 快速开始

### 方法一：使用构建和运行脚本（推荐）

项目提供了一个PowerShell脚本，可以一键构建和运行整个系统：

```powershell
# 构建并运行服务端和客户端
.\build-and-run.ps1

# 只构建并运行服务端
.\build-and-run.ps1 -ServiceOnly

# 只构建并运行客户端
.\build-and-run.ps1 -ClientOnly

# 跳过构建步骤，直接运行
.\build-and-run.ps1 -NoBuild
```

### 方法二：手动构建和运行

#### 1. 构建项目

```bash
# 恢复NuGet包
dotnet restore

# 构建解决方案
dotnet build -c Release
```

#### 2. 运行服务端

```bash
cd CodeIndexer.Service
dotnet run -c Release
```

服务将在 http://localhost:5000 启动，可以通过 http://localhost:5000/swagger 访问API文档。

#### 3. 运行客户端

```bash
cd CodeIndexer.Client
dotnet run -c Release
```

## 使用指南

### 服务端API

服务启动后，可以通过以下API接口使用系统功能：

#### 索引代码目录

```http
POST /api/codeindex/index
Content-Type: application/json

{
  "directoryPath": "C:\Projects\MyProject"
}
```

#### 索引DLL文件目录

```http
POST /api/codeindex/index/dll
Content-Type: application/json

{
  "directoryPath": "C:\Path\To\DLLs"
}
```

#### 索引Unity工程

```http
POST /api/codeindex/index/unity
Content-Type: application/json

{
  "directoryPath": "C:\Path\To\UnityProject"
}
```

#### 按名称模糊查询

```http
GET /api/codeindex/search/name/{namePattern}?maxResults=100
```

#### 按全名精确查询

```http
GET /api/codeindex/search/fullname/{fullName}
```

#### 按元素类型查询

```http
GET /api/codeindex/search/type/{elementType}?maxResults=1000
```

#### 按父元素查询子元素

```http
GET /api/codeindex/search/parent/{parentId}?maxResults=1000
```

### 客户端使用

客户端是一个交互式控制台应用程序，启动后按照提示操作即可：

1. 选择操作（索引目录、查询等）
2. 根据提示输入参数
3. 查看结果

## 常见问题

### 服务无法启动

- 检查端口5000是否被占用，如果被占用，可以修改`CodeIndexer.Service/Properties/launchSettings.json`中的端口配置
- 确保已安装.NET 6.0 SDK或更高版本

### 索引失败

- 确保指定的目录路径存在且有访问权限
- 检查目录中是否包含有效的C#代码文件或DLL文件

### 查询无结果

- 确保已成功完成索引步骤
- 尝试使用更简单的查询条件

## 技术支持

如有问题或建议，请提交Issue或联系项目维护者。