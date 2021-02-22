using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CheckTestOutput.Example
{
    public class SomeTest
    {
        OutputChecker check = new OutputChecker("testoutputs");
        [Fact]
        public void TestString()
        {
            check.CheckString("there should be this thing");
        }

        [Fact]
        public void NamedChecks()
        {
            check.CheckString("there should be this thing", "check1");
            check.CheckString("there should be some other thing", "c2");
        }

        [Fact]
        public void MultilineCheck()
        {
            check.CheckLines(
                Enumerable.Range(0, 10000).Select(i => i.ToString())
            );
        }

        [Fact]
        public void JsonObjectCheck()
        {
            check.CheckJsonObject(
                new { number = 3, str = "jaja", list = new List<object> { 1, "23", new SomeTestObject { Prop = "hmm" } } }
            );
        }

        [Theory]
        [InlineData("positive", "it's 12th January")]
        [InlineData("negative", "the result is -1")]
        [InlineData("zero", "there is 0% growth")]
        public void IncrementNumbers(string checkName, string testString)
        {
            var replacedString = Regex.Replace(testString, "-?\\d+", m => int.Parse(m.Value) + 1 + "");
            check.CheckLines(new [] {
                testString,
                replacedString
            }, checkName);
        }

        class SomeTestObject
        {
            public string Prop { get; set; }
        }
    }
}
