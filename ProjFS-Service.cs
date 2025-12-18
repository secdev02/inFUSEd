/*******************************************************************************
 * File: ProjFS-Service.cs
 * Author: Casey Smith 
 * Date: 2025
 * Version: 1.0.0
 * 
 * Description:
 *   Windows service that creates a virtual file system using the Windows
 *   Projected File System (ProjFS) API. Monitors file access attempts and
 *   sends DNS alerts when virtual files are accessed.
 * 
 * Dependencies:
 *   - .NET Framework 4.8 or higher
 *   - Windows 10 version 1809 (build 17763) or later
 *   - Windows Server 2019 or later
 *   - ProjectedFSLib.dll (Windows system library)
 *   - Windows Projected File System feature must be enabled

 * 
 * 
 * Compilation:
 *   csc ProjFS-Service.cs
 * 
 * Installation:
 *   1. Enable Windows Projected File System feature:
 *      Enable-WindowsOptionalFeature -Online -FeatureName "Client-ProjFS"
 * 
 *   2. Install the service (run as Administrator):
 *       C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe ProjFS-Service.exe
 * 
 *   3. Start the service:
 *      net start WindowsFakeFileSystem
 * 
 *   
 * 
 * Uninstallation:
 *   1. Stop the service:
 *      net stop WindowsFakeFileSystem
 * 
 *   2. Uninstall the service (run as Administrator):
 *      C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /u ProjFS-Service.exe
 *      OR
 *      sc delete WindowsFakeFileSystem
 * 
 *   3. Optionally disable ProjFS feature:
 *      Disable-WindowsOptionalFeature -Online -FeatureName "Client-ProjFS"
 * 
 * Configuration (App.config):
 *   RootPath - Virtual file system location (default: C:\Secrets)
 *   AlertDomain - DNS domain for alerts
 *   DebugMode - Enable debug output (true/false)
 * 
 * Console Mode (for testing):
 *   Minimalist file structures.
 *   ProjFS-Service.exe /console
 * 
 * Notes:
 *   - Service runs as LocalSystem by default
 *   - Virtual files are created on-demand, folder may appear empty
 *   - DNS alerts use Base32 encoding for file/process information
 *   - Ensure firewall allows DNS queries for alerting functionality
 * 
 * License: MIT License
 *
 * 
 ******************************************************************************/




