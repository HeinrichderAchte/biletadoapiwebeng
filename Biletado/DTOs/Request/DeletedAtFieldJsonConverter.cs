using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Biletado.DTOs.Request;

public class DeletedAtFieldJsonConverter : JsonConverter<DeletedAtField>
{
    public override DeletedAtField? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new DeletedAtField { Present = true, Value = null };

        if (reader.TokenType == JsonTokenType.Null)
        {
          
            return result;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
            {
                result.Value = dt;
                return result;
            }

            throw new JsonException("deletedAt string is not a valid datetime");
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using (var doc = JsonDocument.ParseValue(ref reader))
            {
                if (doc.RootElement.TryGetProperty("value", out var valProp))
                {
                    if (valProp.ValueKind == JsonValueKind.Null)
                    {
                        result.Value = null;
                    }
                    else if (valProp.ValueKind == JsonValueKind.String)
                    {
                        var s = valProp.GetString();
                        if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                        {
                            result.Value = dt;
                        }
                        else
                        {
                            throw new JsonException("deletedAt.value is not a valid datetime");
                        }
                    }
                }
            }
            return result;
        }

        throw new JsonException("Unsupported token for DeletedAtField");
    }

    public override void Write(Utf8JsonWriter writer, DeletedAtField value, JsonSerializerOptions options)
    {
        if (!value.Present)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.Value.HasValue)
        {
            writer.WriteStringValue(value.Value.Value.ToString("o"));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

