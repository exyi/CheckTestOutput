using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CheckTestOutput.Example
{
    public class SomeTest
    {
        CheckTestOutput check = new CheckTestOutput("testoutputs");
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

        class SomeTestObject
        {
            public string Prop { get; set; }
        }
    }
}
