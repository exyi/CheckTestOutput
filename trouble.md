# Troubleshooting common issues

## Untracked File

Did you see error like?

```
Error Message:
   System.Exception : SomeTests.MyTest.txt is not explicitly accepted - the file is untracked in git. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md
  Stack Trace:
     at CheckTestOutput.OutputChecker.CheckOutputCore(String outputString, String checkName, String method, String fileExtension)
   at CheckTestOutput.BasicChecks.CheckString(OutputChecker t, String output, String checkName, String fileExtension, String memberName, String sourceFilePath)
   at MyTest()
```

When you run `CheckString(...)` for the first time, the test output file does not exist, so CheckTestOutput can't know if the output is correct or not.
The only reasonable option is to initially fail your test and write the missing file to disk.
You only need to view the file, manually verify that the test output is correct and stage it in git (using `git add $myfile`).

Using command line, run:

```bash
$EDITOR <TheFile.txt>
```

I you think this is a correct output, run

```bash
git add <TheFile.txt>
```

Now the test will pass. Alternatively, I'd recommend using VS Code or a similar editor with git integration.

## Changed file

Did you see error like?

```
System.Exception : SomeTest.JsonObjectCheck.json has changed, the actual output differs from the previous accepted output:

diff --git a/exampleTest/testoutputs/SomeTest.JsonObjectCheck.json b/exampleTest/testoutputs/SomeTest.JsonObjectCheck.json
index bc75540..2da815d 100644
--- a/exampleTest/testoutputs/SomeTest.JsonObjectCheck.json
+++ b/exampleTest/testoutputs/SomeTest.JsonObjectCheck.json
@@ -3,7 +3,7 @@
 	"str": "jaja",
 	"list": [
 		1,
-		"23",
+		"2313",
 		{
 			"Prop": "hmm"
 		}

If this change OK? Stage the file to let the test pass. Confused? See https://github.com/exyi/CheckTestOutput/blob/master/trouble.md#changed-file

  Stack Trace:
     at CheckTestOutput.OutputChecker.CheckOutputCore(String outputString, String checkName, String method, String fileExtension)
   at CheckTestOutput.JsonChecks.CheckJsonObject(OutputChecker t, Object output, String checkName, String fileExtension, String memberName, String sourceFilePath)
   at CheckTestOutput.Example.SomeTest.JsonObjectCheck()
```

This means that you or your coworker have previously accepted a different test output.
Now it fails because the output is different, so either your test is non-deterministic or you changed something in logic.
If you are happy with making the change, simply run `git add <TheFile>` or Stage the changes in your favorite editor / Git UI.

![Example: VS Code's "Stage Changes" button](img/vscode-stage-changes.png)


## My test is not deterministic

This is a problem, you can't test a function which returns something different every time using CheckTestOutput.
Fortunately, lot's of non-determinism can be dealt with:

* Preferably, you'd change your core logic to be deterministic ;)
* If the test only fails with **low probability**, consider retrying it (simply put it in a loop with a try-catch, you don't need any exponential backoffs)
* Often the problem comes from **different ordering** of output due to usage of **hash tables**.
	* In such case you just need to reorder the output alphabetically.
	* For example, [DotVVM reorders all HTML attributes when testing](https://github.com/riganti/dotvvm/blob/f4fd122218103b5b2ad1fda226ad9c8e131faaf3/src/Framework/Testing/ControlTestHelper.cs#L181)
	* For different order of json properties, use `CheckJsonObject(..., normalizePropertyOrder: true)`

* When the problem culprit is non-deterministic IDs (such as UUIDs/GUIDs or even sequential ids may depend on too many factors):
	* CheckTestOutput can sanitize these fragments into a set of sequential ids
	* See [README | Non-deterministic strings](https://github.com/exyi/CheckTestOutput#non-deterministic-strings)
	* TL;DR: use `OutputChecker(sanitizeGuids: true)` for UUIDs or `OutputChecker(nonDeterminismSanitizers: new [] { "Process Id: (\d+)" })` for matching anything else using a Regex.
