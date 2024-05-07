using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTArchiver.JsonConverters;

public class StringToIntConverter : JsonConverter<int>
{
	public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			var number = reader.GetInt32();
			return number;
		}
		if (reader.TokenType == JsonTokenType.String)
		{
			var numberString = reader.GetString();
			if (Int32.TryParse(numberString, out int number))
			{
				return number;
			}
		}
		
		Debugger.Break();
		return -1;
	}

	public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
	{
		/*
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
		*/
		Debugger.Break();
	}
}