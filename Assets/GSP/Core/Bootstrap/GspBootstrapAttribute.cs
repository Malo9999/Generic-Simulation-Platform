using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GspBootstrapAttribute : Attribute
{
    public GspBootstrapKind Kind { get; }
    public string Notes { get; }

    public GspBootstrapAttribute(GspBootstrapKind kind, string notes = "")
    {
        Kind = kind;
        Notes = notes ?? string.Empty;
    }
}
