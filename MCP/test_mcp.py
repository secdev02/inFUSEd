#!/usr/bin/env python3
"""
Test script for ProjFS MCP integration

This script tests the connection between the MCP server and ProjFS service
by sending sample commands through the named pipe.

Tests path normalization (v1.3.2 fix):
- Sends paths with double backslashes (\\TestMCP)
- Verifies they're found with list operations
- Confirms files created via MCP are visible

Usage:
    python test_mcp.py
"""

import json
import struct
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
PIPE_TIMEOUT = 10000  # 10 seconds
MAX_RETRIES = 3
RETRY_DELAY = 2  # seconds


def connect_to_pipe(retries=MAX_RETRIES):
    """Connect to the named pipe with retry logic"""
    for attempt in range(retries):
        try:
            print("  Attempt " + str(attempt + 1) + "/" + str(retries) + "...")
            
            # Wait for pipe to be available
            win32pipe.WaitNamedPipe(PIPE_NAME, PIPE_TIMEOUT)
            
            # Open the pipe
            pipe = win32file.CreateFile(
                PIPE_NAME,
                win32file.GENERIC_READ | win32file.GENERIC_WRITE,
                0,
                None,
                win32file.OPEN_EXISTING,
                0,
                None
            )
            
            print("  ✓ Connected!")
            return pipe
            
        except pywintypes.error as e:
            error_code = e.args[0]
            error_msg = e.args[2] if len(e.args) > 2 else str(e)
            
            if error_code == 121:  # ERROR_SEM_TIMEOUT
                print("  ✗ Timeout - pipe not available")
            elif error_code == 2:  # ERROR_FILE_NOT_FOUND
                print("  ✗ Pipe does not exist")
            elif error_code == 231:  # ERROR_PIPE_BUSY
                print("  ✗ Pipe is busy")
            else:
                print("  ✗ Error " + str(error_code) + ": " + error_msg)
            
            if attempt < retries - 1:
                print("  Waiting " + str(RETRY_DELAY) + " seconds before retry...")
                time.sleep(RETRY_DELAY)
            else:
                raise
    
    return None


def send_command(pipe_handle, command):
    """Send a command and receive response"""
    json_data = json.dumps(command)
    message_bytes = json_data.encode('utf-8')
    
    # Send length + message
    length_bytes = struct.pack('<I', len(message_bytes))
    win32file.WriteFile(pipe_handle, length_bytes)
    win32file.WriteFile(pipe_handle, message_bytes)
    
    # Read response
    result, length_bytes = win32file.ReadFile(pipe_handle, 4)
    message_length = struct.unpack('<I', length_bytes)[0]
    result, response_bytes = win32file.ReadFile(pipe_handle, message_length)
    
    return json.loads(response_bytes.decode('utf-8'))


