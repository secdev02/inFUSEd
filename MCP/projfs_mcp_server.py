#!/usr/bin/env python3
"""
ProjFS MCP Server

An MCP server that provides AI agents with the ability to dynamically
create and manage files in a Windows ProjFS virtual file system.

This server connects to the ProjFS service via Named Pipes and exposes
file system operations as MCP tools.

Installation:
    pip install mcp win32pipe win32file pywintypes

Usage:
    python projfs_mcp_server.py

Add to Claude Desktop config.json:
    {
      "mcpServers": {
        "projfs": {
          "command": "python",
          "args": ["C:\\path\\to\\projfs_mcp_server.py"]
        }
      }
    }
"""

import json
import struct
import sys
import asyncio
from typing import Any, Optional
import logging

try:
    import win32pipe
    import win32file
    import pywintypes
except ImportError:
    print("Error: win32pipe module not found. Install with: pip install pywin32", file=sys.stderr)
    sys.exit(1)

from mcp.server.models import InitializationOptions
from mcp.server import NotificationOptions, Server
from mcp.server.stdio import stdio_server
from mcp import types

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('projfs_mcp_server.log'),
        logging.StreamHandler(sys.stderr)
    ]
)
logger = logging.getLogger('projfs-mcp')

# Configuration
PIPE_NAME = r'\\.\pipe\ProjFS_MCP_Pipe'
PIPE_TIMEOUT = 5000  # 5 seconds


class ProjFSClient:
    """Client for communicating with the ProjFS service via Named Pipe"""
    
    def __init__(self, pipe_name: str = PIPE_NAME):
        self.pipe_name = pipe_name
        self.pipe_handle = None
        
    def connect(self) -> bool:
        """Connect to the ProjFS service named pipe"""
        try:
            logger.info(f"Connecting to pipe: {self.pipe_name}")
            
            # Wait for pipe to be available
            win32pipe.WaitNamedPipe(self.pipe_name, PIPE_TIMEOUT)
            
            # Open the pipe
            self.pipe_handle = win32file.CreateFile(
                self.pipe_name,
                win32file.GENERIC_READ | win32file.GENERIC_WRITE,
                0,
                None,
                win32file.OPEN_EXISTING,
                0,
                None
            )
            
            logger.info("Successfully connected to ProjFS service")
            return True
            
        except pywintypes.error as e:
            logger.error(f"Failed to connect to pipe: {e}")
            return False
    
    def disconnect(self):
        """Disconnect from the named pipe"""
        if self.pipe_handle:
            try:
                win32file.CloseHandle(self.pipe_handle)
                logger.info("Disconnected from ProjFS service")
            except Exception as e:
                logger.error(f"Error closing pipe: {e}")
            finally:
                self.pipe_handle = None
    
    def send_command(self, command: dict) -> Optional[dict]:
        """Send a command to the ProjFS service and receive response"""
        if not self.pipe_handle:
            logger.error("Not connected to pipe")
            return None
        
        try:
            # Serialize command to JSON
            json_data = json.dumps(command)
            message_bytes = json_data.encode('utf-8')
            
            # Send length prefix (4 bytes, little-endian)
            length_bytes = struct.pack('<I', len(message_bytes))
            win32file.WriteFile(self.pipe_handle, length_bytes)
            
            # Send message
            win32file.WriteFile(self.pipe_handle, message_bytes)
            
            logger.debug(f"Sent command: {command['action']}")
            
            # Read length prefix from response
            result, length_bytes = win32file.ReadFile(self.pipe_handle, 4)
            message_length = struct.unpack('<I', length_bytes)[0]
            
            # Read response message
            result, response_bytes = win32file.ReadFile(self.pipe_handle, message_length)
            response_json = response_bytes.decode('utf-8')
            
            # Parse response
            response = json.loads(response_json)
            logger.debug(f"Received response: success={response.get('success')}")
            
            return response
            
        except Exception as e:
            logger.error(f"Error sending command: {e}")
            return None


# Initialize MCP server
server = Server("projfs-filesystem")
projfs_client = ProjFSClient()


