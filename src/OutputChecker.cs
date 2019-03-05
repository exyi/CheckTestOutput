using System;
using System.IO;
using System.Linq;
using Medallion.Shell;

namespace CheckTestOutput
{
    public class OutputChecker
    {
        /// <param name="directory">Directory with the reference outputs, relative to the <see cref="calledFrom"/> parameter.</param>
        public OutputChecker(
            string directory,
            [System.Runtime.CompilerServices.CallerFilePath] string calledFrom = null)
        {
            if (Path.IsPathRooted(directory))
            {
                this.CheckDirectory = directory;
            }
            else
            {
                if (calledFrom == null)
                    throw new ArgumentException($"Either the directory must be absolute path or the calledFrom parameter must be specified.");
                this.CheckDirectory = Path.Combine(Path.GetDirectoryName(calledFrom), directory);
            }

        }

        public string CheckDirectory { get; }


        private string[] RunGitCommand(params string[] args)
        {
            using(var cmd = Command.Run("git", args, o => { o.WorkingDirectory(CheckDirectory); o.Timeout(TimeSpan.FromSeconds(3)); }))
            {
                cmd.Wait();
                if (cmd.Task.Result.ExitCode != 0)
                    throw new Exception($"Git command failed: {cmd.Task.Result.StandardError}");

                return cmd.StandardOutput.GetLines().ToArray();
            }
        }

        private string GetOldContent(string file)
        {
            var lsFiles = RunGitCommand("ls-files", "-s", file);
            if (lsFiles.Length == 0) return null;

            var hash = lsFiles[0].Split(new [] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            if (String.IsNullOrEmpty(hash)) return null;

            var contents = RunGitCommand("cat-file", "blob", hash);

            return string.Join("\n", contents);
        }

        private bool IsModified(string file)
        {
            // command `git ls-files --other --modified $file` returns the file name back iff it is modified or other (untracked)
            var gitOut = RunGitCommand("ls-files", "--other", "--modified", file);
            // if it outputs back the filename, it is changed
            return !gitOut.All(string.IsNullOrEmpty);
        }

        internal void CheckOutputCore(string outputString, string checkName, string method, string fileExtension = "txt")
        {
            Directory.CreateDirectory(CheckDirectory);

            var filename = Path.Combine(CheckDirectory, (checkName == null ? method : $"{method}-{checkName}") + "." + fileExtension);

            if (GetOldContent(filename) == outputString.Replace("\n", ""))
                return;

            using (var t = File.CreateText(filename))
            {
                t.WriteLine(outputString);
            }

            if (IsModified(filename))
            {
                var diff = RunGitCommand("diff", filename);
                if (diff.All(string.IsNullOrEmpty))
                    throw new Exception($"Check {Path.GetFileName(filename)} - the file is probably untracked in git");
                throw new Exception($"Check {Path.GetFileName(filename)} - the expected output is different:\n{string.Join("\n", diff)}");
            }
        }
    }
}
