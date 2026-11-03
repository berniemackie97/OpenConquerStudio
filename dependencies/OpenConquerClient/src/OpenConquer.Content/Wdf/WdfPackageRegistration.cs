namespace OpenConquer.Content.Wdf;

/// <summary>
/// The outcome of one <c>ini/package.ini</c> declaration.
/// </summary>
public enum WdfPackageRegistrationOutcome
{
    Registered = 0,
    FileNotFound = 1,
    DuplicatePrefix = 2,
    ArchiveUnavailable = 3,
}

/// <summary>
/// One <c>ini/package.ini</c> declaration and how it was resolved.
/// </summary>
public readonly record struct WdfPackageRegistration(string DeclaredName, string Prefix, WdfPackageRegistrationOutcome Outcome);
