using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UnityTcp.Editor.Helpers
{
    /// <summary>
    /// Helper class for JSON command processing utilities
    /// </summary>
    public static class JsonCommandHelper
    {
        /// <summary>
        /// Helper method to check if a string is valid JSON
        /// </summary>
        public static bool IsValidJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (
                (text.StartsWith("{") && text.EndsWith("}"))
                || // Object
                (text.StartsWith("[") && text.EndsWith("]"))
            ) // Array
            {
                try
                {
                    JToken.Parse(text);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Helper method to get a summary of parameters for error reporting
        /// </summary>
        public static string GetParamsSummary(JObject @params)
        {
            try
            {
                return @params == null || !@params.HasValues
                    ? "No parameters"
                    : string.Join(
                        ", ",
                        @params
                            .Properties()
                            .Select(static p =>
                                $"{p.Name}: {p.Value?.ToString()?[..Math.Min(20, p.Value?.ToString()?.Length ?? 0)]}"
                            )
                    );
            }
            catch
            {
                return "Could not summarize parameters";
            }
        }
    }
}
