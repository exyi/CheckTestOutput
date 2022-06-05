using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
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
                new { number = 3, str = "jaja", list = new List<object> { 1, "2313", new SomeTestObject { Prop = "hmm" } } }
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

        [Fact]
        public void JsonWithNormalizedOrder()
        {
            var dict = new Dictionary<string, object> {
                { "a", 1 },
                { "b", 2 },
                { "c", 3 },
                { "d", 4 },
                { "e", 5 },
                { "f", 6 },
                { "g", 7 },
                { "h", 8 },
                { "o", 15 },
                { "i", 9 },
                { "j", 10 },
                { "k", 11 },
                { "l", 12 },
                { "m", 13 },
                { "n", 14 },
                { "p", 16 },
                { "q", 17 },
                { "r", 18 },
                { "s", 19 },
                { "t", 20 },
                { "u", 21 },
                { "v", 22 },
                { "w", 23 },
                { "x", 24 },
                { "y", 25 },
                { "z", 26 },
                { "lalala", new SomeTestObject() }
            };
            var random = new Random();
            // lol, Dictionary is bit too stable by default :D

            var randomEl = dict.ElementAt(random.Next(0, dict.Count - 1));
            dict.Remove(randomEl.Key);
            dict.Add("lol", false);
            dict.Add(randomEl.Key, randomEl.Value);
            check.CheckJsonObject(dict, normalizePropertyOrder: true);
        }

        [Fact]
        public void NewtonsoftJsonCompatibility()
        {
            check.CheckJsonObject(
                JObject.FromObject(new { number = 3, str = "jaja", list = new List<object> { 1, "2313", new SomeTestObject { Prop = "hmm" } } })
            );
        }

        [Fact]
        public void ShortTable()
        {
            check.CheckTable(
                someSmallTable
            );
        }

        [Fact]
        public void ShortTableWithManualProperties()
        {
            check.CheckTable(
                someSmallTable,
                properties: new [] { "Title", "ID", "Age", "Something.Prop", "PolymorphicSomething.trrrr" }
            );
        }

        [Fact]
        public void BiggerTable()
        {
            check.CheckTable(
                bitBiggerTable
            );
        }

        class SomeTestObject
        {
            public string Prop { get; set; }
        }

        class BitMoreComplexObject
        {
            public int ID { get; set; }
            public int Age { get; set; }
            public SomeTestObject Something { get; set; }
            public object PolymorphicSomething { get; set; }
            public string Title { get; set; }
            public string SomeText { get; set; }
        }

        static BitMoreComplexObject[] someSmallTable = new []{
            new BitMoreComplexObject {
                ID = 1,
                Age = 10,
                Something = new SomeTestObject { Prop = "hmm" },
                Title = "Yes",
                SomeText = "Lorem ipsum..."
            },
            new BitMoreComplexObject {
                ID = 2,
                Age = 20,
                PolymorphicSomething = new { trrrr = 1 },
                Title = "please no",
                SomeText = "You are telling the enemy exactly what you are going to do. No wonder you have been fighting Lorem Ipsum your entire adult life. Look at these words. Are they small words? And he referred to my words - if they are small, something else must be small. I guarantee you there's no problem, I guarantee. Trump Ipsum is calling for a total and complete shutdown of Muslim text entering your website. Lorem Ipsum is unattractive, both inside and out. I fully understand why its former users left it for something else. They made a good decision."
            },
            new BitMoreComplexObject {
                ID = 3,
                Age = 20,
                Title = "I don't know",
            },
            new BitMoreComplexObject {
                ID = 4,
                Age = 20,
                Title = "...",
            },
        };

        static BitMoreComplexObject[] bitBiggerTable =
            Enumerable.Range(0, 200).Select(id =>
                new BitMoreComplexObject {
                    ID = id,
                    Age = id * 31 % 60,
                    Title = "X" + id,
                    PolymorphicSomething = id % 17 > 0 ? null : new {x = "jjjjj"}
                }
            ).ToArray();
    }
}
