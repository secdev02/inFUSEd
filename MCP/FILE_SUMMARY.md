# ProjFS MCP Integration - File Summary

This package contains everything needed to connect your ProjFS Virtual File System to an MCP Agent (like Claude) for dynamic file and folder creation.

## 📁 Files Overview

### Core Components

#### `ProjFS-Service-MCP.cs` (Modified Service)
- **What**: Enhanced version of your original ProjFS service
- **Version**: 1.3.1 (Performance Optimized)
- **New Features**:
  - Named Pipe server for IPC communication
  - JSON-based command protocol
  - Thread-safe file system operations
  - MCP command handling (create, delete, list files/folders)
  - **Asynchronous MCP server** (prevents blocking)
  - **4-thread pool** for concurrent operations
  - **Optimized locking** with snapshots
  - **Eliminated Explorer freezing**
- **Changes from original**:
  - Added `MCPCommand` and `MCPResponse` classes
  - Added `StartMCPServer()` and `StopMCPServer()` methods
  - Added `ProcessMCPCommand()` for handling MCP requests
  - Added thread-safe locking with `fileSystemLock`
  - Increased thread pool from 1 to 4 threads
  - Optimized callback performance
  - All existing functionality preserved

#### `projfs_mcp_server.py` (MCP Server)
- **What**: Python-based MCP server that bridges Claude and ProjFS
- **Function**: 
  - Connects to ProjFS service via Named Pipe
  - Exposes 7 MCP tools for file system operations
  - Handles JSON serialization/deserialization
  - Manages communication protocol
- **Tools provided**:
  1. `create_virtual_file` - Create text files
  2. `create_virtual_file_base64` - Create binary files
  3. `delete_virtual_file` - Remove files
  4. `create_virtual_directory` - Create folders
  5. `list_virtual_files` - List files
  6. `list_virtual_directories` - List folders
  7. `list_all_virtual_items` - List everything

### Configuration Files

#### `ProjFS-Service-MCP.exe.config` (Service Config)
- **What**: Configuration for the ProjFS service
- **Settings**:
  - `RootPath` - Where virtual files appear (default: C:\Secrets)
  - `AlertDomain` - Your DNS monitoring domain
  - `EnableMCPServer` - Enable/disable MCP integration
  - `MCPPipeName` - Named pipe for communication
  - `FileSystemData` - Initial virtual file system structure (CSV)
- **Note**: This file must be in the same directory as the .exe

#### `claude_desktop_config.example.json` (Claude Config)
- **What**: Example configuration for Claude Desktop
- **Location**: Goes in `%APPDATA%\Claude\claude_desktop_config.json`
- **Purpose**: Tells Claude Desktop how to start the MCP server
- **Customize**: Update the path to your `projfs_mcp_server.py`

#### `requirements.txt` (Python Dependencies)
- **What**: Python packages needed for the MCP server
- **Install**: `pip install -r requirements.txt`
- **Packages**:
  - `mcp` - MCP SDK for building servers
  - `pywin32` - Windows API access (provides win32pipe, win32file, pywintypes)
  - `colorlog` - Optional enhanced logging
- **Note**: `win32pipe` is NOT a separate package - it comes from `pywin32`

### Testing & Documentation

#### `test_mcp.py` (Test Script)
- **What**: Standalone test script to verify MCP connection
- **Function**:
  - Tests Named Pipe connection with retry logic
  - Creates test directory and file
  - Verifies all operations work
  - Lists virtual file system
  - Cleans up test files
- **When to use**: Before configuring Claude Desktop (optional)
- **Note**: Cannot run simultaneously with Claude Desktop

#### `diagnose_pipe.py` (Diagnostic Tool)
- **What**: Troubleshooting tool for pipe connection issues
- **Function**:
  - Checks if pipe exists
  - Tests connectivity
  - Identifies specific error conditions
  - Provides targeted solutions
- **When to use**: When getting timeout or connection errors
- **Output**: Detailed diagnosis and fix recommendations

#### `check_install.py` (Installation Checker)
- **What**: Verifies Python packages are installed correctly
- **Function**:
  - Checks Python version (3.10+ required)
  - Verifies all required packages are importable
  - Tests pywin32 installation
  - Provides troubleshooting guidance
- **When to use**: After running `pip install -r requirements.txt`

#### `README.md` (Full Documentation)
- **What**: Comprehensive documentation
- **Sections**:
  - Architecture explanation
  - Installation steps
  - Configuration reference
  - Usage examples
  - Troubleshooting guide
  - Security considerations
  - Example use cases

#### `QUICKSTART.md` (Quick Setup Guide)
- **What**: Fast 10-step setup guide
- **Purpose**: Get running in 5 minutes
- **Includes**:
  - Minimal configuration
  - Common issues and fixes
  - Example Claude prompts
  - First-run troubleshooting

#### `PYTHON_INSTALL.md` (Python Installation Guide)
- **What**: Detailed Python package installation troubleshooting
- **Purpose**: Solve pip/pywin32 installation issues
- **Includes**:
  - Step-by-step installation
  - Common error solutions
  - Virtual environment setup
  - Manual installation methods
  - Alternative installation approaches

#### `PERFORMANCE.md` (Performance Optimization Guide)
- **What**: Guide for optimizing ProjFS MCP performance
- **Purpose**: Fix Explorer freezing and improve responsiveness
- **Includes**:
  - Performance improvements in v1.3.1
  - Configuration tuning
  - Troubleshooting slow performance
  - Benchmarks and limits
  - Best practices for production
  - Monitoring and profiling tips

## 🔄 How They Work Together

