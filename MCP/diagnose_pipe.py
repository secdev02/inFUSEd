#!/usr/bin/env python3
"""
Named Pipe Diagnostic Tool

Checks the status of the ProjFS MCP named pipe and provides troubleshooting info.
"""

import sys
import time

try:
    import win32pipe
    import win32file
    import pywintypes
except ImportError:
    print("Error: pywin32 not installed. Run: pip install pywin32")
    sys.exit(1)

PIPE_NAME = r'\\.\pipe\ProjFS_MCP_Pipe'


def check_pipe_exists():
    """Check if the pipe exists"""
    print("=" * 60)
    print("Named Pipe Diagnostic Tool")
    print("=" * 60)
    print("\nPipe name: " + PIPE_NAME)
    print("\n[1/3] Checking if pipe exists...")
    
    try:
        # Try to wait for the pipe with a short timeout
        win32pipe.WaitNamedPipe(PIPE_NAME, 100)  # 100ms timeout
        print("✓ Pipe exists!")
        return True
    except pywintypes.error as e:
        error_code = e.args[0]
        
        if error_code == 2:  # ERROR_FILE_NOT_FOUND
            print("✗ Pipe does NOT exist")
            print("\nThe ProjFS service is not running or MCP is disabled.")
            print("\nTo fix:")
            print("  1. Start: ProjFS-Service-MCP.exe /console")
            print("  2. Check: EnableMCPServer=true in App.config")
            print("  3. Verify the console shows:")
            print("     'Starting MCP Server on pipe: ProjFS_MCP_Pipe'")
            return False
        elif error_code == 121:  # ERROR_SEM_TIMEOUT
            print("✓ Pipe exists (but timed out waiting for availability)")
            return True
        else:
            print("✗ Unexpected error: " + str(e))
            return False


def try_connection():
    """Try to connect to the pipe"""
    print("\n[2/3] Attempting connection...")
    
    for attempt in range(3):
        try:
            print("  Attempt " + str(attempt + 1) + "/3...")
            
            # Try to open the pipe
            handle = win32file.CreateFile(
                PIPE_NAME,
                win32file.GENERIC_READ | win32file.GENERIC_WRITE,
                0,
                None,
                win32file.OPEN_EXISTING,
                0,
                None
            )
            
            print("  ✓ Successfully connected!")
            win32file.CloseHandle(handle)
            return True
            
        except pywintypes.error as e:
            error_code = e.args[0]
            error_msg = e.args[2] if len(e.args) > 2 else str(e)
            
            if error_code == 231:  # ERROR_PIPE_BUSY
                print("  ✗ Pipe is BUSY (another client connected)")
            elif error_code == 2:
                print("  ✗ Pipe disappeared!")
            else:
                print("  ✗ Error " + str(error_code) + ": " + error_msg)
            
            if attempt < 2:
                time.sleep(1)
    
    print("\n✗ Could not connect after 3 attempts")
    return False


def provide_diagnosis():
    """Provide diagnosis and recommendations"""
    print("\n[3/3] Diagnosis")
    print("\nBased on the error 'semaphore timeout period has expired':")
    print("\nThe pipe EXISTS but is not accepting connections.")
    print("\nMost likely causes:")
    print("  1. The server is stuck in HandleMCPClient with a previous connection")
    print("  2. Another client (like Claude Desktop) is already connected")
    print("  3. The MCP server thread crashed but the pipe handle remains")
    print("\nRecommended fixes:")
    print("\n  IMMEDIATE FIX:")
    print("    1. Stop the ProjFS service (close console window)")
    print("    2. Wait 2 seconds")
    print("    3. Start it again: ProjFS-Service-MCP.exe /console")
    print("    4. Run this diagnostic again")
    print("\n  IF CLAUDE DESKTOP IS RUNNING:")
    print("    1. Close Claude Desktop completely")
    print("    2. Restart ProjFS service")
    print("    3. Run test_mcp.py first (before starting Claude)")
    print("\n  ALTERNATIVE APPROACH:")
    print("    Don't use test_mcp.py - just configure Claude Desktop")
    print("    and let Claude connect directly. The test script is optional.")
    print("\n  CHECK SERVICE OUTPUT:")
    print("    Look at the ProjFS console for error messages")
    print("    It should show:")
    print("      'Waiting for MCP client connection...'")
    print("      'MCP client connected!'")
    print("      'MCP Request: ...'")
    print("\n  DEBUG MODE:")
    print("    Set DebugMode=true in App.config to see detailed logs")


def main():
    """Main diagnostic function"""
    exists = check_pipe_exists()
    
    if not exists:
        print("\n" + "=" * 60)
        print("RESULT: Pipe does not exist - service not running")
        print("=" * 60)
        return 1
    
    connected = try_connection()
    
    print("\n" + "=" * 60)
    if connected:
        print("RESULT: Everything is working!")
        print("=" * 60)
        print("\nThe pipe is accessible. If test_mcp.py fails, try:")
        print("  1. Close any other clients first")
        print("  2. Run test_mcp.py immediately after restarting the service")
        return 0
    else:
        print("RESULT: Pipe exists but cannot connect")
        print("=" * 60)
        provide_diagnosis()
        return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\n\nDiagnostic cancelled")
        sys.exit(1)
    except Exception as e:
        print("\n\nUnexpected error: " + str(e))
        import traceback
        traceback.print_exc()
        sys.exit(1)
