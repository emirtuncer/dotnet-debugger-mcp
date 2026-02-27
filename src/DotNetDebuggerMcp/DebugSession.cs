using System.Collections.Concurrent;
using DotNetDebuggerMcp.Dap;

namespace DotNetDebuggerMcp;

public enum SessionState
{
    Disconnected,
    Initializing,
    Running,
    Paused,
    Terminated
}

public enum DebugEventType
{
    Initialized,
    Stopped,
    Continued,
    Exited,
    Terminated,
    Output,
    BreakpointChanged,
    Thread
}

public class DebugEvent
{
    public DebugEventType Type { get; set; }
    public string? Message { get; set; }
    public int? ThreadId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class BreakpointInfo
{
    public int Id { get; set; }
    public string File { get; set; } = "";
    public int Line { get; set; }
    public string? Condition { get; set; }
    public bool Verified { get; set; }
    public string? FunctionName { get; set; }
}

public class DebugSession
{
    private volatile SessionState _state = SessionState.Disconnected;
    private volatile int _stoppedThreadId = 0;

    private readonly ConcurrentQueue<DebugEvent> _events = new();
    private const int MaxEvents = 500;

    private readonly object _outputLock = new();
    private readonly List<string> _output = new();
    private const int MaxOutputLines = 1000;

    private readonly object _breakpointLock = new();
    private readonly Dictionary<int, BreakpointInfo> _breakpoints = new();

    public SessionState State => _state;
    public int StoppedThreadId => _stoppedThreadId;

    public void SetState(SessionState state) => _state = state;

    public void SetStoppedThread(int threadId) => _stoppedThreadId = threadId;

    public void Reset()
    {
        _state = SessionState.Disconnected;
        _stoppedThreadId = 0;
        while (_events.TryDequeue(out _)) { }
        lock (_outputLock) _output.Clear();
        lock (_breakpointLock) _breakpoints.Clear();
    }

    // Events
    public void EnqueueEvent(DebugEvent evt)
    {
        _events.Enqueue(evt);

        // Trim oldest if too many
        while (_events.Count > MaxEvents)
            _events.TryDequeue(out _);
    }

    public IReadOnlyList<DebugEvent> DrainEvents(int max = 50)
    {
        var result = new List<DebugEvent>(Math.Min(max, _events.Count));
        for (int i = 0; i < max && _events.TryDequeue(out var evt); i++)
            result.Add(evt);
        return result;
    }

    // Output buffer
    public void AppendOutput(string text)
    {
        lock (_outputLock)
        {
            // Split by lines for easier management
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (_output.Count >= MaxOutputLines)
                    _output.RemoveAt(0);
                _output.Add(line);
            }
        }
    }

    public IReadOnlyList<string> ReadOutput(int maxLines = 100)
    {
        lock (_outputLock)
        {
            int take = Math.Min(maxLines, _output.Count);
            var result = _output.TakeLast(take).ToList();
            _output.Clear();
            return result;
        }
    }

    // Breakpoints
    public void AddBreakpoint(BreakpointInfo bp)
    {
        lock (_breakpointLock)
            _breakpoints[bp.Id] = bp;
    }

    public void RemoveBreakpoint(int id)
    {
        lock (_breakpointLock)
            _breakpoints.Remove(id);
    }

    public IReadOnlyList<BreakpointInfo> GetBreakpoints()
    {
        lock (_breakpointLock)
            return [.. _breakpoints.Values];
    }

    public bool TryGetBreakpoint(int id, out BreakpointInfo? bp)
    {
        lock (_breakpointLock)
            return _breakpoints.TryGetValue(id, out bp);
    }

    public void ClearBreakpoints()
    {
        lock (_breakpointLock)
            _breakpoints.Clear();
    }

    // Helper: get a default thread id for requests that need one
    public int GetDefaultThreadId()
    {
        int tid = _stoppedThreadId;
        return tid > 0 ? tid : 1;
    }

    public void EnsureConnected()
    {
        if (_state == SessionState.Disconnected || _state == SessionState.Terminated)
            throw new InvalidOperationException(
                $"No active debug session (state: {_state}). Call launch_process or attach_process first.");
    }

    public void EnsurePaused()
    {
        EnsureConnected();
        if (_state != SessionState.Paused)
            throw new InvalidOperationException(
                $"Process is not paused (state: {_state}). Cannot inspect. Use pause_execution or wait for a breakpoint.");
    }
}
