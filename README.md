# CheckTestOutput

**A library for semi-manual test output verification. Asks you to `git add` the new output if it changed.**

CheckTestOutput simply compares the test output with the last "accepted" file. When it differs, you get an error.
It is **compared with its version from git index** and throws an **exception if it does not match**, prints a diff and writes a new version to the working tree.
To accept the new version, you simply stage the changed file (`git add ...`).
To inspect the differences, you use your favorite diff tool.

Although it's a nice idea that tests should verify if the results are correct by some smart logic, it not always possible/practical. For example, when testing a transpiler, it would come in handy to solve the halting problem. In such cases, you will end up with an `Assert.Equal("some very long code including many \" \" and \n \n, super fun to read and maintain", generatedCode)`. This project just makes the long asserts less annoying.

## Usage

It requires your project to be in [git version control system](https://git-scm.com/) (it works without git, but does not offer the simple stage-to-accept workflow).
You can use any test framework you want, this thing just throws exceptions -- we'll use XUnit in the examples here.
The `OutputChecker` constructor parameter specifies where are the output files (relative to the test file location - it uses [C#/F# caller info attributes](https://docs.microsoft.com/cs-cz/dotnet/csharp/programming-guide/concepts/caller-information)).
In this case, it's `./testoutputs`.
The file name will be equal to the caller method name, in this case, `SomeTest.TestString.txt`.

This is how you check if simple string matches:

```csharp
public class SomeTest
{
    OutputChecker check = new OutputChecker("testoutputs");
    [Fact]
    public void TestString()
    {
        string someData = DoSomeComputation();
        check.CheckString(someData);
    }
}
```

You can also check if a collection of lines matches:

```csharp
[Fact]
public void TestLines()
{
    IEnumerable<string> someData = GetSomeResults().Select(a => a.ToString());
    check.CheckLines(someData);
}
```

Check if the object matches when it is serialized to JSON (using Newtonsoft.Json)

```csharp
[Fact]
public void TestObject()
{
    PersonViewModel someData = GetDefaultPersonDetail();
    check.CheckJsonObject(someData);
}
```

To use more that one check in one test, you need to give them names (so they don't end up overriding themselves):

```csharp
[Fact]
public void TestWithMultipleChecks()
{
    PersonViewModel person = GetDefaultPersonDetail();
    check.CheckString(someData.CurriculumVitae, checkName: "cv");
    check.CheckString(someData.Genome, checkName: "genome");
}
```

Alternatively, combine them into one anonymous JSON object. It's generally preferable when the string are short - too many tiny files are annoying:

```csharp
[Fact]
public void TestObject()
{
    PersonViewModel person = GetDefaultPersonDetail();
    check.CheckJsonObject(new {
        fname = person.Name,
        lname = person.LastName,
        person.BirthDate
    });
}
```

The `checkName` parameter is also useful for tests with parameters (`[Theory]` in XUnit).
For example, this way we could test a regular expression:

```csharp
[Theory]
[InlineData("positive", "it's 12th January")]
[InlineData("negative", "the result is -1")]
[InlineData("zero", "there is 0% growth")]
public void IncrementNumbers(string checkName, string testString)
{
    var replacedString = Regex.Replace(testString, "-?\\d+", m => int.Parse(m.Value) + 1 + "");
    check.CheckLines(new [] {
        testString,
        " -> ",
        replacedString
    }, checkName);
}
```

The text files have `.txt` file extension by default, but it's easy to change:

```csharp
[Fact]
public void GenerateSomeCsharpCode()
{
    string code = GimmeSourceCode();
    check.CheckString(code, fileExtension: "cs");
}
```

Just keep in mind that dotnet is going to treat these `.cs` files as part of source code unless you `<Compile Remove="testoutputs/**.cs" />` them in the `.csproj` file.

Binary data can be checked using `check.CheckBinary(byte[])` method. No diff is printed in that case, you have to use an external tool to diff binary files.

### F#

CheckTestOutput is reasonably F# friendly, although it's written in C#:

```fsharp
open CheckTestOutput

let check = OutputChecker "testoutputs"

[<Fact>]
let ``Simple object processing - UseGenericUnion`` () =
    computeSomething 123 "456"
    |> string
    |> check.CheckString

    // or if you need checkName
    check.CheckString ("test string", checkName = "test2")
[<Fact>]
let ``Example with anonymous record`` () =
    check.CheckJsonObject {| a = 1; b = "tukabel" |}
```

### Non-deterministic strings

If the test output contains some randomly generated UUIDs it isn't possible to test that the output is always the same. To fix the problem, you would either have to use a seeded random generator, or replace the UUIDs after the fact. CheckTestOutput has a helper functionality which allows you to replace random UUIDs with deterministically generated ones.

You can enable it by setting `sanitizeGuids: true` when creating `OutputChecker` (or `sanitizeQuotedGuids` to sanitize GUIDs in quotes):

```csharp
OutputChecker check = new OutputChecker("testoutputs", sanitizeGuids: true);

[Fact]
public void CheckGuidsJson()
{
    var id1 = Guid.NewGuid();
    var id2 = Guid.NewGuid();

    check.CheckJsonObject(new { id1, id2, id3 = id1 });
}
```

The sanitization preserves equality - it replaces different UUIDs with different stub string and same UUID with the same string. In this case, the checked JSON will be this:

```json
{
	"id1": "aaaaaaaa-bbbb-cccc-dddd-000000000001",
	"id2": "aaaaaaaa-bbbb-cccc-dddd-000000000002",
	"id3": "aaaaaaaa-bbbb-cccc-dddd-000000000001"
}
```

While mostly used for UUIDs, we can replace anything that can be found by a regular expression - just specify a list of regular expressions in the `nonDeterminismSanitizers` parameter.

### Custom checks

The `CheckString`, `CheckLines` and `CheckJsonObject` are just extension methods on the `OutputChecker` class and you can write your own.
The only thing to keep in mind is to include and propagate the `CallerMemberName` and `CallerFilePath` attributes.
As a simple example, this is how to implement a simple helper that changes the default file extension to `js`:

```csharp
public static void CheckJavascript(
    this OutputChecker t,
    string output,
    string checkName = null,
    string fileExtension = "js",
    [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
    [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
{
    t.CheckString(output, checkName, fileExtension, memberName, sourceFilePath);
}

```

For more inspiration, have a look at [CheckExtensions class in the Coberec project](https://github.com/exyi/coberec/blob/83f4a744af8cc9ec2c3e24d86d25840c41617ed2/src/Coberec.ExprCS.Tests/CheckExtensions.cs).

## Installation

[NuGet package](https://www.nuget.org/packages/CheckTestOutput) ¯\\_(ツ)_/¯.

```
dotnet add package CheckTestOutput
```

Alternatively, you can just grab the source codes from `src` folder and copy them into your project (it's MIT licensed, so just keep a link to this project in the copied code).
This library does not have any other dependencies (on new enough dotnet).

