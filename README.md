# CheckTestOutput

A very simple library that performs a check that the test output is same as last time. It compares the test output with version from git index, if it does not fit it simply throws an exception. To accept the new version of output, simply stage the changed file.

Although it's a nice idea that tests should verify if the results are correct by some smart logic, it not always possible/practical. In that cases, you may end up with a assert that simply checks if the result is equal to one of the valid outputs which is annoying to maintain. This project just makes it less annoying.


## Usage

It requires your project to be in git. You can use any test framework you want, this thing just throws some exceptions, I'll use XUnit here. `CheckTestOutput` class just checks if some files match output from your test. The constructor parameter specifies where are the files going to be stored relative to the test file (it uses [C#/F# called info feature](https://docs.microsoft.com/cs-cz/dotnet/csharp/programming-guide/concepts/caller-information)), in this case it's "testoutputs". The file name is the method name, in this case "SomeTest.TestString".

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

Or, you can check if object matches when serialized to JSON (using Newtonsoft.Json)

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

