using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

            DoesGitWork = doesGitWorkCache.GetOrAdd(directory, new Lazy<bool>(() => {
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
            }));

        }

        private static ConcurrentDictionary<string, Lazy<bool>> doesGitWorkCache = new();

        public string CheckDirectory { get; }
        private string[] _nonDeterminismSanitizers;
        /// <summary> List of regular expressions that are replaced by a sequential id for the purpose of the check. The sanitization preserves equality over the checked string (equal strings are replaced by equal id, different strings by different ids) </summary>
        /// <remarks>
        /// As a main point, this is useful for replacing Guids in the checked string by a sequential id.
        /// </remarks>
        public IEnumerable<string> NonDeterminismSanitizers => _nonDeterminismSanitizers;

        private readonly Lazy<bool> DoesGitWork;

        private Process StartGitProcess(params string[] args)
        {
#if DEBUG
            Console.WriteLine("Running git command: " + string.Join(" ", args));
#endif
            // run `git ...args` in CheckDirectory working directory with 3 second timeout
            var procInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = CheckDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
            foreach (var a in args)
                procInfo.ArgumentList.Add(a);
#else
            procInfo.Arguments = WindowsEscapeArguments(args);
#endif
            return Process.Start(procInfo);
        }


#if !(NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER)
        private static string WindowsEscapeArguments(params string[] args)
        {
            // based on the logic from http://stackoverflow.com/questions/5510343/escape-command-line-arguments-in-c-sharp.

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new InvalidOperationException("The nestandard2.0 build is only supported on Windows old .NET Framework");

            return string.Join(" ", args.Select(a => {
                a = Regex.Replace(a, @"(\\*)" + "\"", @"$1$1\" + "\"");
                return "\"" + Regex.Replace(a, @"(\\+)$", @"$1$1") + "\"";
            }));
        }
