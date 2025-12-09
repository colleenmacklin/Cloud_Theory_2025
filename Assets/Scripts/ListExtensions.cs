using System;
using System.Collections.Generic;

public static class ListExtensions
{
    private static Random rng = new Random(); // Initialize Random once for better randomness

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1); // Get a random index from 0 to n
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}
