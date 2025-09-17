using UnityEditor;

namespace UnityTcp.Editor.Helpers
{
    /// <summary>
    /// Helper class for Unity compilation status checking
    /// </summary>
    public static class CompilationHelper
    {
        /// <summary>
        /// Helper to check compilation status across Unity versions
        /// </summary>
        public static bool IsCompiling()
        {
            if (EditorApplication.isCompiling)
            {
                return true;
            }
            try
            {
                System.Type pipeline = System.Type.GetType("UnityEditor.Compilation.CompilationPipeline, UnityEditor");
                var prop = pipeline?.GetProperty("isCompiling", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    return (bool)prop.GetValue(null);
                }
            }
            catch { }
            return false;
        }
    }
}
