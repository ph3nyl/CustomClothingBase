using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace CustomClothingBase;
public class CustomTypeResolver(HashSet<string> Keys) : DefaultJsonTypeInfoResolver
{
    public HashSet<string> Keys { get; set; } = Keys;
    private HexIntConverter _intConverter = new();
    private HexUIntConverter _uintConverter = new();

    public override JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        // Apply the converter only to the target type
        if (jsonTypeInfo != null) //&& type == typeof(ClothingTable))
        {
            foreach (var property in jsonTypeInfo.Properties)
            {
                //Base it off names?
                var name = property.Name;
                if (!Keys.Contains(name))
                    continue;

                var pType = property.PropertyType;
                if (property.PropertyType == typeof(int))
                    property.CustomConverter = _intConverter;
                if (property.PropertyType == typeof(uint))
                    property.CustomConverter = _uintConverter;
            }
        }

        return jsonTypeInfo;
    }
}