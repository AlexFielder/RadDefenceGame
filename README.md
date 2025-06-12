# Rad Defence Game

A MonoGame-based game project updated to use the latest .NET 8 infrastructure and modern C# features.

## Project Structure

- **RadDefenceGame.Windows**: Windows desktop version of the game (builds with .NET 8)
- **RadDefenceGame.Android**: Android version of the game (requires Android workloads)

## Development Environment Requirements

- .NET 8 SDK or later
- MonoGame 3.8.1 or later
- Visual Studio 2022 or VS Code with C# extension

## Building the Project

To build the Windows version:

```bash
dotnet build RadDefenceGame.Windows
```

To build the Android version (requires Android workload):

```bash
dotnet workload install android
dotnet build RadDefenceGame.Android
```

## Running the Game

```bash
dotnet run --project RadDefenceGame.Windows
```

## Features

- Modern .NET 8 architecture
- Latest C# language features
- Particle effects system
- Cross-platform capabilities