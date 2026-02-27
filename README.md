# .NET Debugger MCP Server

An MCP server that allows Claude Code to interactively debug .NET applications using **netcoredbg** and the **Debug Adapter Protocol (DAP)**.

[![Build](https://github.com/emirtuncer/dotnet-debugger-mcp/actions/workflows/build.yml/badge.svg)](https://github.com/emirtuncer/dotnet-debugger-mcp/actions/workflows/build.yml)

## Architecture

```
Claude Code (MCP Client)
        ↓  STDIO / JSON-RPC
DotNetDebuggerMcp (C# MCP Server)
        ↓  TCP port 4712 / DAP
netcoredbg subprocess
        ↓  Debug APIs
.NET process being debugged
```

## Prerequisites

- **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **netcoredbg** — installed via the setup script below

---

## Setup

### Option A — Claude Code Plugin (Recommended)

```bash
claude plugin install github:emirtuncer/dotnet-debugger-mcp
cd ~/.claude/plugins/dotnet-debugger   # or wherever Claude installs plugins
.\setup.ps1       # Windows
bash setup.sh     # Linux / macOS
```

Then restart Claude Code. The `dotnet-debugger` MCP server will appear automatically.

### Option B — Manual Clone

```bash
git clone https://github.com/emirtuncer/dotnet-debugger-mcp.git
cd dotnet-debugger-mcp

# Install netcoredbg
.\setup.ps1       # Windows
bash setup.sh     # Linux / macOS

# Copy and edit the MCP config
cp .mcp.json.example .mcp.json
# Edit .mcp.json — replace /path/to/dotnet-debugger-mcp with the absolute path
```

Then restart Claude Code.

---

## Available Tools (17 total)

### Session Management

| Tool             | Description                                           |
| ---------------- | ----------------------------------------------------- |
| `launch_process` | Launch a .NET assembly (.dll/.exe) under the debugger |
| `attach_process` | Attach to an already-running .NET process by PID      |
| `disconnect`     | End the debug session and stop netcoredbg             |

### Breakpoints

| Tool                      | Description                          |
| ------------------------- | ------------------------------------ |
| `set_breakpoint`          | Set a source breakpoint at file:line |
| `set_function_breakpoint` | Break at a named function's entry    |
| `remove_breakpoint`       | Remove a breakpoint by ID            |
| `list_breakpoints`        | List all active breakpoints          |

### Execution Control

| Tool                 | Description                   |
| -------------------- | ----------------------------- |
| `continue_execution` | Resume after pause/breakpoint |
| `step_over`          | Step over next statement      |
| `step_into`          | Step into next function call  |
| `step_out`           | Step out of current function  |
| `pause_execution`    | Pause the running process     |

### Inspection (requires process to be paused)

| Tool                  | Description                          |
| --------------------- | ------------------------------------ |
| `get_variables`       | Get local variables at current frame |
| `get_call_stack`      | Get the call stack                   |
| `get_threads`         | List all threads                     |
| `evaluate_expression` | Evaluate a C# expression             |

### Console / Output

| Tool               | Description                                            |
| ------------------ | ------------------------------------------------------ |
| `read_output`      | Read buffered stdout/stderr from the process           |
| `send_stdin`       | Send text to the process stdin                         |
| `get_debug_events` | Get pending events (breakpoint hits, exceptions, etc.) |

---

## Example Debugging Session

```
# Start debugging a compiled .NET app
launch_process("C:/myapp/bin/Debug/net8.0/MyApp.dll", stopAtEntry: true)

# Set a breakpoint
set_breakpoint("C:/myapp/src/Program.cs", 42)

# Resume execution
continue_execution()

# Wait for breakpoint — check events
get_debug_events()

# Inspect state
get_variables()
get_call_stack()
evaluate_expression("myVariable.ToString()")

# Step through code
step_over()
step_into()
step_out()

# Read any console output
read_output()

# End session
disconnect()
```

---

## Configuration

- **netcoredbg path**: Set `NETCOREDBG_PATH` environment variable to the `netcoredbg` executable, or ensure `netcoredbg` is on your PATH. The setup scripts populate `./netcoredbg/` automatically.
- **DAP port**: Fixed at `4712` (the default netcoredbg server port)

## Building from Source

```bash
dotnet build src/DotNetDebuggerMcp
```

For a pre-built binary (faster startup):

```bash
dotnet publish src/DotNetDebuggerMcp -c Release -o out/
```

## Notes

- Only one debug session can be active at a time
- Inspection tools (`get_variables`, `get_call_stack`, `evaluate_expression`) require the process to be paused
- Output from the debugged process is buffered (up to 1000 lines); use `read_output` to drain it
- Events are buffered (up to 500); use `get_debug_events` after each step/continue

## License

[MIT](LICENSE)
