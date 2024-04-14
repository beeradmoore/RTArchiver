using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTArchiver.JsonConverters;

public class StringOrDictionaryConverter : JsonConverter<object>
{
	public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			return reader.GetString();
		}
		
		Debugger.Break();
		return null;
	}

	public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
	{
		if (value is string str)
		{
			writer.WriteStringValue(str);
			return;
		}
		else if (value is Dictionary<string, string> dictionary)
		{
			Debugger.Break();
			return;
		}
		
		Debugger.Break();
	}
}