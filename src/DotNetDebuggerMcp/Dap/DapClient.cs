using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotNetDebuggerMcp.Dap;

/// <summary>
/// Communicates with netcoredbg via its stdin/stdout pipes using the DAP protocol.
/// This is the same transport VS Code uses and works with all netcoredbg versions.
/// </summary>
public class DapClient : IAsyncDisposable
{
    private readonly ILogger<DapClient> _logger;
    private readonly DebugSession _session;

    private Process? _netcoredbgProcess;
    private Stream? _writeStream;   // netcoredbg stdin
    private Stream? _readStream;    // netcoredbg stdout
    private CancellationTokenSource _cts = new();
    private Task? _readerTask;

    private int _seq = 0;
    private readonly Dictionary<int, TaskCompletionSource<JObject>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public DapClient(ILogger<DapClient> logger, DebugSession session)
    {
        _logger = logger;
        _session = session;
    }

    public bool IsConnected => _writeStream != null && !(_netcoredbgProcess?.HasExited ?? true);

    public Task StartAsync()
    {
        if (IsConnected)
            throw new InvalidOperationException("DAP client is already running.");

        _cts = new CancellationTokenSource();

        string netcoredbgPath = Environment.GetEnvironmentVariable("NETCOREDBG_PATH") ?? "netcoredbg";

        _logger.LogInformation("Starting netcoredbg at {Path}", netcoredbgPath);

        var psi = new ProcessStartInfo
        {
            FileName = netcoredbgPath,
            Arguments = "--interpreter=vscode",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,  // let stderr go to our stderr
            CreateNoWindow = true,
        };

        _netcoredbgProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start netcoredbg process.");

        _logger.LogInformation("netcoredbg started with PID {Pid}", _netcoredbgProcess.Id);

        _writeStream = _netcoredbgProcess.StandardInput.BaseStream;
        _readStream = _netcoredbgProcess.StandardOutput.BaseStream;

        _readerTask = Task.Run(ReadLoopAsync, _cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await _cts.CancelAsync();

        try { _writeStream?.Close(); } catch { }
        try { _readStream?.Close(); } catch { }
        try
        {
            if (_netcoredbgProcess != null && !_netcoredbgProcess.HasExited)
            {
                _netcoredbgProcess.Kill(entireProcessTree: true);
                await _netcoredbgProcess.WaitForExitAsync();
            }
        }
        catch { }

        if (_readerTask != null)
        {
            try { await _readerTask.WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
        }

        _writeStream = null;
        _readStream = null;
        _netcoredbgProcess = null;

        // Cancel all pending requests
        lock (_pending)
        {
            foreach (var tcs in _pending.Values)
                tcs.TrySetCanceled();
            _pending.Clear();
        }
    }

    public async Task<JObject> SendRequestAsync(string command, object? arguments = null, CancellationToken ct = default)
    {
        if (_writeStream == null)
            throw new InvalidOperationException("DAP client is not running.");

        int seq = Interlocked.Increment(ref _seq);

        var request = new DapRequest
        {
            Seq = seq,
            Command = command,
            Arguments = arguments != null
                ? JObject.FromObject(arguments, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }))
                : null
        };

        var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pending)
            _pending[seq] = tcs;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        linked.Token.Register(() => tcs.TrySetCanceled());

        byte[] data = DapProtocol.Encode(request);

        await _writeLock.WaitAsync(linked.Token);
        try
        {
            await _writeStream.WriteAsync(data, linked.Token);
            await _writeStream.FlushAsync(linked.Token);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger.LogDebug("→ {Command} (seq={Seq})", command, seq);

        JObject response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

        if (!response["success"]?.Value<bool>() ?? false)
        {
            string msg = response["message"]?.Value<string>() ?? "DAP request failed";
            throw new DapException(command, msg);
        }

        return response;
    }

