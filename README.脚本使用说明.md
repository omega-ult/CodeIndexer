# CodeIndexer 脚本使用说明

## 概述

本项目提供了两个主要脚本用于编译和运行 CodeIndexer 应用程序：

1. `build.ps1` - 用于编译整个解决方案
2. `run.ps1` - 用于启动服务端和客户端组件

这些脚本设计为独立运行，支持多种运行模式，包括测试模式。

## 编译脚本 (build.ps1)

### 功能

- 检查 .NET SDK 是否已安装
- 清理解决方案（可选）
- 恢复 NuGet 包
- 编译整个解决方案

### 参数

- `-Configuration <配置名称>` - 指定编译配置，默认为 "Release"
- `-Clean` - 在编译前清理解决方案

### 使用示例

```powershell
# 使用默认配置（Release）编译
.\build.ps1

# 使用 Debug 配置编译
.\build.ps1 -Configuration Debug

# 清理并编译
.\build.ps1 -Clean
```

## 运行脚本 (run.ps1)

### 功能

- 可选择是否在运行前编译
- 支持独立启动服务端或客户端
- 提供测试模式，可以运行项目测试
- 自动打开多个 PowerShell 窗口运行各组件

### 参数

- `-ServiceOnly` - 仅启动服务端
- `-ClientOnly` - 仅启动客户端
- `-NoBuild` - 跳过编译步骤
- `-TestMode` - 运行测试
- `-Configuration <配置名称>` - 指定运行配置，默认为 "Release"

### 使用示例

```powershell
# 编译并启动所有组件
.\run.ps1

# 仅启动服务端
.\run.ps1 -ServiceOnly

# 仅启动客户端（不编译）
.\run.ps1 -ClientOnly -NoBuild

# 运行测试
.\run.ps1 -TestMode

# 运行测试后启动服务端
.\run.ps1 -TestMode -ServiceOnly
```

## 注意事项

1. 这些脚本需要在 PowerShell 环境中运行
2. 需要安装 .NET 6.0 SDK 或更高版本
3. 每个组件会在单独的 PowerShell 窗口中运行，关闭窗口即可停止相应组件
4. 服务端默认在 http://localhost:5000 运行，Swagger 文档在 http://localhost:5000/swagger

## 故障排除

如果遇到问题，请检查：

1. .NET SDK 是否正确安装
2. 是否有足够的权限运行脚本
3. 端口 5000 是否被其他应用占用

如需更多帮助，请参考项目文档或提交 Issue。