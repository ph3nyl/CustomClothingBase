using System.Diagnostics;

namespace CustomClothingBase;

public class HexUIntConverter : JsonConverter<uint>
{
    public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
        => writer.WriteStringValue($"0x{value:X}");

    public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is string hexString && hexString.StartsWith("0x"))
            return Convert.ToUInt32(hexString.Substring(2), 16);

        throw new JsonException("Invalid format for hex number");
    }
}

public class HexIntConverter : JsonConverter<int>
{
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteStringValue($"0x{value:X}");

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is string hexString && Int32.TryParse(hexString, out var value))
            return value;

        throw new JsonException("Invalid format for hex number");
    }
}

/// <summary>
/// Generic Dictionary converted with numeric keys
/// </summary>
public class HexKeyDictionaryConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>> where TKey : struct, IComparable, IFormattable
{
    public override Dictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token.");

        var dictionary = new Dictionary<TKey, TValue>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token.");

            string propertyName = reader.GetString();

            // Check if the key is a hexadecimal string starting with "0x"
            if (propertyName != null && propertyName.StartsWith("0x"))
            {
                // Remove "0x" and convert the string to TKey
                string hexString = propertyName.Substring(2);
                TKey key = ConvertKeyFromHexString<TKey>(hexString);

                // Read the value
                reader.Read();
                TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options);

                dictionary[key] = value;
            }
            else
            {
                throw new JsonException("Invalid key format. Expected hexadecimal string starting with '0x'.");
            }
        }

        throw new JsonException("Unexpected end of JSON while reading dictionary.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            // Write the key as a hex string
            string keyAsHex = ConvertKeyToHexString(kvp.Key);
            writer.WritePropertyName(keyAsHex);

            // Serialize the value
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    private string ConvertKeyToHexString(TKey key)
    {
        if (key is IFormattable formattableKey)
        {
            return $"0x{formattableKey.ToString("X", null)}";
        }

        throw new JsonException("Key type is not a valid numeric type.");
    }

    private TKey ConvertKeyFromHexString<TKey>(string hexString)
    {
        Type keyType = typeof(TKey);
        if (keyType == typeof(int))
        {
            return (TKey)(object)Convert.ToInt32(hexString, 16);
        }
        else if (keyType == typeof(uint))
        {
            return (TKey)(object)Convert.ToUInt32(hexString, 16);
        }
        else if (keyType == typeof(long))
        {
            return (TKey)(object)Convert.ToInt64(hexString, 16);
        }
        else if (keyType == typeof(ulong))
        {
            return (TKey)(object)Convert.ToUInt64(hexString, 16);
        }
        else if (keyType == typeof(short))
        {
            return (TKey)(object)Convert.ToInt16(hexString, 16);
        }
        else if (keyType == typeof(ushort))
        {
            return (TKey)(object)Convert.ToUInt16(hexString, 16);
        }
        else
        {
            throw new JsonException($"Unsupported key type: {keyType}");
        }
    }
}
