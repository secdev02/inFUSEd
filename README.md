# inFUSEd
Deception Based File System - Based on ProjFS - AI Capable Content Generation 

# Windows ProjFS Virtual File System Service

## Description

Windows service that creates a virtual file system using the Windows Projected File System (ProjFS) API. Monitors file access attempts and sends DNS alerts when virtual files are accessed.

## Dependencies

- .NET Framework 4.8 or higher
- Windows 10 version 1809 (build 17763) or later
- Windows Server 2019 or later
- ProjectedFSLib.dll (Windows system library)
- Windows Projected File System feature must be enabled
- A Canarytoken DNS for alerting or WebHook

## Compilation

```bash
csc ProjFS-Service.cs
```

## Installation

1. Enable Windows Projected File System feature:
   ```powershell
   Enable-WindowsOptionalFeature -Online -FeatureName "Client-ProjFS"
   ```

2. Install the service (run as Administrator):
   ```cmd
   C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe ProjFS-Service.exe
   ```

3. Start the service:
   ```cmd
   net start WindowsFakeFileSystem
   ```

## Uninstallation

1. Stop the service:
   ```cmd
   net stop WindowsFakeFileSystem
   ```

2. Uninstall the service (run as Administrator):
   ```cmd
   C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /u ProjFS-Service.exe
   ```
   OR
   ```cmd
   sc delete WindowsFakeFileSystem
   ```

3. Optionally disable ProjFS feature:
   ```powershell
   Disable-WindowsOptionalFeature -Online -FeatureName "Client-ProjFS"
   ```

## Configuration (ProjFS-Service.exe.config)

- **RootPath** - Virtual file system location (default: C:\Secrets)
- **AlertDomain** - DNS domain for alerts
- **DebugMode** - Enable debug output (true/false)

## Console Mode (for testing)

Minimalist file structures.

```cmd
ProjFS-Service.exe /console
```

## Notes

- Service runs as LocalSystem by default
- Virtual files are created on-demand, folder may appear empty
- DNS alerts use Base32 encoding for file/process information
- Ensure firewall allows DNS queries for alerting functionality

## License

MIT License

# Claude API Integration for ProjFS Service



## Overview
The ProjFS Service now supports dynamic file structure and content generation using the Claude API.

## Configuration

### ProjFS-Service.exe.config Settings

1. **UseApiForStructure** (true/false)
   - When true, the service will call Claude API to generate the file system structure instead of using the static FileSystemData in the config
   - Default: false

2. **UseApiForContent** (true/false)
   - When true, the service will call Claude API to generate realistic file content when files are accessed
   - Default: false

3. **AnthropicApiKey**
   - Your Anthropic API key
   - Required when UseApiForStructure or UseApiForContent is true
   - Get your key from: https://console.anthropic.com/

### Example Configuration

```xml
<appSettings>
  <add key="RootPath" value="C:\Secrets" />
  <add key="AlertDomain" value="example.com" />
  <add key="DebugMode" value="false" />
  <add key="UseApiForStructure" value="true" />
  <add key="UseApiForContent" value="true" />
  <add key="AnthropicApiKey" value="sk-ant-api03-..." />
  <add key="FileSystemData" value="..." />
</appSettings>
```

## Features

### Dynamic File Structure Generation
When UseApiForStructure is enabled, Claude will generate a realistic corporate IT file structure including:
- Network configurations
- Server documentation
- Security policies
- Database files
- Backup information
- And more...

The generated structure is in CSV format and loaded at service startup.

### Dynamic Content Generation
When UseApiForContent is enabled, Claude will generate realistic file content when files are accessed, including:
- Authentic-looking corporate documents
- Context-aware content based on file name and extension
- Realistic formatting for different file types

## Usage Modes

### Mode 1: Static Structure and Content (Default)
```xml
<add key="UseApiForStructure" value="false" />
<add key="UseApiForContent" value="false" />
```
Uses the FileSystemData from ProjFS-Service.exe.config and generic placeholder content.

### Mode 2: API-Generated Structure, Static Content
```xml
<add key="UseApiForStructure" value="true" />
<add key="UseApiForContent" value="false" />
<add key="AnthropicApiKey" value="sk-ant-api03-..." />
```
Claude generates the file structure at startup, but files contain placeholder content.

### Mode 3: Static Structure, API-Generated Content
```xml
<add key="UseApiForStructure" value="false" />
<add key="UseApiForContent" value="true" />
<add key="AnthropicApiKey" value="sk-ant-api03-..." />
```
Uses FileSystemData for structure, but Claude generates realistic content when files are accessed.

### Mode 4: Fully Dynamic (Recommended for Honeypots)
```xml
<add key="UseApiForStructure" value="true" />
<add key="UseApiForContent" value="true" />
<add key="AnthropicApiKey" value="sk-ant-api03-..." />
```
Claude generates both the file structure and content dynamically, creating a highly realistic honeypot environment.

## Notes

- API calls are made synchronously when files are accessed (Mode 3 & 4)
- File structure is generated once at service startup (Mode 2 & 4)
- Ensure your API key has sufficient quota for the expected usage
- API calls may introduce latency when files are first accessed
- Content generation happens on-demand per file access

## Security Considerations

- Store your API key securely in the ProjFS-Service.exe.config
- Consider using Windows DPAPI or other encryption for the config file
- Monitor API usage to prevent unexpected costs
- The API key should have appropriate rate limits configured

Sample Output 

<img width="1043" height="621" alt="image" src="https://github.com/user-attachments/assets/a5a73e1f-f125-4de2-8cb3-c6a88cf1488e" />

<img width="1199" height="420" alt="image" src="https://github.com/user-attachments/assets/35ea2dd0-490a-4a22-8b85-c5eb9e4328f0" />

<img width="900" height="364" alt="image" src="https://github.com/user-attachments/assets/cecb8e46-ccaa-4152-be6a-5199d97686ca" />

<img width="679" height="336" alt="image" src="https://github.com/user-attachments/assets/efd50d0c-5459-4bb0-bf09-3095e76d939c" />








This work is inspired and informed from my time at as a researcher @ThinkstCanary 💚 `https://canary.tools/`


