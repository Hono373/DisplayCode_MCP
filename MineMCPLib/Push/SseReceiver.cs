using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace MineMCPLib.Push;

/// <summary>
/// Client 端 SSE 接收器
/// 用于接收 Service 推送的 MCP 工具执行结果、LLM 流式输出等
/// 
/// 使用方式：
/// 1. 创建 SseReceiver 实例并注册消息处理回调
/// 2. 调用 ConnectAsync 建立 SSE 连接
/// 3. 在回调中处理接收到的 PushMessage
/// 4. 断开连接时调用 DisconnectAsync
/// </summary>
public class SseReceiver : IAsyncDisposable
{
    private HttpClient? _httpClient;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly ILogger<SseReceiver>? _logger;

    /// <summary>
    /// 序列 ID（连接成功后从服务端获取）
    /// </summary>
    public string? SequenceId { get; private set; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _receiveTask != null && !_receiveTask.IsCompleted;

    /// <summary>
    /// 接收到工具结果时的回调
    /// </summary>
    public event Func<PushMessage, Task>? OnToolResult;

    /// <summary>
    /// 接收到流式文本片段时的回调
    /// </summary>
    public event Func<PushMessage, Task>? OnStreamChunk;

    /// <summary>
    /// 接收到流式结束信号时的回调
    /// </summary>
    public event Func<PushMessage, Task>? OnStreamEnd;

    /// <summary>
    /// 接收到错误信息时的回调
    /// </summary>
    public event Func<PushMessage, Task>? OnError;

    /// <summary>
    /// 接收到完成信号时的回调
    /// </summary>
    public event Func<PushMessage, Task>? OnComplete;

    /// <summary>
    /// 接收到任意消息时的回调
    /// </summary>
    public event Func<PushMessage, Task>? OnMessage;

    /// <summary>
    /// 连接状态变更时的回调
    /// </summary>
    public event Action<bool>? OnConnectionChanged;

    /// <summary>
    /// SSE 事件流缓冲区
    /// </summary>
    private readonly StringBuilder _buffer = new();

    public SseReceiver(ILogger<SseReceiver>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接到 SSE 端点
    /// </summary>
    /// <param name="sseUrl">SSE 端点 URL，如 http://localhost:5000/sse</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ConnectAsync(string sseUrl, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger?.LogWarning("已连接到 SSE 端点，请先断开连接");
            return;
        }

        _httpClient = new HttpClient();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _logger?.LogInformation("正在连接到 SSE 端点: {Url}", sseUrl);

        try
        {
            var response = await _httpClient.GetAsync(
                sseUrl,
                HttpCompletionOption.ResponseHeadersRead,
                _cts.Token);

            response.EnsureSuccessStatusCode();

            _receiveTask = ReceiveLoop(response, _cts.Token);
            OnConnectionChanged?.Invoke(true);

            // 等待连接初始化（接收初始 sequenceId）
            await Task.Delay(500, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "连接 SSE 端点失败");
            throw;
        }
    }

    /// <summary>
    /// 断开 SSE 连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        _logger?.LogInformation("正在断开 SSE 连接");

        _cts?.Cancel();

