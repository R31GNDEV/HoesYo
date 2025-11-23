# HoesYo - Chrono Vault Activator
*This is a bypass for Genshin Impacts anti-cheat = HoYoProtect*

## Prerequisites
To build and run this project, you will need:

Visual Studio 2022 (Community, Professional, or Enterprise edition).

.NET 8.0 SDK installed. > You can install this by selecting the .NET desktop development workload in the Visual Studio Installer.

## Project Setup
The project is configured as a .NET 8.0 Console Application.

### 1. Download and Open
Download or clone the project files (including HoesYo.sln, HoesYo.csproj, and Program.cs) to your PC.

Open Visual Studio 2022.

### 2. Dependency Management
The project uses the following NuGet packages, which should be automatically restored by Visual Studio:

NAudio (Version 2.2.1): Used for detecting audio output from the speakers/loopback device to synchronize launch actions.

System.Management (Version 8.0.0): Used for system-level operations, such as disabling and enabling the network adapter via WMI (Windows Management Instrumentation).

If the dependencies do not restore automatically, you can:

Go to Project > Manage NuGet Packages... > Console. The required packages will be listed under "Installed." If any are missing, search for and install them.

Alternatively, open a command prompt in the solution directory and run: dotnet restore

### 3. Build

You may build your own executable! Find it in __bin\Release\net8.0__

(or just go use the one in releases)

# How to Use

**Run as Administrator**


# Execution Flow:

The application will output status messages to the console.

It will attempt to stop the specified target service (HoYoProtect).

It will disable the active network adapter.

The target application (e.g., GenshinImpact.exe) is launched.

The console will display the message ⏳ **Mock-Auth Window**: Waiting for audio cue or failsafe timer....

Once the required audio cue is detected or the maximum wait time is reached, the utility will restore the network connection.

The program will complete with the message ✅ The game process is now running, blinded, and connected..
