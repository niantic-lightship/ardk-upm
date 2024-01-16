// Copyright 2022-2024 Niantic.
using Collections = System.Collections.Generic;

namespace Utilities
{
    public static class CollectionExtensions
    {
        public static int IndexOf<T>(this Collections.IReadOnlyList<T> self, T elementToFind)
        {
            for (int i = 0; i < self.Count; i++)
            {
                if (Equals(self[i], elementToFind))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
