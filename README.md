# CheckTestOutput

Although it's a nice idea that tests should verify if the results are correct by some smart logic, it not always possible/practical. For example, if you are testing a transpiler, it would come in handy to solve the halting problem, which is not possible. In that cases, you may end up with an assertion that simply checks if the result is equal to one of the valid outputs and that is annoying to maintain. This project just makes it less annoying.

It is a very simple library that performs a check that the test output is the same as last time. It compares the test output with its version from git index and if it does not match it simply throws an exception. To accept a new version of output, you can simply stage a changed file produced by the test. Or, to find where is the difference, you can use your favorite tool for comparing changes in the source tree.


## Usage

It requires your project to be in [git version control system](https://git-scm.com/). You can use any test framework you want, this thing just throws some exceptions, I'll use XUnit in the examples here. `CheckTestOutput` class just checks if some files match output from your test. The constructor parameter specifies where are the files going to be stored relative to the test file location (it uses [C#/F# caller info attributes](https://docs.microsoft.com/cs-cz/dotnet/csharp/programming-guide/concepts/caller-information)), in this case, it's `./testoutputs`. The file name is the method name, in this case, `SomeTest.TestString.txt`.

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

Or, you can check if the object matches when it is serialized to JSON (using Newtonsoft.Json)

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
public void TestWithMultipleChecks()
{
    PersonViewModel person = GetDefaultPersonDetail();
    check.CheckJsonObject(new { fname = person.Name, lname = person.LastName, person.BirthDate });
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

### Non-deterministic string

If you test string, for example, contains some randomly generated GUIDs it's not possible to test that the output is always the same. You could either make sure that the logic you are testing is fully deterministic, or you can fix it later. CheckTestOutput has a helper functionality which allows you to replace random GUIDs with deterministically generated ones.

You can enable it by setting `sanitizeGuids: true` when creating `OutputChecker` (or `sanitizeQuotedGuids` if you want to only sanitize GUIDs in quotes):

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

Or, you can just grab the source codes from `src` folder and copy them into your project (it's MIT licensed, so just keep a link to this project in the copied code or something). This project has a dependency on [MedallionShell](https://github.com/madelson/MedallionShell) - an amazing library for executing processes without glitches the standard .NET API has. Also, we have a dependency on [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) for the JSON serialization. If you are copying the code you'll probably install these.
