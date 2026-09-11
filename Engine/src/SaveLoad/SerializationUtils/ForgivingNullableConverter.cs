using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RhyCiv.Engine.SaveLoad.SerializationUtils;

/// <summary>
/// Reads a nullable number or flag that an older build wrote as an empty object.
/// <para>
/// The save writer asked the framework for a property's type code and wrote it
/// accordingly. A nullable value type is a struct, so the answer was "object", and
/// the writer emitted the boxed value as <c>{}</c> -- a research goal came out as
/// <c>"ResearchGoal": {}</c>. Nothing complained at the time; the failure came on
/// the way back in, where the reader wants a number and finds an object and gives
/// up on the whole save. In practice that meant every game saved after the player
/// had picked a research goal, started a revolution, or stolen a technology could
/// not be loaded again.
/// </para>
/// <para>
/// The writer emits the value itself now. This accepts both, so the games players
/// already have on disk open rather than being lost: <c>{}</c> reads as absent,
/// which is what those saves would have meant had the field been dropped instead
/// of mangled.
/// </para>
/// </summary>
public class ForgivingNullableConverter<T> : JsonConverter<T?> where T : struct
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            // The damaged form. Skip past it and report the field as absent.
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                reader.Skip();
                return null;

            default:
                return JsonSerializer.Deserialize<T>(ref reader, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value.Value, options);
    }
}