using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFakeFileSystemService
{
    // Main Service Class
    public partial class WindowsFakeFileSystemService : ServiceBase
    {
        private ProjFSProvider provider;
        private Thread serviceThread;
        private ManualResetEvent stopEvent;
        
        public WindowsFakeFileSystemService()
        {
            ServiceName = "WindowsFakeFileSystem";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            stopEvent = new ManualResetEvent(false);
            serviceThread = new Thread(ServiceWorkerThread);
            serviceThread.Start();
        }

        protected override void OnStop()
        {
            if (provider != null)
            {
                provider.StopVirtualizing();
            }
            
            stopEvent.Set();
            if (serviceThread != null)
            {
                serviceThread.Join(5000);
            }
        }

        private void ServiceWorkerThread()
        {
            try
            {
                string rootPath = ConfigurationManager.AppSettings["RootPath"] ?? @"C:\Secrets";
                string alertDomain = ConfigurationManager.AppSettings["AlertDomain"] ?? "TODO-INSERTTOKENHERE";
                bool debugMode = bool.Parse(ConfigurationManager.AppSettings["DebugMode"] ?? "false");
                bool useApiForStructure = bool.Parse(ConfigurationManager.AppSettings["UseApiForStructure"] ?? "false");
                bool useApiForContent = bool.Parse(ConfigurationManager.AppSettings["UseApiForContent"] ?? "false");
                string apiKey = ConfigurationManager.AppSettings["AnthropicApiKey"] ?? "";
                string csvData = ConfigurationManager.AppSettings["FileSystemData"] ?? "";
                
                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }
                
                if (useApiForStructure && !string.IsNullOrEmpty(apiKey))
                {
                    string apiGeneratedData = ClaudeApiHelper.GenerateFileStructure(apiKey).Result;
                    
                    if (!string.IsNullOrEmpty(apiGeneratedData) && !apiGeneratedData.StartsWith("Error"))
                    {
                        csvData = apiGeneratedData;
                    }
                }
                
                if (string.IsNullOrEmpty(csvData))
                {
                    EventLog.WriteEntry("WindowsFakeFileSystem", "No file system data available", EventLogEntryType.Error);
                    return;
                }
                
                Guid guid = Guid.NewGuid();
                
                provider = new ProjFSProvider(rootPath, csvData, alertDomain, debugMode, useApiForContent, apiKey);
                
                int result = ProjFSNative.PrjMarkDirectoryAsPlaceholder(rootPath, null, IntPtr.Zero, ref guid);
                
                provider.StartVirtualizing();
                
                stopEvent.WaitOne();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("WindowsFakeFileSystem", "Error: " + ex.Message, EventLogEntryType.Error);
            }
        }
    }

    // Program Entry Point
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "/console")
            {
                RunInConsoleMode();
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new WindowsFakeFileSystemService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
        
        static void RunInConsoleMode()
        {
            string rootPath = ConfigurationManager.AppSettings["RootPath"] ?? @"C:\Secrets";
            string alertDomain = ConfigurationManager.AppSettings["AlertDomain"] ?? "INSERT TOKEN HERE";
            bool debugMode = bool.Parse(ConfigurationManager.AppSettings["DebugMode"] ?? "false");
            bool useApiForStructure = bool.Parse(ConfigurationManager.AppSettings["UseApiForStructure"] ?? "false");
            bool useApiForContent = bool.Parse(ConfigurationManager.AppSettings["UseApiForContent"] ?? "false");
            string apiKey = ConfigurationManager.AppSettings["AnthropicApiKey"] ?? "";
            string csvData = ConfigurationManager.AppSettings["FileSystemData"] ?? "";
            
            Console.WriteLine("Virtual Folder: " + rootPath);
            Console.WriteLine("Debug Mode: " + debugMode);
            Console.WriteLine("Use API for Structure: " + useApiForStructure);
            Console.WriteLine("Use API for Content: " + useApiForContent);
            
            try
            {
                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                    Console.WriteLine("Created directory: " + rootPath);
                }
                
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(rootPath));
                Console.WriteLine("Available free space: " + drive.AvailableFreeSpace + " bytes");
                
                if (useApiForStructure && !string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("Generating file structure using Claude API...");
                    string apiGeneratedData = ClaudeApiHelper.GenerateFileStructure(apiKey).Result;
                    
                    if (!string.IsNullOrEmpty(apiGeneratedData) && !apiGeneratedData.StartsWith("Error"))
                    {
                        csvData = apiGeneratedData;
                        Console.WriteLine("File structure generated.");
                    }
                    else
                    {
                        Console.WriteLine("API failed to generate structure, using fallback data from config.");
                        Console.WriteLine("API Error: " + apiGeneratedData);
                    }
                }
                
                if (string.IsNullOrEmpty(csvData))
                {
                    Console.WriteLine("ERROR: No file system data available!");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    return;
                }
                
                var provider = new ProjFSProvider(rootPath, csvData, alertDomain, debugMode, useApiForContent, apiKey);
                Guid guid = Guid.NewGuid();
                int result = ProjFSNative.PrjMarkDirectoryAsPlaceholder(rootPath, null, IntPtr.Zero, ref guid);
                
                provider.StartVirtualizing();
                
                Console.WriteLine("Projected File System Provider started. Press any key to exit.");
                Console.ReadKey();
                
                provider.StopVirtualizing();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                if (ex is Win32Exception)
                {
                    Console.WriteLine("Win32 Error Code: " + ((Win32Exception)ex).NativeErrorCode);
                }
            }
        }
    }

    // Projected File System Provider
    class ProjFSProvider
    {
        private readonly string rootPath;
        private readonly Dictionary<string, List<FileEntry>> fileSystem = new Dictionary<string, List<FileEntry>>();
        private IntPtr instanceHandle;
        private readonly bool enableDebug;
        private readonly string alertDomain;
        private readonly bool useApiForContent;
        private readonly string apiKey;
        private Dictionary<Guid, int> enumerationIndices = new Dictionary<Guid, int>();

        public ProjFSProvider(string rootPath, string csvStr, string alertDomain, bool enableDebug, bool useApiForContent, string apiKey)
        {
            this.rootPath = rootPath;
            this.enableDebug = enableDebug;
            this.alertDomain = alertDomain;
            this.useApiForContent = useApiForContent;
            this.apiKey = apiKey;
            LoadFileSystemFromCsvString(csvStr);
        }

        private static string BytesToBase32(byte[] bytes)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            string output = "";
            for (int bitIndex = 0; bitIndex < bytes.Length * 8; bitIndex += 5)
            {
                int dualbyte = bytes[bitIndex / 8] << 8;
                if (bitIndex / 8 + 1 < bytes.Length)
                    dualbyte |= bytes[bitIndex / 8 + 1];
                dualbyte = 0x1f & (dualbyte >> (16 - bitIndex % 8 - 5));
                output += alphabet[dualbyte];
            }
            return output;
        }

        private void AlertOnFileAccess(string filePath, string imgFileName)
        {
            Console.WriteLine(string.Format("Alerting on: {0} from process {1}", filePath, imgFileName));
            string[] pathParts = filePath.Split('\\');
            string filename = pathParts[pathParts.Length - 1];
            string[] imgParts = imgFileName.Split('\\');
            string imgname = imgParts[imgParts.Length - 1];
            string fnb32 = BytesToBase32(Encoding.UTF8.GetBytes(filename));
            string inb32 = BytesToBase32(Encoding.UTF8.GetBytes(imgname));
            Random rnd = new Random();
            string uniqueval = "u" + rnd.Next(1000, 10000).ToString() + ".";

            try
            {
                Task.Run(() => Dns.GetHostEntry(uniqueval + "f" + fnb32 + ".i" + inb32 + "." + alertDomain));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void LoadFileSystemFromCsvString(string csvStr)
        {
            if (string.IsNullOrEmpty(csvStr))
            {
                Console.WriteLine("Warning: CSV data is empty!");
                return;
            }

            if (enableDebug)
            {
                Console.WriteLine("CSV Data Length: " + csvStr.Length);
                Console.WriteLine("First 200 chars: " + (csvStr.Length > 200 ? csvStr.Substring(0, 200) : csvStr));
            }

            string[] lines = csvStr.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int lineCount = 0;
            int validCount = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                lineCount++;
                var parts = line.Split(',');
                
                if (parts.Length != 4)
                {
                    if (enableDebug)
                    {
                        Console.WriteLine(string.Format("Skipping invalid line {0}: {1}", lineCount, line));
                    }
                    continue;
                }

                try
                {
                    string path = parts[0].TrimStart('\\');
                    string name = Path.GetFileName(path);
                    string parentPath = Path.GetDirectoryName(path);
                    bool isDirectory = bool.Parse(parts[1]);
                    long fileSize = long.Parse(parts[2]);

                    long unixTimestamp = long.Parse(parts[3]);
                    DateTime lastWriteTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimestamp);

                    if (string.IsNullOrEmpty(parentPath))
                    {
                        parentPath = "\\";
                    }

                    if (!fileSystem.ContainsKey(parentPath))
                    {
                        fileSystem[parentPath] = new List<FileEntry>();
                    }

                    fileSystem[parentPath].Add(new FileEntry
                    {
                        Name = name,
                        IsDirectory = isDirectory,
                        FileSize = fileSize,
                        LastWriteTime = lastWriteTime,
                        Opened = false,
                        LastAlert = 0
                    });

                    validCount++;
                }
                catch (Exception ex)
                {
                    if (enableDebug)
                    {
                        Console.WriteLine(string.Format("Error parsing line {0}: {1} - {2}", lineCount, line, ex.Message));
                    }
                }
            }

            Console.WriteLine(string.Format("Loaded {0} valid entries from {1} lines", validCount, lineCount));
            
            if (enableDebug)
            {
                Console.WriteLine("File system paths:");
                foreach (var key in fileSystem.Keys)
                {
                    Console.WriteLine(string.Format("  {0}: {1} entries", key, fileSystem[key].Count));
                }
            }
        }

        public void StartVirtualizing()
        {
            ProjFSNative.PrjCallbacks callbacks = new ProjFSNative.PrjCallbacks
            {
                StartDirectoryEnumerationCallback = StartDirectoryEnumeration,
                EndDirectoryEnumerationCallback = EndDirectoryEnumeration,
                GetDirectoryEnumerationCallback = GetDirectoryEnumeration,
                GetPlaceholderInfoCallback = GetPlaceholderInfo,
                NotificationCallback = NotificationCB,
                GetFileDataCallback = GetFileData
            };

            ProjFSNative.PrjStartVirutalizingOptions options = new ProjFSNative.PrjStartVirutalizingOptions
            {
                flags = ProjFSNative.PrjStartVirutalizingFlags.PrjFlagNone,
                PoolThreadCount = 1,
                ConcurrentThreadCount = 1,
                NotificationMappings = new ProjFSNative.PrjNotificationMapping(),
                NotificationMappingCount = 0
            };

            Console.WriteLine("Attempting to start virtualization...");
            int hr = ProjFSNative.PrjStartVirtualizing(rootPath, ref callbacks, IntPtr.Zero, IntPtr.Zero, ref instanceHandle);
            if (hr != 0)
            {
                Console.WriteLine("PrjStartVirtualizing failed. HRESULT: " + hr);
                throw new Win32Exception(hr);
            }
            Console.WriteLine("Virtualization started successfully.");
        }

        public void StopVirtualizing()
        {
            if (instanceHandle != IntPtr.Zero)
            {
                Console.WriteLine("Stopping virtualization...");

                ProjFSNative.PrjStopVirtualizing(instanceHandle);
                instanceHandle = IntPtr.Zero;

                DirectoryInfo di = new DirectoryInfo(rootPath);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }

                Console.WriteLine("Virtualization stopped.");
            }
        }

        private long GetUnixTimeStamp()
        {
            long ticks = DateTime.UtcNow.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks;
            ticks /= 10000000;
            return ticks;
        }

        private int NotificationCB(ProjFSNative.PrjCallbackData callbackData, bool isDirectory, ProjFSNative.PrjNotification notification, string destinationFileName, ref ProjFSNative.PrjNotificationParameters operationParameters)
        {
            if (notification != ProjFSNative.PrjNotification.FileOpened || isDirectory)
                return ProjFSNative.S_OK;

            string parentPath = Path.GetDirectoryName(callbackData.FilePathName);
            if (string.IsNullOrEmpty(parentPath))
            {
                parentPath = "\\";
            }
            string fileName = Path.GetFileName(callbackData.FilePathName);

            List<FileEntry> entries;
            if (!fileSystem.TryGetValue(parentPath, out entries))
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            var entry = entries.Find(e => string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null || entry.IsDirectory)
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            if (entry.Opened && (GetUnixTimeStamp() - entry.LastAlert) > 5)
            {
                entry.LastAlert = GetUnixTimeStamp();
                AlertOnFileAccess(callbackData.FilePathName.ToLower(), callbackData.TriggeringProcessImageFileName);
            }

            return ProjFSNative.S_OK;
        }

        private int StartDirectoryEnumeration(ProjFSNative.PrjCallbackData callbackData, ref Guid enumerationId)
        {
            return ProjFSNative.S_OK;
        }

        private int EndDirectoryEnumeration(ProjFSNative.PrjCallbackData callbackData, ref Guid enumerationId)
        {
            if (enumerationIndices.ContainsKey(enumerationId))
            {
                enumerationIndices.Remove(enumerationId);
            }
            return ProjFSNative.S_OK;
        }

        private int GetDirectoryEnumeration(ProjFSNative.PrjCallbackData callbackData, ref Guid enumerationId, string searchExpression, IntPtr dirEntryBufferHandle)
        {
            string directoryPath = callbackData.FilePathName ?? "";
            bool single = false;

            if (string.IsNullOrEmpty(directoryPath))
            {
                directoryPath = "\\";
            }

            List<FileEntry> entries;
            if (!fileSystem.TryGetValue(directoryPath, out entries))
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            int currentIndex;
            if (!enumerationIndices.TryGetValue(enumerationId, out currentIndex))
            {
                currentIndex = 0;
                enumerationIndices[enumerationId] = currentIndex;
            }

            if (callbackData.Flags == ProjFSNative.PrjCallbackDataFlags.RestartScan)
            {
                currentIndex = 0;
                enumerationIndices[enumerationId] = 0;
            }
            else if (callbackData.Flags == ProjFSNative.PrjCallbackDataFlags.ReturnSingleEntry)
            {
                single = true;
            }

            entries.Sort(delegate(FileEntry a, FileEntry b) { return ProjFSNative.PrjFileNameCompare(a.Name, b.Name); });

            for (; currentIndex < entries.Count; currentIndex++)
            {
                if (currentIndex >= entries.Count)
                {
                    return ProjFSNative.S_OK;
                }

                var entry = entries[currentIndex];

                if (!ProjFSNative.PrjFileNameMatch(entry.Name, searchExpression))
                {
                    enumerationIndices[enumerationId] = currentIndex + 1;
                    continue;
                }

                ProjFSNative.PrjFileBasicInfo fileInfo = new ProjFSNative.PrjFileBasicInfo
                {
                    IsDirectory = entry.IsDirectory,
                    FileSize = entry.FileSize,
                    CreationTime = entry.LastWriteTime.ToFileTime(),
                    LastAccessTime = entry.LastWriteTime.ToFileTime(),
                    LastWriteTime = entry.LastWriteTime.ToFileTime(),
                    ChangeTime = entry.LastWriteTime.ToFileTime(),
                    FileAttributes = entry.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal
                };

                int result = ProjFSNative.PrjFillDirEntryBuffer(entry.Name, ref fileInfo, dirEntryBufferHandle);
                if (result != ProjFSNative.S_OK)
                {
                    return ProjFSNative.S_OK;
                }

                enumerationIndices[enumerationId] = currentIndex + 1;
                if (single)
                    return ProjFSNative.S_OK;
            }

            return ProjFSNative.S_OK;
        }

        private int GetPlaceholderInfo(ProjFSNative.PrjCallbackData callbackData)
        {
            string filePath = callbackData.FilePathName ?? "";

            if (string.IsNullOrEmpty(filePath))
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            string parentPath = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(parentPath))
            {
                parentPath = "\\";
            }

            List<FileEntry> entries;
            if (!fileSystem.TryGetValue(parentPath, out entries))
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            FileEntry entry = null;
            foreach (var e in entries)
            {
                if (string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    break;
                }
            }

            if (entry == null)
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            entries.Sort(delegate(FileEntry a, FileEntry b) { return ProjFSNative.PrjFileNameCompare(a.Name, b.Name); });

            ProjFSNative.PrjPlaceholderInfo placeholderInfo = new ProjFSNative.PrjPlaceholderInfo
            {
                FileBasicInfo = new ProjFSNative.PrjFileBasicInfo
                {
                    IsDirectory = entry.IsDirectory,
                    FileSize = entry.FileSize,
                    CreationTime = entry.LastWriteTime.ToFileTime(),
                    LastAccessTime = entry.LastWriteTime.ToFileTime(),
                    LastWriteTime = entry.LastWriteTime.ToFileTime(),
                    ChangeTime = entry.LastWriteTime.ToFileTime(),
                    FileAttributes = entry.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal
                }
            };

            int result = ProjFSNative.PrjWritePlaceholderInfo(
                callbackData.NamespaceVirtualizationContext,
                filePath,
                ref placeholderInfo,
                (uint)Marshal.SizeOf(placeholderInfo));

            return result;
        }

        private int GetFileData(ProjFSNative.PrjCallbackData callbackData, ulong byteOffset, uint length)
        {
            string parentPath = Path.GetDirectoryName(callbackData.FilePathName);
            if (string.IsNullOrEmpty(parentPath))
            {
                parentPath = "\\";
            }
            string fileName = Path.GetFileName(callbackData.FilePathName);

            AlertOnFileAccess(callbackData.FilePathName, callbackData.TriggeringProcessImageFileName);

            List<FileEntry> entries;
            if (!fileSystem.TryGetValue(parentPath, out entries))
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            var entry = entries.Find(e => string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null || entry.IsDirectory)
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            entry.Opened = true;
            entry.LastAlert = GetUnixTimeStamp();

            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] textBytes;
            
            if (useApiForContent && !string.IsNullOrEmpty(apiKey))
            {
                string fileExtension = Path.GetExtension(fileName);
                string content = ClaudeApiHelper.GenerateFileContent(apiKey, fileName, fileExtension, callbackData.FilePathName).Result;
                textBytes = Encoding.UTF8.GetBytes(content);
            }
            else
            {
                textBytes = Encoding.UTF8.GetBytes(string.Format("This is the content of {0}", fileName));
            }
            
            byte[] fileContent = new byte[bom.Length + textBytes.Length];
            Buffer.BlockCopy(bom, 0, fileContent, 0, bom.Length);
            Buffer.BlockCopy(textBytes, 0, fileContent, bom.Length, textBytes.Length);

            if (byteOffset >= (ulong)fileContent.Length)
            {
                return ProjFSNative.S_OK;
            }

            uint bytesToWrite = Math.Min(length, (uint)(fileContent.Length - (int)byteOffset));
            IntPtr buffer = ProjFSNative.PrjAllocateAlignedBuffer(instanceHandle, bytesToWrite);
            try
            {
                Marshal.Copy(fileContent, (int)byteOffset, buffer, (int)bytesToWrite);
                return ProjFSNative.PrjWriteFileData(instanceHandle, ref callbackData.DataStreamId, buffer, byteOffset, bytesToWrite);
            }
            finally
            {
                ProjFSNative.PrjFreeAlignedBuffer(buffer);
            }
        }
    }

    // Claude API Helper
    class ClaudeApiHelper
    {
        public static async Task<string> GenerateFileStructure(string apiKey)
        {
            string prompt = "Generate CSV data for a corporate IT honeypot file system. Format: path,isDirectory,fileSize,unixTimestamp. Example: \\Network,true,0,1743942586 and \\Network\\Config.pdf,false,5000,1743942586. Create 20-30 realistic corporate files in folders like Network, Server, Security, Databases. Use backslash for paths. Output only CSV lines, no explanation.";

            return await CallClaudeApi(apiKey, prompt);
        }

        public static async Task<string> GenerateFileContent(string apiKey, string fileName, string fileExtension, string fullPath)
        {
            string prompt = string.Concat(new string[] {
                "Generate realistic content for corporate file: ",
                fileName,
                " (extension: ",
                fileExtension,
                "). Make it look like a real corporate document. Output only the file content."
            });

            return await CallClaudeApi(apiKey, prompt);
        }

        private static async Task<string> CallClaudeApi(string apiKey, string prompt)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                
                using (var client = new WebClient())
                {
                    client.Headers.Add("x-api-key", apiKey);
                    client.Headers.Add("anthropic-version", "2023-06-01");
                    client.Headers.Add("content-type", "application/json");

                    string escapedPrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\r", "\\n").Replace("\n", "\\n").Replace("\t", "\\t");
                    
                    string requestBody = string.Concat(new string[] {
                        "{\"model\":\"claude-sonnet-4-20250514\",\"max_tokens\":4000,\"messages\":[{\"role\":\"user\",\"content\":\"",
                        escapedPrompt,
                        "\"}]}"
                    });

                    Console.WriteLine("Request body length: " + requestBody.Length);
                    
                    string response = "";
                    try
                    {
                        response = await Task.Run(() => client.UploadString("https://api.anthropic.com/v1/messages", requestBody));
                    }
                    catch (WebException webEx)
                    {
                        if (webEx.Response != null)
                        {
                            using (var reader = new System.IO.StreamReader(webEx.Response.GetResponseStream()))
                            {
                                string errorResponse = reader.ReadToEnd();
                                Console.WriteLine("API Error Response: " + errorResponse);
                            }
                        }
                        throw;
                    }
                    
                    Console.WriteLine("Raw API response length: " + response.Length);
                    
                    int contentStart = response.IndexOf("\"text\":\"") + 8;
                    int contentEnd = response.IndexOf("\"", contentStart);
                    
                    while (contentEnd > 0 && response[contentEnd - 1] == '\\')
                    {
                        contentEnd = response.IndexOf("\"", contentEnd + 1);
                    }
                    
                    if (contentStart > 7 && contentEnd > contentStart)
                    {
                        string content = response.Substring(contentStart, contentEnd - contentStart);
                        content = content.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                        
                        content = content.Trim();
                        if (content.StartsWith("```csv"))
                        {
                            content = content.Substring(6);
                        }
                        else if (content.StartsWith("```"))
                        {
                            content = content.Substring(3);
                        }
                        
                        if (content.EndsWith("```"))
                        {
                            content = content.Substring(0, content.Length - 3);
                        }
                        
                        content = content.Trim();
                        
                        Console.WriteLine("Extracted content length: " + content.Length);
                        Console.WriteLine("First 300 chars: " + (content.Length > 300 ? content.Substring(0, 300) : content));
                        
                        return content;
                    }
                    
                    return "Error parsing API response";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calling Claude API: " + ex.Message);
                Console.WriteLine("Stack trace: " + ex.StackTrace);
                return "Error calling Claude API: " + ex.Message;
            }
        }
    }

    // File Entry Model
    class FileEntry
    {
        public string Name { get; set; }
        public bool IsDirectory { get; set; }
        public long FileSize { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool Opened { get; set; }
        public long LastAlert { get; set; }
    }

    // Native P/Invoke Declarations
    static class ProjFSNative
    {
        public const int S_OK = 0;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;
        public const int ERROR_FILE_NOT_FOUND = 2;

        [DllImport("ProjectedFSLib.dll")]
        public static extern IntPtr PrjAllocateAlignedBuffer(IntPtr namespaceVirtualizationContext, uint size);

        [DllImport("ProjectedFSLib.dll", CharSet = CharSet.Unicode)]
        public static extern bool PrjDoesNameContainWildCards(string fileName);

        [DllImport("ProjectedFSLib.dll", CharSet = CharSet.Unicode)]
        public static extern int PrjFileNameCompare(string fileName1, string fileName2);

        [DllImport("ProjectedFSLib.dll", CharSet = CharSet.Unicode)]
        public static extern bool PrjFileNameMatch(string fileNameToCheck, string pattern);

        [DllImport("ProjectedFSLib.dll", CharSet = CharSet.Unicode)]
        public static extern int PrjFillDirEntryBuffer(string fileName, ref PrjFileBasicInfo fileBasicInfo,
            IntPtr dirEntryBufferHandle);

        [DllImport("ProjectedFSLib.dll")]
        public static extern void PrjFreeAlignedBuffer(IntPtr buffer);

        [DllImport("ProjectedFSLib.dll", CharSet = CharSet.Unicode)]
        public static extern int PrjMarkDirectoryAsPlaceholder(string rootPathName, string targetPathName,
            IntPtr versionInfo, ref Guid virtualizationInstanceID);

        [DllImport("ProjectedFSLib.dll", CharSet = CharSet.Unicode)]
        public static extern int PrjStartVirtualizing(string virtualizationRootPath, ref PrjCallbacks callbacks,
            IntPtr instanceContext, IntPtr options, ref IntPtr namespaceVirtualizationContext);

        [DllImport("ProjectedFSLib.dll")]
        public static extern void PrjStopVirtualizing(IntPtr namespaceVirtualizationContext);

        [DllImport("ProjectedFSLib.dll")]
        public static extern int PrjDeleteFile(IntPtr namespaceVirtualizationContext, string destinationFileName, int updateFlags, ref int failureReason);

        [DllImport("ProjectedFSLib.dll")]
        public static extern int PrjWriteFileData(IntPtr namespaceVirtualizationContext, ref Guid dataStreamId,
            IntPtr buffer, ulong byteOffset, uint length);

        [DllImport("ProjectedFSLib.dll", CharSet = CharSet.Unicode)]
        public static extern int PrjWritePlaceholderInfo(IntPtr namespaceVirtualizationContext,
            string destinationFileName, ref PrjPlaceholderInfo placeholderInfo, uint placeholderInfoSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct PrjCallbacks
        {
            public PrjStartDirectoryEnumerationCb StartDirectoryEnumerationCallback;
            public PrjEndDirectoryEnumerationCb EndDirectoryEnumerationCallback;
            public PrjGetDirectoryEnumerationCb GetDirectoryEnumerationCallback;
            public PrjGetPlaceholderInfoCb GetPlaceholderInfoCallback;
            public PrjGetFileDataCb GetFileDataCallback;
            public PrjQueryFileNameCb QueryFileNameCallback;
            public PrjNotificationCb NotificationCallback;
            public PrjCancelCommandCb CancelCommandCallback;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PrjCallbackData
        {
            public uint Size;
            public PrjCallbackDataFlags Flags;
            public IntPtr NamespaceVirtualizationContext;
            public int CommandId;
            public Guid FileId;
            public Guid DataStreamId;
            public string FilePathName;
            public IntPtr VersionInfo;
            public uint TriggeringProcessId;
            public string TriggeringProcessImageFileName;
            public IntPtr InstanceContext;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PrjFileBasicInfo
        {
            public bool IsDirectory;
            public long FileSize;
            public long CreationTime;
            public long LastAccessTime;
            public long LastWriteTime;
            public long ChangeTime;
            public FileAttributes FileAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PrjNotificationParameters
        {
            public PrjNotifyTypes PostCreateNotificationMask;
            public PrjNotifyTypes FileRenamedNotificationMask;
            public bool FileDeletedOnHandleCloseIsFileModified;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PrjPlaceholderInfo
        {
            public PrjFileBasicInfo FileBasicInfo;
            public uint EaBufferSize;
            public uint OffsetToFirstEa;
            public uint SecurityBufferSize;
            public uint OffsetToSecurityDescriptor;
            public uint StreamsInfoBufferSize;
            public uint OffsetToFirstStreamInfo;
            public PrjPlaceholderVersionInfo VersionInfo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)] 
            public byte[] VariableData;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PrjStartVirutalizingOptions
        {
            public PrjStartVirutalizingFlags flags;
            public uint PoolThreadCount;
            public uint ConcurrentThreadCount;
            public PrjNotificationMapping NotificationMappings;
            public uint NotificationMappingCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PrjNotificationMapping
        {
            public PrjNotifyTypes NotificationBitMask;
            public string NotifcationRoot;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PrjPlaceholderVersionInfo
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)PrjPlaceholderID.Length)] 
            public byte[] ProviderID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)PrjPlaceholderID.Length)] 
            public byte[] ContentID;
        }

        [Flags]
        public enum PrjCallbackDataFlags : uint
        {
            RestartScan = 1,
            ReturnSingleEntry = 2
        }

        public enum PrjNotification : uint
        {
            FileOpened = 0x2,
            NewFileCreated = 0x4,
            FileOverwritten = 0x8,
            PreDelete = 0x10,
            PreRename = 0x20,
            PreSetHardlink = 0x40,
            FileRename = 0x80,
            HardlinkCreated = 0x100,
            FileHandleClosedNoModification = 0x200,
            FileHandleClosedFileModified = 0x400,
            FileHandleClosedFileDeleted = 0x800,
            FilePreConvertToFull = 0x1000
        }

        public enum PrjNotifyTypes : uint
        {
            None,
            SuppressNotifications,
            FileOpened,
            NewFileCreated,
            FileOverwritten,
            PreDelete,
            PreRename,
            PreSetHardlink,
            FileRenamed,
            HardlinkCreated,
            FileHandleClosedNoModification,
            FileHandleClosedFileModified,
            FileHandleClosedFileDeleted,
            FilePreConvertToFull,
            UseExistingMask
        }

        public enum PrjPlaceholderID : uint
        {
            Length = 128
        }

        public enum PrjStartVirutalizingFlags : uint
        {
            PrjFlagNone,
            PrjFlagUseNegativePathCache
        }

        public delegate int PrjCancelCommandCb(IntPtr callbackData);

        public delegate int PrjEndDirectoryEnumerationCb(PrjCallbackData callbackData, ref Guid enumerationId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate int PrjGetDirectoryEnumerationCb(PrjCallbackData callbackData, ref Guid enumerationId,
            string searchExpression, IntPtr dirEntryBufferHandle);

        public delegate int PrjGetFileDataCb(PrjCallbackData callbackData, ulong byteOffset, uint length);

        public delegate int PrjGetPlaceholderInfoCb(PrjCallbackData callbackData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate int PrjNotificationCb(PrjCallbackData callbackData, bool isDirectory, PrjNotification notification,
            string destinationFileName, ref PrjNotificationParameters operationParameters);

        public delegate int PrjStartDirectoryEnumerationCb(PrjCallbackData callbackData, ref Guid enumerationId);

        public delegate int PrjQueryFileNameCb(IntPtr callbackData);
    }

    // Service Installer
    [RunInstaller(true)]
    public class ProjectInstaller : System.Configuration.Install.Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            serviceProcessInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            // Set the account under which the service will run
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            // Configure the service
            serviceInstaller.ServiceName = "WindowsFakeFileSystem";
            serviceInstaller.DisplayName = "Windows Fake File System Service";
            serviceInstaller.Description = "Monitors file system access using Windows Projected File System";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            // Add installers to collection
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
