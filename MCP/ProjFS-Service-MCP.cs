/*******************************************************************************
 * File: ProjFS-Service-MCP.cs
 * Author: Casey Smith (Modified for MCP Integration)
 * Date: 2025
 * Version: 1.3.2
 * 
 * Description:
 *   Windows service that creates a virtual file system using the Windows
 *   Projected File System (ProjFS) API with MCP integration via Named Pipes.
 *   
 *   ENHANCEMENTS v1.3.2:
 *   - Fixed path normalization for MCP commands (\\path vs \path)
 *   - Added NormalizePath() helper to ensure consistency
 *   - MCP list operations now correctly find files created via MCP
 *   
 *   ENHANCEMENTS v1.3.1:
 *   - Increased thread pool from 1 to 4 threads for better concurrency
 *   - Asynchronous MCP server to prevent blocking ProjFS callbacks
 *   - Optimized locking strategy with snapshots in hot paths
 *   - Separate threads for each MCP client connection
 *   - Eliminated Explorer freezing issues
 *   
 *   ENHANCEMENTS v1.3.0:
 *   - Named Pipe server for MCP communication
 *   - JSON-based command protocol
 *   - Thread-safe operations
 *   - Remote file/folder creation, deletion, and listing
 * 
 * Compilation:
 *   csc /reference:System.Configuration.dll /reference:System.Xml.Linq.dll /reference:System.Runtime.Serialization.dll ProjFS-Service-MCP.cs
 * 
 * Configuration (App.config):
 *   Add this setting:
 *   <add key="EnableMCPServer" value="true"/>
 *   <add key="MCPPipeName" value="ProjFS_MCP_Pipe"/>
 ******************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WindowsFakeFileSystemService
{
    // MCP Command structures
    public class MCPCommand
    {
        public string action { get; set; }
        public string path { get; set; }
        public string content { get; set; }
        public bool isBase64 { get; set; }
    }

    public class MCPResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public List<string> data { get; set; }
    }

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
                provider.SaveConfiguration();
                provider.StopMCPServer();
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
                bool autoSave = bool.Parse(ConfigurationManager.AppSettings["AutoSave"] ?? "true");
                bool enableMCP = bool.Parse(ConfigurationManager.AppSettings["EnableMCPServer"] ?? "true");
                string mcpPipeName = ConfigurationManager.AppSettings["MCPPipeName"] ?? "ProjFS_MCP_Pipe";
                
                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }
                
                Guid guid = Guid.NewGuid();
                
                string csvData = ConfigurationManager.AppSettings["FileSystemData"];
                if (string.IsNullOrEmpty(csvData))
                {
                    csvData = GetDefaultCsvData();
                }
                
                provider = new ProjFSProvider(rootPath, csvData, alertDomain, debugMode, autoSave);
                
                int result = ProjFSNative.PrjMarkDirectoryAsPlaceholder(rootPath, null, IntPtr.Zero, ref guid);
                
                provider.StartVirtualizing();
                
                if (enableMCP)
                {
                    Thread mcpThread = new Thread(() => provider.StartMCPServer(mcpPipeName));
                    mcpThread.Start();
                }
                
                stopEvent.WaitOne();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("WindowsFakeFileSystem", "Error: " + ex.Message, EventLogEntryType.Error);
            }
        }
        
        private string GetDefaultCsvData()
        {
            return @"\Network,true,0,1743942586
\Network\Network Diagram.pdf,false,2303,1727206186
\Network\Router Configuration.xml,false,25267,1741508986";
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
            bool debugMode = bool.Parse(ConfigurationManager.AppSettings["DebugMode"] ?? "true");
            bool autoSave = bool.Parse(ConfigurationManager.AppSettings["AutoSave"] ?? "true");
            bool enableMCP = bool.Parse(ConfigurationManager.AppSettings["EnableMCPServer"] ?? "true");
            string mcpPipeName = ConfigurationManager.AppSettings["MCPPipeName"] ?? "ProjFS_MCP_Pipe";
            
            Console.WriteLine("=== ProjFS Virtual File System v1.3.2 (MCP Enabled) ===");
            Console.WriteLine("Virtual Folder: " + rootPath);
            Console.WriteLine("Debug Mode: " + debugMode);
            Console.WriteLine("Auto-Save: " + autoSave);
            Console.WriteLine("MCP Server: " + enableMCP);
            if (enableMCP)
            {
                Console.WriteLine("MCP Pipe Name: " + mcpPipeName);
            }
            
            try
            {
                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                    Console.WriteLine("Created directory: " + rootPath);
                }
                
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(rootPath));
                Console.WriteLine("Available free space: " + drive.AvailableFreeSpace + " bytes");
                
                string csvData = ConfigurationManager.AppSettings["FileSystemData"];
                if (string.IsNullOrEmpty(csvData))
                {
                    csvData = @"\Network,true,0,1743942586
\Network\Network Diagram.pdf,false,2303,1727206186
\Network\Router Configuration.xml,false,25267,1741508986
\TestData,true,0,1743942586";
                }
                
                var provider = new ProjFSProvider(rootPath, csvData, alertDomain, debugMode, autoSave);
                Guid guid = Guid.NewGuid();
                int result = ProjFSNative.PrjMarkDirectoryAsPlaceholder(rootPath, null, IntPtr.Zero, ref guid);
                
                if (result != 0)
                {
                    Console.WriteLine("Failed to mark directory as placeholder. HRESULT: " + result);
                    Console.ReadKey();
                    return;
                }
                
                provider.StartVirtualizing();
                
                if (enableMCP)
                {
                    Thread mcpThread = new Thread(() => provider.StartMCPServer(mcpPipeName));
                    mcpThread.Start();
                }
                
                Console.WriteLine("\nProjected File System Provider started.");
                Console.WriteLine("Press 'S' to save configuration, any other key to exit.");
                
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key == ConsoleKey.S)
                {
                    provider.SaveConfiguration();
                    Console.WriteLine("\nConfiguration saved to App.config");
                }
                
                provider.StopMCPServer();
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
                Console.WriteLine("\nPress any key to exit.");
                Console.ReadKey();
            }
        }
    }

    // Projected File System Provider
    class ProjFSProvider
    {
        private readonly string rootPath;
        private readonly Dictionary<string, List<FileEntry>> fileSystem = new Dictionary<string, List<FileEntry>>();
        private readonly Dictionary<string, byte[]> fileContents = new Dictionary<string, byte[]>();
        private IntPtr instanceHandle;
        private readonly bool enableDebug;
        private readonly string alertDomain;
        private readonly bool autoSave;
        private Dictionary<Guid, int> enumerationIndices = new Dictionary<Guid, int>();
        private readonly object fileSystemLock = new object();
        
        // MCP Server
        private bool mcpRunning = false;
        private Thread mcpServerThread;

        public ProjFSProvider(string rootPath, string csvStr, string alertDomain, bool enableDebug, bool autoSave)
        {
            this.rootPath = rootPath;
            this.enableDebug = enableDebug;
            this.alertDomain = alertDomain;
            this.autoSave = autoSave;
            LoadFileSystemFromCsvString(csvStr);
        }

        public void StartMCPServer(string pipeName)
        {
            mcpRunning = true;
            
            if (enableDebug)
            {
                Console.WriteLine("Starting MCP Server on pipe: " + pipeName);
            }
            
            while (mcpRunning)
            {
                NamedPipeServerStream pipeServer = null;
                
                try
                {
                    pipeServer = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    
                    if (enableDebug)
                    {
                        Console.WriteLine("Waiting for MCP client connection...");
                    }
                    
                    // Wait for connection with timeout
                    IAsyncResult result = pipeServer.BeginWaitForConnection(null, null);
                    int timeout = 1000; // 1 second timeout to check mcpRunning flag
                    
                    if (!result.AsyncWaitHandle.WaitOne(timeout))
                    {
                        // Timeout - close this pipe and create a new one
                        pipeServer.Close();
                        continue;
                    }
                    
                    pipeServer.EndWaitForConnection(result);
                    
                    if (enableDebug)
                    {
                        Console.WriteLine("MCP client connected!");
                    }
                    
                    // Handle the client in a separate thread to avoid blocking
                    Thread clientThread = new Thread(() => HandleMCPClient(pipeServer));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                    
                    // Don't wait for the client thread - immediately loop to accept new connections
                    // The client thread will handle disposal of its pipe
                }
                catch (Exception ex)
                {
                    if (enableDebug && mcpRunning)
                    {
                        Console.WriteLine("MCP Server error: " + ex.Message);
                    }
                    
                    if (pipeServer != null)
                    {
                        try { pipeServer.Close(); } catch { }
                    }
                }
            }
        }

        private void HandleMCPClient(NamedPipeServerStream pipeServer)
        {
            try
            {
                while (pipeServer.IsConnected && mcpRunning)
                {
                    byte[] lengthBytes = new byte[4];
                    int bytesRead = pipeServer.Read(lengthBytes, 0, 4);
                    
                    if (bytesRead == 0)
                        break;
                    
                    int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                    
                    if (messageLength <= 0 || messageLength > 1048576) // 1MB max
                    {
                        if (enableDebug)
                        {
                            Console.WriteLine("Invalid message length: " + messageLength);
                        }
                        break;
                    }
                    
                    byte[] messageBytes = new byte[messageLength];
                    bytesRead = pipeServer.Read(messageBytes, 0, messageLength);
                    
                    if (bytesRead == 0)
                        break;
                    
                    string jsonRequest = Encoding.UTF8.GetString(messageBytes);
                    
                    if (enableDebug)
                    {
                        Console.WriteLine("MCP Request: " + jsonRequest);
                    }
                    
                    MCPCommand command = DeserializeJson<MCPCommand>(jsonRequest);
                    MCPResponse response = ProcessMCPCommand(command);
                    
                    string jsonResponse = SerializeJson(response);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                    byte[] responseLengthBytes = BitConverter.GetBytes(responseBytes.Length);
                    
                    pipeServer.Write(responseLengthBytes, 0, 4);
                    pipeServer.Write(responseBytes, 0, responseBytes.Length);
                    pipeServer.Flush();
                }
            }
            catch (Exception ex)
            {
                if (enableDebug)
                {
                    Console.WriteLine("MCP Client handler error: " + ex.Message);
                }
            }
            finally
            {
                // Clean up the pipe for this client
                try
                {
                    if (pipeServer != null)
                    {
                        pipeServer.Close();
                        pipeServer.Dispose();
                    }
                }
                catch { }
                
                if (enableDebug)
                {
                    Console.WriteLine("MCP client disconnected");
                }
            }
        }

        private MCPResponse ProcessMCPCommand(MCPCommand command)
        {
            MCPResponse response = new MCPResponse { data = new List<string>() };
            
            try
            {
                // Normalize path to ensure consistency (single leading backslash)
                if (!string.IsNullOrEmpty(command.path))
                {
                    string originalPath = command.path;
                    command.path = NormalizePath(command.path);
                    
                    if (enableDebug && originalPath != command.path)
                    {
                        Console.WriteLine(string.Format("Path normalized: '{0}' -> '{1}'", originalPath, command.path));
                    }
                }
                
                switch (command.action)
                {
                    case "create_file":
                        byte[] content;
                        if (command.isBase64)
                        {
                            content = Convert.FromBase64String(command.content);
                        }
                        else
                        {
                            content = Encoding.UTF8.GetBytes(command.content ?? "");
                        }
                        response.success = SaveFileToVirtualRoot(command.path, content);
                        response.message = response.success ? "File created successfully" : "Failed to create file";
                        break;
                        
                    case "delete_file":
                        response.success = DeleteFileFromVirtualRoot(command.path);
                        response.message = response.success ? "File deleted successfully" : "Failed to delete file";
                        break;
                        
                    case "list_files":
                        response.data = ListVirtualFiles(command.path ?? "\\");
                        response.success = true;
                        response.message = "Files listed successfully";
                        break;
                        
                    case "list_directories":
                        response.data = ListVirtualDirectories(command.path ?? "\\");
                        response.success = true;
                        response.message = "Directories listed successfully";
                        break;
                        
                    case "list_all":
                        List<string> allItems = new List<string>();
                        List<string> dirs = ListVirtualDirectories(command.path ?? "\\");
                        List<string> files = ListVirtualFiles(command.path ?? "\\");
                        allItems.AddRange(dirs.Select(d => "[DIR] " + d));
                        allItems.AddRange(files);
                        response.data = allItems;
                        response.success = true;
                        response.message = "All items listed successfully";
                        break;
                        
                    case "create_directory":
                        EnsureDirectoryExists(command.path);
                        response.success = true;
                        response.message = "Directory created successfully";
                        break;
                        
                    default:
                        response.success = false;
                        response.message = "Unknown action: " + command.action;
                        break;
                }
            }
            catch (Exception ex)
            {
                response.success = false;
                response.message = "Error: " + ex.Message;
            }
            
            return response;
        }

        public void StopMCPServer()
        {
            mcpRunning = false;
            
            if (enableDebug)
            {
                Console.WriteLine("Stopping MCP Server...");
            }
        }

        private T DeserializeJson<T>(string json)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                return (T)serializer.ReadObject(ms);
            }
        }

        private string SerializeJson(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
                serializer.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "\\";
            
            // Remove any leading backslashes and re-add a single one
            path = path.TrimStart('\\');
            
            if (string.IsNullOrEmpty(path))
                return "\\";
            
            return "\\" + path;
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
            if (enableDebug)
            {
                Console.WriteLine(string.Format("Alerting on: {0} from process {1}", filePath, imgFileName));
            }
            
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
                if (enableDebug)
                {
                    Console.WriteLine("DNS Alert Error: " + ex.Message);
                }
            }
        }

        private void LoadFileSystemFromCsvString(string csvStr)
        {
            string[] lines = csvStr.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var parts = line.Split(',');
                if (parts.Length != 4) continue;

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
            }
        }

        private string GenerateCsvFromFileSystem()
        {
            StringBuilder csv = new StringBuilder();
            List<string> allPaths = new List<string>();
            
            CollectAllPaths("\\", allPaths);
            allPaths.Sort();
            
            foreach (string path in allPaths)
            {
                string parentPath = Path.GetDirectoryName(path);
                string fileName = Path.GetFileName(path);
                
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "\\";
                }
                
                List<FileEntry> entries;
                if (!fileSystem.TryGetValue(parentPath, out entries))
                {
                    continue;
                }
                
                FileEntry entry = entries.Find(e => string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase));
                if (entry == null) continue;
                
                long unixTimestamp = (long)(entry.LastWriteTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                
                csv.AppendFormat("{0},{1},{2},{3}\r\n",
                    path,
                    entry.IsDirectory.ToString().ToLower(),
                    entry.FileSize,
                    unixTimestamp);
            }
            
            return csv.ToString();
        }
        
        private void CollectAllPaths(string currentPath, List<string> result)
        {
            List<FileEntry> entries;
            if (!fileSystem.TryGetValue(currentPath, out entries))
            {
                return;
            }
            
            foreach (var entry in entries)
            {
                string fullPath = currentPath == "\\" 
                    ? "\\" + entry.Name 
                    : currentPath + "\\" + entry.Name;
                
                result.Add(fullPath);
                
                if (entry.IsDirectory)
                {
                    CollectAllPaths(fullPath, result);
                }
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                string configPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                string csvData = GenerateCsvFromFileSystem();
                
                XDocument doc = XDocument.Load(configPath);
                XElement appSettings = doc.Root.Element("appSettings");
                
                if (appSettings != null)
                {
                    XElement fileSystemDataElement = appSettings.Elements("add")
                        .FirstOrDefault(e => (string)e.Attribute("key") == "FileSystemData");
                    
                    if (fileSystemDataElement != null)
                    {
                        fileSystemDataElement.SetAttributeValue("value", csvData);
                    }
                    else
                    {
                        appSettings.Add(new XElement("add",
                            new XAttribute("key", "FileSystemData"),
                            new XAttribute("value", csvData)));
                    }
                    
                    doc.Save(configPath);
                    ConfigurationManager.RefreshSection("appSettings");
                    
                    if (enableDebug)
                    {
                        Console.WriteLine("Configuration saved successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                if (enableDebug)
                {
                    Console.WriteLine("Error saving configuration: " + ex.Message);
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

            ProjFSNative.PrjNotificationMapping[] mappings = new ProjFSNative.PrjNotificationMapping[1];
            mappings[0] = new ProjFSNative.PrjNotificationMapping
            {
                NotificationBitMask = ProjFSNative.PrjNotifyTypes.FileOpened | 
                                     ProjFSNative.PrjNotifyTypes.NewFileCreated | 
                                     ProjFSNative.PrjNotifyTypes.FileOverwritten |
                                     ProjFSNative.PrjNotifyTypes.FileHandleClosedFileModified |
                                     ProjFSNative.PrjNotifyTypes.FileHandleClosedFileDeleted,
                NotifcationRoot = ""
            };

            IntPtr mappingsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ProjFSNative.PrjNotificationMapping)));
            Marshal.StructureToPtr(mappings[0], mappingsPtr, false);

            ProjFSNative.PrjStartVirutalizingOptions options = new ProjFSNative.PrjStartVirutalizingOptions
            {
                flags = ProjFSNative.PrjStartVirutalizingFlags.PrjFlagNone,
                PoolThreadCount = 4,
                ConcurrentThreadCount = 4,
                NotificationMappings = mappingsPtr,
                NotificationMappingCount = 1
            };

            if (enableDebug)
            {
                Console.WriteLine("Attempting to start virtualization...");
            }
            
            IntPtr optionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ProjFSNative.PrjStartVirutalizingOptions)));
            Marshal.StructureToPtr(options, optionsPtr, false);

            int hr = ProjFSNative.PrjStartVirtualizing(rootPath, ref callbacks, IntPtr.Zero, optionsPtr, ref instanceHandle);
            
            Marshal.FreeHGlobal(mappingsPtr);
            Marshal.FreeHGlobal(optionsPtr);

            if (hr != 0)
            {
                string errorMsg = string.Format("PrjStartVirtualizing failed. HRESULT: 0x{0:X8}", hr);
                Console.WriteLine(errorMsg);
                throw new Win32Exception(hr, errorMsg);
            }
            
            if (enableDebug)
            {
                Console.WriteLine("Virtualization started successfully.");
            }
        }

        public void StopVirtualizing()
        {
            if (instanceHandle != IntPtr.Zero)
            {
                if (enableDebug)
                {
                    Console.WriteLine("Stopping virtualization...");
                }

                ProjFSNative.PrjStopVirtualizing(instanceHandle);
                instanceHandle = IntPtr.Zero;

                try
                {
                    DirectoryInfo di = new DirectoryInfo(rootPath);
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
                catch (Exception ex)
                {
                    if (enableDebug)
                    {
                        Console.WriteLine("Cleanup warning: " + ex.Message);
                    }
                }

                if (enableDebug)
                {
                    Console.WriteLine("Virtualization stopped.");
                }
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
            if (isDirectory)
                return ProjFSNative.S_OK;

            string parentPath = Path.GetDirectoryName(callbackData.FilePathName);
            if (string.IsNullOrEmpty(parentPath))
            {
                parentPath = "\\";
            }
            string fileName = Path.GetFileName(callbackData.FilePathName);

            List<FileEntry> entries;
            lock (fileSystemLock)
            {
                if (!fileSystem.TryGetValue(parentPath, out entries))
                {
                    return ProjFSNative.S_OK;
                }
            }

            var entry = entries.Find(e => string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase));
            
            if (notification == ProjFSNative.PrjNotification.FileOpened)
            {
                if (entry == null || entry.IsDirectory)
                {
                    return ProjFSNative.S_OK;
                }

                if (entry.Opened && (GetUnixTimeStamp() - entry.LastAlert) > 5)
                {
                    entry.LastAlert = GetUnixTimeStamp();
                    AlertOnFileAccess(callbackData.FilePathName.ToLower(), callbackData.TriggeringProcessImageFileName);
                }
            }
            else if (notification == ProjFSNative.PrjNotification.FileHandleClosedFileModified)
            {
                if (entry != null && !entry.IsDirectory)
                {
                    string fullPath = Path.Combine(rootPath, callbackData.FilePathName.TrimStart('\\'));
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            byte[] data = File.ReadAllBytes(fullPath);
                            lock (fileSystemLock)
                            {
                                fileContents[callbackData.FilePathName.ToLower()] = data;
                                entry.FileSize = data.Length;
                                entry.LastWriteTime = DateTime.UtcNow;
                            }
                            
                            if (enableDebug)
                            {
                                Console.WriteLine(string.Format("File modified: {0}, new size: {1}", callbackData.FilePathName, data.Length));
                            }
                            
                            if (autoSave)
                            {
                                SaveConfiguration();
                            }
                            
                            AlertOnFileAccess(callbackData.FilePathName.ToLower(), callbackData.TriggeringProcessImageFileName);
                        }
                        catch (Exception ex)
                        {
                            if (enableDebug)
                            {
                                Console.WriteLine("Error reading modified file: " + ex.Message);
                            }
                        }
                    }
                }
            }
            else if (notification == ProjFSNative.PrjNotification.NewFileCreated)
            {
                string fullPath = Path.Combine(rootPath, callbackData.FilePathName.TrimStart('\\'));
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] data = File.ReadAllBytes(fullPath);
                        lock (fileSystemLock)
                        {
                            fileContents[callbackData.FilePathName.ToLower()] = data;
                            
                            FileEntry newEntry = new FileEntry
                            {
                                Name = fileName,
                                IsDirectory = false,
                                FileSize = data.Length,
                                LastWriteTime = DateTime.UtcNow,
                                Opened = true,
                                LastAlert = GetUnixTimeStamp()
                            };
                            
                            entries.Add(newEntry);
                        }
                        
                        if (enableDebug)
                        {
                            Console.WriteLine(string.Format("New file created: {0}, size: {1}", callbackData.FilePathName, data.Length));
                        }
                        
                        if (autoSave)
                        {
                            SaveConfiguration();
                        }
                        
                        AlertOnFileAccess(callbackData.FilePathName.ToLower(), callbackData.TriggeringProcessImageFileName);
                    }
                    catch (Exception ex)
                    {
                        if (enableDebug)
                        {
                            Console.WriteLine("Error reading new file: " + ex.Message);
                        }
                    }
                }
            }
            else if (notification == ProjFSNative.PrjNotification.FileOverwritten)
            {
                if (entry != null && !entry.IsDirectory)
                {
                    string fullPath = Path.Combine(rootPath, callbackData.FilePathName.TrimStart('\\'));
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            byte[] data = File.ReadAllBytes(fullPath);
                            lock (fileSystemLock)
                            {
                                fileContents[callbackData.FilePathName.ToLower()] = data;
                                entry.FileSize = data.Length;
                                entry.LastWriteTime = DateTime.UtcNow;
                            }
                            
                            if (enableDebug)
                            {
                                Console.WriteLine(string.Format("File overwritten: {0}, new size: {1}", callbackData.FilePathName, data.Length));
                            }
                            
                            if (autoSave)
                            {
                                SaveConfiguration();
                            }
                            
                            AlertOnFileAccess(callbackData.FilePathName.ToLower(), callbackData.TriggeringProcessImageFileName);
                        }
                        catch (Exception ex)
                        {
                            if (enableDebug)
                            {
                                Console.WriteLine("Error reading overwritten file: " + ex.Message);
                            }
                        }
                    }
                }
            }

            return ProjFSNative.S_OK;
        }

        private int StartDirectoryEnumeration(ProjFSNative.PrjCallbackData callbackData, ref Guid enumerationId)
        {
            return ProjFSNative.S_OK;
        }

        private int EndDirectoryEnumeration(ProjFSNative.PrjCallbackData callbackData, ref Guid enumerationId)
        {
            lock (fileSystemLock)
            {
                if (enumerationIndices.ContainsKey(enumerationId))
                {
                    enumerationIndices.Remove(enumerationId);
                }
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

            // Get a snapshot of entries quickly to minimize lock time
            List<FileEntry> entriesSnapshot;
            lock (fileSystemLock)
            {
                List<FileEntry> entries;
                if (!fileSystem.TryGetValue(directoryPath, out entries))
                {
                    return ProjFSNative.ERROR_FILE_NOT_FOUND;
                }
                
                // Create a shallow copy to work with outside the lock
                entriesSnapshot = new List<FileEntry>(entries);
            }

            int currentIndex;
            lock (fileSystemLock)
            {
                if (!enumerationIndices.TryGetValue(enumerationId, out currentIndex))
                {
                    currentIndex = 0;
                    enumerationIndices[enumerationId] = currentIndex;
                }
            }

            if (callbackData.Flags == ProjFSNative.PrjCallbackDataFlags.RestartScan)
            {
                currentIndex = 0;
                lock (fileSystemLock)
                {
                    enumerationIndices[enumerationId] = 0;
                }
            }
            else if (callbackData.Flags == ProjFSNative.PrjCallbackDataFlags.ReturnSingleEntry)
            {
                single = true;
            }

            entriesSnapshot.Sort(delegate(FileEntry a, FileEntry b) { return ProjFSNative.PrjFileNameCompare(a.Name, b.Name); });

            for (; currentIndex < entriesSnapshot.Count; currentIndex++)
            {
                if (currentIndex >= entriesSnapshot.Count)
                {
                    return ProjFSNative.S_OK;
                }

                var entry = entriesSnapshot[currentIndex];

                if (!ProjFSNative.PrjFileNameMatch(entry.Name, searchExpression))
                {
                    lock (fileSystemLock)
                    {
                        enumerationIndices[enumerationId] = currentIndex + 1;
                    }
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

                lock (fileSystemLock)
                {
                    enumerationIndices[enumerationId] = currentIndex + 1;
                }
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

            // Quick lookup with minimal lock time
            FileEntry entry = null;
            lock (fileSystemLock)
            {
                List<FileEntry> entries;
                if (!fileSystem.TryGetValue(parentPath, out entries))
                {
                    return ProjFSNative.ERROR_FILE_NOT_FOUND;
                }

                foreach (var e in entries)
                {
                    if (string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Create a copy to work with outside the lock
                        entry = new FileEntry
                        {
                            Name = e.Name,
                            IsDirectory = e.IsDirectory,
                            FileSize = e.FileSize,
                            LastWriteTime = e.LastWriteTime,
                            Opened = e.Opened,
                            LastAlert = e.LastAlert
                        };
                        break;
                    }
                }
            }

            if (entry == null)
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

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
            lock (fileSystemLock)
            {
                if (!fileSystem.TryGetValue(parentPath, out entries))
                {
                    return ProjFSNative.ERROR_FILE_NOT_FOUND;
                }
            }

            var entry = entries.Find(e => string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null || entry.IsDirectory)
            {
                return ProjFSNative.ERROR_FILE_NOT_FOUND;
            }

            entry.Opened = true;
            entry.LastAlert = GetUnixTimeStamp();

            byte[] fileContent;
            string lowerPath = callbackData.FilePathName.ToLower();
            
            lock (fileSystemLock)
            {
                if (fileContents.ContainsKey(lowerPath))
                {
                    fileContent = fileContents[lowerPath];
                }
                else
                {
                    byte[] bom = { 0xEF, 0xBB, 0xBF };
                    byte[] textBytes = Encoding.UTF8.GetBytes(string.Format("This is the content of {0}", fileName));
                    fileContent = new byte[bom.Length + textBytes.Length];
                    Buffer.BlockCopy(bom, 0, fileContent, 0, bom.Length);
                    Buffer.BlockCopy(textBytes, 0, fileContent, bom.Length, textBytes.Length);
                }
            }

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

        public bool SaveFileToVirtualRoot(string virtualPath, byte[] content)
        {
            try
            {
                // Ensure path has single leading backslash
                virtualPath = NormalizePath(virtualPath);

                string parentPath = Path.GetDirectoryName(virtualPath);
                string fileName = Path.GetFileName(virtualPath);

                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "\\";
                }

                EnsureDirectoryExists(parentPath);

                List<FileEntry> entries;
                lock (fileSystemLock)
                {
                    if (!fileSystem.TryGetValue(parentPath, out entries))
                    {
                        entries = new List<FileEntry>();
                        fileSystem[parentPath] = entries;
                    }

                    var existingEntry = entries.Find(e => string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingEntry != null)
                    {
                        existingEntry.FileSize = content.Length;
                        existingEntry.LastWriteTime = DateTime.UtcNow;
                        existingEntry.Opened = false;
                        existingEntry.LastAlert = 0;
                    }
                    else
                    {
                        FileEntry newEntry = new FileEntry
                        {
                            Name = fileName,
                            IsDirectory = false,
                            FileSize = content.Length,
                            LastWriteTime = DateTime.UtcNow,
                            Opened = false,
                            LastAlert = 0
                        };
                        entries.Add(newEntry);
                    }

                    string lowerPath = virtualPath.ToLower();
                    fileContents[lowerPath] = content;
                }

                if (enableDebug)
                {
                    Console.WriteLine(string.Format("File saved to virtual root: {0}, size: {1} bytes", virtualPath, content.Length));
                }
                
                if (autoSave)
                {
                    SaveConfiguration();
                }

                return true;
            }
            catch (Exception ex)
            {
                if (enableDebug)
                {
                    Console.WriteLine("Error saving file to virtual root: " + ex.Message);
                }
                return false;
            }
        }

        public bool SaveTextFileToVirtualRoot(string virtualPath, string textContent)
        {
            byte[] content = Encoding.UTF8.GetBytes(textContent);
            return SaveFileToVirtualRoot(virtualPath, content);
        }

        private void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || directoryPath == "\\")
            {
                return;
            }

            string[] parts = directoryPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string currentPath = "\\";

            foreach (string part in parts)
            {
                string parentPath = currentPath;
                if (currentPath == "\\")
                {
                    currentPath = "\\" + part;
                }
                else
                {
                    currentPath = currentPath + "\\" + part;
                }

                List<FileEntry> entries;
                lock (fileSystemLock)
                {
                    if (!fileSystem.TryGetValue(parentPath, out entries))
                    {
                        entries = new List<FileEntry>();
                        fileSystem[parentPath] = entries;
                    }

                    var existingDir = entries.Find(e => string.Equals(e.Name, part, StringComparison.OrdinalIgnoreCase) && e.IsDirectory);
                    
                    if (existingDir == null)
                    {
                        entries.Add(new FileEntry
                        {
                            Name = part,
                            IsDirectory = true,
                            FileSize = 0,
                            LastWriteTime = DateTime.UtcNow,
                            Opened = false,
                            LastAlert = 0
                        });
                    }
                }
                
                string physicalPath = Path.Combine(rootPath, currentPath.TrimStart('\\'));
                if (!Directory.Exists(physicalPath))
                {
                    Directory.CreateDirectory(physicalPath);
                    
                    if (enableDebug)
                    {
                        Console.WriteLine("Created physical directory: " + currentPath);
                    }
                }
            }
        }

        public bool DeleteFileFromVirtualRoot(string virtualPath)
        {
            try
            {
                // Ensure path has single leading backslash
                virtualPath = NormalizePath(virtualPath);

                string parentPath = Path.GetDirectoryName(virtualPath);
                string fileName = Path.GetFileName(virtualPath);

                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "\\";
                }

                List<FileEntry> entries;
                lock (fileSystemLock)
                {
                    if (!fileSystem.TryGetValue(parentPath, out entries))
                    {
                        return false;
                    }

                    var entry = entries.Find(e => string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase));
                    if (entry != null && !entry.IsDirectory)
                    {
                        entries.Remove(entry);
                        
                        string lowerPath = virtualPath.ToLower();
                        if (fileContents.ContainsKey(lowerPath))
                        {
                            fileContents.Remove(lowerPath);
                        }

                        if (enableDebug)
                        {
                            Console.WriteLine("Deleted file from virtual root: " + virtualPath);
                        }
                        
                        if (autoSave)
                        {
                            SaveConfiguration();
                        }

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                if (enableDebug)
                {
                    Console.WriteLine("Error deleting file from virtual root: " + ex.Message);
                }
                return false;
            }
        }

        public List<string> ListVirtualFiles(string directoryPath)
        {
            List<string> result = new List<string>();
            
            // Normalize path
            directoryPath = NormalizePath(directoryPath);

            List<FileEntry> entries;
            lock (fileSystemLock)
            {
                if (fileSystem.TryGetValue(directoryPath, out entries))
                {
                    foreach (var entry in entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            result.Add(entry.Name);
                        }
                    }
                }
            }

            return result;
        }

        public List<string> ListVirtualDirectories(string directoryPath)
        {
            List<string> result = new List<string>();
            
            // Normalize path
            directoryPath = NormalizePath(directoryPath);

            List<FileEntry> entries;
            lock (fileSystemLock)
            {
                if (fileSystem.TryGetValue(directoryPath, out entries))
                {
                    foreach (var entry in entries)
                    {
                        if (entry.IsDirectory)
                        {
                            result.Add(entry.Name);
                        }
                    }
                }
            }

            return result;
        }
    }

    class FileEntry
    {
        public string Name { get; set; }
        public bool IsDirectory { get; set; }
        public long FileSize { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool Opened { get; set; }
        public long LastAlert { get; set; }
    }

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
            public IntPtr NotificationMappings;
            public uint NotificationMappingCount;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

        [Flags]
        public enum PrjNotifyTypes : uint
        {
            None = 0,
            SuppressNotifications = 0x1,
            FileOpened = 0x2,
            NewFileCreated = 0x4,
            FileOverwritten = 0x8,
            PreDelete = 0x10,
            PreRename = 0x20,
            PreSetHardlink = 0x40,
            FileRenamed = 0x80,
            HardlinkCreated = 0x100,
            FileHandleClosedNoModification = 0x200,
            FileHandleClosedFileModified = 0x400,
            FileHandleClosedFileDeleted = 0x800,
            FilePreConvertToFull = 0x1000,
            UseExistingMask = 0x2000
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

    [RunInstaller(true)]
    public class ProjectInstaller : System.Configuration.Install.Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            serviceProcessInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            serviceInstaller.ServiceName = "WindowsFakeFileSystem";
            serviceInstaller.DisplayName = "Windows Fake File System Service";
            serviceInstaller.Description = "Monitors file system access using Windows Projected File System";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
