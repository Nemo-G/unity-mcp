using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityTcp.Editor.Helpers; // For Response class

namespace UnityTcp.Editor.Tools
{
    /// <summary>
    /// Handles screenshot capture operations within Unity Editor.
    /// </summary>
    public static class ManageScreenshot
    {
        // --- Main Handler ---

        // Define the list of valid actions
        private static readonly List<string> ValidActions = new List<string>
        {
            "capture",
            "capture_game_view",
            "capture_main_camera",
            "capture_specific_camera",
            "capture_scene_camera"
        };

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString().ToLower();
            if (string.IsNullOrEmpty(action))
            {
                return Response.Error("Action parameter is required.");
            }

            // Check if the action is valid before switching
            if (!ValidActions.Contains(action))
            {
                string validActionsList = string.Join(", ", ValidActions);
                return Response.Error(
                    $"Unknown action: '{action}'. Valid actions are: {validActionsList}"
                );
            }

            try
            {
                switch (action)
                {
                    case "capture":
                    case "capture_game_view":
                        return CaptureGameView(@params);
                    case "capture_main_camera":
                        return CaptureMainCamera(@params);
                    case "capture_specific_camera":
                        return CaptureSpecificCamera(@params);
                    case "capture_scene_camera":
                        return CaptureSceneCamera(@params);

                    default:
                        string validActionsListDefault = string.Join(", ", ValidActions);
                        return Response.Error(
                            $"Unknown action: '{action}'. Valid actions are: {validActionsListDefault}"
                        );
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageScreenshot] Action '{action}' failed: {e}");
                return Response.Error(
                    $"Internal error processing action '{action}': {e.Message}"
                );
            }
        }

        // --- Action Implementations ---

        /// <summary>
        /// Menu item to capture screenshot from game view
        /// </summary>
        [MenuItem("Tools/Capture Screenshot")]
        public static void CaptureScreenshotMenuItem()
        {
            CaptureGameViewAndSave();
        }

        private static object CaptureGameView(JObject @params)
        {
            string customPath = @params["path"]?.ToString();
            string customFilename = @params["filename"]?.ToString();
            int? width = @params["width"]?.ToObject<int?>();
            int? height = @params["height"]?.ToObject<int?>();

            try
            {
                string savedPath = CaptureGameViewAndSave(customPath, customFilename, width, height);
                if (savedPath != null)
                {
                    return Response.Success(
                        "Screenshot captured successfully from Game view.", 
                        new { path = savedPath, camera = "Game View" }
                    );
                }
                else
                {
                    return Response.Error("Failed to capture screenshot from Game view.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to capture screenshot from Game view: {e.Message}");
            }
        }

        private static object CaptureMainCamera(JObject @params)
        {
            string customPath = @params["path"]?.ToString();
            string customFilename = @params["filename"]?.ToString();
            int? width = @params["width"]?.ToObject<int?>();
            int? height = @params["height"]?.ToObject<int?>();

            try
            {
                string savedPath = CaptureAndSave(customPath, customFilename, width, height);
                if (savedPath != null)
                {
                    return Response.Success(
                        "Screenshot captured successfully from main camera.", 
                        new { path = savedPath, camera = "Main Camera" }
                    );
                }
                else
                {
                    return Response.Error("Failed to capture screenshot from main camera.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to capture screenshot: {e.Message}");
            }
        }

        private static object CaptureSpecificCamera(JObject @params)
        {
            string cameraName = @params["cameraName"]?.ToString();
            string customPath = @params["path"]?.ToString();
            string customFilename = @params["filename"]?.ToString();
            int? width = @params["width"]?.ToObject<int?>();
            int? height = @params["height"]?.ToObject<int?>();

            if (string.IsNullOrEmpty(cameraName))
            {
                return Response.Error("'cameraName' parameter is required for capture_specific_camera action.");
            }