        try
        {
            if (_receiveTask != null)
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (OperationCanceledException)
        {
            // 预期取消
        }
        catch (TimeoutException)
        {
            _logger?.LogWarning("SSE 接收任务未能在超时时间内结束");
        }
        finally
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _cts?.Dispose();
            _cts = null;
            _receiveTask = null;
            OnConnectionChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// SSE 接收循环
    /// </summary>
    private async Task ReceiveLoop(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var reader = new StreamReader(stream);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    break;
                }

                ProcessSseLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            // 预期取消
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SSE 接收循环异常");
        }
        finally
        {
            OnConnectionChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// 处理 SSE 行
    /// </summary>
    private void ProcessSseLine(string line)
    {
        // SSE 格式: "data: {json}\n\n"
        if (line.StartsWith("data: "))
        {
            var json = line["data: ".Length..].Trim();

            // 忽略注释行（心跳）
            if (json.StartsWith(":"))
            {
                return;
            }

            try
            {
                var message = JsonSerializer.Deserialize<PushMessage>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                if (message != null)
                {
                    // 首次连接时保存 sequenceId
                    if (SequenceId == null && message.Type == PushMessageType.Progress && message.Content == "connected")
                    {
                        SequenceId = message.SequenceId;
                        _logger?.LogInformation("SSE 连接成功，SequenceId: {SequenceId}", SequenceId);
                    }

                    DispatchMessage(message);
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "解析 SSE 消息失败: {Json}", json);
            }
        }
    }

    /// <summary>
    /// 分发消息到对应的事件处理器
    /// </summary>
    private void DispatchMessage(PushMessage message)
    {
        _logger?.LogDebug("收到 PushMessage: Type={Type}, SequenceId={SequenceId}", message.Type, message.SequenceId);

        // 触发通用消息事件
        OnMessage?.Invoke(message);

        // 根据类型触发专用事件
        switch (message.Type)
        {
            case PushMessageType.ToolResult:
                OnToolResult?.Invoke(message);
                break;

            case PushMessageType.StreamChunk:
                OnStreamChunk?.Invoke(message);
                break;

            case PushMessageType.StreamEnd:
                OnStreamEnd?.Invoke(message);
                break;

            case PushMessageType.Error:
                OnError?.Invoke(message);
                break;

            case PushMessageType.Progress:
                // 进度消息暂时不触发专用事件
                break;

            case PushMessageType.Complete:
                OnComplete?.Invoke(message);
                break;
        }
    }

    /// <summary>
    /// 异步释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

/// <summary>
/// SseReceiver 扩展方法 - 便捷的流式处理
/// </summary>
public static class SseReceiverExtensions
{
    /// <summary>
    /// 将 SSE 流式消息转换为 IAsyncEnumerable
    /// </summary>
    public static IAsyncEnumerable<string> ToStreamEnumerable(
        this SseReceiver receiver,
        string? targetSequenceId = null,
        CancellationToken cancellationToken = default)
    {
        return new SseStreamEnumerable(receiver, targetSequenceId, cancellationToken);
    }

    private class SseStreamEnumerable : IAsyncEnumerable<string>
    {
        private readonly SseReceiver _receiver;
        private readonly string? _targetSequenceId;
        private readonly CancellationToken _cancellationToken;

        public SseStreamEnumerable(SseReceiver receiver, string? targetSequenceId, CancellationToken cancellationToken)
        {
            _receiver = receiver;
            _targetSequenceId = targetSequenceId;
            _cancellationToken = cancellationToken;
        }

        public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new SseStreamEnumerator(_receiver, _targetSequenceId, _cancellationToken);
        }
    }

    private class SseStreamEnumerator : IAsyncEnumerator<string>
    {
        private readonly SseReceiver _receiver;
        private readonly string? _targetSequenceId;
        private readonly CancellationToken _cancellationToken;
        private readonly Channel<string> _channel;

        public SseStreamEnumerator(SseReceiver receiver, string? targetSequenceId, CancellationToken cancellationToken)
        {
            _receiver = receiver;
            _targetSequenceId = targetSequenceId;
            _cancellationToken = cancellationToken;
            _channel = Channel.CreateUnbounded<string>();

            // 注册流式消息回调
            _receiver.OnStreamChunk += async message =>
            {
                if (IsTargetMessage(message))
                {
                    await _channel.Writer.WriteAsync(message.Content ?? "", _cancellationToken);
                }
            };

            _receiver.OnStreamEnd += message =>
            {
                if (IsTargetMessage(message))
                {
                    _channel.Writer.Complete();
                }
                return Task.CompletedTask;
            };

            _receiver.OnError += message =>
            {
                if (IsTargetMessage(message))
                {
                    _channel.Writer.TryComplete();
                }
                return Task.CompletedTask;
            };
        }

        private bool IsTargetMessage(PushMessage message)
        {
            return _targetSequenceId == null || message.SequenceId == _targetSequenceId;
        }

        public string Current => default!;

        public ValueTask<bool> MoveNextAsync()
        {
            return _channel.Reader.WaitToReadAsync(_cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
