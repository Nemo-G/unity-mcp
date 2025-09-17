using UnityEditor;
using UnityEngine;

namespace UnityTcp.Editor.Windows
{
    /// <summary>
    /// Simple status window that can be docked for quick TCP bridge status viewing
    /// </summary>
    public class TcpBridgeStatusWindow : EditorWindow
    {
        private double lastUpdate;
        private const double UpdateInterval = 1.0; // Update every second
        private GUIStyle statusStyle;

        [MenuItem("Tools/Unity TCP Bridge/Status Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<TcpBridgeStatusWindow>("TCP Status");
            window.minSize = new Vector2(200, 100);
            window.Show();
        }

        private void OnGUI()
        {
            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            // Update periodically
            if (EditorApplication.timeSinceStartup - lastUpdate > UpdateInterval)
            {
                lastUpdate = EditorApplication.timeSinceStartup;
                Repaint();
            }

            GUILayout.Space(10);

            bool isRunning = UnityTcpBridge.IsRunning;
            int currentPort = UnityTcpBridge.GetCurrentPort();

            // Status indicator
            Color statusColor = isRunning ? Color.green : Color.red;
            string statusText = isRunning ? "RUNNING" : "STOPPED";

            GUI.color = statusColor;
            EditorGUILayout.LabelField($"● {statusText}", statusStyle);
            GUI.color = Color.white;

            if (isRunning)
            {
                EditorGUILayout.LabelField($"Port: {currentPort}", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField($"localhost:{currentPort}", EditorStyles.centeredGreyMiniLabel);
            }

            GUILayout.Space(10);

            // Quick action buttons
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !isRunning;
            if (GUILayout.Button("Start"))
            {
                UnityTcpBridge.Start();
            }

            GUI.enabled = isRunning;
            if (GUILayout.Button("Stop"))
            {
                UnityTcpBridge.Stop();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (GUILayout.Button("Open Control Window"))
            {
                TcpBridgeControlWindow.ShowWindow();
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