            try
            {
                Camera targetCamera = GameObject.Find(cameraName)?.GetComponent<Camera>();
                if (targetCamera == null)
                {
                    // Try finding by name in all cameras
                    Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                    foreach (Camera cam in cameras)
                    {
                        if (cam.name.Equals(cameraName, StringComparison.OrdinalIgnoreCase))
                        {
                            targetCamera = cam;
                            break;
                        }
                    }
                }

                if (targetCamera == null)
                {
                    return Response.Error($"Camera '{cameraName}' not found in the scene.");
                }

                string savedPath = CaptureAndSave(customPath, customFilename, width, height, targetCamera);
                if (savedPath != null)
                {
                    return Response.Success(
                        $"Screenshot captured successfully from camera '{cameraName}'.", 
                        new { path = savedPath, camera = cameraName }
                    );
                }
                else
                {
                    return Response.Error($"Failed to capture screenshot from camera '{cameraName}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to capture screenshot from camera '{cameraName}': {e.Message}");
            }
        }

        private static object CaptureSceneCamera(JObject @params)
        {
            string customPath = @params["path"]?.ToString();
            string customFilename = @params["filename"]?.ToString();
            int? width = @params["width"]?.ToObject<int?>();
            int? height = @params["height"]?.ToObject<int?>();

            try
            {
                // Get the active scene view camera
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    return Response.Error("No active Scene view found. Please ensure a Scene view is open.");
                }

                Camera sceneCamera = sceneView.camera;
                if (sceneCamera == null)
                {
                    return Response.Error("Scene view camera not found.");
                }

                string savedPath = CaptureAndSave(customPath, customFilename, width, height, sceneCamera, "SceneCamera");
                if (savedPath != null)
                {
                    return Response.Success(
                        "Screenshot captured successfully from Scene camera.", 
                        new { path = savedPath, camera = "Scene Camera" }
                    );
                }
                else
                {
                    return Response.Error("Failed to capture screenshot from Scene camera.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to capture screenshot from Scene camera: {e.Message}");
            }
        }

