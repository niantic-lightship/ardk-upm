using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Niantic.Lightship.AR.RCA.Utilities
{
    internal static class _ReadOnlyCollectionExtensions
    {
        internal static ReadOnlyCollection<T> AsNonNullReadOnly<T>(this T[] source)
        {
            if (source == null || source.Length == 0)
                return new ReadOnlyCollection<T>(new List<T>());

            return new ReadOnlyCollection<T>(source);
        }
    }
}