def test_connection():
    """Test connection to ProjFS service"""
    print("=" * 60)
    print("ProjFS MCP Connection Test")
    print("=" * 60)
    
    try:
        print("\n[1/6] Connecting to pipe: " + PIPE_NAME)
        pipe = connect_to_pipe()
        
        if pipe is None:
            return False
        
        # Test 1: List root directory
        print("\n[2/6] Testing: List root directory")
        response = send_command(pipe, {
            "action": "list_all",
            "path": "\\",
            "content": "",
            "isBase64": False
        })
        print("Response:", response)
        if response.get("success"):
            print("✓ Test passed")
            items = response.get("data", [])
            if items:
                print("  Found items:")
                for item in items[:5]:  # Show first 5
                    print("    - " + item)
                if len(items) > 5:
                    print("    ... and " + str(len(items) - 5) + " more")
        else:
            print("✗ Test failed:", response.get("message"))
        
        # Test 2: Create directory
        print("\n[3/6] Testing: Create directory")
        response = send_command(pipe, {
            "action": "create_directory",
            "path": "\\TestMCP",
            "content": "",
            "isBase64": False
        })
        print("Response:", response)
        if response.get("success"):
            print("✓ Test passed")
        else:
            print("✗ Test failed:", response.get("message"))
        
        # Test 3: Create file
        print("\n[4/6] Testing: Create text file")
        response = send_command(pipe, {
            "action": "create_file",
            "path": "\\TestMCP\\test_file.txt",
            "content": "This is a test file created by the MCP test script.",
            "isBase64": False
        })
        print("Response:", response)
        if response.get("success"):
            print("✓ Test passed")
        else:
            print("✗ Test failed:", response.get("message"))
        
        # Test 4: List files in TestMCP
        print("\n[5/6] Testing: List files in TestMCP directory")
        response = send_command(pipe, {
            "action": "list_files",
            "path": "\\TestMCP",
            "content": "",
            "isBase64": False
        })
        print("Response:", response)
        if response.get("success"):
            files = response.get("data", [])
            if files:
                print("  Files found:")
                for f in files:
                    print("    - " + f)
                
                # Verify the test file we just created is in the list
                if "test_file.txt" in files:
                    print("✓ Test passed - Created file is visible!")
                else:
                    print("✗ Test FAILED - Created file NOT found in list!")
                    print("  This indicates a path normalization issue.")
            else:
                print("✗ Test FAILED - No files found (but we just created one!)")
                print("  This indicates a path normalization issue.")
        else:
            print("✗ Test failed:", response.get("message"))
        
        # Test 5: Delete file
        print("\n[6/6] Testing: Delete test file")
        response = send_command(pipe, {
            "action": "delete_file",
            "path": "\\TestMCP\\test_file.txt",
            "content": "",
            "isBase64": False
        })
        print("Response:", response)
        if response.get("success"):
            print("✓ Test passed")
        else:
            print("✗ Test failed:", response.get("message"))
        
        win32file.CloseHandle(pipe)
        
        print("\n" + "=" * 60)
        print("All tests completed!")
        print("=" * 60)
        print("\nNext steps:")
        print("1. Check the virtual file system at the configured RootPath")
        print("2. Open a file to trigger DNS alerts")
        print("3. Configure Claude Desktop with the MCP server")
        print("4. Test MCP tools with Claude")
        
    except pywintypes.error as e:
        error_code = e.args[0] if len(e.args) > 0 else 0
        error_msg = e.args[2] if len(e.args) > 2 else str(e)
        
        print("\n✗ Connection failed: Error " + str(error_code) + " - " + error_msg)
        print("\nTroubleshooting:")
        
        if error_code == 121:  # ERROR_SEM_TIMEOUT
            print("\nThe pipe exists but is not responding (timeout).")
            print("Possible causes:")
            print("  1. The MCP server thread may have crashed")
            print("  2. The server is stuck processing a previous connection")
            print("  3. The pipe is created but WaitForConnection hasn't been called yet")
            print("\nSolutions:")
            print("  1. Restart the ProjFS service (close and reopen console mode)")
            print("  2. Check the service console for error messages")
            print("  3. Make sure no other process is connected to the pipe")
            print("  4. Try running this test again (the timing may align better)")
        elif error_code == 2:  # ERROR_FILE_NOT_FOUND
            print("\nThe named pipe does not exist.")
            print("Solutions:")
            print("  1. Make sure ProjFS-Service-MCP is running")
            print("  2. Check if EnableMCPServer=true in App.config")
            print("  3. Verify MCPPipeName matches in both configs")
        elif error_code == 231:  # ERROR_PIPE_BUSY
            print("\nThe pipe is busy with another connection.")
            print("Solutions:")
            print("  1. Wait a moment and try again")
            print("  2. Close any other MCP clients")
            print("  3. Restart the ProjFS service")
        else:
            print("  1. Make sure ProjFS-Service-MCP is running")
            print("  2. Check if EnableMCPServer=true in App.config")
            print("  3. Verify MCPPipeName matches in both configs")
            print("  4. Try running the service in console mode:")
            print("     ProjFS-Service-MCP.exe /console")
        
        return False
    
    except Exception as e:
        print("\n✗ Unexpected error:", e)
        import traceback
        traceback.print_exc()
        return False
    
    return True


if __name__ == "__main__":
    print("\nMake sure the ProjFS service is running before testing!")
    print("Press Enter to continue or Ctrl+C to cancel...")
    try:
        input()
    except KeyboardInterrupt:
        print("\nTest cancelled")
        sys.exit(0)
    
    success = test_connection()
    sys.exit(0 if success else 1)