```
┌─────────────────────────────────────────────────────────────┐
│  1. ProjFS-Service-MCP.exe reads ProjFS-Service-MCP.exe.config  │
│     - Gets RootPath, AlertDomain, MCP settings              │
│     - Starts virtual file system                            │
│     - Starts Named Pipe server                              │
└─────────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  2. Claude Desktop reads claude_desktop_config.json         │
│     - Finds "projfs" MCP server configuration               │
│     - Launches: python projfs_mcp_server.py                 │
└─────────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  3. projfs_mcp_server.py starts                             │
│     - Connects to Named Pipe (ProjFS_MCP_Pipe)              │
│     - Registers MCP tools with Claude                       │
│     - Waits for MCP requests                                │
└─────────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  4. Claude sends MCP request                                │
│     User: "Create a passwords.txt file"                     │
│     Claude: calls create_virtual_file tool                  │
└─────────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  5. MCP Server → Named Pipe → ProjFS Service                │
│     - JSON command sent through pipe                        │
│     - ProjFS creates virtual file                           │
│     - Response sent back through pipe                       │
└─────────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  6. File appears in C:\Secrets\                             │
│     - Virtual file system updated                           │
│     - Config saved (if AutoSave=true)                       │
│     - File ready to trigger alerts                          │
└─────────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  7. Someone opens the file                                  │
│     - ProjFS detects access                                 │
│     - DNS alert sent to AlertDomain                         │
│     - You receive notification                              │
└─────────────────────────────────────────────────────────────┘
```

## 🚀 Getting Started Order

Follow this order for setup:

1. **Read**: `QUICKSTART.md` - Understand the basic setup
2. **Edit**: `ProjFS-Service-MCP.exe.config` - Set your AlertDomain
3. **Compile**: `ProjFS-Service-MCP.cs` → Get the .exe
4. **Install**: Python packages from `requirements.txt`
5. **Test**: Run service in console mode
6. **Verify**: Run `test_mcp.py` to check connection
7. **Configure**: Edit Claude Desktop config with `claude_desktop_config.example.json`
8. **Use**: Ask Claude to create files!
9. **Reference**: `README.md` for detailed info and troubleshooting

## 🔧 Customization Points

### Virtual File System Location
- **File**: `ProjFS-Service-MCP.exe.config`
- **Setting**: `<add key="RootPath" value="C:\Secrets"/>`
- **Change to**: Any directory you want (will be created if needed)

### DNS Alert Domain
- **File**: `ProjFS-Service-MCP.exe.config`
- **Setting**: `<add key="AlertDomain" value="YOUR-TOKEN.oastify.com"/>`
- **Required**: Get from Burp Collaborator or similar service

### Named Pipe Name
- **Files**: Both config files must match
- **Service**: `<add key="MCPPipeName" value="ProjFS_MCP_Pipe"/>`
- **MCP Server**: Update `PIPE_NAME` in `projfs_mcp_server.py`

### Initial File Structure
- **File**: `ProjFS-Service-MCP.exe.config`
- **Setting**: `<add key="FileSystemData" value="..."/>`
- **Format**: CSV: `path,isDirectory,size,unixTimestamp`

## 📊 Feature Comparison

| Feature | Original ProjFS-Service.cs | ProjFS-Service-MCP.cs v1.3.1 |
|---------|---------------------------|----------------------|
| Virtual File System | ✓ | ✓ |
| DNS Alerts | ✓ | ✓ |
| CSV Configuration | ✓ | ✓ |
| Auto-save Changes | ✓ | ✓ |
| **MCP Integration** | ✗ | ✓ |
| **Named Pipe Server** | ✗ | ✓ |
| **Remote File Creation** | ✗ | ✓ |
| **Thread-safe Operations** | ✗ | ✓ |
| **JSON API** | ✗ | ✓ |
| **Async MCP Server** | ✗ | ✓ |
| **4-Thread Pool** | ✗ (1 thread) | ✓ |
| **Optimized Locking** | ✗ | ✓ |
| **Explorer Performance** | Good | Excellent |

## 💡 Tips

### Development Mode
Use console mode while testing:
```bash
ProjFS-Service-MCP.exe /console
```
You'll see real-time debug output.

### Production Mode
Install as Windows service for always-on operation:
```powershell
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe ProjFS-Service-MCP.exe
net start WindowsFakeFileSystem
```

### Logging
- **Service logs**: Windows Event Viewer → Application
- **MCP logs**: `projfs_mcp_server.log` in current directory
- **Debug output**: Enable `DebugMode=true` in config

### Security
- Named Pipe is local-only (no network exposure)
- Service runs as LocalSystem by default
- Virtual files inherit physical directory permissions
- DNS queries may contain sensitive filenames (use secure monitoring)

## 🆘 Quick Troubleshooting

| Problem | Check | Solution |
|---------|-------|----------|
| Service won't start | Event Viewer | Check .NET Framework version |
| MCP connection fails | Pipe name | Verify both configs match |
| No files appear | ProjFS enabled | Run `Enable-WindowsOptionalFeature` |
| No DNS alerts | AlertDomain | Verify domain is reachable |
| Claude can't connect | Config path | Check Claude Desktop config path |

## 📝 Notes

- **Backward Compatible**: The MCP version is fully compatible with the original service
- **Optional MCP**: Set `EnableMCPServer=false` to disable MCP integration
- **No Breaking Changes**: All original functionality preserved
- **C# Version**: Compiled for .NET Framework 4.8
- **Python Version**: Requires Python 3.10+
- **Windows Only**: ProjFS is Windows-specific
- **User Preference**: No C# string interpolation used (as requested)
