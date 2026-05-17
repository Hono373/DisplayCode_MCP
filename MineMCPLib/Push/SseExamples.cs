namespace MineMCPLib.Push.Usage;

/// <summary>
/// SSE 推送功能使用示例
/// 演示 Service 端推送和 Client 端接收的完整流程
/// </summary>

// =============================================================================
// 1. Service 端示例 (ASP.NET Core Minimal API)
// =============================================================================

/*
// Program.cs 中注册服务
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSsePusher();
var app = builder.Build();

// SSE 端点
app.MapGet("/sse", async (HttpContext context, SsePusher pusher) =>
{
    await SsePusher.HandleSseEndpoint(context, pusher, context.RequestAborted);
});

// MCP 工具执行后推送结果
app.MapPost("/mcp/execute", async (string toolName, string args, SsePusher pusher) =>
{
    var sequenceId = Guid.NewGuid().ToString("N")[..12];
    
    // 执行工具
    var result = await ExecuteToolAsync(toolName, args);
    
    // 推送结果给客户端
    await pusher.PushToolResultAsync(toolName, result, sequenceId);
    
    return Results.Ok(new { sequenceId, status = "completed" });
});

// LLM 流式输出示例
app.MapPost("/mcp/stream", async (string prompt, SsePusher pusher, CancellationToken ct) =>
{
    var sequenceId = Guid.NewGuid().ToString("N")[..12];
    var client = MEAILLMTalk.GetClient("http://localhost:1234/v1", "llama3");
    
    var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, prompt)
    };
    
    try
    {
        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            if (ct.IsCancellationRequested) break;
            
            await pusher.PushStreamChunkAsync(
                update.Text ?? "", 
                sequenceId, 
                isFinal: false,
                ct);
        }
        
        await pusher.PushStreamEndMessage(sequenceId, ct);
    }
    catch (Exception ex)
    {
        await pusher.PushErrorAsync(ex.Message, sequenceId, ct);
    }
    
    return Results.Ok(new { sequenceId });
});

app.Run();
*/

// =============================================================================
// 2. Client 端示例
// =============================================================================

/*
// 方式 A: 事件回调模式
async Task EventBasedExample()
{
    var receiver = new SseReceiver();
    
    // 注册事件处理器
    receiver.OnToolResult += async msg =>
    {
        Console.WriteLine($"[工具结果] {msg.ToolName}: {msg.Content}");
        // 收到工具结果后，可以回传给 LLM 继续推理
        var llmResponse = await MEAILLMTalk.SendTextToLLMAsync(
            $"工具 {msg.ToolName} 返回: {msg.Content}",
            "llama3",
            "http://localhost:1234/v1");
        Console.WriteLine($"[LLM 回复] {llmResponse}");
    };
    
    receiver.OnStreamChunk += msg =>
    {
        Console.Write(msg.Content);
        return Task.CompletedTask;
    };
    
    receiver.OnStreamEnd += msg =>
    {
        Console.WriteLine("\n[流式结束]");
        return Task.CompletedTask;
    };
    
    receiver.OnError += msg =>
    {
        Console.WriteLine($"[错误] {msg.Content}");
        return Task.CompletedTask;
    };
    
    // 连接 SSE
    await receiver.ConnectAsync("http://localhost:5000/sse");
    
    // 等待接收
    await Task.Delay(TimeSpan.FromMinutes(5));
    
    await receiver.DisconnectAsync();
}

// 方式 B: IAsyncEnumerable 模式（推荐用于流式）
async Task EnumerableBasedExample()
{
    var receiver = new SseReceiver();
    
    await receiver.ConnectAsync("http://localhost:5000/sse");
    var sequenceId = receiver.SequenceId;
    
    Console.WriteLine($"已连接，SequenceId: {sequenceId}");
    
    // 获取流式输出
    await foreach (var chunk in receiver.ToStreamEnumerable(sequenceId))
    {
        Console.Write(chunk);
    }
    
    Console.WriteLine("\n流式输出完成");
    
    await receiver.DisposeAsync();
}

// 方式 C: 完整 MCP 工具调用 + SSE 接收
async Task McpToolCallWithSse()
{
    var receiver = new SseReceiver();
    
    receiver.OnToolResult += async msg =>
    {
        Console.WriteLine($"收到工具结果: {msg.ToolName}");
        Console.WriteLine($"结果: {msg.Content}");
        
        // 继续 LLM 推理
        var nextPrompt = $"基于工具 {msg.ToolName} 的结果 {msg.Content}，继续分析...";
        var llmResponse = await MEAILLMTalk.SendTextToLLMAsync(nextPrompt, "llama3");
        
        Console.WriteLine($"LLM 继续推理: {llmResponse}");
    };
    
    await receiver.ConnectAsync("http://localhost:5000/sse");
    
    // 调用 MCP 工具（服务端执行后通过 SSE 推送结果）
    using var httpClient = new HttpClient();
    var request = new
    {
        sequenceId = receiver.SequenceId,
        toolName = "web_search",
        args = new { query = "C# 异步编程最佳实践" }
    };
    
    var response = await httpClient.PostAsJsonAsync(
        "http://localhost:5000/mcp/execute",
        request);
    
    Console.WriteLine($"工具调用请求已发送: {response.StatusCode}");
    
    await Task.Delay(TimeSpan.FromMinutes(2));
    await receiver.DisconnectAsync();
}
*/

