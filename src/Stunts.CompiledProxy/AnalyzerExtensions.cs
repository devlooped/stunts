using System.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Stunts
{
    static class AnalyzerExtensions
    {
        public static void CheckDebugger(this AnalyzerConfigOptionsProvider analyzerOptions, string debugableName)
        {
            var options = analyzerOptions.GlobalOptions;
            if (BuildProperties.DebugSourceGenerators(options) || BuildProperties.Debug(options, debugableName))
                Debugger.Launch();
        }
    }
}
