using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CheckTestOutput
{
    public class OutputChecker
    {
        /// <summary> Checks that the provided test output matched a file from the <paramref name="directory"/>. Filename is a {callingClass}.{callingMethod}.fileExtension </summary>
        /// <param name="directory">Directory with the reference outputs, relative to the <see cref="calledFrom"/> parameter.</param>
        /// <param name="sanitizeGuids">Replace all strings that look like Guid by a sequential id. The sanitization preserves equality.</param>
        /// <param name="sanitizeQuotedGuids">Replace all strings that look like Guid and are in quotes by a sequential id. The sanitization preserves equality.</param>
        /// <param name="nonDeterminismSanitizers">List of regular expressions that are replaced by a sequential id for the purpose of the check.</param>
        public OutputChecker(
            string directory,
            bool sanitizeGuids = false,
            bool sanitizeQuotedGuids = false,
            IEnumerable<string> nonDeterminismSanitizers = null,
            [System.Runtime.CompilerServices.CallerFilePath] string calledFrom = null)
        {
            const string guidRegex = "([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12})";

            var s = nonDeterminismSanitizers?.ToList() ?? new List<string>();
            if (sanitizeGuids)
                s.Add(guidRegex);
            else if (sanitizeQuotedGuids)
                s.Add('"' + guidRegex + '"');
            _nonDeterminismSanitizers = s.ToArray();

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

            DoesGitWork = new Lazy<bool>(() => {
                try
                {
                    var path = RunGitCommand("rev-parse", "--show-toplevel");
                    return true;
                }
                catch (Win32Exception)
                {
                    Console.WriteLine("CheckTestOutput warning: git command not found. Falling back to simple file-based checking. Make sure that git is installed and in the PATH.");
                    return false;
                }
                catch (Exception e) when (e.Message.StartsWith("Git command failed: fatal: not a git repository"))
                {
                    Console.WriteLine("CheckTestOutput warning: project is not in git. Falling back to simple file-based checking");
                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine("CheckTestOutput warning: an error occurred while calling git. Falling back to simple file-based checking.");
                    Console.WriteLine("Error: " + e);
                    return false;
                }
            });

        }

        public string CheckDirectory { get; }
        private string[] _nonDeterminismSanitizers;
        /// <summary> List of regular expressions that are replaced by a sequential id for the purpose of the check. The sanitization preserves equality over the checked string (equal strings are replaced by equal id, different strings by different ids) </summary>
        /// <remarks>
        /// As a main point, this is useful for replacing Guids in the checked string by a sequential id.
        /// </remarks>
        public IEnumerable<string> NonDeterminismSanitizers => _nonDeterminismSanitizers;

        private readonly Lazy<bool> DoesGitWork;

        private string[] RunGitCommand(params string[] args)
        {
            // run `git ...args` in CheckDirectory working directory with 3 second timeout
            var procInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = CheckDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            foreach (var a in args)
                procInfo.ArgumentList.Add(a);

            var proc = Process.Start(procInfo);
            if (!proc.WaitForExit(3000))
            {
                proc.Kill();
                throw new Exception("Git command timed out");
            }

            if (proc.ExitCode != 0)
                throw new Exception("Git command failed: " + proc.StandardError.ReadToEnd());
            
            return proc.StandardOutput.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }

        static string[] ReadAllLines(StreamReader reader)
        {
            var lines = new List<string>();
            while (!reader.EndOfStream && reader.ReadLine() is {} line)
                lines.Add(line);
            return lines.ToArray();
        }

        private string GetOldContent(string file)
        {
            if (DoesGitWork.Value)
            {
                var lsFiles = RunGitCommand("ls-files", "-s", file);
                if (lsFiles.Length == 0) return null;

                var hash = lsFiles[0].Split(new [] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
                if (String.IsNullOrEmpty(hash)) return null;

                var contents = RunGitCommand("cat-file", "blob", hash);

                return string.Join("\n", contents);
            }
            else
            {
                return string.Join("\n", File.ReadLines(file));
            }
        }

        private bool IsModified(string file)
        {
            // command `git ls-files --other --modified $file` returns the file name back iff it is modified or other (untracked)
            var gitOut = RunGitCommand("ls-files", "--other", "--modified", "--deleted", file);
            // if it outputs back the filename, it is changed
            return !gitOut.All(string.IsNullOrEmpty);
        }

        /// <summary> Applies the <see cref="NonDeterminismSanitizers" /> to the string. </summary>
        public string SanitizeString(string outputString)
        {
            var x = new Dictionary<string, string>();

            foreach (var p in this.NonDeterminismSanitizers)
            {
                outputString = Regex.Replace(outputString, p, match => {
                    if (!x.ContainsKey(match.Value))
                    {
                        x[match.Value] = $"aaaaaaaa-bbbb-cccc-dddd-{(x.Count + 1):D12}";
                    }
                    return x[match.Value];
                });
            }
            return outputString;
        }

        internal void CheckOutputCore(string outputString, string checkName, string method, string fileExtension = "txt")
        {
            outputString = SanitizeString(outputString);

            Directory.CreateDirectory(CheckDirectory);

            var filename = Path.Combine(CheckDirectory, (checkName == null ? method : $"{method}-{checkName}") + "." + fileExtension);


            if (GetOldContent(filename) == outputString.Replace("\r", ""))
            {
                // fine! Just check that the file is not changed - if it is changed or deleted, we rewrite
                if (IsModified(filename))
                {
                    using (var t = File.CreateText(filename))
                    {
                        t.WriteLine(outputString);
                    }
                }
                return;
            }

            if (DoesGitWork.Value)
            {
                using (var t = File.CreateText(filename))
                {
                    t.WriteLine(outputString);
                }

                if (IsModified(filename))
                {
                    var diff = RunGitCommand("diff", filename);
                    if (diff.All(string.IsNullOrEmpty))
                        throw new Exception($"{Path.GetFileName(filename)} is not explicitly accepted - the file is untracked in git. To let this test pass, view the file and stage it. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md#untracked-file\n");
                    throw new Exception(
                        $"{Path.GetFileName(filename)} has changed, the actual output differs from the previous accepted output:\n\n" +
                        string.Join("\n", diff) + "\n\n" +
                        "If this change OK? To let the test pass, stage the file in git. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md#changed-file\n"

                    );
                }
            }
            else
            {
                throw new Exception($"{Path.GetFileName(filename)}has changed, the previous accepted output differs from the actual output:\n\n{outputString}\n\nNote that CheckTestOutput could not use git on your system, so the \"UX\" is limited.");
            }
        }
    }
}