// =============================================================================
// 3. 架构图
// =============================================================================

/*
                    ┌─────────────────────────────────────────────────────────────────┐
                    │                        Service 端                               │
                    │  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐  │
                    │  │ MCP Server │───▶│   LLM 引擎   │───▶│     SsePusher        │  │
                    │  │ (工具执行)  │    │  (IChatClient)│    │  PushMessage         │  │
                    │  └─────────────┘    └─────────────┘    │  Type/Content/       │  │
                    │         │                │             │  SequenceId          │  │
                    │         │                │             └──────────┬──────────┘  │
                    │         │                │                          │             │
                    └─────────┼────────────────┼──────────────────────────┼─────────────┘
                              │                │                          │
                              │ SSE POST       │ Stream Chunk             │ SSE GET
                              │ (触发工具)      │ (流式输出)                │ (长连接)
                              ▼                ▼                          ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│                                    HTTP 传输层                                           │
└───────────────────────────────────────────────────────────────────────────────────────┘
                              │                │                          │
                              ▼                ▼                          ▼
                    ┌─────────┼────────────────┼──────────────────────────┼─────────────┐
                    │         ▼                │                          ▼             │
                    │  ┌─────────────────────────────────────────────────────────┐   │
                    │  │                     Client 端                            │   │
                    │  │  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │   │
                    │  │  │   前端 UI   │◀───│  LLM 推理    │◀───│  SseReceiver │  │   │
                    │  │  │  (压缩显示)  │    │ (继续推理)   │    │ (接收消息)   │  │   │
                    │  │  └─────────────┘    └─────────────┘    └─────────────┘  │   │
                    │  │                                                    │       │   │
                    │  │  原则：Service 推送引擎层数据                         │       │   │
                    │  │         Client 决定如何压缩后暴露给前端              │       │   │
                    │  └─────────────────────────────────────────────────────────┘   │
                    │                                                                   │
                    └───────────────────────────────────────────────────────────────────┘

    PushMessage 结构示例:
    ┌─────────────────────────────────────────────────────────┐
    │  {                                                      │
    │    "type": "ToolResult",     // 或 StreamChunk/Error等  │
    │    "toolName": "web_search",                       │
    │    "content": "找到 10 条结果...",                   │
    │    "sequenceId": "abc123",                          │
    │    "metadata": { "tokens": 150, "latency": 230 },    │
    │    "isFinal": true                                  │
    │  }                                                      │
    └─────────────────────────────────────────────────────────┘
*/

public static class SseUsageExamples
{
    /// <summary>
    /// 运行示例（需先启动 Service 端）
    /// </summary>
    public static async Task RunExamples()
    {
        Console.WriteLine("=== SSE 推送功能使用示例 ===");
        Console.WriteLine("请参考代码注释中的示例用法");
        await Task.CompletedTask;
    }
}
