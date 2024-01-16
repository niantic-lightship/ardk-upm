// Copyright 2022-2024 Niantic.
using System.IO;

namespace Niantic.Lightship.AR.Utilities.UnityAssets
{
    internal static class ProjectBrowserUtilities
    {
        /// <summary>
        /// Builds the asset path
        /// </summary>
        /// <param name="fileNameWithFileExtension">Includes file extension (i.e. "foo.fbx" or "bar.asset")</param>
        /// <param name="targetDirectory">The directory the file wants to live in</param>
        /// <returns></returns>
        public static string BuildAssetPath(string fileNameWithFileExtension, string targetDirectory)
        {
            var assetPath = Path.Combine(targetDirectory, fileNameWithFileExtension);

            if (File.Exists(assetPath))
            {
                var assetName = Path.GetFileNameWithoutExtension(fileNameWithFileExtension);
                var assetExt = Path.GetExtension(fileNameWithFileExtension);
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
