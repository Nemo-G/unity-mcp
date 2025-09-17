using UnityEditor;
using UnityEngine;
using UnityTcp.Editor.Helpers;

namespace UnityTcp.Editor.Windows
{
    public class TcpBridgeControlWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool debugLogsEnabled;
        private string statusMessage = "";
        private GUIStyle headerStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private double lastStatusUpdate;
        private const double StatusUpdateInterval = 0.5; // Update every 500ms

        [MenuItem("Tools/Unity TCP Bridge/Control Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<TcpBridgeControlWindow>("TCP Bridge Control");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            debugLogsEnabled = EditorPrefs.GetBool("UnityTcp.DebugLogs", false);
            UpdateStatus();
        }

        private void OnGUI()
        {
            InitializeStyles();

            // Update status periodically
            if (EditorApplication.timeSinceStartup - lastStatusUpdate > StatusUpdateInterval)
            {
                UpdateStatus();
                lastStatusUpdate = EditorApplication.timeSinceStartup;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            DrawServerStatus();
            DrawServerControls();
            DrawConfiguration();
            DrawPortManagement();
            DrawDebugSection();

            EditorGUILayout.EndScrollView();
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    wordWrap = true
                };
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    padding = new RectOffset(10, 10, 8, 8)
                };
            }
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Unity TCP Bridge Control", headerStyle);
            GUILayout.Space(10);

            // Version info
            EditorGUILayout.LabelField($"Version: 1.0.0", EditorStyles.miniLabel);
            GUILayout.Space(5);
        }

        private void DrawServerStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);

            bool isRunning = UnityTcpBridge.IsRunning;
            int currentPort = UnityTcpBridge.GetCurrentPort();

            // Status indicator
            Color statusColor = isRunning ? Color.green : Color.red;
            string statusText = isRunning ? "RUNNING" : "STOPPED";

            GUI.color = statusColor;
            EditorGUILayout.LabelField($"● {statusText}", EditorStyles.boldLabel);
            GUI.color = Color.white;

            if (isRunning)
            {
                EditorGUILayout.LabelField($"Port: {currentPort}");
                EditorGUILayout.LabelField($"Address: localhost:{currentPort}");
                EditorGUILayout.LabelField($"Platform: {Application.platform}");
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Status Message:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(statusMessage, statusStyle);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawServerControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Server Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Start Button
            GUI.enabled = !UnityTcpBridge.IsRunning;
            if (GUILayout.Button("Start Server", buttonStyle))
            {
                try
                {
                    UnityTcpBridge.Start();
                    statusMessage = "Server started successfully";
                    TcpLog.Info("TCP Bridge started via Control Window");
                }
                catch (System.Exception ex)
                {
                    statusMessage = $"Failed to start server: {ex.Message}";
                    TcpLog.Error($"Failed to start TCP Bridge: {ex.Message}");
                }
            }

            // Stop Button
            GUI.enabled = UnityTcpBridge.IsRunning;
            if (GUILayout.Button("Stop Server", buttonStyle))
            {
                try
                {
                    UnityTcpBridge.Stop();
                    statusMessage = "Server stopped successfully";
                    TcpLog.Info("TCP Bridge stopped via Control Window");
                }
                catch (System.Exception ex)
                {
                    statusMessage = $"Failed to stop server: {ex.Message}";
                    TcpLog.Error($"Failed to stop TCP Bridge: {ex.Message}");
                }
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Restart Button
            if (GUILayout.Button("Restart Server", buttonStyle))
            {
                try
                {
                    UnityTcpBridge.Stop();
                    System.Threading.Thread.Sleep(100); // Brief pause
                    UnityTcpBridge.Start();
                    statusMessage = "Server restarted successfully";
                    TcpLog.Info("TCP Bridge restarted via Control Window");
                }
                catch (System.Exception ex)
                {
                    statusMessage = $"Failed to restart server: {ex.Message}";
                    TcpLog.Error($"Failed to restart TCP Bridge: {ex.Message}");
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            // Debug logs toggle
            bool newDebugLogsEnabled = EditorGUILayout.Toggle("Enable Debug Logs", debugLogsEnabled);
            if (newDebugLogsEnabled != debugLogsEnabled)
            {
                debugLogsEnabled = newDebugLogsEnabled;
                EditorPrefs.SetBool("UnityTcp.DebugLogs", debugLogsEnabled);
                statusMessage = debugLogsEnabled ? "Debug logging enabled" : "Debug logging disabled";
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawPortManagement()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Port Management", EditorStyles.boldLabel);

            int currentPort = UnityTcpBridge.GetCurrentPort();
            EditorGUILayout.LabelField($"Current Port: {currentPort}");

            var storedConfig = PortManager.GetStoredPortConfig();
            if (storedConfig != null)
            {
                EditorGUILayout.LabelField($"Stored Port: {storedConfig.unity_port}");
                EditorGUILayout.LabelField($"Project: {System.IO.Path.GetFileName(storedConfig.project_path)}");
            }

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Discover New Port", buttonStyle))
            {
                try
                {
                    int newPort = PortManager.DiscoverNewPort();
                    statusMessage = $"New port discovered: {newPort}. Restart server to use it.";
                }
                catch (System.Exception ex)
                {
                    statusMessage = $"Failed to discover new port: {ex.Message}";
                }
            }

            if (GUILayout.Button("Check Port Availability", buttonStyle))
            {
                bool isAvailable = PortManager.IsPortAvailable(currentPort);
                statusMessage = $"Port {currentPort} is {(isAvailable ? "available" : "in use")}";
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawDebugSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Debug Information", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Unity Version: {Application.unityVersion}");
            EditorGUILayout.LabelField($"Platform: {Application.platform}");
            EditorGUILayout.LabelField($"Project Path: {Application.dataPath}");

            GUILayout.Space(5);

            if (GUILayout.Button("Open Unity Console", buttonStyle))
            {
                EditorApplication.ExecuteMenuItem("Window/General/Console");
            }

            EditorGUILayout.EndVertical();
        }

        private void UpdateStatus()
        {
            // This method is called periodically to refresh status
            // The GUI will automatically update based on UnityTcpBridge.IsRunning property
            
            // Clear old status messages after some time
            if (!string.IsNullOrEmpty(statusMessage) && EditorApplication.timeSinceStartup > lastStatusUpdate + 10)
            {
                statusMessage = "";
            }
        }

        private void OnInspectorUpdate()
        {
            // Force repaint to keep UI responsive
            Repaint();
        }
    }
}