@server.list_tools()
async def handle_list_tools() -> list[types.Tool]:
    """List available tools for the MCP client"""
    return [
        types.Tool(
            name="create_virtual_file",
            description="""Create a file in the virtual ProjFS file system.
            
The file will be tracked by the ProjFS service and trigger DNS alerts when accessed.
Use this to plant decoy files that will alert you when opened.

Path format: Use backslash paths like '\\Documents\\secret.txt' or 'Documents\\passwords.xlsx'""",
            inputSchema={
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Virtual path for the file (e.g., '\\Documents\\secret.txt')"
                    },
                    "content": {
                        "type": "string",
                        "description": "Text content of the file"
                    }
                },
                "required": ["path", "content"]
            }
        ),
        types.Tool(
            name="create_virtual_file_base64",
            description="""Create a binary file in the virtual ProjFS file system from base64-encoded content.
            
Use this for non-text files like PDFs, images, or Office documents.
The content should be base64-encoded.""",
            inputSchema={
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Virtual path for the file"
                    },
                    "content_base64": {
                        "type": "string",
                        "description": "Base64-encoded binary content"
                    }
                },
                "required": ["path", "content_base64"]
            }
        ),
        types.Tool(
            name="delete_virtual_file",
            description="""Delete a file from the virtual ProjFS file system.
            
Removes the file from the virtual file system. Physical access attempts will no longer work.""",
            inputSchema={
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Virtual path of the file to delete"
                    }
                },
                "required": ["path"]
            }
        ),
        types.Tool(
            name="list_virtual_files",
            description="""List all files in a virtual directory.
            
Shows only files (not directories) in the specified path.""",
            inputSchema={
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Virtual directory path (default: root '\\\\')",
                        "default": "\\\\"
                    }
                },
                "required": []
            }
        ),
        types.Tool(
            name="list_virtual_directories",
            description="""List all subdirectories in a virtual directory.
            
Shows only directories (not files) in the specified path.""",
            inputSchema={
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Virtual directory path (default: root '\\\\')",
                        "default": "\\\\"
                    }
                },
                "required": []
            }
        ),
        types.Tool(
            name="list_all_virtual_items",
            description="""List all items (files and directories) in a virtual directory.
            
Provides a complete view of the virtual file system structure at the specified path.""",
            inputSchema={
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Virtual directory path (default: root '\\\\')",
                        "default": "\\\\"
                    }
                },
                "required": []
            }
        ),
        types.Tool(
            name="create_virtual_directory",
            description="""Create a directory structure in the virtual file system.
            
Creates all necessary parent directories. Use this before creating files in nested paths.""",
            inputSchema={
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Virtual directory path to create (e.g., '\\Documents\\SecretFolder')"
                    }
                },
                "required": ["path"]
            }
        )
    ]


