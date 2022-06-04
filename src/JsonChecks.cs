using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CheckTestOutput
{
    public static class JsonChecks
    {
        public static void CheckJsonObject(
            this OutputChecker t,
            object output,
            JsonSerializerOptions jsonOptions = null,
            bool normalizePropertyOrder = false,
            string checkName = null,
            string fileExtension = "json",
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = null,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
        {
            jsonOptions ??= new JsonSerializerOptions() { WriteIndented = true };
            var strOutput =
                JsonSerializer.Serialize(output, jsonOptions);

            if (normalizePropertyOrder)
            {
                // this awesome, just give me back newtonsoft...
                var jsonDocument = JsonDocument.Parse(strOutput);
                var outputStream = new MemoryStream();
                var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true });
                NormalizePropertyOrder(jsonDocument.RootElement, writer);
                writer.Flush();
                strOutput = System.Text.Encoding.UTF8.GetString(outputStream.ToArray());
            }


            // indent using tabs for back compatibility
            strOutput = Regex.Replace(strOutput, "^(  )+", m => new string('\t', m.Value.Length / 2), RegexOptions.Multiline);
            t.CheckOutputCore(
                strOutput,
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }

        static void NormalizePropertyOrder(JsonElement e, Utf8JsonWriter output)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                var properties = new List<(string name, JsonElement e)>();
                foreach (var prop in e.EnumerateObject())
                {
                    properties.Add((prop.Name, prop.Value));
                }
                properties.Sort();
                output.WriteStartObject();
                foreach (var p in properties)
                {
                    output.WritePropertyName(p.name);
                    NormalizePropertyOrder(p.e, output);
                }
                output.WriteEndObject();
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                output.WriteStartArray();
                foreach (var item in e.EnumerateArray())
                {
                    NormalizePropertyOrder(item, output);
                }
                output.WriteEndArray();
            }
            else
            {
                e.WriteTo(output);
            }
        }
    }
}
