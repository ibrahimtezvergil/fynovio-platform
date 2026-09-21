using Contracts;

namespace CRM.Application;

internal static class PartyDisplay
{
    public static string Name(PartyDirectoryEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Surname) ? entry.Name : $"{entry.Name} {entry.Surname}";
}
