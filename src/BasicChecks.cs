using System.Collections.Generic;
using System.IO;

namespace CheckTestOutput
{
    public static class BasicChecks
    {
        /// <summary> Verifies that the provided <paramref name="output" /> equals to the `outputDirectory/TestClass.TestMethod.txt` file. </summary>
        /// <param name="checkName"> If not null, checkName will be appended to the calling <paramref name="memberName" />. Intended to be used when having multiple checks in one method. </param>
        public static void CheckString(
            this OutputChecker t,
            string output,
            string checkName = null,
            string fileExtension = "txt",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null,
            bool allowAlternatives = false)
        {
            t.CheckOutputCore(
                output,
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension,
                allowAlternatives
            );
        }

        /// <summary> Verifies that the provided <paramref name="output" /> equals to the `outputDirectory/TestClass.TestMethod.bin` file. </summary>
        /// <param name="checkName"> If not null, checkName will be appended to the calling <paramref name="memberName" />. Intended to be used when having multiple checks in one method. </param>
        public static void CheckBinary(
            this OutputChecker t,
            byte[] output,
            string checkName = null,
            string fileExtension = "bin",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            t.CheckOutputBinaryCore(
                output,
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }

        /// <summary> Verifies that the provided <paramref name="output" /> equals to the `outputDirectory/TestClass.TestMethod.txt` file. File is compared line-by-line. </summary>
        /// <param name="checkName"> If not null, checkName will be appended to the calling <paramref name="memberName" />. Intended to be used when having multiple checks in one method. </param>
        public static void CheckLines(
            this OutputChecker t,
            IEnumerable<string> output,
            string checkName = null,
            string fileExtension = "txt",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            t.CheckOutputCore(
                string.Join("\n", output),
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }
    }
}