@server.call_tool()
async def handle_call_tool(
    name: str, arguments: dict[str, Any] | None
) -> list[types.TextContent | types.ImageContent | types.EmbeddedResource]:
    """Handle tool execution requests"""
    
    if not arguments:
        arguments = {}
    
    # Ensure connection to ProjFS service
    if not projfs_client.pipe_handle:
        if not projfs_client.connect():
            return [types.TextContent(
                type="text",
                text="Error: Cannot connect to ProjFS service. Ensure the service is running."
            )]
    
    try:
        if name == "create_virtual_file":
            path = arguments.get("path", "")
            content = arguments.get("content", "")
            
            response = projfs_client.send_command({
                "action": "create_file",
                "path": path,
                "content": content,
                "isBase64": False
            })
            
            if response and response.get("success"):
                return [types.TextContent(
                    type="text",
                    text=f"✓ Created virtual file: {path}\n\nThe file is now part of the virtual file system and will trigger DNS alerts when accessed."
                )]
            else:
                error_msg = response.get("message", "Unknown error") if response else "No response from service"
                return [types.TextContent(
                    type="text",
                    text=f"✗ Failed to create file: {error_msg}"
                )]
        
        elif name == "create_virtual_file_base64":
            path = arguments.get("path", "")
            content_base64 = arguments.get("content_base64", "")
            
            response = projfs_client.send_command({
                "action": "create_file",
                "path": path,
                "content": content_base64,
                "isBase64": True
            })
            
            if response and response.get("success"):
                return [types.TextContent(
                    type="text",
                    text=f"✓ Created virtual binary file: {path}"
                )]
            else:
                error_msg = response.get("message", "Unknown error") if response else "No response from service"
                return [types.TextContent(
                    type="text",
                    text=f"✗ Failed to create binary file: {error_msg}"
                )]
        
        elif name == "delete_virtual_file":
            path = arguments.get("path", "")
            
            response = projfs_client.send_command({
                "action": "delete_file",
                "path": path,
                "content": "",
                "isBase64": False
            })
            
            if response and response.get("success"):
                return [types.TextContent(
                    type="text",
                    text=f"✓ Deleted virtual file: {path}"
                )]
            else:
                error_msg = response.get("message", "Unknown error") if response else "No response from service"
                return [types.TextContent(
                    type="text",
                    text=f"✗ Failed to delete file: {error_msg}"
                )]
        
        elif name == "list_virtual_files":
            path = arguments.get("path", "\\")
            
            response = projfs_client.send_command({
                "action": "list_files",
                "path": path,
                "content": "",
                "isBase64": False
            })
            
            if response and response.get("success"):
                files = response.get("data", [])
                if files:
                    file_list = "\n".join([f"  • {f}" for f in files])
                    return [types.TextContent(
                        type="text",
                        text=f"Files in {path}:\n{file_list}\n\nTotal: {len(files)} file(s)"
                    )]
                else:
                    return [types.TextContent(
                        type="text",
                        text=f"No files found in {path}"
                    )]
            else:
                error_msg = response.get("message", "Unknown error") if response else "No response from service"
                return [types.TextContent(
                    type="text",
                    text=f"✗ Failed to list files: {error_msg}"
                )]
        
        elif name == "list_virtual_directories":
            path = arguments.get("path", "\\")
            
            response = projfs_client.send_command({
                "action": "list_directories",
                "path": path,
                "content": "",
                "isBase64": False
            })
            
            if response and response.get("success"):
                directories = response.get("data", [])
                if directories:
                    dir_list = "\n".join([f"  📁 {d}" for d in directories])
                    return [types.TextContent(
                        type="text",
                        text=f"Directories in {path}:\n{dir_list}\n\nTotal: {len(directories)} director(ies)"
                    )]
                else:
                    return [types.TextContent(
                        type="text",
                        text=f"No directories found in {path}"
                    )]
            else:
                error_msg = response.get("message", "Unknown error") if response else "No response from service"
                return [types.TextContent(
                    type="text",
                    text=f"✗ Failed to list directories: {error_msg}"
                )]
        
        elif name == "list_all_virtual_items":
            path = arguments.get("path", "\\")
            
            response = projfs_client.send_command({
                "action": "list_all",
                "path": path,
                "content": "",
                "isBase64": False
            })
            
            if response and response.get("success"):
                items = response.get("data", [])
                if items:
                    item_list = "\n".join([f"  {item}" for item in items])
                    return [types.TextContent(
                        type="text",
                        text=f"Contents of {path}:\n{item_list}\n\nTotal: {len(items)} item(s)"
                    )]
                else:
                    return [types.TextContent(
                        type="text",
                        text=f"No items found in {path}"
                    )]
            else:
                error_msg = response.get("message", "Unknown error") if response else "No response from service"
                return [types.TextContent(
                    type="text",
                    text=f"✗ Failed to list items: {error_msg}"
                )]
        
        elif name == "create_virtual_directory":
            path = arguments.get("path", "")
            
            response = projfs_client.send_command({
                "action": "create_directory",
                "path": path,
                "content": "",
                "isBase64": False
            })
            
            if response and response.get("success"):
                return [types.TextContent(
                    type="text",
                    text=f"✓ Created virtual directory: {path}"
                )]
            else:
                error_msg = response.get("message", "Unknown error") if response else "No response from service"
                return [types.TextContent(
                    type="text",
                    text=f"✗ Failed to create directory: {error_msg}"
                )]
        
        else:
            return [types.TextContent(
                type="text",
                text=f"Unknown tool: {name}"
            )]
    
    except Exception as e:
        logger.error(f"Error executing tool {name}: {e}", exc_info=True)
        return [types.TextContent(
            type="text",
            text=f"Error executing tool: {str(e)}"
        )]


async def main():
    """Main entry point for the MCP server"""
    logger.info("Starting ProjFS MCP Server...")
    
    # Connect to ProjFS service
    if not projfs_client.connect():
        logger.error("Failed to connect to ProjFS service")
        logger.error("Make sure the ProjFS service is running with MCP enabled")
        return
    
    try:
        async with stdio_server() as (read_stream, write_stream):
            logger.info("MCP Server ready and accepting connections")
            await server.run(
                read_stream,
                write_stream,
                InitializationOptions(
                    server_name="projfs-filesystem",
                    server_version="1.0.0",
                    capabilities=server.get_capabilities(
                        notification_options=NotificationOptions(),
                        experimental_capabilities={},
                    ),
                ),
            )
    finally:
        projfs_client.disconnect()
        logger.info("ProjFS MCP Server stopped")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logger.info("Server interrupted by user")
    except Exception as e:
        logger.error(f"Fatal error: {e}", exc_info=True)
        sys.exit(1)
