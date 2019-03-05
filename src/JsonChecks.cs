using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

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
            var serializer = new JsonSerializer();
            var outputString = new System.Text.StringBuilder();
            using (var w = new JsonTextWriter(new StringWriter(outputString)))
            {
                w.Indentation = 1;
                w.IndentChar = '\t';
                w.Formatting = Formatting.Indented;
                serializer.Serialize(w, output);
            }
            t.CheckOutputCore(
                outputString.ToString(),
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }
    }
}
