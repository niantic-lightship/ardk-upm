// Copyright 2022-2024 Niantic.
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using System.Collections;

#if UNITY_ANDROID && UNITY_EDITOR
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEditor.Android;
#endif

namespace Niantic.ARDK.Editor
{
#if UNITY_ANDROID && UNITY_EDITOR
  // Modify the build time AndroidManifest to include Ardk required queries and dependencies
  // Currently, this will add the following to the AndroidManifest if not present:
  // -  <uses-permission android:name="android.permission.CAMERA" />
  // -  <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
  // -  <uses-feature android:name="android.hardware.camera.ar" android:required="false" />
  // -  <application/activity android:name="com.google.ar.core.InstallActivity" .. more metadata //>
  // -  <meta-data android:name="com.google.ar.core" android:value="optional" />
  // -  <queries/package> android:name="com.google.ar.core" //>
  internal class ArdkPostGenerateAndroidManifest:
    IPostGenerateGradleAndroidProject
  {
    // List of permissions:
    private const string CameraPermissionString = "android.permission.CAMERA";
    private const string ReadPermissionString = "android.permission.READ_EXTERNAL_STORAGE";
    private const string InternetPermissionString = "android.permission.INTERNET";

    // List of features:
    // Used to specify feature usage
    internal const string ARCoreFeatureString = "android.hardware.camera.ar";

    // List of Native Libraries to include
    internal const string OpenClPixelLibraryString = "libOpenCL-pixel.so";
    internal const string OpenClLibraryString = "libOpenCL.so";

    // Activities:
    // Specifically used to request ARCore install if not present
    internal const string ARCoreInstallActivityString = "com.google.ar.core.InstallActivity";

    internal const string ARCoreInstallActivityConfigString =
      "keyboardHidden|orientation|screenSize";

    internal const string ARCoreInstallActivityLaunchModeString = "singleTop";

    // ARCore:
    // Used to query existence of ARCore and add the package as optional
    internal const string ARCoreNameString = "com.google.ar.core";

    // Called by Unity after the Gradle build (AndroidManifest merging is complete)
    // Append any missing Ardk requirements to the merged AndroidManifest
    public void OnPostGenerateGradleAndroidProject(string basePath)
    {

// We use a custom version for the gradle plugin version for 2021 due to us needing newer features
// not present in the one shipped in unity 2021. Once unity gets to 2022.2, the unity gradle
// version is adequate for what we need and we can use it versus our custom version
#if UNITY_2022_2_OR_NEWER
#else
      SetGradlePluginVersion("4.2.0", GetBuildGradlePath(basePath));
#endif

      var needsWrite = false;
      var androidManifest = new AndroidManifest(GetManifestPath(basePath));
      needsWrite |= androidManifest.AddPermissionRequest(CameraPermissionString);
      needsWrite |= androidManifest.AddPermissionRequest(ReadPermissionString);
      needsWrite |= androidManifest.AddPermissionRequest(InternetPermissionString);
      needsWrite |= androidManifest.AddQuery(ARCoreNameString);
      needsWrite |= androidManifest.AddFeature(ARCoreFeatureString, false);
      needsWrite |= androidManifest.AddOpenClNativeLibraries();
      needsWrite |= androidManifest.AddARCoreInstallActivity();

      if (needsWrite)
      {
        androidManifest.Save();
      }
    }

    public int callbackOrder
    {
      get
      {
        return 2;
      }
    }

    private string _manifestFilePath;
    private string _buildGradlePath;

    private string GetManifestPath(string basePath)
    {
      if (string.IsNullOrEmpty(_manifestFilePath))
      {
        var pathBuilder = new StringBuilder(basePath);
        pathBuilder.Append(Path.DirectorySeparatorChar).Append("src");
        pathBuilder.Append(Path.DirectorySeparatorChar).Append("main");
        pathBuilder.Append(Path.DirectorySeparatorChar).Append("AndroidManifest.xml");
        _manifestFilePath = pathBuilder.ToString();
      }

      return _manifestFilePath;
    }

    private string GetBuildGradlePath(string basePath)
    {
      var pathBuilder = new StringBuilder(basePath);
      pathBuilder.Append(Path.DirectorySeparatorChar).Append("..");
      pathBuilder.Append(Path.DirectorySeparatorChar).Append("build.gradle");
      return pathBuilder.ToString();
    }

    // Tool to set gradle plugin version when we aren't using the one shipped with unity
    private void SetGradlePluginVersion(string version, string buildGradlePath)
    {
      if (File.Exists(buildGradlePath))
      {
        string[] arrLine = File.ReadAllLines(buildGradlePath);
        for (int i = 0; i < arrLine.Length; i++)
        {
          if (arrLine[i].Contains("com.android.tools.build:gradle:"))
          {
            arrLine[i] = Regex.Replace(arrLine[i], "\\d+\\.\\d+\\.\\d+", version);
          }
        }
        File.WriteAllLines(buildGradlePath, arrLine);
      }
      else
      {
        Log.Warning("build.gradle not found. Unable to set gradle plugin version");
      }
    }

    // Tools for manipulating an xmlDocument with Android specifics
    private class AndroidXmlDocument:
      XmlDocument
    {
      private string m_Path;
      protected XmlNamespaceManager nsMgr;
      public readonly string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

      public AndroidXmlDocument(string path)
      {
        m_Path = path;
        using (var reader = new XmlTextReader(m_Path))
        {
          reader.Read();
          Load(reader);
        }

        nsMgr = new XmlNamespaceManager(NameTable);
        nsMgr.AddNamespace("android", AndroidXmlNamespace);
      }

      public string Save()
      {
        return SaveAs(m_Path);
      }

      private string SaveAs(string path)
      {
        using (var writer = new XmlTextWriter(path, new UTF8Encoding(false)))
        {
          writer.Formatting = Formatting.Indented;
          Save(writer);
        }

        return path;
      }
    }

    // Tools for manipulating an xml document as an AndroidManifest
    // Contains Ardk specific functionality
    private class AndroidManifest:
      AndroidXmlDocument
    {
      private readonly XmlElement ApplicationElement;
      private readonly XmlElement ManifestElement;

      private const string ApplicationNullErrorMessage =
        "Could not get Application block in AndroidManifest, ARCore may not work as expected";

      private const string ManifestNullErrorMessage =
        "Could not get Manifest block in AndroidManifest, ARCore may not work as expected";

      public AndroidManifest(string path)
        : base(path)
      {
        ApplicationElement = SelectSingleNode("/manifest/application") as XmlElement;
        ManifestElement = SelectSingleNode("/manifest") as XmlElement;

        // These should never be null, but log an error if they are
        if (ApplicationElement == null)
          Log.Error(ApplicationNullErrorMessage);

        if (ManifestElement == null)
          Log.Error(ManifestNullErrorMessage);
      }

      // Helper to add a <queries> element to the <manifest> block
      internal bool AddQuery(string packageName)
      {
        bool changed = false;
        var queryNodes = SelectNodes
          ($"/manifest/queries/package[@android:name='{packageName}']", nsMgr);

        if (queryNodes?.Count == 0)
        {
          var query = CreateElement("queries");
          var package = CreateElement("package");
          var attr = CreateAttribute("android", "name", AndroidXmlNamespace);
          attr.Value = packageName;
          package.Attributes.Append(attr);
          query.AppendChild(package);
          ManifestElement.AppendChild(query);

          changed = true;
        }
        return changed;
      }

      // Helper to add a <uses-permission> element to the <manifest> block
      internal bool AddPermissionRequest(string permissionName)
      {
        bool changed = false;
        var cameraNodes = SelectNodes
          ($"/manifest/uses-permission[@android:name='{permissionName}']", nsMgr);

        if (cameraNodes?.Count == 0)
        {
          var elem = CreateElement("uses-permission");
          elem.Attributes.Append(CreateAndroidAttribute("name", permissionName));
          ManifestElement.AppendChild(elem);
          changed = true;
        }
        return changed;
      }

      // Helper to add a <uses-feature> element to the <manifest> block
      // Specify required if the feature is required
      internal bool AddFeature(string featureName, bool required = false)
      {
        bool changed = false;
        var cameraNodes = SelectNodes
        (
          $"/manifest/uses-feature[@android:name='{featureName}']",
          nsMgr
        );

        if (cameraNodes?.Count == 0)
        {
          var elem = CreateElement("uses-permission");
          elem.Attributes.Append(CreateAndroidAttribute("name", featureName));
          elem.Attributes.Append(CreateAndroidAttribute("required", required ? "true" : "false"));
          ManifestElement.AppendChild(elem);
          changed = true;
        }
        return changed;
      }

      internal bool AddNativeLibrary(string libraryName, bool required = false)
      {
        bool changed = false;
        var libraryNodes = SelectNodes
        (
          $"/manifest/application/uses-native-library[@android:name='{libraryName}']",
          nsMgr
        );

        if (libraryNodes?.Count == 0)
        {
          var elem = CreateElement("uses-native-library");
          elem.Attributes.Append(CreateAndroidAttribute("name", libraryName));
          elem.Attributes.Append(CreateAndroidAttribute("required", required ? "true" : "false"));
          ApplicationElement.AppendChild(elem);
          changed = true;
        }
        return changed;
      }

      // Lightship specific helper to add ARCore install activity and ARCore metadata as optional
      internal bool AddARCoreInstallActivity()
      {
        bool changed = false;
        var installActivityBlock = SelectNodes
        (
          $"/manifest/application/activity[@android:name='{ArdkPostGenerateAndroidManifest.ARCoreInstallActivityString}']",
          nsMgr
        );
        if (installActivityBlock?.Count == 0)
        {
          var elem = CreateElement("activity");
          elem.Attributes.Append
          (
            CreateAndroidAttribute
              ("name", ArdkPostGenerateAndroidManifest.ARCoreInstallActivityString)
          );

          elem.Attributes.Append
          (
            CreateAndroidAttribute
              ("configChanges", ArdkPostGenerateAndroidManifest.ARCoreInstallActivityConfigString)
          );

          elem.Attributes.Append
          (
            CreateAndroidAttribute("excludeFromRecents", "true")
          );

          elem.Attributes.Append
          (
            CreateAndroidAttribute
              ("launchMode", ArdkPostGenerateAndroidManifest.ARCoreInstallActivityLaunchModeString)
          );

          // Note that this is added to the <manifest/application>
          ApplicationElement.AppendChild(elem);
          changed = true;
        }

        var arcoreMetadata = SelectNodes
        (
          $"/manifest/application/meta-data[@android:name='{ArdkPostGenerateAndroidManifest.ARCoreNameString}']",
          nsMgr
        );
        if (arcoreMetadata?.Count == 0)
        {
          var metadataElem = CreateElement("meta-data");
          metadataElem.Attributes.Append
            (CreateAndroidAttribute("name", ArdkPostGenerateAndroidManifest.ARCoreNameString));

          metadataElem.Attributes.Append(CreateAndroidAttribute("value", "optional"));

          // Note that this is added to the <manifest/application>
          ApplicationElement.AppendChild(metadataElem);
          changed = true;
        }
        return changed;
      }

      internal bool AddOpenClNativeLibraries()
      {
        bool changed = false;

        var applicationBlockOpenClPixel = SelectNodes
        (
          $"/manifest/application/uses-native-library[@android:name='{ArdkPostGenerateAndroidManifest.OpenClPixelLibraryString}']",
          nsMgr
        );
        if (applicationBlockOpenClPixel?.Count == 0)
        {
          var elem = CreateElement("uses-native-library");
          elem.Attributes.Append
            (CreateAndroidAttribute("name",
              ArdkPostGenerateAndroidManifest.OpenClPixelLibraryString)
          );
          elem.Attributes.Append(CreateAndroidAttribute("required", "false"));

          ApplicationElement.AppendChild(elem);
          changed = true;
        }

        var applicationBlockOpenCl = SelectNodes
        (
          $"/manifest/application/uses-native-library[@android:name='{ArdkPostGenerateAndroidManifest.OpenClLibraryString}']",
          nsMgr
        );
        if (applicationBlockOpenCl?.Count == 0)
        {
          var elem = CreateElement("uses-native-library");
          elem.Attributes.Append
            (CreateAndroidAttribute ("name",
              ArdkPostGenerateAndroidManifest.OpenClLibraryString)
          );
          elem.Attributes.Append(CreateAndroidAttribute("required", "false"));

          ApplicationElement.AppendChild(elem);
          changed = true;
        }
        return changed;
      }

      // Helper to create a generic attribute
      private XmlAttribute CreateAndroidAttribute(string key, string value)
      {
        var attr = CreateAttribute("android", key, AndroidXmlNamespace);
        attr.Value = value;
        return attr;
      }
    }
  }
#endif
}
