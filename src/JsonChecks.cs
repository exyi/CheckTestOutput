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
            this CheckTestOutput t,
            object output,
            string checkName = null,
            string fileExtension = "json",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            t.CheckOutputCore(
                file => {
                    var serializer = new JsonSerializer();
                    using (var w = new JsonTextWriter(new StreamWriter(file)))
                    {
                        w.Indentation = 1;
                        w.IndentChar = '\t';
                        w.Formatting = Formatting.Indented;
                        serializer.Serialize(w, output);
                        w.WriteWhitespace(Environment.NewLine);
                    }
                },
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }
    }
}
