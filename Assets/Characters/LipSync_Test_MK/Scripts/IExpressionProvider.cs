// IExpressionProvider.cs
// Provides ARKit-like weights by name every frame.

using System.Collections.Generic;

public interface IExpressionProvider
{
    // Returns a map of ARKit-like channel names to weights in 0..1 range.
    Dictionary<string, float> GetWeights();
}
