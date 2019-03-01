using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CheckTestOutput
{
    public static class BasicChecks
    {
        public static void CheckString(
            this OutputChecker t,
            string output,
            string checkName = null,
            string fileExtension = "txt",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            t.CheckOutputCore(
                file => {
                    using (var w = new StreamWriter(file))
                    {
                        w.WriteLine(output);
                    }
                },
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }

        public static void CheckLines(
            this OutputChecker t,
            IEnumerable<string> output,
            string checkName = null,
            string fileExtension = "txt",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            t.CheckOutputCore(
                file => {
                    using (var w = new StreamWriter(file))
                    {
                        foreach (var o in output)
                            w.WriteLine(o);
                    }
                },
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }
    }
}
