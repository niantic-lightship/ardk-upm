// Copyright 2022-2024 Niantic.
using System;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    /**
     * Used to assess if java is running on a machine and execute .jar files
     * through the command line.
     *
     * This class is taken directly from UnityEditor.XR.ARCore.Editor
     * Original class is internal visibility so copied to reproduce functionality.
     * Would be nice to get the class made accessible.
     */
    internal static class JavaUtils
    {
        public static bool TryGetFullPathToJava(out string fullPathToJava, out string diagnosticMessage)
        {
#if UNITY_ANDROID
            var jdkRootPath = AndroidExternalToolsSettings.jdkRootPath;
            if (string.IsNullOrEmpty(jdkRootPath))
            {
                (fullPathToJava, diagnosticMessage) = (null, "No JDK root path set in Preferences > External Tools > Android > JDK.");
                return false;
            }

            var javaPath = Path.Combine(jdkRootPath, "bin", "java");

#if UNITY_EDITOR_WIN
            javaPath += ".exe";
#endif

            if (!File.Exists(javaPath))
            {
                (fullPathToJava, diagnosticMessage) = (null, $"Could not find Java executable at expected path: {javaPath}");
                return false;
            }

            (fullPathToJava, diagnosticMessage) = (javaPath, null);
            return true;
#else
            (fullPathToJava, diagnosticMessage) = (null, "Cannot get Java path unless the active build platform is Android.");
            return false;
#endif
        }

        public static bool canExecute => TryGetFullPathToJava(out _, out _);

        public static (string stdout, string stderr, int exitCode) Execute(string jarFile, string arguments = null)
        {
            if (string.IsNullOrEmpty(jarFile))
                throw new ArgumentException($"{jarFile} must not be null or empty.", nameof(jarFile));

            if (!TryGetFullPathToJava(out var fullPathToJava, out var diagnosticMessage))
                throw new InvalidOperationException(diagnosticMessage);

            var invocation = $"-jar \"{jarFile}\"";
            return UseCli.Execute(fullPathToJava, string.IsNullOrEmpty(arguments) ? invocation : $"{invocation} {arguments}");
        }

        public static (string stdout, string stderr, int exitCode) Execute(string jarFile, string[] arguments) =>
            Execute(jarFile, arguments == null ? null : string.Join(" ", arguments));
    }
}
