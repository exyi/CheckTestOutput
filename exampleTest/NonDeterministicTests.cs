using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CheckTestOutput.Example
{
    public class NonDeterministicTests
    {
        OutputChecker check = new OutputChecker("testoutputs", sanitizeGuids: true);
        [Fact]
        public void CheckGuids()
        {
            Assert.Equal(1, check.NonDeterminismSanitizers.Count());
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var s = $"{id1} | {id2} | {id1}";
            check.CheckString(s);
        }

        [Fact]
        public void CheckGuidsJson()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            check.CheckJsonObject(new { id1, id2, id3 = id1 });
        }
    }
}
