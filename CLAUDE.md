# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **Space Engineers Programmable Block Script** for automated farm management. The project uses the Malware Development Kit (MDK2) framework to develop, build, and deploy in-game scripts for Space Engineers.

## Technology Stack

- **Framework**: .NET Framework 4.8 (C# 6.0)
- **Target Platform**: Space Engineers Programmable Blocks
- **Build System**: MDK2 (Malware Development Kit 2)
- **Package Manager**: NuGet

## ⚠️ CRITICAL: C# 6.0 Language Constraints

**ALL CODE MUST BE COMPATIBLE WITH C# 6.0**. This project is constrained to C# 6.0 syntax and features due to Space Engineers API limitations. DO NOT use features from C# 7.0 or later, including:

- **Readonly structs** (C# 7.2) - Use regular structs or const fields instead
- **Tuple syntax** `(int, string)` (C# 7.0) - Use named classes or structs
- **Pattern matching** with `is` expressions (C# 7.0)
- **Out variables** `int.TryParse(s, out var result)` (C# 7.0)
- **Expression-bodied constructors/destructors** (C# 7.0)
- **Local functions** (C# 7.0)
- **Ref returns and ref locals** (C# 7.0)

**Available C# 6.0 features:**
- Expression-bodied members for properties and methods
- Auto-property initializers
- String interpolation `$"text {variable}"`
- Null-conditional operators `?.` and `?[]`
- `nameof` expressions
- Exception filters in catch blocks

## Build Commands

```bash
# Build the project (Debug configuration)
dotnet build "Farmhand/Farmhand.csproj" -c Debug

# Build for Release
dotnet build "Farmhand/Farmhand.csproj" -c Release

# Build solution file
dotnet build "Farmhand.sln"
```

## Project Structure

The project follows a component-based architecture with a base `Block` class that provides common functionality for all Space Engineers blocks:

### Core Architecture

- **Program.cs**: Main entry point implementing `MyGridProgram` interface
- **Block.cs**: Abstract base class for all block types with custom data management
- **Component Classes**: Specialized wrappers for different Space Engineers block types
  - `FarmPlot.cs`: Manages agricultural plots with plant monitoring and lighting
  - `IrrigationSystem.cs`: Handles water/ice management systems
  - `LcdPanel.cs`: Controls text display panels
  - `AirVent.cs`: Monitors atmospheric conditions
  - `ProgrammableBlock.cs`: Self-referential block for configuration
  - `Timer.cs`: Event-driven timer automation with state-based triggering
- **Support Classes**: Core system management components
  - `StateManager.cs`: State change detection and timer event coordination

### Key Design Patterns

1. **Component Pattern**: Each block type wraps Space Engineers API interfaces with custom logic
2. **Template Method**: Base `Block` class defines common operations (validation, custom data parsing)
3. **State Management**: Main program loop monitors and updates all connected blocks every 100 ticks
4. **Event-Driven Automation**: Timer blocks trigger actions based on state changes using observer pattern
5. **Configuration-Based Behavior**: Timer events configured via custom data INI settings

### Custom Data System

The project uses Space Engineers' custom data feature for configuration. All blocks inherit a standardized custom data management system through the base `Block` class:

- Configuration stored in INI format in block custom data
- Header: `[Farmhand]`
- Automatic parsing and validation on each update cycle

### Automation and Event System

The project implements a sophisticated event-driven automation system using Timer blocks and StateManager:

#### Timer Event Configuration

Timer blocks support multiple event triggers configured via custom data:
- **OnWaterLowTrue/False**: Responds to farm plot water levels
- **OnIceLowTrue/False**: Responds to irrigation system ice levels
- **OnPressurizedTrue/False**: Responds to air vent pressure changes
- **OnCropReadyTrue/False**: Responds to harvest-ready crops
- **OnCropDeadTrue/False**: Responds to dead crops
- **OnCropAvailableTrue/False**: Responds to crops available for harvest
- **TriggerNow**: Controls immediate vs. countdown triggering

#### State Change Detection

The `StateManager` class provides:
- **State Tracking**: Monitors boolean states across update cycles
- **Change Detection**: Only triggers events when states actually change
- **Event Coordination**: Automatically triggers appropriate timer events
- **Multi-Timer Support**: Can manage multiple timer blocks simultaneously

#### Event Naming Convention

Events follow a consistent pattern: `{StateName}{True|False}`
- State changes from false→true trigger `{StateName}True` events
- State changes from true→false trigger `{StateName}False` events

## Development Workflow

### Building Scripts

The MDK2 system automatically packages the script for deployment to Space Engineers. Configuration is managed through:

- `Farmhand.mdk.ini`: Project-specific MDK settings
- `Farmhand.mdk.local.ini`: Local development overrides (not in source control)

### Script Minification

Controlled via `Farmhand.mdk.ini`:
- `minify=none`: No optimization (default for development)
- Available options: `trim`, `stripcomments`, `lite`, `full`

### File Exclusions

Files matching these patterns are excluded from the final script:
- `obj/**/*`
- `MDK/**/*`
- `**/*.debug.cs`

## Space Engineers Integration

### Block Group System

The script operates on blocks within a named group (configured via Programmable Block custom data):
- Groups multiple farm-related blocks for coordinated management
- Automatically discovers and categorizes blocks within the group
- Supports hot-swapping of blocks without script restart

### Update Frequency

- Runs every 100 game ticks (~1.67 seconds at normal sim speed)
- Provides real-time monitoring and automated responses

### Visual Feedback System

Farm plots use integrated lighting to indicate status:
- **Purple**: Empty planter
- **White**: Growing plant
- **Cyan + Blinking**: Ready to harvest
- **Red**: Dead plant
- **Fast Blinking**: Low water warning

### Key Space Engineers API Documentation

- [How to search for blocks on a grid](https://github.com/malware-dev/MDK-SE/wiki/The-Grid-Terminal-System)
- [How to draw on a text surface with sprites](https://github.com/malware-dev/MDK-SE/wiki/Text-Panels-and-Drawing-Sprites)
