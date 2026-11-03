using System.Diagnostics.CodeAnalysis;

namespace Grundet.Api.Features.Permissions;

/// <summary>
/// Discrete, namespaced capability identifiers describing what the current viewer may do.
/// Serializes as a JSON string (via the global <c>JsonStringEnumConverter</c>), so a collection
/// of <see cref="Permission"/> ships to the frontend as a string array. This is a plain enum on
/// purpose — a <c>[Flags]</c> bitfield would serialize to a combined value and break that contract.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "'Permission' is the deliberate domain name for the capability model and is the API/wire contract; renaming would obscure intent.")]
public enum Permission
{
    /// <summary>Rename the team (Team settings).</summary>
    TeamManageSettings,

    /// <summary>Delete the team.</summary>
    TeamDelete,

    /// <summary>Invite new members to the team.</summary>
    TeamInviteMembers,

    /// <summary>Change a member's role or remove a member (incl. resend/revoke invites).</summary>
    TeamManageMembers,

    // Future: RequirementEdit, RequirementDelete, FeatureCreate, ... added when per-resource roles exist.
}