    private async Task ReadLoopAsync()
    {
        _logger.LogDebug("DAP read loop started");
        try
        {
            while (!_cts.Token.IsCancellationRequested && _readStream != null)
            {
                JObject? msg = await DapProtocol.ReadMessageAsync(_readStream, _cts.Token);
                if (msg == null)
                {
                    _logger.LogInformation("DAP stream closed (netcoredbg exited)");
                    _session.SetState(SessionState.Terminated);
                    break;
                }

                string type = msg["type"]?.Value<string>() ?? "";
                _logger.LogDebug("← {Type} {Extra}", type,
                    type == "response" ? msg["command"]?.Value<string>() : msg["event"]?.Value<string>());

                if (type == "response")
                {
                    int requestSeq = msg["request_seq"]?.Value<int>() ?? -1;
                    TaskCompletionSource<JObject>? pending;
                    lock (_pending)
                        _pending.Remove(requestSeq, out pending);
                    pending?.TrySetResult(msg);
                }
                else if (type == "event")
                {
                    HandleEvent(msg);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DAP read loop error");
            _session.SetState(SessionState.Terminated);
        }
        _logger.LogDebug("DAP read loop ended");
    }

    private void HandleEvent(JObject msg)
    {
        string eventType = msg["event"]?.Value<string>() ?? "";
        JToken? body = msg["body"];

        switch (eventType)
        {
            case "initialized":
                _session.SetState(SessionState.Running);
                _session.EnqueueEvent(new DebugEvent { Type = DebugEventType.Initialized });
                break;

            case "stopped":
                _session.SetState(SessionState.Paused);
                int threadId = body?["threadId"]?.Value<int>() ?? 0;
                string reason = body?["reason"]?.Value<string>() ?? "unknown";
                string? desc = body?["description"]?.Value<string>();
                string? hitBpIds = body?["hitBreakpointIds"] != null
                    ? string.Join(",", body["hitBreakpointIds"]!.Values<int>())
                    : null;
                _session.SetStoppedThread(threadId);
                _session.EnqueueEvent(new DebugEvent
                {
                    Type = DebugEventType.Stopped,
                    Message = $"Stopped ({reason})" + (desc != null ? $": {desc}" : "") +
                              (hitBpIds != null ? $" [bp ids: {hitBpIds}]" : ""),
                    ThreadId = threadId
                });
                break;

            case "continued":
                _session.SetState(SessionState.Running);
                _session.EnqueueEvent(new DebugEvent { Type = DebugEventType.Continued });
                break;

            case "exited":
                int exitCode = body?["exitCode"]?.Value<int>() ?? -1;
                _session.SetState(SessionState.Terminated);
                _session.EnqueueEvent(new DebugEvent
                {
                    Type = DebugEventType.Exited,
                    Message = $"Process exited with code {exitCode}"
                });
                break;

            case "terminated":
                _session.SetState(SessionState.Terminated);
                _session.EnqueueEvent(new DebugEvent { Type = DebugEventType.Terminated, Message = "Debug session terminated" });
                break;

            case "output":
                string category = body?["category"]?.Value<string>() ?? "console";
                string output = body?["output"]?.Value<string>() ?? "";
                if (!string.IsNullOrEmpty(output))
                {
                    _session.AppendOutput(output);
                    _session.EnqueueEvent(new DebugEvent
                    {
                        Type = DebugEventType.Output,
                        Message = $"[{category}] {output.TrimEnd()}"
                    });
                }
                break;

            case "breakpoint":
                var bp = body?["breakpoint"];
                if (bp != null)
                {
                    int bpId = bp["id"]?.Value<int>() ?? 0;
                    bool verified = bp["verified"]?.Value<bool>() ?? false;
                    _session.EnqueueEvent(new DebugEvent
                    {
                        Type = DebugEventType.BreakpointChanged,
                        Message = $"Breakpoint {bpId} {(verified ? "verified" : "unverified")}"
                    });
                }
                break;

            case "thread":
                int tid = body?["threadId"]?.Value<int>() ?? 0;
                string threadReason = body?["reason"]?.Value<string>() ?? "";
                _session.EnqueueEvent(new DebugEvent
                {
                    Type = DebugEventType.Thread,
                    Message = $"Thread {tid} {threadReason}",
                    ThreadId = tid
                });
                break;

            default:
                _logger.LogDebug("Unhandled DAP event: {EventType}", eventType);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
        _netcoredbgProcess?.Dispose();
        _writeLock.Dispose();
    }
}

public class DapException : Exception
{
    public string Command { get; }

    public DapException(string command, string message)
        : base($"DAP '{command}' failed: {message}")
    {
        Command = command;
    }
}
