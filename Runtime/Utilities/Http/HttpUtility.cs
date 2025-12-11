using System;
using System.Text.RegularExpressions;
using Niantic.Protobuf;
using Niantic.Protobuf.Reflection;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.Http
{
    internal static class HttpUtility
    {
        /// <summary>
        /// Parse a response string into a response object.
        /// </summary>
        /// <param name="responseText">the response text</param>
        /// <typeparam name="TResponse">the response object type</typeparam>
        /// <returns>the object, if we succeed</returns>
        internal static TResponse ParseResponse<TResponse>(string responseText) where TResponse : class
        {
            // Check if TResponse is a protobuf message type. The calling methods aren't necessarily made to handle
            // protobufs cleanly, but if we end up here, use reflection as a last resort.
            if (IsProtobufMessageType<TResponse>())
            {
                try
                {
                    var parser = new JsonParser(JsonParser.Settings.Default);
                    return (TResponse)parser.Parse(responseText, GetProtobufMessageDescriptor<TResponse>());
                }
                catch (Exception)
                {
                    return null;
                }
            }

            try
            {
                // Use Unity's JsonUtility for regular serializable types
                return JsonUtility.FromJson<TResponse>(responseText);
            }
            catch (Exception)
            {
                // If JSON parsing fails, leave response as null
                return null;
            }
        }

        /// <summary>
        /// Extract the value of a given parameter from a header string.
        /// Header strings are of the form "parameterName=parameterValue; parameterName2=parameterValue2"
        /// </summary>
        /// <param name="header">the header string</param>
        /// <param name="parameterName">the name of the parameter</param>
        /// <returns>the value, if found (or the empty string)</returns>
        internal static string GetHeaderValue(string header, string parameterName)
        {
            // If no header supplied, just return the empty string
            if (header == null)
            {
                return string.Empty;
            }

            // The pattern looks for the parameter name, an equals sign,
            // and then captures everything until a semicolon or the end of the string.
            var match = Regex.Match(header, $@"{Regex.Escape(parameterName)}\s*=\s*([^;]*)", RegexOptions.IgnoreCase);

            // If the match was successful, return the captured group
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        // Checks if a type is a protobuf. Useful when working with generics with no object access.
        internal static bool IsProtobufMessageType<T>()
        {
            return typeof(IMessage).IsAssignableFrom(typeof(T));
        }

        // Use reflection to try to grab the proto descriptor from a type.
        private static MessageDescriptor GetProtobufMessageDescriptor<T>()
        {
            var type = typeof(T);
            var descriptorProperty = type.GetProperty("Descriptor",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);

            if (descriptorProperty != null)
            {
                var prop = descriptorProperty.GetValue(null);
                if (prop is MessageDescriptor descriptor)
                {
                    return descriptor;
                }
            }

            throw new InvalidOperationException($"Unable to get protobuf descriptor for type {type.Name}");
        }
    }
}
