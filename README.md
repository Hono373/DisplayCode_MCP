# PicAnalysis

图片自动分类服务，基于 AI 的图片分析工具。

## 功能概述

自动监控文件夹，使用 AI 分析图片内容并分类。

## 工作流程

```
监控文件夹 ──→ AI 分析 ──→ 分类归档
     ↓
  图片文件
```

## 分类规则

| 分类 | 说明 |
|------|------|
| **纯图片** | 无文字内容，如风景、表情包等 |
| **有用图片** | 含文字、漫画分镜等有价值内容 |
| **非图片** | 误识别的非图片文件 |

## 技术栈

- **协议**: MCP (Model Context Protocol)
- **框架**: ASP.NET Core
- **AI**: OpenAI 图片分析

## 目录结构

```
PicAnalysis/
├── Program.cs           # 服务入口
├── Tools/MyMcp.cs       # 核心分类逻辑
└── Serialize/          # 数据持久化
```

## 快速开始

```bash
cd PicAnalysis
dotnet run
```

服务运行于 `http://localhost:5000/mcp`
