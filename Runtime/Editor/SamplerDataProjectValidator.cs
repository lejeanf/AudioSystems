#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace jeanf.audiosystems
{
    /// <summary>
    /// Sweeps every SamplerData asset in the project through SamplerDataValidation without
    /// entering playmode. Issues are logged with the asset as context so they can be pinged
    /// straight from the console.
    /// </summary>
    public static class SamplerDataProjectValidator
    {
        [MenuItem("Tools/Audio Systems/Validate SamplerData Assets")]
        public static void ValidateAllFromMenu()
        {
            int total = 0;
            int flagged = ValidateProject(true, ref total);

            EditorUtility.DisplayDialog("SamplerData validation",
                flagged == 0
                    ? $"{total} SamplerData asset(s) checked - no problems found."
                    : $"{total} SamplerData asset(s) checked - {flagged} with problems. See the console for details.",
                "OK");
        }

        /// <summary>Returns the number of assets with at least one issue; logs them when asked.</summary>
        public static int ValidateProject(bool logIssues, ref int totalChecked)
        {
            int flagged = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:SamplerData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<SamplerData>(path);
                if (data == null) continue;

                totalChecked++;
                var issues = SamplerDataValidation.Validate(data);
                if (issues.Count == 0) continue;

                flagged++;
                if (!logIssues) continue;

                foreach (var issue in issues)
                {
                    string message = $"[SamplerData] {path}: {issue.Message}";
                    if (issue.IsError) Debug.LogError(message, data);
                    else Debug.LogWarning(message, data);
                }
            }

            return flagged;
        }
    }
}
#endif
