namespace Access.Application;

public sealed class RoleAssignmentConflictException(string message) : Exception(message);
