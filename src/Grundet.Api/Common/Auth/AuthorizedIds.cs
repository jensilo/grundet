namespace Grundet.Api.Common.Auth;

/// <summary>
/// Team identifier that is guaranteed to have passed <see cref="IResourceAuthorizationService.RequireTeamAccessAsync"/>.
/// </summary>
public readonly record struct AuthorizedTeamId
{
    /// <summary>
    /// Internal-only constructor to preserve the authorization invariant at the API boundary.
    /// </summary>
    internal AuthorizedTeamId(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public void ThrowIfDefault()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("AuthorizedTeamId must not be default.");
        }
    }
}

/// <summary>
/// Team member user identifier that is guaranteed to belong to an authorized team.
/// </summary>
public readonly record struct AuthorizedTeamMemberUserId
{
    /// <summary>
    /// Internal-only constructor to preserve the authorization invariant at the API boundary.
    /// </summary>
    internal AuthorizedTeamMemberUserId(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public void ThrowIfDefault()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("AuthorizedTeamMemberUserId must not be default.");
        }
    }
}

/// <summary>
/// Project identifier that is guaranteed to have passed <see cref="IResourceAuthorizationService.RequireProjectAccessAsync"/>.
/// </summary>
public readonly record struct AuthorizedProjectId
{
    /// <summary>
    /// Internal-only constructor to preserve the authorization invariant at the API boundary.
    /// </summary>
    internal AuthorizedProjectId(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public void ThrowIfDefault()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("AuthorizedProjectId must not be default.");
        }
    }
}

/// <summary>
/// Feature identifier that is guaranteed to have passed <see cref="IResourceAuthorizationService.RequireFeatureAccessAsync"/>.
/// </summary>
public readonly record struct AuthorizedFeatureId
{
    /// <summary>
    /// Internal-only constructor to preserve the authorization invariant at the API boundary.
    /// </summary>
    internal AuthorizedFeatureId(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public void ThrowIfDefault()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("AuthorizedFeatureId must not be default.");
        }
    }
}

/// <summary>
/// Requirement identifier that is guaranteed to have passed <see cref="IResourceAuthorizationService.RequireRequirementAccessAsync"/>.
/// </summary>
public readonly record struct AuthorizedRequirementId
{
    /// <summary>
    /// Internal-only constructor to preserve the authorization invariant at the API boundary.
    /// </summary>
    internal AuthorizedRequirementId(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public void ThrowIfDefault()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("AuthorizedRequirementId must not be default.");
        }
    }
}

/// <summary>
/// Work item identifier that is guaranteed to have passed <see cref="IResourceAuthorizationService.RequireWorkItemAccessAsync"/>.
/// </summary>
public readonly record struct AuthorizedWorkItemId
{
    /// <summary>
    /// Internal-only constructor to preserve the authorization invariant at the API boundary.
    /// </summary>
    internal AuthorizedWorkItemId(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public void ThrowIfDefault()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("AuthorizedWorkItemId must not be default.");
        }
    }
}

/// <summary>
/// Work item link identifier that is guaranteed to have passed <see cref="IResourceAuthorizationService.RequireWorkItemLinkAccessAsync"/>.
/// </summary>
public readonly record struct AuthorizedWorkItemLinkId
{
    /// <summary>
    /// Internal-only constructor to preserve the authorization invariant at the API boundary.
    /// </summary>
    internal AuthorizedWorkItemLinkId(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public void ThrowIfDefault()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("AuthorizedWorkItemLinkId must not be default.");
        }
    }
}
