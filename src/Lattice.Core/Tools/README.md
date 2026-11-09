# Tools

Tools are optional capabilities registered by the application composition root. Core provides one generic contract and registry; reasoning does not reference individual tool implementations.

## Contract

Each `ITool` exposes a `ToolDescriptor` containing a stable ID, display name, description, input descriptors, and required permissions. Input descriptors define names, primitive JSON types, descriptions, and whether each input is required.

`ToolRegistry` discovers registered tools, rejects duplicate IDs and invalid descriptors, validates argument objects against the descriptor, checks granted permissions, and bounds invocation time. The default execution limit is five seconds and the configured limit cannot exceed one minute. Tool exceptions are contained at the capability boundary and become failed invocation results.

A successful invocation returns a `ToolObservation` containing the tool ID and its output. This is evidence attributed to its producer, not a knowledge-base assertion. It is not automatically persisted or used as a premise. The intelligence/context layer must decide whether an observation is relevant and whether any claim can be accepted from it.

## First capability

`CalculatorTool` performs decimal addition, subtraction, multiplication, and division. It has no external effects and requires no permissions. It exists to exercise the generic contract without introducing network access, credentials, or mutable external state.

The CLI is currently the explicit invocation surface:

- `:tools` lists descriptors and their inputs.
- `:tool calculator {"left":12,"operation":"multiply","right":3}` invokes the tool.

This is a manual invocation path, not autonomous tool selection. The generic contract is ready for additional providers, but no planner, external adapter, durable observation store, or automatic knowledge promotion is implied.
