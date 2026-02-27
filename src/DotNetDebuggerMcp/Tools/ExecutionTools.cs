using System.ComponentModel;
using DotNetDebuggerMcp.Dap;
using ModelContextProtocol.Server;

namespace DotNetDebuggerMcp.Tools;

[McpServerToolType]
public class ExecutionTools(DebugSession session, DapClient dap)
{
    [McpServerTool(Name = "continue_execution")]
    [Description("Resume execution after a pause or breakpoint hit. Optionally specify a threadId.")]
    public async Task<string> ContinueExecution(
        [Description("Thread ID to continue (defaults to the stopped thread)")] int? threadId = null)
    {
        session.EnsureConnected();

        if (session.State == SessionState.Running)
            return "Process is already running.";

        int tid = threadId ?? session.GetDefaultThreadId();

        try
        {
            await dap.SendRequestAsync("continue", new ContinueArguments { ThreadId = tid });
            session.SetState(SessionState.Running);
            return $"Execution resumed (thread {tid}).";
        }
        catch (Exception ex)
        {
            return $"Error resuming execution: {ex.Message}";
        }
    }

    [McpServerTool(Name = "step_over")]
    [Description("Execute the next statement without stepping into function calls (step over).")]
    public async Task<string> StepOver(
        [Description("Thread ID to step (defaults to the stopped thread)")] int? threadId = null)
    {
        session.EnsurePaused();

        int tid = threadId ?? session.GetDefaultThreadId();

        try
        {
            await dap.SendRequestAsync("next", new NextArguments { ThreadId = tid });
            session.SetState(SessionState.Running);
            return $"Step over sent (thread {tid}). Call get_debug_events to see where execution stopped.";
        }
        catch (Exception ex)
        {
            return $"Error stepping over: {ex.Message}";
        }
    }

    [McpServerTool(Name = "step_into")]
    [Description("Step into the next function call.")]
    public async Task<string> StepInto(
        [Description("Thread ID to step (defaults to the stopped thread)")] int? threadId = null)
    {
        session.EnsurePaused();

        int tid = threadId ?? session.GetDefaultThreadId();

        try
        {
            await dap.SendRequestAsync("stepIn", new StepInArguments { ThreadId = tid });
            session.SetState(SessionState.Running);
            return $"Step into sent (thread {tid}). Call get_debug_events to see where execution stopped.";
        }
        catch (Exception ex)
        {
            return $"Error stepping into: {ex.Message}";
        }
    }

    [McpServerTool(Name = "step_out")]
    [Description("Step out of the current function, returning to the caller.")]
    public async Task<string> StepOut(
        [Description("Thread ID to step (defaults to the stopped thread)")] int? threadId = null)
    {
        session.EnsurePaused();

        int tid = threadId ?? session.GetDefaultThreadId();

        try
        {
            await dap.SendRequestAsync("stepOut", new StepOutArguments { ThreadId = tid });
            session.SetState(SessionState.Running);
            return $"Step out sent (thread {tid}). Call get_debug_events to see where execution stopped.";
        }
        catch (Exception ex)
        {
            return $"Error stepping out: {ex.Message}";
        }
    }

    [McpServerTool(Name = "pause_execution")]
    [Description("Pause the running process. Equivalent to pressing the 'Pause' button in a debugger.")]
    public async Task<string> PauseExecution()
    {
        session.EnsureConnected();

        if (session.State == SessionState.Paused)
            return "Process is already paused.";

        int tid = session.GetDefaultThreadId();

        try
        {
            await dap.SendRequestAsync("pause", new { threadId = tid });
            return "Pause request sent. Use get_debug_events to see the stop event.";
        }
        catch (Exception ex)
        {
            return $"Error pausing execution: {ex.Message}";
        }
    }
}
