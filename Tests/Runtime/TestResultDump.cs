using System.IO;
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(jeanf.audiosystems.tests.TestResultDump))]

namespace jeanf.audiosystems.tests
{
    /// <summary>
    /// Writes the NUnit run result to Temp/ so a CLI driver can poll it. An assembly-level
    /// TestRunCallback is invoked inside the run itself, so unlike a TestRunnerApi callbacks
    /// object it survives the play-mode domain reloads.
    /// </summary>
    public class TestResultDump : ITestRunCallback
    {
        public const string ResultPath = "Temp/jeanf.audiosystems.tests.result.txt";

        public void RunStarted(ITest testsToRun)
        {
            try { File.WriteAllText(ResultPath, "RUNNING"); } catch { /* result file is best-effort */ }
        }

        public void RunFinished(ITestResult result)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"RESULT {result.ResultState} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} duration={result.Duration:F1}s");
                Append(result, sb);
                File.WriteAllText(ResultPath, sb.ToString());
            }
            catch { /* result file is best-effort */ }
        }

        private static void Append(ITestResult r, System.Text.StringBuilder sb)
        {
            if (!r.HasChildren)
            {
                var msg = string.IsNullOrEmpty(r.Message) ? "" : $" :: {r.Message.Replace('\n', ' ').Trim()}";
                sb.AppendLine($"{r.ResultState}: {r.FullName}{msg}");
                return;
            }
            foreach (var c in r.Children) Append(c, sb);
        }

        public void TestStarted(ITest test) { }
        public void TestFinished(ITestResult result) { }
    }
}
