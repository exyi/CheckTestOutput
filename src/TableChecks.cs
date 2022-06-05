using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CheckTestOutput
{
    public static class TableChecks
    {
        /// <summary> Verifies that the provided object array <paramref name="output" /> is equal to the `outputDirectory/TestClass.TestMethod.txt` file. The array is (hopefully) formatted as a human readable table. </summary>
        /// <param name="properties"> A list of properties to serialize. You can use values like `A.B.C` to get properties of nested objects. </param>
        /// <param name="maxColumnLength"> If the lengths of the value is over this number, it will be printed bellow the row instead of attempting to fit it into the table </param>
        /// <param name="jsonOptions"> The table is created by Json serializing each object, these json serialization options will be passed to <see cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)" /> call. </param>
        /// <param name="normalizePropertyOrder"> If true, object properties will be sorted alphabetically. </param>
        /// <param name="checkName"> If not null, checkName will be appended to the calling <paramref name="memberName" />. Intended to be used when having multiple checks in one method. </param>
        public static void CheckTable<T>(
            this OutputChecker t,
            IEnumerable<T> objects,
            string[] properties = null,
            int maxColumnLength = 40,
            JsonSerializerOptions jsonOptions = null,
            bool normalizePropertyOrder = false,
            string checkName = null,
            string fileExtension = "txt",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            jsonOptions ??= new JsonSerializerOptions() { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var propertyLengths = new Dictionary<string, int>();
            var jsonRows = new List<JsonElement>();

            int i = 0;
            foreach (var o in objects)
            {
                if (o is null)
                    continue;

                if (i > 100_000)
                {
                    throw new Exception($"CheckTestOutput.CheckTable is limited to 100_000 objects, otherwise diffing the output will take approximately infinite time.");
                }

                var json = JsonDocument.Parse(JsonChecks.SerializeJson(o, jsonOptions, normalizePropertyOrder)).RootElement;

                ComputePropertyLengths(json, propertyLengths, properties);
                jsonRows.Add(json);
                i++;
            }

            foreach (var p in propertyLengths.ToArray())
            {
                if (p.Key.Length > p.Value)
                    propertyLengths[p.Key] = p.Key.Length;
            }
            var maxPropertyLength = propertyLengths.Where(x => x.Value > maxColumnLength).Select(k => k.Key.Length).Append(0).Max();

            var tableColumns =
                (properties?.AsEnumerable() ?? propertyLengths.Keys.OrderBy(x => propertyLengths[x]))
                .Where(x => propertyLengths[x] <= maxColumnLength).ToArray();

            var table = new StringBuilder();

            // header
            foreach (var c in tableColumns)
            {
                // assume that header properties don't need to be quoted
                table.Append(c);
                table.Append(' ', propertyLengths[c] - c.Length + 1);
            }
            var rowLength = table.Length;
            table.AppendLine().Append('^', rowLength).AppendLine();

            foreach (var row in jsonRows)
            {
                foreach (var c in tableColumns)
                {
                    var value = MaybeUnquoted(GetProperty(row, c));
                    table.Append(value);
                    table.Append(' ', propertyLengths[c] - value.Length + 1);
                }
                table.AppendLine();

                bool hasAdditionalField = false;
                if (properties is null)
                {
                    foreach (var prop in row.EnumerateObject())
                    {
                        if (prop.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || prop.Value.ValueKind is JsonValueKind.String && prop.Value.GetString() == "")
                            continue;
                        if (propertyLengths[prop.Name] <= maxColumnLength)
                            continue;
                        hasAdditionalField = true;
                        table.Append('\t').Append(prop.Name).Append(' ', maxPropertyLength - prop.Name.Length).Append(": ").Append(MaybeUnquoted(prop.Value)).AppendLine();
                    }
                }
                else
                {
                    foreach (var prop in properties)
                    {
                        if (GetProperty(row, prop) is not {} jsonValue) continue;
                        if (propertyLengths[prop] <= maxColumnLength) continue;
                        hasAdditionalField = true;
                        table.Append('\t').Append(prop).Append(' ', maxPropertyLength - prop.Length).Append(": ").Append(MaybeUnquoted(jsonValue)).AppendLine();
                    }
                }
                if (hasAdditionalField)
                    table.AppendLine();
            }

            t.CheckString(table.ToString(), checkName, fileExtension, memberName, sourceFilePath);
        }

        static void ComputePropertyLengths(JsonElement json, Dictionary<string, int> lengths, string[] properties)
        {
            if (json.ValueKind != JsonValueKind.Object)
            {
                throw new Exception("CheckTestOutput.CheckTable can only be used for objects which serialize to a Json object.");
            }
            if (properties is null)
            {
                foreach (var prop in json.EnumerateObject())
                {
                    var len = ApproxJsonLength(prop.Value);
                    if (!lengths.TryGetValue(prop.Name, out var existingMax) || existingMax < len)
                    {
                        lengths[prop.Name] = len;
                    }
                }
            }
            else
            {
                foreach (var prop in properties)
                {
                    var propValueNullable = GetProperty(json, prop);
                    if (propValueNullable is not {} propValue)
                    {
                        continue;
                    }
                    var len = ApproxJsonLength(propValue);
                    if (!lengths.TryGetValue(prop, out var existingMax) || existingMax < len)
                    {
                        lengths[prop] = len;
                    }
                }
            }
        }

        static string MaybeUnquoted(JsonElement? json) =>
            json is null ? "" :
            json.Value.ValueKind == JsonValueKind.String && CanBeUnquoted(json.Value.GetString()) ? json.Value.GetString() :
            json.Value.GetRawText();


        /// Upper bound on the number of characters in the serialized JSON
        static int ApproxJsonLength(JsonElement json) => MaybeUnquoted(json).Length;

        static bool CanBeUnquoted(string str)
        {
            if (str.IndexOfAny(new[] { '|', ',', '\r', '\n', ' ', '\t' }) >= 0)
            {
                return false;
            }
            foreach (var c in str)
                if (char.IsWhiteSpace(c))
                    return false;

            return true;
        }

        static JsonElement? GetProperty(JsonElement json, string property)
        {
            int parsingIndex = 0;
            while (property.IndexOf('.', startIndex: parsingIndex) is var dotIndex && dotIndex >= 0)
            {
                var nestedName = property.AsSpan().Slice(parsingIndex, dotIndex - parsingIndex);
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(nestedName, out var nestedValue))
                {
                    json = nestedValue;
                    parsingIndex = dotIndex + 1;
                }
                else
                {
                    return null;
                }
            }

            var name = property.AsSpan().Slice(parsingIndex);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var value))
            {
                if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || value.ValueKind is JsonValueKind.String && value.GetString() == "")
                {
                    return null;
                }
                return value;
            }
            else
                return null;
        }
    }
}
