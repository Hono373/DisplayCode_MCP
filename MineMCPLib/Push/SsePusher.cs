using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MineMCPLib.Push;

/// <summary>
/// Service 端 SSE 推送服务
/// 用于向 Client 推送 MCP 工具执行结果、LLM 流式输出等
/// 
/// 使用方式：
/// 1. 在 ASP.NET Core Controller 中创建 SsePusher 实例
/// 2. 调用 RegisterClient 获取 sequenceId 并设置 SSE 响应
/// 3. 在后台任务中调用 PushAsync 推送消息
/// 4. Client 连接 SSE 端点接收消息
/// 
/// ASP.NET Core 示例:
/// <code>
/// [HttpGet("sse")]
/// public async Task SseEndpoint(CancellationToken ct)
/// {
///     var pusher = new SsePusher();
///     var connectionId = HttpContext.Connection.Id;
///     
///     // 注册并设置 SSE 头
///     Response.Headers.CacheControl = "no-cache";
///     Response.Headers.Connection = "keep-alive";
///     Response.ContentType = "text/event-stream";
///     
///     var sequenceId = pusher.RegisterClient(connectionId, (msg) =>
///     {
///         return Response.WriteAsync(msg.ToSseLine(), ct);
///     });
///     
///     // 发送初始消息
///     var init = PushMessage.ProgressMessage("connected", sequenceId: sequenceId);
///     await Response.WriteAsync(init.ToSseLine(), ct);
///     await Response.Body.FlushAsync(ct);
///     
///     // 保持连接直到断开
///     await Task.Delay(Timeout.Infinite, ct);
/// }
/// </code>
/// </summary>
public class SsePusher
{
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
    private readonly ILogger<SsePusher>? _logger;

    public SsePusher(ILogger<SsePusher>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册一个新的客户端连接
    /// </summary>
    /// <param name="connectionId">连接 ID</param>
    /// <param name="sendFunc">发送函数，接收 SSE 格式字符串并写入响应</param>
    /// <returns>分配的 sequenceId</returns>
    public string RegisterClient(string connectionId, Func<string, CancellationToken, Task> sendFunc)
    {
        var sequenceId = Guid.NewGuid().ToString("N")[..12];
        var client = new ClientConnection(connectionId, sequenceId, sendFunc);
        _clients[connectionId] = client;

        client.OnDisconnect += () => UnregisterClient(connectionId);
        _logger?.LogInformation("SSE 客户端注册: {ConnectionId}, SequenceId: {SequenceId}", connectionId, sequenceId);

        return sequenceId;
    }

    /// <summary>
    /// 注销客户端连接
    /// </summary>
    public void UnregisterClient(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out var client))
        {
            _logger?.LogInformation("SSE 客户端注销: {ConnectionId}", connectionId);
            client.Dispose();
        }
    }

    /// <summary>
    /// 向所有客户端推送消息
    /// </summary>
    public async Task PushToAllAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        var line = message.ToSseLine();
        var tasks = _clients.Values.Select(c => c.SendAsync(line, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 向特定 SequenceId 的客户端推送消息
    /// </summary>
    public async Task PushToClientAsync(string sequenceId, PushMessage message, CancellationToken cancellationToken = default)
    {
        var client = _clients.Values.FirstOrDefault(c => c.SequenceId == sequenceId);
        if (client != null)
        {
            var line = message.ToSseLine();
            await client.SendAsync(line, cancellationToken);
        }
    }

    /// <summary>
    /// 推送工具执行结果
    /// </summary>
    public async Task PushToolResultAsync(string toolName, string result, string? sequenceId = null, CancellationToken cancellationToken = default)
    {
        var message = PushMessage.ToolResultMessage(toolName, result, sequenceId);
        await PushToClientAsync(sequenceId ?? "*", message, cancellationToken);
    }

    /// <summary>
    /// 推送流式文本片段
    /// </summary>
    public async Task PushStreamChunkAsync(string chunk, string? sequenceId = null, bool isFinal = false, CancellationToken cancellationToken = default)
    {
        var message = PushMessage.StreamChunkMessage(chunk, sequenceId, isFinal);
        await PushToClientAsync(sequenceId ?? "*", message, cancellationToken);
    }

    /// <summary>
    /// 推送错误信息
    /// </summary>
    public async Task PushErrorAsync(string error, string? sequenceId = null, CancellationToken cancellationToken = default)
    {
        var message = PushMessage.ErrorMessage(error, sequenceId);
        await PushToClientAsync(sequenceId ?? "*", message, cancellationToken);
    }

    /// <summary>
    /// 推送完成消息
    /// </summary>
    public async Task PushCompleteAsync(string? summary = null, string? sequenceId = null, CancellationToken cancellationToken = default)
    {
        var message = PushMessage.CompleteMessage(summary, sequenceId);
        await PushToClientAsync(sequenceId ?? "*", message, cancellationToken);
    }

    /// <summary>
    /// 获取当前连接的客户端数量
    /// </summary>
    public int ClientCount => _clients.Count;

    /// <summary>
    /// 获取所有 SequenceId
    /// </summary>
    public IEnumerable<string> GetAllSequenceIds() => _clients.Values.Select(c => c.SequenceId);

    /// <summary>
    /// 根据 sequenceId 获取连接
    /// </summary>
    public ClientConnection? GetClientBySequenceId(string sequenceId)
    {
        return _clients.Values.FirstOrDefault(c => c.SequenceId == sequenceId);
    }
}

/// <summary>
/// SSE 客户端连接封装
/// </summary>
public class ClientConnection : IDisposable
{
    private readonly Func<string, CancellationToken, Task>? _sendFunc;
    private readonly CancellationTokenSource? _cts;

    /// <summary>
    /// 连接 ID
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// 序列 ID
    /// </summary>
    public string SequenceId { get; }

    /// <summary>
    /// 连接状态
    /// </summary>
    public bool IsConnected { get; private set; } = true;

    /// <summary>
    /// 断开连接事件
    /// </summary>
    public event Action? OnDisconnect;

    public ClientConnection(string connectionId, string sequenceId, Func<string, CancellationToken, Task>? sendFunc = null)
    {
        ConnectionId = connectionId;
        SequenceId = sequenceId;
        _sendFunc = sendFunc;
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    public async Task SendAsync(string data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return;

        try
        {
            if (_sendFunc != null)
            {
                await _sendFunc(data, cancellationToken);
            }
        }
        catch (Exception)
        {
            IsConnected = false;
            OnDisconnect?.Invoke();
        }
    }

    /// <summary>
    /// 标记连接断开
    /// </summary>
    public void Disconnect()
    {
        IsConnected = false;
        OnDisconnect?.Invoke();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

/// <summary>
/// SsePusher 扩展方法（用于 ASP.NET Core DI）
/// </summary>
public static class SsePusherExtensions
{
    /// <summary>
    /// 注册 SsePusher 到服务集合
    /// </summary>
    public static IServiceCollection AddSsePusher(this IServiceCollection services)
    {
        services.AddSingleton<SsePusher>();
        return services;
    }

    /// <summary>
    /// 注册 SsePusher 到服务集合（带日志）
    /// </summary>
    public static IServiceCollection AddSsePusher(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<SsePusher>();
        services.AddSingleton<SsePusher>(new SsePusher(logger));
        return services;
    }
}
