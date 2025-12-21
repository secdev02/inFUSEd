# Quick Start Guide - ProjFS MCP Integration

## Fast Setup (5 minutes)

### 1. Enable ProjFS (One-time)
```powershell
# Run as Administrator
Enable-WindowsOptionalFeature -Online -FeatureName "Client-ProjFS"
# Reboot if prompted
```

### 2. Compile Service
```bash
csc /reference:System.Configuration.dll /reference:System.Xml.Linq.dll /reference:System.Runtime.Serialization.dll ProjFS-Service-MCP.cs
```

### 3. Configure
Edit `ProjFS-Service-MCP.exe.config`:
```xml
<add key="AlertDomain" value="YOUR-BURP-COLLABORATOR.oastify.com"/>
<add key="EnableMCPServer" value="true"/>
```

### 4. Install Python Dependencies
```bash
# Install all requirements (this installs pywin32, which provides win32pipe)
pip install -r requirements.txt

# Verify installation
python check_install.py
```

**Note**: If you get errors, see `PYTHON_INSTALL.md` for troubleshooting.

### 5. Run Service (Console Mode for Testing)
```bash
ProjFS-Service-MCP.exe /console
```

You should see:
```
=== ProjFS Virtual File System v1.3.0 (MCP Enabled) ===
Virtual Folder: C:\Secrets
MCP Server: True
MCP Pipe Name: ProjFS_MCP_Pipe
Virtualization started successfully.
Waiting for MCP client connection...
```

### 6. Test Connection
In a **new terminal**:
```bash
python test_mcp.py
```

Expected output:
```
✓ Connected successfully
✓ Test passed
All tests completed!
```

### 7. Configure Claude Desktop
Edit your Claude Desktop config:

**File**: `%APPDATA%\Claude\claude_desktop_config.json`

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

Replace `C:\\path\\to\\` with your actual path.

### 8. Restart Claude Desktop

Close and reopen Claude Desktop. The MCP server will start automatically.

### 9. Test with Claude

Open Claude and try:
```
Create a fake file called "passwords.txt" in the Documents folder 
with some decoy credentials
```

Claude will use the MCP tools to create the virtual file!

### 10. Verify Files

1. Open Windows Explorer
2. Navigate to `C:\Secrets`
3. You should see the virtual files
4. Open one to trigger a DNS alert

### 11. Check Alerts

- Go to your Burp Collaborator or DNS monitoring service
- You should see DNS queries with base32-encoded filenames
- Decode them to see what was accessed

## Common First-Run Issues

### Issue: "The semaphore timeout period has expired"
**Symptom**: test_mcp.py times out, but service console shows "MCP client connected!"

**Cause**: Another client is connected or the server is handling a previous connection

**Fix**: 
```bash
# 1. Stop the service (close console window or Ctrl+C)
# 2. Wait 2 seconds
# 3. Restart the service
ProjFS-Service-MCP.exe /console
# 4. Run test immediately (within 5 seconds)
python test_mcp.py
```

**Alternative**: Skip test_mcp.py and go directly to configuring Claude Desktop. The test is optional!

**Best Practice**: Don't run test_mcp.py while Claude Desktop is running - they compete for the same pipe connection.

### Issue: "Pipe does not exist"
**Fix**: Check that `EnableMCPServer=true` in App.config

### Issue: "Import error: win32pipe"
**Fix**: Run `pip install pywin32`

### Issue: No files in C:\Secrets
**Fix**: Try stopping and restarting the service

### Issue: Claude doesn't show MCP tools
**Fix**: 
1. Check Claude Desktop config path is correct
2. Restart Claude Desktop completely
3. Check MCP server logs: `type projfs_mcp_server.log`

## Example Claude Prompts

### Create Decoy Files
```
Create the following fake files in the virtual file system:
1. passwords.txt in Documents with admin credentials
2. aws_keys.txt in Documents with AWS access keys
3. database_backup.sql in Backups folder
```

### Build Directory Structure
```
Create a folder structure that looks like:
- HR/
  - Employee_Records/
    - salaries.xlsx
    - ssn_data.txt
- IT/
  - Network/
    - router_configs.xml
    - vpn_credentials.txt
```

### List Current Files
```
Show me everything in the virtual file system
```

### Clean Up
```
Delete all test files from the virtual file system
```

## Next Steps

- **Production**: Install as Windows service (see README.md)
- **Security**: Configure proper alerting and monitoring
- **Advanced**: Create more sophisticated decoy content
- **Integration**: Connect to your SIEM or alerting platform

## Need Help?

- Check the full README.md for detailed documentation
- Run `test_mcp.py` to verify connectivity
- Check logs in Event Viewer (for service mode)
- Enable `DebugMode=true` for verbose output
