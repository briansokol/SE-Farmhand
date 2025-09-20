# Farmhand

A Space Engineers programmable block script for automated farm management.

## Features

- **Plant Monitoring**: Tracks growth status, health, and yield of farm plots
- **Visual Status**: Color-coded lighting system for quick status assessment
- **Water Management**: Monitors irrigation systems and alerts for low water levels
- **Atmospheric Control**: Displays pressurization status via air vents
- **LCD Output**: Real-time farm status on connected displays

## Installation

1. Build the project using Visual Studio or `dotnet build`
2. Deploy the compiled script to a Programmable Block in Space Engineers
3. Configure the block group name in the Programmable Block's custom data
4. Add farm plots, irrigation systems, LCD panels, and air vents to the named group

## Status Indicators

- **Purple Light**: Empty planter
- **White Light**: Plant growing
- **Cyan Blinking**: Ready to harvest
- **Red Light**: Dead plant
- **Fast Blinking**: Low water warning

Built with [MDK2](https://github.com/malware-dev/MDK-SE) for Space Engineers.
