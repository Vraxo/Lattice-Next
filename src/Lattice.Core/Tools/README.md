# Tools

Tool implementations belong in this directory and share one generic contract defined by Core.

A tool describes its purpose, inputs, outputs, and effects. Generic infrastructure is responsible for discovery, validation, authorization, invocation, limits, and recording.

The intelligence must not contain branches for specific tool names or implementations. Tool results enter context as sourced observations.

The contract is deliberately not implemented in this scaffold. Define it only when the first real tool slice is ready to be built and tested.
