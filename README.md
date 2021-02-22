# CheckTestOutput

A library for semi-manual tests. Run a function, manually check the output. But only if it is different than last run. Built on git - stage the new version to accept it.

Although it's a nice idea that tests should verify if the results are correct by some smart logic, it not always possible/practical. For example, when testing a transpiler, it would come in handy to solve the halting problem. In that cases, you may end up with an assertion that simply checks if the result is equal to one of the valid outputs and that is annoying to maintain. This project just makes the long `Assert.Equal("....", generatedCode)` less annoying.

The library simply checks that the test output is the same as last time.
It the test output is **compared with its version from git index** and throws an **exception if it does not match**, prints a diff and writes a new version to the working tree.
To accept the new version, you stage the changed file.
Or, to inspect the differences, you can use your favorite diff tool.


## Usage

It requires your project to be in [git version control system](https://git-scm.com/) (it works without git, but does not offer the simple stage-to-accept workflow).
You can use any test framework you want, this thing just throws exceptions -- we'll use XUnit in the examples here.
The `OutputChecker` constructor parameter specifies where are the files with the expected output going to be stored relative to the test file location (it uses [C#/F# caller info attributes](https://docs.microsoft.com/cs-cz/dotnet/csharp/programming-guide/concepts/caller-information)).
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

You can check if the object matches when it is serialized to JSON (using Newtonsoft.Json)

```csharp
[Fact]
public void TestObject()
{
    PersonViewModel someData = GetDefaultPersonDetail();
    check.CheckJsonObject(someData);
}
```

In case you want to use more that one check in one test, you can give them names (so they don't end up overriding themselves):

```csharp
[Fact]
public void TestWithMultipleChecks()
{
    PersonViewModel person = GetDefaultPersonDetail();
    check.CheckString(someData.CurriculumVitae, checkName: "cv");
    check.CheckString(someData.Genome, checkName: "genome");
}
```

Or you can combine them into one anonymous JSON object (this is preferable when it's short - you don't end up with so many tiny files):

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

The `checkName` parameter is also useful for tests with parameters (Theory in XUnit).
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

The text files have `.txt` file extension by default, if that annoys you because your strings have some specific format and syntax highlighting does not work in text files...

```csharp
[Fact]
public void GenerateSomeCsharpCode()
{
    string code = GimmeSourceCode();
    check.CheckString(code, fileExtension: "cs");
}
```

Just keep in mind that dotnet is going to treat these `.cs` files as part of source code unless you `<Compile Remove="testoutputs/**.cs" />` them in the `.csproj` file.

### F#

This library is F# friendly, although it's written in C#:

```fsharp
open CheckTestOutput

let check = OutputChecker "testoutputs"

[<Fact>]
let ``Simple object processing - UseGenericUnion`` () = task {
    computeSomething 123 "456"
    |> string
    |> check.CheckString

    // or if you need checkName
    check.CheckString ("test string", checkName = "test2")
}
```

### Non-deterministic strings

If you test string, for example, contains some randomly generated GUIDs it's not possible to test that the output is always the same. You could either make sure that the logic you are testing is fully deterministic, or you can fix it later. CheckTestOutput has a helper functionality which allows you to replace random GUIDs with deterministically generated ones.

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

The sanitization preserves equality - it replaces different GUIDs with different stub string and same GUID with the same string. In this case, the checked JSON will be this:

```json
{
	"id1": "aaaaaaaa-bbbb-cccc-dddd-000000000001",
	"id2": "aaaaaaaa-bbbb-cccc-dddd-000000000002",
	"id3": "aaaaaaaa-bbbb-cccc-dddd-000000000001"
}
```

You can replace anything that can be found by a regular expression, just specify the regexes in the `nonDeterminismSanitizers` parameter.

## Installation

Just install [CheckTestOutput NuGet package](https://www.nuget.org/packages/CheckTestOutput).

```
dotnet add package CheckTestOutput
```

Alternatively, you can just grab the source codes from `src` folder and copy them into your project (it's MIT licensed, so just keep a link to this project in the copied code or something). This project has a dependency on [MedallionShell](https://github.com/madelson/MedallionShell) - an amazing library for executing processes without the glitches of the `Process` class. Also, it has a dependency on [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) for the JSON serialization. If you are copying the code you'll probably install these.
