# CheckTestOutput

Although it's a nice idea that tests should verify if the results are correct by some smart logic, it not always possible/practical. For example, if you are testing a transpiler, it would come in handy to solve the halting problem, which is not possible. In that cases, you may end up with an assert that simply checks if the result is equal to one of the valid outputs and that is annoying to maintain. This project just makes it less annoying.

It is a very simple library that performs a check that the test output is same as last time. It compares the test output with version from git index and if it does not match it simply throws an exception. To accept a new version of output, you can simply stage a changed file produced by the test. Or, to find where is the difference, you can use your favorite tool for comparing changes in the source tree.


## Usage

It requires your project to be in [git version control system](https://git-scm.com/). You can use any test framework you want, this thing just throws some exceptions, I'll use XUnit in the examples here. `CheckTestOutput` class just checks if some files match output from your test. The constructor parameter specify where are the files going to be stored relative to the test file location (it uses [C#/F# caller info attributes](https://docs.microsoft.com/cs-cz/dotnet/csharp/programming-guide/concepts/caller-information)), in this case it's `./testoutputs`. The file name is the method name, in this case `SomeTest.TestString.txt`.

This is how you check if simple string matches:

```csharp
public class SomeTest
{
    CheckTestOutput check = new CheckTestOutput("testoutputs");
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

Or, you can check if object matches when it is serialized to JSON (using Newtonsoft.Json)

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

## Instalation

Just install [CheckTestOutput NuGet package](https://www.nuget.org/packages/CheckTestOutput).

```
dotnet add package CheckTestOutput
```

Or, you can just grab the source codes from `src` folder and copy them into your project (it's MIT licensed, so just keep a link to this project in the copied code or something). This project has dependency on [MedallionShell](https://github.com/madelson/MedallionShell) - an amazing library for executing processes without glitches the standard .NET API has. Also, we have a dependency on [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) for the JSON serialization. If you are copying the code you'll probably install these.