        /// <summary>
        /// Function to capture Game view screenshot and save to file
        /// </summary>
        public static string CaptureGameViewAndSave(string customPath = null, string customFilename = null, int? width = null, int? height = null)
        {
            try
            {
                // Generate filename with GameView prefix
                string filename;
                if (!string.IsNullOrEmpty(customFilename))
                {
                    filename = customFilename.EndsWith(".png") ? customFilename : customFilename + ".png";
                }
                else
                {
                    filename = $"GameView-{System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.png";
                }

                // Determine save path
                string savePath;
                if (!string.IsNullOrEmpty(customPath))
                {
                    // Ensure directory exists
                    Directory.CreateDirectory(customPath);
                    savePath = Path.Combine(customPath, filename);
                }
                else
                {
                    // Use project path with Screenshots folder
                    string projectPath = Path.GetDirectoryName(Application.dataPath); // Get project root (parent of Assets)
                    string screenshotsFolder = Path.Combine(projectPath, "Screenshots");
                    
                    // Create Screenshots folder if it doesn't exist
                    if (!Directory.Exists(screenshotsFolder))
                    {
                        Directory.CreateDirectory(screenshotsFolder);
                        Debug.Log($"Created Screenshots folder at: {screenshotsFolder}");
                    }
                    
                    savePath = Path.Combine(screenshotsFolder, filename);
                }

                // Try different capture methods based on mode and availability
                Texture2D screenshot = null;
                
                if (Application.isPlaying)
                {
                    // In play mode: Try main camera first, then GameView reflection
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        screenshot = CaptureScreenByRenderTexture(mainCamera, width, height);
                    }
                    
                    if (screenshot == null)
                    {
                        // Fallback to GameView reflection approach
                        screenshot = CaptureGameViewInEditMode(width, height);
                    }
                }
                else
                {
                    // In edit mode: Use GameView reflection approach first, then main camera fallback
                    screenshot = CaptureGameViewInEditMode(width, height);
                    
                    if (screenshot == null)
                    {
                        // Fallback to main camera if available
                        Camera mainCamera = Camera.main;
                        if (mainCamera != null)
                        {
                            screenshot = CaptureScreenByRenderTexture(mainCamera, width, height);
                        }
                    }
                }
                
                if (screenshot == null)
                {
                    Debug.LogError("Failed to capture screenshot using all available methods!");
                    return null;
                }

                // Encode to PNG and save
                byte[] bytes = screenshot.EncodeToPNG();
                UnityEngine.Object.Destroy(screenshot); // Free GPU memory
                
                File.WriteAllBytes(savePath, bytes);
                Debug.Log("Game view screenshot saved to: " + savePath);
                return savePath;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to capture game view screenshot: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Capture screenshot from camera using RenderTexture approach
        /// </summary>
        private static Texture2D CaptureScreenByRenderTexture(Camera camera, int? width = null, int? height = null)
        {
            if (camera == null)
            {
                Debug.LogError("Camera is null!");
                return null;
            }

            try
            {
                // Use custom dimensions or default to reasonable size
                int captureWidth = width ?? 1920;
                int captureHeight = height ?? 1080;
                
                Rect rect = new Rect(0, 0, captureWidth, captureHeight);
                
                // Create a RenderTexture object  
                RenderTexture rt = new RenderTexture((int)rect.width, (int)rect.height, 24);
                
                // Store original target texture to restore later
                RenderTexture originalTarget = camera.targetTexture;
                
                // Temporarily set the camera's targetTexture to rt, and manually render the camera  
                camera.targetTexture = rt;
                camera.Render();
                
                // Activate this rt, and read pixels from it.  
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = rt;
                
                Texture2D screenShot = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
                // Note: At this time, it reads pixels from RenderTexture.active 
                screenShot.ReadPixels(rect, 0, 0);
                screenShot.Apply();
                
                // Reset related parameters so the camera continues to display on screen  
                camera.targetTexture = originalTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(rt);
                
                return screenShot;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to capture screenshot using RenderTexture: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Capture Game view in Edit mode using GameView's internal render texture via reflection
        /// </summary>
        private static Texture2D CaptureGameViewInEditMode(int? width = null, int? height = null)
        {
            try
            {
                // Get active GameView
                var gameView = GetActiveGameView();
                if (gameView == null)
                {
                    Debug.LogError("Game View not found! Falling back to camera rendering.");
                    return CaptureWithCameraRenderingFallback(width, height);
                }

                // Get GameView's internal render texture via reflection
                var textureProperty = gameView.GetType().GetProperty("targetTexture",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (textureProperty == null)
                {
                    Debug.LogError("Failed to access GameView's targetTexture. Falling back to camera rendering.");
                    return CaptureWithCameraRenderingFallback(width, height);
                }

                RenderTexture activeRT = RenderTexture.active;
                RenderTexture gameViewRT = (RenderTexture)textureProperty.GetValue(gameView, null);

                if (gameViewRT == null)
                {
                    Debug.LogError("GameView render texture is null. Falling back to camera rendering.");
                    return CaptureWithCameraRenderingFallback(width, height);
                }

                // Determine final dimensions
                int finalWidth = width ?? gameViewRT.width;
                int finalHeight = height ?? gameViewRT.height;

                // Create temporary RenderTexture to read pixels
                RenderTexture tempRT;
                
                // If custom dimensions are specified and different from game view, resize
                if ((width.HasValue || height.HasValue) && (finalWidth != gameViewRT.width || finalHeight != gameViewRT.height))
                {
                    tempRT = RenderTexture.GetTemporary(finalWidth, finalHeight, 0, gameViewRT.format);
                    // Scale the game view texture to the desired size
                    Graphics.Blit(gameViewRT, tempRT);
                }
                else
                {
                    // Use game view dimensions
                    tempRT = RenderTexture.GetTemporary(gameViewRT.width, gameViewRT.height, 0, gameViewRT.format);
                    Graphics.Blit(gameViewRT, tempRT);
                }

                RenderTexture.active = tempRT;
                Texture2D screenshot = new Texture2D(tempRT.width, tempRT.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
                screenshot.Apply();

                RenderTexture.active = activeRT;
                RenderTexture.ReleaseTemporary(tempRT);

                return screenshot;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to capture game view using reflection: {e.Message}. Falling back to camera rendering.");
                return CaptureWithCameraRenderingFallback(width, height);
            }
        }

        /// <summary>
        /// Get the active Game View window using reflection
        /// </summary>
        private static EditorWindow GetActiveGameView()
        {
            try
            {
                System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null)
                    return null;

                // Try to get the focused game view first
                EditorWindow focusedWindow = EditorWindow.focusedWindow;
                if (focusedWindow != null && focusedWindow.GetType() == gameViewType)
                    return focusedWindow;

                // If no focused game view, get any game view that exists
                EditorWindow[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType) as EditorWindow[];
                if (gameViews != null && gameViews.Length > 0)
                    return gameViews[0];

                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not get Game View: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fallback method using camera rendering when GameView access fails
        /// </summary>
        private static Texture2D CaptureWithCameraRenderingFallback(int? width = null, int? height = null)
        {
            try
            {
                int captureWidth = width ?? Screen.width;
                int captureHeight = height ?? Screen.height;

                // Create render texture
                RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24);
                
                // Get all cameras and render them to the render texture
                Camera[] cameras = Camera.allCameras;
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = rt;
                
                // Clear the render texture
                GL.Clear(true, true, Color.black);
                
                // Render all active cameras to the render texture in order
                foreach (Camera cam in cameras)
                {
                    if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                    {
                        RenderTexture prevTarget = cam.targetTexture;
                        cam.targetTexture = rt;
                        cam.Render();
                        cam.targetTexture = prevTarget;
                    }
                }

                // Read pixels from render texture
                Texture2D screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                screenshot.Apply();
                
                // Cleanup
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(rt);
                
                return screenshot;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Camera rendering fallback failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper method to resize a texture
        /// </summary>
        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            
            Texture2D result = new Texture2D(targetWidth, targetHeight);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }

        /// <summary>
        /// Function to capture screenshot and save to file
        /// </summary>
        public static string CaptureAndSave(string customPath = null, string customFilename = null, int? width = null, int? height = null, Camera specificCamera = null, string cameraPrefix = null)
        {
            // Get the camera to use
            Camera cam = specificCamera ?? Camera.main;
            if (cam == null)
            {
                Debug.LogError("No main camera found!");
                return null;
            }

            // Use the new RenderTexture approach
            Texture2D screenshot = CaptureScreenByRenderTexture(cam, width, height);
            if (screenshot == null)
            {
                Debug.LogError("Failed to capture screenshot using RenderTexture!");
                return null;
            }

            // Encode to PNG
            byte[] bytes = screenshot.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(screenshot);

            // Generate filename with camera prefix
            string filename;
            if (!string.IsNullOrEmpty(customFilename))
            {
                filename = customFilename.EndsWith(".png") ? customFilename : customFilename + ".png";
            }
            else
            {
                string prefix = cameraPrefix ?? (specificCamera != null ? specificCamera.name : "MainCamera");
                filename = $"{prefix}-{System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.png";
            }

            // Determine save path
            string savePath;
            if (!string.IsNullOrEmpty(customPath))
            {
                // Ensure directory exists
                Directory.CreateDirectory(customPath);
                savePath = Path.Combine(customPath, filename);
            }
            else
            {
                // Use project path with Screenshots folder
                string projectPath = Path.GetDirectoryName(Application.dataPath); // Get project root (parent of Assets)
                string screenshotsFolder = Path.Combine(projectPath, "Screenshots");
                
                // Create Screenshots folder if it doesn't exist
                if (!Directory.Exists(screenshotsFolder))
                {
                    Directory.CreateDirectory(screenshotsFolder);
                    Debug.Log($"Created Screenshots folder at: {screenshotsFolder}");
                }
                
                savePath = Path.Combine(screenshotsFolder, filename);
            }

            // Save to file
            File.WriteAllBytes(savePath, bytes);

            Debug.Log("Screenshot saved to: " + savePath);
            return savePath;
        }
    }
}
