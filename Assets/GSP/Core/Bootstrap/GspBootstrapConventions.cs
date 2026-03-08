using System;

public static class GspBootstrapConventions
{
    public static bool MatchesBootstrapNaming(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        return className.EndsWith("Bootstrap", StringComparison.Ordinal)
            || className.EndsWith("Bootstrapper", StringComparison.Ordinal);
    }

    public static GspBootstrapKind GuessKindFromPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return GspBootstrapKind.Unknown;
        }

        if (assetPath.Contains("/_Bootstrap/", StringComparison.OrdinalIgnoreCase))
        {
            return GspBootstrapKind.Platform;
        }

        if (assetPath.Contains("/Simulations/", StringComparison.OrdinalIgnoreCase))
        {
            return GspBootstrapKind.Simulation;
        }

        if (assetPath.Contains("/Preview/", StringComparison.OrdinalIgnoreCase))
        {
            return GspBootstrapKind.Preview;
        }

        if (assetPath.Contains("/Editor/", StringComparison.OrdinalIgnoreCase))
        {
            return GspBootstrapKind.Tool;
        }

        return GspBootstrapKind.Unknown;
    }
}
