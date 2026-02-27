using System.ComponentModel;
using System.Text;
using DotNetDebuggerMcp.Dap;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace DotNetDebuggerMcp.Tools;

[McpServerToolType]
public class BreakpointTools(DebugSession session, DapClient dap)
{
    // Track breakpoints per file so we can issue setBreakpoints with all BPs for a file
    private readonly Dictionary<string, List<SourceBreakpointRecord>> _fileBreakpoints = new();
    private readonly object _lock = new();
    private int _nextLocalId = 1;

    private record SourceBreakpointRecord(int LocalId, int Line, string? Condition);

    [McpServerTool(Name = "set_breakpoint")]
    [Description("Set a source breakpoint at a specific file and line number. Returns the breakpoint ID to use for removal.")]
    public async Task<string> SetBreakpoint(
        [Description("Absolute or relative path to the source file")] string file,
        [Description("Line number (1-based)")] int line,
        [Description("Optional C# condition expression (e.g., 'x > 5')")] string? condition = null)
    {
        session.EnsureConnected();

        string normalizedFile = Path.GetFullPath(file);

        int localId;
        List<SourceBreakpointRecord> fileRecords;

        lock (_lock)
        {
            localId = _nextLocalId++;
            if (!_fileBreakpoints.TryGetValue(normalizedFile, out fileRecords!))
            {
                fileRecords = new List<SourceBreakpointRecord>();
                _fileBreakpoints[normalizedFile] = fileRecords;
            }
            fileRecords.Add(new SourceBreakpointRecord(localId, line, condition));
        }

        try
        {
            var result = await SendSetBreakpointsForFile(normalizedFile, fileRecords);
            return result;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                fileRecords.RemoveAll(r => r.LocalId == localId);
            }
            return $"Error setting breakpoint: {ex.Message}";
        }
    }

    [McpServerTool(Name = "set_function_breakpoint")]
    [Description("Set a breakpoint at the entry of a named function (e.g., 'MyNamespace.MyClass.MyMethod').")]
    public async Task<string> SetFunctionBreakpoint(
        [Description("Fully qualified function name to break at")] string functionName,
        [Description("Optional C# condition expression")] string? condition = null)
    {
        session.EnsureConnected();

        try
        {
            var args = new SetFunctionBreakpointsArguments
            {
                Breakpoints = [new FunctionBreakpoint { Name = functionName, Condition = condition }]
            };

            var response = await dap.SendRequestAsync("setFunctionBreakpoints", args);
            var bps = response["body"]?["breakpoints"] as JArray;
            var bp = bps?.FirstOrDefault();

            if (bp == null)
                return $"Function breakpoint set for {functionName} (no response details).";

            int id = bp["id"]?.Value<int>() ?? 0;
            bool verified = bp["verified"]?.Value<bool>() ?? false;
            string msg = bp["message"]?.Value<string>() ?? "";

            if (id > 0)
            {
                session.AddBreakpoint(new BreakpointInfo
                {
                    Id = id,
                    FunctionName = functionName,
                    Condition = condition,
                    Verified = verified
                });
            }

            return verified
                ? $"Function breakpoint set (id={id}): {functionName}"
                : $"Function breakpoint pending (id={id}): {functionName}" + (msg.Length > 0 ? $" — {msg}" : "");
        }
        catch (Exception ex)
        {
            return $"Error setting function breakpoint: {ex.Message}";
        }
    }

    [McpServerTool(Name = "remove_breakpoint")]
    [Description("Remove a breakpoint by its local ID (returned by set_breakpoint).")]
    public async Task<string> RemoveBreakpoint(
        [Description("Breakpoint ID returned by set_breakpoint")] int breakpointId)
    {
        session.EnsureConnected();

        // Find which file this breakpoint belongs to
        string? targetFile = null;
        List<SourceBreakpointRecord>? fileRecords = null;

        lock (_lock)
        {
            foreach (var kvp in _fileBreakpoints)
            {
                if (kvp.Value.Any(r => r.LocalId == breakpointId))
                {
                    targetFile = kvp.Key;
                    fileRecords = kvp.Value;
                    break;
                }
            }

            if (targetFile == null || fileRecords == null)
                return $"Breakpoint {breakpointId} not found.";

            fileRecords.RemoveAll(r => r.LocalId == breakpointId);
        }

        try
        {
            await SendSetBreakpointsForFile(targetFile!, fileRecords!);
            session.RemoveBreakpoint(breakpointId);
            return $"Breakpoint {breakpointId} removed.";
        }
        catch (Exception ex)
        {
            return $"Error removing breakpoint: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_breakpoints")]
    [Description("List all currently active breakpoints.")]
    public string ListBreakpoints()
    {
        var bps = session.GetBreakpoints();
        if (bps.Count == 0)
        {
            lock (_lock)
            {
                int total = _fileBreakpoints.Values.Sum(v => v.Count);
                if (total == 0)
                    return "No breakpoints set.";
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("Active breakpoints:");

        lock (_lock)
        {
            int num = 1;
            foreach (var kvp in _fileBreakpoints)
            {
                foreach (var record in kvp.Value)
                {
                    sb.Append($"  [{record.LocalId}] {kvp.Key}:{record.Line}");
                    if (record.Condition != null)
                        sb.Append($" (when: {record.Condition})");
                    sb.AppendLine();
                    num++;
                }
            }
        }

        foreach (var bp in bps.Where(b => b.FunctionName != null))
        {
            sb.Append($"  [fn:{bp.Id}] function: {bp.FunctionName}");
            if (bp.Condition != null)
                sb.Append($" (when: {bp.Condition})");
            sb.Append(bp.Verified ? " ✓" : " (unverified)");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string> SendSetBreakpointsForFile(string file, List<SourceBreakpointRecord> records)
    {
        var args = new SetBreakpointsArguments
        {
            Source = new DapSource { Path = file, Name = Path.GetFileName(file) },
            Breakpoints = records.Select(r => new SourceBreakpoint { Line = r.Line, Condition = r.Condition }).ToArray()
        };

        var response = await dap.SendRequestAsync("setBreakpoints", args);
        var bps = response["body"]?["breakpoints"] as JArray ?? [];

        var sb = new StringBuilder();
        for (int i = 0; i < records.Count && i < bps.Count; i++)
        {
            var record = records[i];
            var bp = bps[i];
            int id = bp["id"]?.Value<int>() ?? record.LocalId;
            bool verified = bp["verified"]?.Value<bool>() ?? false;
            int actualLine = bp["line"]?.Value<int>() ?? record.Line;
            string? msg = bp["message"]?.Value<string>();

            session.AddBreakpoint(new BreakpointInfo
            {
                Id = record.LocalId,
                File = file,
                Line = actualLine,
                Condition = record.Condition,
                Verified = verified
            });

            sb.AppendLine(verified
                ? $"Breakpoint set (id={record.LocalId}) at {Path.GetFileName(file)}:{actualLine}"
                : $"Breakpoint pending (id={record.LocalId}) at {Path.GetFileName(file)}:{record.Line}" +
                  (msg != null ? $" — {msg}" : " (will bind when code loads)"));
        }

        return sb.ToString().TrimEnd();
    }
}
