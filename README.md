# Test Framework

A test framework inspired by Gauge, focused on extensibility and execution context, currently in an experimental stage.

## Overview

- **Extensible architecture**  
  Core components can be overridden to customize test discovery and parsing.

- **Lightweight runner model**  
  Designed to make adding support for new languages straightforward. Currently, only the .NET runner is implemented.

- **Execution hooks**  
  Used to extend behavior during different phases of execution.

- **Parallel test execution**  
  Supported via separate processes.

- **Markdown-based test definitions**  
  By default, test files are defined using markdown syntax.

## Usage
### Setup test project

Add `Bindings/Dotnet/TestFramework.Bindings.Dotnet.csproj` as a project reference.

Create a `manifest.json` file in the same directory with the following content:
```json
{
    "Runner": "Dotnet"
}
```

### Build and run framework
Navigate to the repository root and build the solution:
```bash
dotnet build
```

Navigate to the `Core` directory and run:
```bash
dotnet run -- <path-to-csproj>
```

To list all available options:
```bash
dotnet run -- --help
```