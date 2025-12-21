#!/usr/bin/env python3
"""
Installation Check Script for ProjFS MCP

Verifies that all required packages are installed correctly.
Run this after installing requirements.txt
"""

import sys

def check_python_version():
    """Check Python version"""
    version = sys.version_info
    print("=" * 60)
    print("Python Installation Check")
    print("=" * 60)
    print("\n[1/4] Python Version Check")
    print("    Version: " + str(version.major) + "." + str(version.minor) + "." + str(version.micro))
    
    if version.major >= 3 and version.minor >= 10:
        print("    Status: ✓ OK (3.10+ required)")
        return True
    else:
        print("    Status: ✗ FAILED (Need Python 3.10 or higher)")
        return False

def check_packages():
    """Check required packages"""
    print("\n[2/4] Package Installation Check")
    
    packages = [
        ('mcp', 'MCP SDK'),
        ('win32pipe', 'Named Pipes (from pywin32)'),
        ('win32file', 'File API (from pywin32)'),
        ('pywintypes', 'Windows Types (from pywin32)'),
    ]
    
    all_ok = True
    for module, name in packages:
        try:
            __import__(module)
            print("    ✓ " + name)
        except ImportError as e:
            print("    ✗ " + name + " - NOT FOUND")
            all_ok = False
    
    return all_ok

def check_pywin32_version():
    """Check pywin32 version"""
    print("\n[3/4] pywin32 Version Check")
    try:
        import win32api
        # Try to get version - not all builds have this
        try:
            import pywintypes
            print("    Version: Installed (version info not available)")
        except:
            print("    Version: Unknown")
        print("    Status: ✓ OK")
        return True
    except ImportError:
        print("    Status: ✗ NOT INSTALLED")
        print("\n    Install with: pip install pywin32")
        return False

def check_mcp_version():
    """Check MCP SDK version"""
    print("\n[4/4] MCP SDK Check")
    try:
        import mcp
        # Try to get version if available
        version = "installed"
        if hasattr(mcp, '__version__'):
            version = mcp.__version__
        print("    Version: " + str(version))
        print("    Status: ✓ OK")
        return True
    except ImportError:
        print("    Status: ✗ NOT INSTALLED")
        print("\n    Install with: pip install mcp")
        return False

def print_summary(checks):
    """Print summary"""
    print("\n" + "=" * 60)
    print("Summary")
    print("=" * 60)
    
    if all(checks):
        print("\n✓ All checks passed! You're ready to run the MCP server.")
        print("\nNext steps:")
        print("  1. Start ProjFS service: ProjFS-Service-MCP.exe /console")
        print("  2. Test connection: python test_mcp.py")
        print("  3. Configure Claude Desktop")
        print("  4. Start using MCP tools!")
    else:
        print("\n✗ Some checks failed. Please fix the issues above.")
        print("\nQuick fix:")
        print("  pip install -r requirements.txt")
        print("\nFor detailed help, see PYTHON_INSTALL.md")

def main():
    """Main check function"""
    checks = []
    
    checks.append(check_python_version())
    checks.append(check_packages())
    checks.append(check_pywin32_version())
    checks.append(check_mcp_version())
    
    print_summary(checks)
    
    return 0 if all(checks) else 1

if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\n\nCheck cancelled by user")
        sys.exit(1)
    except Exception as e:
        print("\n\nUnexpected error: " + str(e))
        import traceback
        traceback.print_exc()
        sys.exit(1)
