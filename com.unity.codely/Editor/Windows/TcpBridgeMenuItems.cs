using UnityEditor;
using UnityTcp.Editor.Helpers;

namespace UnityTcp.Editor.Windows
{
    /// <summary>
    /// Menu items for quick TCP bridge operations
    /// </summary>
    public static class TcpBridgeMenuItems
    {
        [MenuItem("Tools/Unity TCP Bridge/Start Server")]
        public static void StartServer()
        {
            if (!UnityTcpBridge.IsRunning)
            {
                UnityTcpBridge.Start();
                TcpLog.Info("TCP Bridge started via menu");
            }
            else
            {
                TcpLog.Info("TCP Bridge is already running");
            }
        }

        [MenuItem("Tools/Unity TCP Bridge/Start Server", true)]
        public static bool StartServerValidate()
        {
            return !UnityTcpBridge.IsRunning;
        }

        [MenuItem("Tools/Unity TCP Bridge/Stop Server")]
        public static void StopServer()
        {
            if (UnityTcpBridge.IsRunning)
            {
                UnityTcpBridge.Stop();
                TcpLog.Info("TCP Bridge stopped via menu");
            }
            else
            {
                TcpLog.Info("TCP Bridge is not running");
            }
        }

        [MenuItem("Tools/Unity TCP Bridge/Stop Server", true)]
        public static bool StopServerValidate()
        {
            return UnityTcpBridge.IsRunning;
        }

        [MenuItem("Tools/Unity TCP Bridge/Restart Server")]
        public static void RestartServer()
        {
            UnityTcpBridge.Stop();
            System.Threading.Thread.Sleep(100);
            UnityTcpBridge.Start();
            TcpLog.Info("TCP Bridge restarted via menu");
        }

        [MenuItem("Tools/Unity TCP Bridge/Show Server Status")]
        public static void ShowServerStatus()
        {
            bool isRunning = UnityTcpBridge.IsRunning;
            int port = UnityTcpBridge.GetCurrentPort();

            string message = isRunning 
                ? $"TCP Bridge is RUNNING on port {port}\nConnect to: localhost:{port}"
                : "TCP Bridge is STOPPED";

            EditorUtility.DisplayDialog("TCP Bridge Status", message, "OK");
        }

        [MenuItem("Tools/Unity TCP Bridge/Discover New Port")]
        public static void DiscoverNewPort()
        {
            try
            {
                int newPort = PortManager.DiscoverNewPort();
                string message = $"New port discovered: {newPort}\n\nRestart the TCP Bridge to use the new port.";
                
                bool restart = EditorUtility.DisplayDialog(
                    "New Port Discovered", 
                    message, 
                    "Restart Now", 
                    "Later"
                );

                if (restart)
                {
                    RestartServer();
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Port Discovery Failed", ex.Message, "OK");
            }
        }

        [MenuItem("Tools/Unity TCP Bridge/Enable Debug Logging")]
        public static void EnableDebugLogging()
        {
            EditorPrefs.SetBool("UnityTcp.DebugLogs", true);
            TcpLog.Info("Debug logging enabled");
        }

        [MenuItem("Tools/Unity TCP Bridge/Enable Debug Logging", true)]
        public static bool EnableDebugLoggingValidate()
        {
            return !EditorPrefs.GetBool("UnityTcp.DebugLogs", false);
        }

        [MenuItem("Tools/Unity TCP Bridge/Disable Debug Logging")]
        public static void DisableDebugLogging()
        {
            EditorPrefs.SetBool("UnityTcp.DebugLogs", false);
            TcpLog.Info("Debug logging disabled");
        }

        [MenuItem("Tools/Unity TCP Bridge/Disable Debug Logging", true)]
        public static bool DisableDebugLoggingValidate()
        {
            return EditorPrefs.GetBool("UnityTcp.DebugLogs", false);
        }
    }
}
