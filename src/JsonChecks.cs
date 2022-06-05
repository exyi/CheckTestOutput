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
        /// <summary> Verifies that the provided object <paramref name="output" />, serialized as Json using System.Text.Json, is equal to the `outputDirectory/TestClass.TestMethod.json` file. </summary>
        /// <param name="jsonOptions"> Json serialization options will be passed to <see cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)" />. </param>
        /// <param name="normalizePropertyOrder"> If true, object properties will be sorted alphabetically. </param>
        /// <param name="checkName"> If not null, checkName will be appended to the calling <paramref name="memberName" />. Intended to be used when having multiple checks in one method. </param>
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
            jsonOptions ??= new JsonSerializerOptions() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var strOutput = SerializeJson(output, jsonOptions, normalizePropertyOrder);

            // indent using tabs for back compatibility
            strOutput = Regex.Replace(strOutput, "^(  )+", m => new string('\t', m.Value.Length / 2), RegexOptions.Multiline);
            t.CheckOutputCore(
                strOutput,
                checkName,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}",
                fileExtension
            );
        }

        internal static string SerializeJson(object obj, JsonSerializerOptions options, bool normalizePropertyOrder)
        {
            if (obj is null) return "null";

            var type = obj.GetType();
            bool isToStringableObject = false;
            while (type != null)
            {
                if (type.FullName == "Newtonsoft.Json.Linq.JToken")
                {
                    isToStringableObject = true;
                    break;
                }
                type = type.BaseType;
            }

            var output =
                isToStringableObject ? obj.ToString() :
                JsonSerializer.Serialize(obj, options);

            return normalizePropertyOrder ? NormalizePropertyOrder(output) : output;
        }

        static string NormalizePropertyOrder(string json)
        {
            // this awesome, just give me back newtonsoft...
            var jsonDocument = JsonDocument.Parse(json);
            var outputStream = new MemoryStream();
            var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true });
            NormalizePropertyOrder(jsonDocument.RootElement, writer);
            writer.Flush();
            return System.Text.Encoding.UTF8.GetString(outputStream.ToArray());
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
