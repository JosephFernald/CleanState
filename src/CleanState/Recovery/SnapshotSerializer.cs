// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CleanState.Recovery
{
    /// <summary>
    /// Serializes and deserializes <see cref="MachineSnapshot"/> to and from JSON.
    /// Handles the Dictionary&lt;string, object&gt; round-trip problem where
    /// System.Text.Json deserializes values as JsonElement instead of their
    /// original CLR types.
    /// </summary>
    public static class SnapshotSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new ObjectDictionaryConverter() }
        };

        private static readonly JsonSerializerOptions CompactOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            Converters = { new ObjectDictionaryConverter() }
        };

        /// <summary>
        /// Serialize a snapshot to a formatted JSON string.
        /// </summary>
        public static string ToJson(MachineSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return JsonSerializer.Serialize(snapshot, Options);
        }

        /// <summary>
        /// Serialize a snapshot to a compact (non-indented) JSON string.
        /// </summary>
        public static string ToJsonCompact(MachineSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return JsonSerializer.Serialize(snapshot, CompactOptions);
        }

        /// <summary>
        /// Serialize a snapshot to a UTF-8 byte array.
        /// </summary>
        public static byte[] ToBytes(MachineSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return JsonSerializer.SerializeToUtf8Bytes(snapshot, CompactOptions);
        }

        /// <summary>
        /// Deserialize a snapshot from a JSON string. Values in DomainData are
        /// automatically converted to their proper CLR types (int, double, bool, string,
        /// arrays of those types).
        /// </summary>
        public static MachineSnapshot FromJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return JsonSerializer.Deserialize<MachineSnapshot>(json, Options);
        }

        /// <summary>
        /// Deserialize a snapshot from a UTF-8 byte array.
        /// </summary>
        public static MachineSnapshot FromBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            return JsonSerializer.Deserialize<MachineSnapshot>(bytes, Options);
        }

        /// <summary>
        /// Converts Dictionary&lt;string, object&gt; values to proper CLR types
        /// during deserialization. Without this, System.Text.Json deserializes
        /// object values as JsonElement, causing InvalidCastException when
        /// MachineContext.Get&lt;T&gt;() is called after restore.
        /// </summary>
        private sealed class ObjectDictionaryConverter : JsonConverter<Dictionary<string, object>>
        {
            public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Expected StartObject");

                var dict = new Dictionary<string, object>();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return dict;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException("Expected PropertyName");

                    string key = reader.GetString();
                    reader.Read();
                    dict[key] = ReadValue(ref reader);
                }

                throw new JsonException("Unexpected end of JSON");
            }

            public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                foreach (var kvp in value)
                {
                    writer.WritePropertyName(kvp.Key);
                    WriteValue(writer, kvp.Value);
                }
                writer.WriteEndObject();
            }

            private static object ReadValue(ref Utf8JsonReader reader)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        return reader.GetString();

                    case JsonTokenType.Number:
                        // Try int first, then long, then double
                        if (reader.TryGetInt32(out int intVal))
                            return intVal;
                        if (reader.TryGetInt64(out long longVal))
                            return longVal;
                        return reader.GetDouble();

                    case JsonTokenType.True:
                        return true;

                    case JsonTokenType.False:
                        return false;

                    case JsonTokenType.Null:
                        return null;

                    case JsonTokenType.StartArray:
                        return ReadArray(ref reader);

                    case JsonTokenType.StartObject:
                        return ReadNestedObject(ref reader);

                    default:
                        throw new JsonException($"Unexpected token type: {reader.TokenType}");
                }
            }

            private static object ReadArray(ref Utf8JsonReader reader)
            {
                var list = new List<object>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        // Try to produce a typed array for common cases
                        if (list.Count == 0)
                            return Array.Empty<object>();

                        if (list[0] is int)
                        {
                            var arr = new int[list.Count];
                            for (int i = 0; i < list.Count; i++)
                                arr[i] = (int)list[i];
                            return arr;
                        }

                        if (list[0] is double)
                        {
                            var arr = new double[list.Count];
                            for (int i = 0; i < list.Count; i++)
                                arr[i] = (double)list[i];
                            return arr;
                        }

                        if (list[0] is string)
                        {
                            var arr = new string[list.Count];
                            for (int i = 0; i < list.Count; i++)
                                arr[i] = (string)list[i];
                            return arr;
                        }

                        return list.ToArray();
                    }
                    list.Add(ReadValue(ref reader));
                }
                throw new JsonException("Unexpected end of array");
            }

            private static Dictionary<string, object> ReadNestedObject(ref Utf8JsonReader reader)
            {
                var dict = new Dictionary<string, object>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return dict;
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException("Expected PropertyName");
                    string key = reader.GetString();
                    reader.Read();
                    dict[key] = ReadValue(ref reader);
                }
                throw new JsonException("Unexpected end of object");
            }

            private static void WriteValue(Utf8JsonWriter writer, object value)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                switch (value)
                {
                    case int i:
                        writer.WriteNumberValue(i);
                        break;
                    case long l:
                        writer.WriteNumberValue(l);
                        break;
                    case float f:
                        writer.WriteNumberValue(f);
                        break;
                    case double d:
                        writer.WriteNumberValue(d);
                        break;
                    case bool b:
                        writer.WriteBooleanValue(b);
                        break;
                    case string s:
                        writer.WriteStringValue(s);
                        break;
                    case int[] intArr:
                        writer.WriteStartArray();
                        for (int idx = 0; idx < intArr.Length; idx++)
                            writer.WriteNumberValue(intArr[idx]);
                        writer.WriteEndArray();
                        break;
                    case double[] dblArr:
                        writer.WriteStartArray();
                        for (int idx = 0; idx < dblArr.Length; idx++)
                            writer.WriteNumberValue(dblArr[idx]);
                        writer.WriteEndArray();
                        break;
                    case string[] strArr:
                        writer.WriteStartArray();
                        for (int idx = 0; idx < strArr.Length; idx++)
                            writer.WriteStringValue(strArr[idx]);
                        writer.WriteEndArray();
                        break;
                    case bool[] boolArr:
                        writer.WriteStartArray();
                        for (int idx = 0; idx < boolArr.Length; idx++)
                            writer.WriteBooleanValue(boolArr[idx]);
                        writer.WriteEndArray();
                        break;
                    default:
                        // Fallback: let System.Text.Json handle it
                        JsonSerializer.Serialize(writer, value, value.GetType());
                        break;
                }
            }
        }
    }
}
