# Python Installation Guide for ProjFS MCP

## Quick Fix

The error occurs because `win32pipe` is **not a standalone package**. It's part of `pywin32`.

### Install Correctly:

```bash
# Install all requirements at once
pip install -r requirements.txt
```

OR install individually:

```bash
# Install MCP SDK
pip install mcp

# Install pywin32 (provides win32pipe, win32file, pywintypes)
pip install pywin32
```

## Complete Installation Steps

### Step 1: Verify Python Version

```bash
python --version
```

**Required**: Python 3.10 or higher

If you need to update Python, download from: https://www.python.org/downloads/

### Step 2: Install Dependencies

```bash
pip install -r requirements.txt
```

Expected output:
```
Successfully installed mcp-0.9.0 pywin32-306 colorlog-6.8.0
```

### Step 3: Run pywin32 Post-Install (if needed)

Some systems require this step:

```bash
python Scripts/pywin32_postinstall.py -install
```

**Note**: The `Scripts` folder is in your Python installation directory, for example:
- `C:\Python312\Scripts\pywin32_postinstall.py`
- `C:\Users\YourName\AppData\Local\Programs\Python\Python312\Scripts\pywin32_postinstall.py`

If you can't find it, try:
```bash
python -c "import sys; print(sys.prefix + '\\Scripts\\pywin32_postinstall.py')"
```

### Step 4: Verify Installation

```bash
python -c "import win32pipe, win32file, pywintypes; print('✓ pywin32 installed correctly')"
```

Expected output:
```
✓ pywin32 installed correctly
```

## Common Issues & Solutions

### Issue 1: "ERROR: Could not find a version that satisfies the requirement win32pipe"

**Cause**: Trying to install `win32pipe` directly instead of `pywin32`

**Solution**: 
```bash
# DON'T DO THIS:
pip install win32pipe  # ✗ Wrong!

# DO THIS INSTEAD:
pip install pywin32    # ✓ Correct!
```

### Issue 2: "No module named 'win32pipe'" after installation

**Cause**: pywin32 post-install script not run

**Solution**:
```bash
# Find your Python Scripts directory
where python

# Run post-install (adjust path to your Python installation)
python C:\Python312\Scripts\pywin32_postinstall.py -install
```

### Issue 3: "Access Denied" during installation

**Cause**: Insufficient permissions

**Solution**: Run as Administrator:
```bash
# Open PowerShell or Command Prompt as Administrator
pip install --user pywin32
```

### Issue 4: Multiple Python Versions

**Cause**: pip installing to wrong Python version

**Solution**: Use specific Python version:
```bash
# Use Python launcher
py -3.12 -m pip install -r requirements.txt

# Or specify full path
C:\Python312\python.exe -m pip install -r requirements.txt
```

### Issue 5: "ImportError: DLL load failed"

**Cause**: Missing Visual C++ Redistributable

**Solution**: Install Microsoft Visual C++ Redistributable:
- Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe
- Run the installer
- Restart your computer
- Reinstall pywin32: `pip install --force-reinstall pywin32`

### Issue 6: Using Virtual Environment

If using a virtual environment:

```bash
# Create virtual environment
python -m venv projfs_venv

# Activate it
projfs_venv\Scripts\activate

# Install requirements
pip install -r requirements.txt
```

Update Claude Desktop config to use the venv Python:
```json
{
  "mcpServers": {
    "projfs": {
      "command": "C:\\path\\to\\projfs_venv\\Scripts\\python.exe",
      "args": ["C:\\path\\to\\projfs_mcp_server.py"]
    }
  }
}
```

## Package Details

### What gets installed:

1. **mcp** - Model Context Protocol SDK
   - Provides MCP server framework
   - Handles tool registration and communication

2. **pywin32** (builds 306+) - Python for Windows Extensions
   - Provides: `win32pipe` (Named Pipes)
   - Provides: `win32file` (File operations)
   - Provides: `pywintypes` (Windows data types)
   - Required for IPC with ProjFS service

3. **colorlog** (optional) - Enhanced logging
   - Prettier log output
   - Not required, but recommended

## Verify Complete Installation

Run this comprehensive check:

```python
# Save as check_install.py
import sys

print("Python version:", sys.version)
print("\nChecking packages...\n")

packages = {
    'mcp': 'MCP SDK',
    'win32pipe': 'Windows Named Pipes (pywin32)',
    'win32file': 'Windows File API (pywin32)',
    'pywintypes': 'Windows Types (pywin32)',
}

for module, name in packages.items():
    try:
        __import__(module)
        print(f"✓ {name}: OK")
    except ImportError as e:
        print(f"✗ {name}: MISSING - {e}")

print("\n" + "="*50)
print("If all show OK, you're ready to go!")
print("If any show MISSING, reinstall: pip install pywin32")
```

Run it:
```bash
python check_install.py
```

Expected output:
```
Python version: 3.12.0 ...

Checking packages...

✓ MCP SDK: OK
✓ Windows Named Pipes (pywin32): OK
✓ Windows File API (pywin32): OK
✓ Windows Types (pywin32): OK

==================================================
If all show OK, you're ready to go!
```

## Still Having Issues?

### Check pip version:
```bash
pip --version
```

Should be pip 20.0 or higher. Upgrade if needed:
```bash
python -m pip install --upgrade pip
```

### Clear pip cache:
```bash
pip cache purge
pip install --no-cache-dir pywin32
```

### Manual pywin32 installation:

1. Download the wheel file from PyPI:
   - Go to: https://pypi.org/project/pywin32/#files
   - Download the `.whl` file matching your Python version and architecture
   - Example: `pywin32-306-cp312-cp312-win_amd64.whl` for Python 3.12 64-bit

2. Install the wheel:
   ```bash
   pip install path\to\pywin32-306-cp312-cp312-win_amd64.whl
   ```

## Next Steps

Once installation is complete:

1. Test the MCP server:
   ```bash
   python projfs_mcp_server.py
   ```

2. If you see connection errors, that's expected - it means Python packages are installed correctly and it's trying to connect to the ProjFS service

3. Make sure ProjFS service is running:
   ```bash
   ProjFS-Service-MCP.exe /console
   ```

4. Test the connection:
   ```bash
   python test_mcp.py
   ```

## Alternative: Conda Installation

If pip continues to have issues, try Conda:

```bash
# Install Anaconda or Miniconda first
# Then create environment:
conda create -n projfs python=3.12
conda activate projfs
conda install -c conda-forge pywin32
pip install mcp
```

## Environment Variables

Sometimes adding Python to PATH helps:

```powershell
# Add to User PATH
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\Python312;C:\Python312\Scripts", "User")
```

Restart your terminal after changing PATH.
