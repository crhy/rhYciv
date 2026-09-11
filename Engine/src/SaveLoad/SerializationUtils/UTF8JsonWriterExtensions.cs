using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RhyCiv.Engine.SaveLoad.SerializationUtils;

public static class Utf8JsonWriterExtensions
{
    public static T[] Clamp<T>(this T[] array, T ignoreValue = default!)
    {
        int i = array.Length - 1;
        for (; i >= 0 && EqualityComparer<T>.Default.Equals(array[i], ignoreValue); i--)
        {
        }

        if (i == -1)
        {
            return [];
        }
        var res = new T[i +1];
        Array.Copy(array, 0, res, 0, i +1);
        return res;
    }
    public static void WriteNonDefaultFields(this Utf8JsonWriter writer, string objectName, object instance)
    {
        writer.WriteStartObject(objectName);
        WriteObjectContents(writer, instance);
    }

    public static void WriteNonDefaultFields(this Utf8JsonWriter writer, object instance)
    {
        writer.WriteStartObject();
        WriteObjectContents(writer, instance);
    }
    private static void WriteObjectContents(this Utf8JsonWriter writer, object instance)
    {
        var type = instance.GetType();
        foreach (var info in
                 type.GetProperties())
        {
            // A nullable value type -- int?, bool? -- is a struct, so asking the
            // framework for its type code answers Object, and the branch that
            // handles an object wrote the boxed number as "{}": a research goal
            // came out as ResearchGoal: {}, which the reader cannot turn back into
            // a number. Every save written once the player had chosen a research
            // goal, or begun a revolution, or stolen a technology, was therefore
            // unreadable -- the save appeared to succeed and the game would not
            // load it again. Unwrap the nullable and write the value it holds.
            var declaredType = info.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(declaredType);
            var typeCode = Type.GetTypeCode(underlyingType ?? declaredType);
            var value = info.GetValue(instance);

            // For a nullable, absent and default are different states the save has
            // to keep apart: null means "no research goal", 0 means "the advance at
            // index zero". So it is written whenever it is not null, rather than
            // being dropped for matching the underlying type's default.
            if (underlyingType != null)
            {
                if (value != null)
                {
                    WriteValue(info.Name, writer, typeCode, value);
                }

                continue;
            }

            var defaultValue = GetDefaultValueFor(typeCode);
            if ((defaultValue == null && value != null) || (defaultValue != null && !defaultValue.Equals(value)))
            {
                WriteValue(info.Name, writer, typeCode, value);
            }
        }
        writer.WriteEndObject();
    }

    private static void WriteValue(string name, Utf8JsonWriter writer, TypeCode typeCode, object? value)
    {
        if (value is null)
        {
            return;
        }

        switch (typeCode)
        {
            case TypeCode.Empty:
                break;
            case TypeCode.Object:
                // A dictionary is an IEnumerable of key/value pairs, and falling
                // into the branch below wrote it as an array of {Key, Value}
                // objects -- which the reader, which deserialises it as a
                // Dictionary, cannot parse. Any save holding a unit with script
                // data on it, such as a barbarian's horde flag, could not be
                // loaded at all. Written as a JSON object it round-trips.
                if (value is IDictionary dictionary)
                {
                    if (dictionary.Count == 0)
                    {
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        writer.WritePropertyName(name);
                    }

                    writer.WriteStartObject();
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        writer.WritePropertyName(Convert.ToString(entry.Key) ?? string.Empty);
                        writer.WriteStringValue(Convert.ToString(entry.Value) ?? string.Empty);
                    }
                    writer.WriteEndObject();
                    break;
                }

                if (value is IEnumerable enumerable)
                {
                    var enumerator = enumerable.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var element = enumerator.Current;
                        if (element != null)
                        {
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                writer.WritePropertyName(name);
                            }

                            writer.WriteStartArray();
                            WriteValue(string.Empty, writer, Type.GetTypeCode(element.GetType()), element);
                            while (enumerator.MoveNext())
                            {
                                element = enumerator.Current;
                                if (element != null)
                                    WriteValue(string.Empty, writer, Type.GetTypeCode(element.GetType()), element);
                            }
                            writer.WriteEndArray();
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        writer.WritePropertyName(name);
                    }
                    writer.WriteStartObject();
                    writer.WriteObjectContents(value);
                }

                break;
            case TypeCode.DBNull:
                break;
            case TypeCode.Boolean:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteBooleanValue(Convert.ToBoolean(value));
                break;
            case TypeCode.Char:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteStringValue(value.ToString());
                break;
            case TypeCode.SByte:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToSByte(value));
                break;
            case TypeCode.Byte:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToByte(value));
                break;
            case TypeCode.Int16:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToInt16(value));
                break;
            case TypeCode.UInt16:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToUInt16(value));
                break;
            case TypeCode.Int32:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToInt32(value));
                break;
            case TypeCode.UInt32:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToUInt32(value));
                break;
            case TypeCode.Int64:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToInt64(value));
                break;
            case TypeCode.UInt64:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToUInt64(value));
                break;
            case TypeCode.Single:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToSingle(value));
                break;
            case TypeCode.Double:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToDouble(value));
                break;
            case TypeCode.Decimal:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteNumberValue(Convert.ToDecimal(value));
                break;
            case TypeCode.DateTime:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteStringValue(value.ToString());
                break;
            case TypeCode.String:
                if (!string.IsNullOrWhiteSpace(name))
                {
                    writer.WritePropertyName(name);
                }
                writer.WriteStringValue(value.ToString());
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Return the default value for a field, there's probaby a framework method for this... 
    /// </summary>
    /// <param name="typeCode"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static object? GetDefaultValueFor(TypeCode typeCode)
    {
        return typeCode switch
        {
            TypeCode.Empty or TypeCode.DBNull => null,
            TypeCode.Object => null,
            TypeCode.Boolean => false,
            TypeCode.Char => '\0',
            TypeCode.SByte => default(sbyte),
            TypeCode.Byte => default(byte),
            TypeCode.Int16 => default(short),
            TypeCode.UInt16 => default(ushort),
            TypeCode.Int32 => default(int),
            TypeCode.UInt32 => default(uint),
            TypeCode.Int64 => default(long),
            TypeCode.UInt64 => default(ulong),
            TypeCode.Single => default(float),
            TypeCode.Double => default(double),
            TypeCode.Decimal => default(decimal),
            TypeCode.DateTime => default(DateTime),
            TypeCode.String => default(string),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
