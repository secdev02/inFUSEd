# inFUSEd
Deception Based File System - Based on ProjFS - AI Capable Content Generation 



# Claude API Integration for ProjFS Service

## Overview
The ProjFS Service now supports dynamic file structure and content generation using the Claude API.

## Configuration

### App.config Settings

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
Uses the FileSystemData from App.config and generic placeholder content.

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

- Store your API key securely in the App.config
- Consider using Windows DPAPI or other encryption for the config file
- Monitor API usage to prevent unexpected costs
- The API key should have appropriate rate limits configured
