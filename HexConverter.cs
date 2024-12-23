using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization.Metadata;

namespace CustomClothingBase;

public class HexConverter<T> : JsonConverter<T> where T : INumber<T>
{
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue($"0x{value:X}");

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            reader.GetString() is string hexString &&
            hexString.TryParseHex<T>(out var result))
            return result;

        throw new JsonException("Invalid format for hex number");
    }
}

/// <summary>
/// Generic Dictionary converted with numeric keys
/// </summary>
public class HexKeyDictionaryConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>> where TKey : INumber<TKey>
{
    public override Dictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var dictionary = new Dictionary<TKey, TValue>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            // Read the key as a string
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            string keyHex = reader.GetString();
            if (!keyHex.TryParseHex<TKey>(out var key))
            {
                throw new JsonException($"Invalid hex key: {keyHex}");
            }

            // Read the value
            reader.Read();
            TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options);

            dictionary.Add(key, value);
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            string keyHex = "0x" + kvp.Key.ToString("X", CultureInfo.InvariantCulture);
            writer.WritePropertyName(keyHex);
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }
}

public static class HexExtensions
{
    public static bool TryParseHex<T>(this string hexString, out T result) where T : INumber<T>
        => hexString.StartsWith("0x") ?
            T.TryParse(hexString.Substring(2), NumberStyles.HexNumber, null, out result) :
            T.TryParse(hexString, NumberStyles.HexNumber, null, out result);
}

public class HexTypeResolver(HashSet<string> Keys) : DefaultJsonTypeInfoResolver
{
    public HashSet<string> Keys { get; set; } = Keys;
    HexConverter<uint> uintConverter = new();
    HexConverter<int> intConverter = new();

    public override JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        // Apply the converter only to the target types
        if (jsonTypeInfo != null)
        {
            foreach (var property in jsonTypeInfo.Properties)
            {
                //Base it off names?
                var name = property.Name;
                if (!Keys.Contains(name))
                    continue;

                var pType = property.PropertyType;
                if (property.PropertyType == typeof(int))
                    property.CustomConverter = intConverter;
                if (property.PropertyType == typeof(uint))
                    property.CustomConverter = uintConverter;
            }
        }

        return jsonTypeInfo;
    }
}