#endif


        private void HandleProcessExit(Process proc, Task outputReaderTask, params string[] args)
        {
            try
            {
                // Literally, a Raspberry PI with a shitty SD card has faster IO than Azure Windows VM
                var timeout = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 15_000 : 3_000;
                if (!proc.WaitForExit(timeout))
                {
                    proc.Kill();
                    throw new Exception($"`git {string.Join(" ", args)}` command timed out");
                }

                if (proc.ExitCode != 0)
                    throw new Exception($"`git {string.Join(" ", args)}` command failed: " + proc.StandardError.ReadToEnd());

                outputReaderTask.Wait();
            }
            finally
            {
                proc.Dispose();
            }
        }

        private string[] RunGitCommand(params string[] args)
        {
            var output = RunGitBinaryCommand(args);

            using var reader = new StreamReader(output);
            return ReadAllLines(reader);
        }

        private MemoryStream RunGitBinaryCommand(params string[] args)
        {
            var proc = StartGitProcess(args);

            MemoryStream ret = new();

            var outputReaderTask = Task.Run(() =>
            {
                proc.StandardOutput.BaseStream.CopyTo(ret);
            });

            HandleProcessExit(proc, outputReaderTask, args);

            ret.Position = 0;
            return ret;
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

        private byte[] GetOldBinaryContent(string file)
        {
            if (DoesGitWork.Value)
            {
                var lsFiles = RunGitCommand("ls-files", "-s", file);
                if (lsFiles.Length == 0)
                    return null;

                var hash = lsFiles[0].Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
                if (String.IsNullOrEmpty(hash))
                    return null;

                var data = RunGitBinaryCommand("cat-file", "blob", hash);

                return data.ToArray();
            }
            else
            {
                return File.ReadAllBytes(file);
            }
        }

        private bool IsModified(string file)
        {
            // command `git ls-files --other --modified $file` returns the file name back iff it is modified or other (untracked)
            var gitOut = RunGitCommand("ls-files", "--other", "--modified", "--deleted", file);
            // if it outputs back the filename, it is changed
            return !gitOut.All(string.IsNullOrEmpty);
        }

        private bool IsNewFile(string file)
        {
            var gitOut = RunGitCommand("ls-files", "--other", file);
            // if it outputs back the filename, it is other (untracked)
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

        internal void CheckOutputCore(string outputString, string checkName, string method, string fileExtension = "txt", bool allowAlternatives = false)
        {
            outputString = outputString.Replace("\r\n", "\n").TrimEnd('\n');
            outputString = SanitizeString(outputString);

            Directory.CreateDirectory(CheckDirectory);

            var alternativeIndex = 0;
            string filename;
            while (true)
            {
                filename = !allowAlternatives
                    ? Path.Combine(CheckDirectory, (checkName == null ? method : $"{method}-{checkName}") + "." + fileExtension)
                    : Path.Combine(CheckDirectory, (checkName == null ? method : $"{method}-{checkName}") + $"-alt{alternativeIndex:000}." + fileExtension);

                if (GetOldContent(filename)?.TrimEnd('\n') == outputString)
                {
                    // fine! Just check that the file is not changed - if it is changed or deleted, we rewrite
                    if (IsModified(filename))
                    {
                        using (var t = File.CreateText(filename))
                        {
                            t.Write(outputString);
                            t.Write("\n");
                        }
                    }

                    return;
                }

                if (allowAlternatives && File.Exists(filename))
                {
                    alternativeIndex++;
                    continue;
                }
                break;
            }

            if (DoesGitWork.Value)
            {
                using (var t = File.CreateText(filename))
                {
                    t.Write(outputString);
                    t.Write("\n");
                }

                if (IsModified(filename))
                {
                    if (IsNewFile(filename))
                    {
                        throw new Exception($"{Path.GetFileName(filename)} is not explicitly accepted - the file is untracked in git. To let this test pass, view the file and stage it. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md#untracked-file\n");
                    }


                    var diff = RunGitCommand("diff", filename);
                    if (diff.All(string.IsNullOrEmpty))
                    {
                        // I guess fine from our perspective, but it's weird...
                        Console.WriteLine($"CheckTestOutput warning: {Path.GetFileName(filename)} is modified, but the diff is empty.");
                        return;
                    }
                    throw new Exception(
                        $"{Path.GetFileName(filename)} has changed, the actual output differs from the previous accepted output:\n\n" +
                        string.Join("\n", diff) + "\n\n" +
                        "Is this change OK? To let the test pass, stage the file in git. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md#changed-file\n"

                    );
                }
            }
            else
            {
                throw new Exception($"{Path.GetFileName(filename)} has changed, the previous accepted output differs from the actual output:\n\n{outputString}\n\nNote that CheckTestOutput could not use git on your system, so the \"UX\" is limited.");
            }
        }

        internal void CheckOutputBinaryCore(byte[] outputBytes, string checkName, string method, string fileExtension = "bin")
        {
            Directory.CreateDirectory(CheckDirectory);

            var filename = Path.Combine(CheckDirectory, (checkName == null ? method : $"{method}-{checkName}") + "." + fileExtension);

            if (GetOldBinaryContent(filename)?.SequenceEqual(outputBytes) == true)
            {
                // fine! Just check that the file is not changed - if it is changed or deleted, we rewrite
                if (IsModified(filename))
                {
                    using (var t = File.Create(filename))
                    {
                        t.Write(outputBytes, 0, outputBytes.Length);
                    }
                }
                return;
            }

            if (DoesGitWork.Value)
            {
                using (var t = File.Create(filename))
                {
                    t.Write(outputBytes, 0, outputBytes.Length);
                }

                if (IsModified(filename))
                {
                    if (IsNewFile(filename))
                    {
                        throw new Exception($"{Path.GetFileName(filename)} is not explicitly accepted - the file is untracked in git. To let this test pass, view the file and stage it. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md#untracked-file\n");
                    }


                    var diff = RunGitCommand("diff", filename);
                    if (diff.All(string.IsNullOrEmpty))
                    {
                        // I guess fine from our perspective, but it's weird...
                        Console.WriteLine($"CheckTestOutput warning: {Path.GetFileName(filename)} is modified, but the diff is empty.");
                        return;
                    }
                    throw new Exception(
                        $"{Path.GetFileName(filename)} has changed, the actual output differs from the previous accepted output!"
                        + "Is the change OK? To let the test pass, stage the file in git. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md#changed-file\n"
                    );
                }
            }
            else
            {
                throw new Exception($"{Path.GetFileName(filename)} has changed, the previous accepted output differs from the actual output.");
            }
        }
    }
}
