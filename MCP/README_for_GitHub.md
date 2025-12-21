# ProjFS MCP Integration

Connect Windows Projected File System (ProjFS) honeypots to AI agents via Model Context Protocol (MCP). Allows AI assistants like Claude to dynamically create decoy files that trigger DNS alerts when accessed.

## Overview

This project extends a Windows ProjFS virtual file system with MCP integration, enabling AI agents to create and manage honeypot files. When someone accesses these files, DNS queries are sent to your monitoring domain (e.g., Burp Collaborator).

**Key Components:**
- ProjFS Service: Creates virtual file system and monitors file access
- MCP Server: Python bridge between AI agents and ProjFS service
- Named Pipe IPC: Communication between components

## Requirements

**Windows:**
- Windows 10 version 1809 (build 17763) or later
- Windows Server 2019 or later
- .NET Framework 4.8 or higher
- Windows Projected File System feature enabled

**Python:**
- Python 3.10 or higher
- pywin32 package
- mcp package

## Installation

### Step 1: Enable Windows Projected File System

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName "Client-ProjFS"
```

Reboot if prompted.

### Step 2: Compile the ProjFS Service

```bash
csc /reference:System.Configuration.dll /reference:System.Xml.Linq.dll /reference:System.Runtime.Serialization.dll ProjFS-Service-MCP.cs
```

### Step 3: Configure the Service

Edit `ProjFS-Service-MCP.exe.config`:

```xml
<add key="RootPath" value="C:\Secrets"/>
<add key="AlertDomain" value="YOUR-TOKEN.oastify.com"/>
<add key="EnableMCPServer" value="true"/>
<add key="MCPPipeName" value="ProjFS_MCP_Pipe"/>
```

Replace `YOUR-TOKEN.oastify.com` with your actual DNS monitoring domain.

### Step 4: Install Python Dependencies

```bash
pip install -r requirements.txt
```

### Step 5: Configure Claude Desktop

Add to `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "projfs": {
      "command": "python",
      "args": ["C:\\path\\to\\projfs_mcp_server.py"]
    }
  }
}
```

## Usage

### Running the Service

**Console Mode (for testing):**
```bash
ProjFS-Service-MCP.exe /console
```

**Windows Service (for production):**
```powershell
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe ProjFS-Service-MCP.exe
net start WindowsFakeFileSystem
```

### Using with Claude

Once configured, ask Claude to create files:

```
Create a fake AWS credentials file in the Documents folder with decoy API keys
```

Claude will use the MCP tools to create virtual files in your ProjFS file system.

### Testing the Connection

```bash
python test_mcp.py
```

Expected output indicates successful connection and file operations.

## MCP Tools Available

The following tools are exposed to AI agents:

- `create_virtual_file` - Create text files
- `create_virtual_file_base64` - Create binary files
- `delete_virtual_file` - Remove files
- `create_virtual_directory` - Create folder structures
- `list_virtual_files` - List files in a directory
- `list_virtual_directories` - List subdirectories
- `list_all_virtual_items` - List everything in a directory

## How It Works

1. AI agent (Claude) sends MCP request to create a file
2. MCP server receives request and sends JSON command through Named Pipe
3. ProjFS service creates virtual file in memory
4. File appears in Windows Explorer at the configured path
5. When someone opens the file, ProjFS detects the access
6. DNS query sent to monitoring domain with encoded filename and process name
7. You receive alert at your monitoring service

## DNS Alert Format

When a file is accessed:

```
u1234.fFILENAME_BASE32.iPROCESS_BASE32.your-domain.com
```

Decode the Base32 strings to identify what file was accessed and by which process.

## Configuration Reference

**App.config Settings:**

- `RootPath` - Virtual file system location (default: C:\Secrets)
- `AlertDomain` - DNS domain for alerts (required)
- `DebugMode` - Enable verbose logging (true/false)
- `AutoSave` - Auto-save configuration changes (true/false)
- `EnableMCPServer` - Enable MCP integration (true/false)
- `MCPPipeName` - Named pipe for IPC (default: ProjFS_MCP_Pipe)
- `FileSystemData` - CSV structure for virtual files

## Performance

Version 1.3.2 includes performance optimizations:

- 4-thread pool for concurrent operations
- Asynchronous MCP server
- Optimized locking with snapshots
- Fast Explorer browsing (under 100ms)
- MCP operations complete in under 50ms

For best performance, set:

```xml
<add key="DebugMode" value="false"/>
<add key="AutoSave" value="false"/>
```

## Troubleshooting

**Service won't start:**
- Check .NET Framework version (4.8+)
- Verify ProjFS feature is enabled
- Check Windows Event Viewer for errors

**MCP connection timeout:**
- Ensure service is running
- Verify pipe name matches in both configs
- Restart service and try again

**Files not appearing:**
- Check `RootPath` directory exists
- Verify ProjFS feature is enabled
- Try deleting and recreating the root directory

**No DNS alerts:**
- Verify `AlertDomain` is set correctly
- Test DNS resolution manually
- Check firewall settings
- Enable `DebugMode` to see alert attempts

## Security Considerations

- Named Pipe is local-only (no network exposure)
- Service runs as LocalSystem by default
- Virtual files inherit physical directory permissions
- DNS queries may contain sensitive filenames
- Only deploy in controlled environments
- Ensure proper authorization before use

## File Structure

```
ProjFS-Service-MCP.cs       - Enhanced ProjFS service with MCP integration
projfs_mcp_server.py         - Python MCP server
ProjFS-Service-MCP.exe.config - Service configuration
requirements.txt             - Python dependencies
test_mcp.py                  - Connection test script
diagnose_pipe.py             - Diagnostic tool
check_install.py             - Installation verification
```

## Documentation

- `QUICKSTART.md` - Fast 10-step setup guide
- `README.md` - Comprehensive documentation
- `PERFORMANCE.md` - Performance optimization guide
- `PYTHON_INSTALL.md` - Python installation troubleshooting
- `PATH_FIX.md` - Path normalization details
- `VERSION_HISTORY.md` - Complete version history

## Version History

**v1.3.2** (Current)
- Fixed path normalization for MCP commands
- Files created via MCP now visible in list operations

**v1.3.1**
- Performance optimizations (4-thread pool, async MCP server)
- Eliminated Explorer freezing issues

**v1.3.0**
- Initial MCP integration
- Named Pipe server
- JSON command protocol

## License

MIT License - See original ProjFS-Service.cs for details

## Credits

Based on the ProjFS honeypot concept by Casey Smith. Enhanced with MCP integration for AI agent control.

## Contributing

Contributions welcome. Areas for improvement:
- Additional MCP tools
- Performance optimization
- Enhanced logging
- Cross-platform support

## Support

For issues:
1. Check troubleshooting section above
2. Review logs (enable DebugMode)
3. Run diagnostic tools (test_mcp.py, diagnose_pipe.py)
4. Check documentation files

## Warning

This is a security testing tool. Use responsibly and only in authorized environments.
