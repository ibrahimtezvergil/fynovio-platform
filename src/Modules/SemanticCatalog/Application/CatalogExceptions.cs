using Contracts;

namespace SemanticCatalog.Application;

/// <summary>The caller lacks the owner's settings action (same denial the CRM settings handlers raise, same HTTP
/// mapping in Host).</summary>
public sealed class CatalogAuthorizationDeniedException : InvalidOperationException
{
    public string ActionKey { get; }
    public string ReasonCode { get; }
    public AuthorizationDenialStage DenialStage { get; }

    public CatalogAuthorizationDeniedException(string actionKey, string reasonCode, AuthorizationDenialStage denialStage)
        : base($"Action '{actionKey}' was denied ({reasonCode}).")
    {
        ActionKey = actionKey;
        ReasonCode = reasonCode;
        DenialStage = denialStage;
    }
}

/// <summary>`expectedRowVersion` no longer matches: someone changed the definition first.</summary>
public sealed class CatalogConcurrencyConflictException()
    : InvalidOperationException("The definition was changed by someone else. Reload and try again.");

/// <summary>The caller reused an idempotency key for a request with a different payload — a client bug, not a retry.</summary>
public sealed class IdempotencyKeyReusedException(string operation, string idempotencyKey)
    : InvalidOperationException($"Idempotency key '{idempotencyKey}' was already used for a different {operation} request.");

public sealed class FieldKeyConflictException(string key)
    : InvalidOperationException($"A field definition with the key '{key}' already exists for this tenant and object type.");

public sealed class FieldLimitExceededException(int limit)
    : InvalidOperationException($"Cannot create or reactivate a field definition: the limit of {limit} active fields per object type per tenant has been reached.");

/// <summary>A view column names a field that does not exist for this owner (422 `view_column_unknown`).</summary>
public sealed class ViewColumnUnknownException(string key)
    : InvalidOperationException($"The view uses the field '{key}', which is not defined.");

/// <summary>A new or edited view may not place a deprecated field (422 `view_column_deprecated`): deprecation means the field has
/// left the forms and the list. An existing view that already uses a field keeps working when it is deprecated later.</summary>
public sealed class ViewColumnDeprecatedException(string key)
    : InvalidOperationException($"The view uses the field '{key}', which is deprecated.");

public sealed class ViewKeyConflictException(string key)
    : InvalidOperationException($"A view with the key '{key}' already exists for this tenant and object type.");

public sealed class ViewLimitExceededException(int limit)
    : InvalidOperationException($"Cannot create or reactivate a view: the limit of {limit} active views per object type per tenant has been reached.");
