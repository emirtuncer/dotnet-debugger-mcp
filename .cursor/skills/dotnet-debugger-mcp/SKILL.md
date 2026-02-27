---
name: dotnet-debugger-mcp
description: Use the dotnet-debugger-mcp MCP server to debug .NET applications. Use when the user wants to debug a .NET app, set breakpoints, step through code, inspect variables, or attach to a running .NET process. Applies when the dotnet-debugger-mcp MCP is configured and the task involves .NET debugging (C#, F#, .NET Core/8).
---

# .NET Debugger MCP

Use the **dotnet-debugger-mcp** MCP tools to run and debug .NET assemblies (DAP via netcoredbg). Only one debug session is active at a time.

## Tool groups

- **Session:** `launch_process`, `attach_process`, `disconnect`
- **Breakpoints:** `set_breakpoint`, `set_function_breakpoint`, `remove_breakpoint`, `list_breakpoints`
- **Execution:** `continue_execution`, `step_over`, `step_into`, `step_out`, `pause_execution`
- **Inspection (only when paused):** `get_variables`, `get_call_stack`, `get_threads`, `evaluate_expression`
- **Console:** `read_output`, `send_stdin`, `get_debug_events`

## Workflow

1. **Start session** — Either:
   - **Launch:** `launch_process(program, stopAtEntry: true)` to start a built .dll/.exe under the debugger (use absolute path for `program`).
   - **Attach:** `attach_process(pid)` to attach to an already-running .NET process (no need to start it via the MCP).
2. **Set breakpoints** — `set_breakpoint(filePath, line)` or `set_function_breakpoint(name)` before or after launch.
3. **Run / step** — `continue_execution()`, then `get_debug_events()` to see breakpoint hits; use `step_over` / `step_into` / `step_out` when paused.
4. **Inspect** — When paused, call `get_variables()`, `get_call_stack()`, `evaluate_expression("expr")`.
5. **Console** — Use `read_output()` to read stdout/stderr; use `send_stdin(text)` to send input.
6. **End** — `disconnect()` when done.

## Rules

- **One session:** If a session is active, call `disconnect()` before starting a new one.
- **Inspection only when paused:** `get_variables`, `get_call_stack`, `evaluate_expression` require the process to be paused (breakpoint, step, or pause).
- **Drain events/output:** After `continue_execution()` or steps, call `get_debug_events()` to get breakpoint/exception events; call `read_output()` to get process output (buffered, up to 1000 lines).
- **Paths:** Use absolute paths for the program and for breakpoint file paths (e.g. `C:/repo/src/Program.cs` or Windows `C:\\...` as required by the tool).

## Example

```
launch_process("C:/myapp/bin/Debug/net8.0/MyApp.dll", stopAtEntry: true)
set_breakpoint("C:/myapp/src/Program.cs", 42)
continue_execution()
get_debug_events()
get_variables()
evaluate_expression("myVariable")
step_over()
disconnect()
```
