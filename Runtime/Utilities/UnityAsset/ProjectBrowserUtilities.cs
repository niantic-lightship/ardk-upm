using System.IO;

namespace Niantic.Lightship.AR.Utilities.UnityAsset
{
    internal static class ProjectBrowserUtilities
    {
        // @param fileName Includes file extension (i.e. "foo.fbx" or "bar.asset")
        // @param targetDirectory The directory the file wants to live in
        public static string BuildAssetPath(string fileName, string targetDirectory)
        {
            var assetPath = Path.Combine(targetDirectory, fileName);

            if (File.Exists(assetPath))
            {
                var assetName = Path.GetFileNameWithoutExtension(fileName);
                var assetExt = Path.GetExtension(fileName);
                var count = 0;

                do
                {
                    count += 1;
                    var newName = $"{assetName}_{count}{assetExt}";
                    assetPath = Path.Combine(targetDirectory, newName);
                } while (File.Exists(assetPath));
            }

            return assetPath;
        }
    }
}
