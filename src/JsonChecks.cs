using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CheckTestOutput
{
    public static class JsonChecks
    {
        public static void CheckJsonObject(
            this OutputChecker t,
            object output,
            string checkName = null,
            string fileExtension = "json",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            var strOutput =
                JsonSerializer.Serialize(output, new JsonSerializerOptions() { WriteIndented = true });


            // indent using tabs for back compatibility
            strOutput = Regex.Replace(strOutput, "^(  )+", m => new string('\t', m.Value.Length / 2), RegexOptions.Multiline);
            t.CheckOutputCore(
                strOutput,
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }
    }
}
