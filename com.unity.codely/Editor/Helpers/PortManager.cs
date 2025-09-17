using System;
using System.IO;
using UnityEditor;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityTcp.Editor.Helpers
{
    /// <summary>
    /// Manages dynamic port allocation and persistent storage for Unity TCP connections
    /// </summary>
    public static class PortManager
    {
        private static bool IsDebugEnabled()
        {
            try { return EditorPrefs.GetBool("UnityTcp.DebugLogs", false); }
            catch { return false; }
        }

        private const int DefaultPort = 25916;
        private const int MaxPortAttempts = 100;
        private const string RegistryFileName = ".com-unity-codely.json";

        [Serializable]
        public class PortConfig
        {
            public int unity_port;
            public string created_date;
            public string project_path;
            
            // Status/heartbeat fields
            public bool reloading;
            public string reason;
            public int seq;
            public string last_heartbeat;
        }

        /// <summary>
        /// Get the port to use - either from storage or discover a new one
        /// Will try stored port first, then fallback to discovering new port
        /// </summary>
        /// <returns>Port number to use</returns>
        public static int GetPortWithFallback()
        {
            // Try to load stored port first, but only if it's from the current project
            var storedConfig = GetStoredPortConfig();
            if (storedConfig != null && 
                storedConfig.unity_port > 0 && 
                string.Equals(storedConfig.project_path ?? string.Empty, Application.dataPath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                IsPortAvailable(storedConfig.unity_port))
            {
                if (IsDebugEnabled()) Debug.Log($"<b><color=#2EA3FF>Unity TCP</color></b>: Using stored port {storedConfig.unity_port} for current project");
                return storedConfig.unity_port;
            }

            // If stored port exists but is currently busy, wait briefly for release
            if (storedConfig != null && storedConfig.unity_port > 0)
            {
                if (WaitForPortRelease(storedConfig.unity_port, 1500))
                {
                    if (IsDebugEnabled()) Debug.Log($"<b><color=#2EA3FF>Unity TCP</color></b>: Stored port {storedConfig.unity_port} became available after short wait");
                    return storedConfig.unity_port;
                }
                // Prefer sticking to the same port; let the caller handle bind retries/fallbacks
                return storedConfig.unity_port;
            }

            // If no valid stored port, find a new one and save it
            int newPort = FindAvailablePort();
            SavePort(newPort);
            return newPort;
        }

        /// <summary>
        /// Discover and save a new available port (used by Auto-Connect button)
        /// </summary>
        /// <returns>New available port</returns>
        public static int DiscoverNewPort()
        {
            int newPort = FindAvailablePort();
            SavePort(newPort);
            if (IsDebugEnabled()) Debug.Log($"<b><color=#2EA3FF>Unity TCP</color></b>: Discovered and saved new port: {newPort}");
            return newPort;
        }

        /// <summary>
        /// Find an available port starting from the default port
        /// </summary>
        /// <returns>Available port number</returns>
        private static int FindAvailablePort()
        {
            // Always try default port first
            if (IsPortAvailable(DefaultPort))
            {
                if (IsDebugEnabled()) Debug.Log($"<b><color=#2EA3FF>Unity TCP</color></b>: Using default port {DefaultPort}");
                return DefaultPort;
            }

            if (IsDebugEnabled()) Debug.Log($"<b><color=#2EA3FF>Unity TCP</color></b>: Default port {DefaultPort} is in use, searching for alternative...");

            // Search for alternatives
            for (int port = DefaultPort + 1; port < DefaultPort + MaxPortAttempts; port++)
            {
                if (IsPortAvailable(port))
                {
                    if (IsDebugEnabled()) Debug.Log($"<b><color=#2EA3FF>Unity TCP</color></b>: Found available port {port}");
                    return port;
                }
            }

            throw new Exception($"No available ports found in range {DefaultPort}-{DefaultPort + MaxPortAttempts}");
        }

        /// <summary>
        /// Check if a specific port is available for binding
        /// </summary>
        /// <param name="port">Port to check</param>
        /// <returns>True if port is available</returns>
        public static bool IsPortAvailable(int port)
        {
            try
            {
                var testListener = new TcpListener(IPAddress.Loopback, port);
                testListener.Start();
                testListener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a port is currently being used by Unity TCP server
        /// This helps avoid unnecessary port changes when Unity itself is using the port
        /// </summary>
        /// <param name="port">Port to check</param>
        /// <returns>True if port appears to be used by Unity TCP server</returns>
        public static bool IsPortUsedByUnityTcp(int port)
        {
            try
            {
                // Try to make a quick connection to see if it's a Unity TCP server
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
                if (connectTask.Wait(100)) // 100ms timeout
                {
                    // If connection succeeded, it's likely the Unity TCP server
                    return client.Connected;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Wait for a port to become available for a limited amount of time.
        /// Used to bridge the gap during domain reload when the old listener
        /// hasn't released the socket yet.
        /// </summary>
        private static bool WaitForPortRelease(int port, int timeoutMs)
        {
            int waited = 0;
            const int step = 100;
            while (waited < timeoutMs)
            {
                if (IsPortAvailable(port))
                {
                    return true;
                }

                // If the port is in use by a Unity TCP instance, continue waiting briefly
                if (!IsPortUsedByUnityTcp(port))
                {
                    // In use by something else; don't keep waiting
                    return false;
                }

                Thread.Sleep(step);
                waited += step;
            }
            return IsPortAvailable(port);
        }

        /// <summary>
        /// Save port to persistent storage, preserving existing status information
        /// </summary>
        /// <param name="port">Port to save</param>
        private static void SavePort(int port)
        {
            try
            {
                // Load existing config to preserve status information
                var existingConfig = GetStoredPortConfig();
                
                var portConfig = new PortConfig
                {
                    unity_port = port,
                    created_date = existingConfig?.created_date ?? DateTime.UtcNow.ToString("O"),
                    project_path = Application.dataPath,
                    
                    // Preserve existing status fields
                    reloading = existingConfig?.reloading ?? false,
                    reason = existingConfig?.reason ?? "ready",
                    seq = existingConfig?.seq ?? 0,
                    last_heartbeat = existingConfig?.last_heartbeat
                };

                SavePortConfig(portConfig);

                if (IsDebugEnabled()) Debug.Log($"<b><color=#2EA3FF>Unity TCP</color></b>: Saved port {port} to storage");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not save port to storage: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save port configuration to persistent storage
        /// </summary>
        /// <param name="portConfig">Port configuration to save</param>
        public static void SavePortConfig(PortConfig portConfig)
        {
            try
            {
                string registryDir = GetRegistryDirectory();
                Directory.CreateDirectory(registryDir);

                string registryFile = GetRegistryFilePath();
                string json = JsonConvert.SerializeObject(portConfig, Formatting.Indented);
                // Write to project root config file
                File.WriteAllText(registryFile, json, new System.Text.UTF8Encoding(false));
                
                // Also maintain backwards compatibility by writing to legacy location
                try
                {
                    string legacyDir = GetLegacyRegistryDirectory();
                    Directory.CreateDirectory(legacyDir);
                    string legacyFile = Path.Combine(legacyDir, "unity-tcp-port.json");
                    File.WriteAllText(legacyFile, json, new System.Text.UTF8Encoding(false));
                }
                catch
                {
                    // Ignore legacy write failures
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not save port config to storage: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load port from persistent storage
        /// </summary>
        /// <returns>Stored port number, or 0 if not found</returns>
        private static int LoadStoredPort()
        {
            try
            {
                string registryFile = GetRegistryFilePath();
                
                if (!File.Exists(registryFile))
                {
                    // Backwards compatibility: try the legacy locations
                    // First try the new legacy location in project root
                    string projectLegacy = Path.Combine(GetRegistryDirectory(), "unity-tcp-port.json");
                    if (File.Exists(projectLegacy))
                    {
                        registryFile = projectLegacy;
                    }
                    else
                    {
                        // Then try the old user home location
                        string userHomeLegacy = Path.Combine(GetLegacyRegistryDirectory(), "unity-tcp-port.json");
                        if (File.Exists(userHomeLegacy))
                        {
                            registryFile = userHomeLegacy;
                        }
                        else
                        {
                            // Also check hash-based files in user home
                            string hashBased = Path.Combine(GetLegacyRegistryDirectory(), $"unity-tcp-port-{ComputeProjectHash(Application.dataPath)}.json");
                            if (File.Exists(hashBased))
                            {
                                registryFile = hashBased;
                            }
                            else
                            {
                                return 0;
                            }
                        }
                    }
                }

                string json = File.ReadAllText(registryFile);
                var portConfig = JsonConvert.DeserializeObject<PortConfig>(json);

                return portConfig?.unity_port ?? 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not load port from storage: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get the current stored port configuration
        /// </summary>
        /// <returns>Port configuration if exists, null otherwise</returns>
        public static PortConfig GetStoredPortConfig()
        {
            try
            {
                string registryFile = GetRegistryFilePath();
                
                if (!File.Exists(registryFile))
                {
                    // Backwards compatibility: try the legacy locations
                    // First try the new legacy location in project root
                    string projectLegacy = Path.Combine(GetRegistryDirectory(), "unity-tcp-port.json");
                    if (File.Exists(projectLegacy))
                    {
                        registryFile = projectLegacy;
                    }
                    else
                    {
                        // Then try the old user home location
                        string userHomeLegacy = Path.Combine(GetLegacyRegistryDirectory(), "unity-tcp-port.json");
                        if (File.Exists(userHomeLegacy))
                        {
                            registryFile = userHomeLegacy;
                        }
                        else
                        {
                            // Also check hash-based files in user home
                            string hashBased = Path.Combine(GetLegacyRegistryDirectory(), $"unity-tcp-port-{ComputeProjectHash(Application.dataPath)}.json");
                            if (File.Exists(hashBased))
                            {
                                registryFile = hashBased;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }

                string json = File.ReadAllText(registryFile);
                return JsonConvert.DeserializeObject<PortConfig>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not load port config: {ex.Message}");
                return null;
            }
        }

        private static string GetRegistryDirectory()
        {
            // Use project root directory (parent of Assets folder)
            string assetsPath = Application.dataPath;
            string projectRoot = Directory.GetParent(assetsPath)?.FullName ?? assetsPath;
            return projectRoot;
        }

        private static string GetLegacyRegistryDirectory()
        {
            // Legacy location in user home directory
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-tcp");
        }

        private static string GetRegistryFilePath()
        {
            string dir = GetRegistryDirectory();
            return Path.Combine(dir, RegistryFileName);
        }

        private static string ComputeProjectHash(string input)
        {
            try
            {
                using SHA1 sha1 = SHA1.Create();
                byte[] bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
                byte[] hashBytes = sha1.ComputeHash(bytes);
                var sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString()[..8]; // short, sufficient for filenames
            }
            catch
            {
                return "default";
            }
        }
    }
}
