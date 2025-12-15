# Contributing to NetZeroMQ

Thank you for your interest in contributing to NetZeroMQ! This document provides guidelines and information for contributors.

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Git
- A C# IDE (Visual Studio, VS Code with C# extension, or JetBrains Rider)

### Setting Up the Development Environment

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/netzmq.git
   cd netzmq
   ```
3. Build the project:
   ```bash
   dotnet build
   ```
4. Run the tests:
   ```bash
   dotnet test
   ```

## How to Contribute

### Reporting Bugs

Before creating a bug report, please check existing issues to avoid duplicates.

When filing an issue, please include:
- A clear, descriptive title
- Steps to reproduce the problem
- Expected vs actual behavior
- Your environment (OS, .NET version, NetZeroMQ version)
- Any relevant code snippets or error messages

### Suggesting Features

Feature requests are welcome! Please provide:
- A clear description of the feature
- The use case and benefits
- Any implementation ideas (optional)

### Pull Requests

1. **Create a branch** from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**:
   - Follow the existing code style
   - Add XML documentation for public APIs
   - Write tests for new functionality
   - Update documentation if needed

3. **Test your changes**:
   ```bash
   dotnet test
   ```

4. **Commit your changes**:
   - Use clear, descriptive commit messages
   - Reference related issues (e.g., "Fixes #123")

5. **Push and create a PR**:
   ```bash
   git push origin feature/your-feature-name
   ```
   Then create a Pull Request on GitHub.

## Code Style Guidelines

### General

- Use C# 12 features where appropriate
- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful names for variables, methods, and classes

### Documentation

- Add XML documentation comments for all public types and members
- Keep comments concise and up-to-date
- Include code examples in documentation where helpful

### Testing

- Write unit tests for new functionality
- Aim for high test coverage
- Use descriptive test names that explain the expected behavior
- Use FluentAssertions for assertions

## Project Structure

```
netzmq/
├── src/
│   ├── NetZeroMQ/           # High-level API
│   ├── NetZeroMQ.Core/      # Low-level P/Invoke bindings
│   └── NetZeroMQ.Native/    # Native library packaging
├── tests/
│   ├── NetZeroMQ.Tests/     # Unit and integration tests
│   └── NetZeroMQ.Core.Tests/
├── examples/             # Example projects
└── native/               # Native library binaries
```

## Questions?

Feel free to open an issue for any questions about contributing.

Thank you for contributing!
