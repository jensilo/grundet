namespace Grundet.Api.Common.Errors;

public static class ProblemTypes
{
    public const string Validation = "https://grundet.app/errors/validation";
    public const string NotFound = "https://grundet.app/errors/not-found";
    public const string Forbidden = "https://grundet.app/errors/forbidden";
    public const string Unauthorized = "https://grundet.app/errors/unauthorized";
    public const string Conflict = "https://grundet.app/errors/conflict";
    public const string InvalidToken = "https://grundet.app/errors/invalid-token";
    public const string ServerMisconfiguration = "https://grundet.app/errors/server-misconfiguration";
    public const string JiraConnectionRequired = "https://grundet.app/errors/jira-connection-required";
    public const string TooManyRequests = "https://grundet.app/errors/too-many-requests";
    public const string OnboardingRequired = "https://grundet.app/errors/onboarding-required";
    public const string PrivateAlphaAccessRequired = "https://grundet.app/errors/private-alpha-access-required";
    public const string SoloWorkspaceAlreadyExists = "https://grundet.app/errors/solo-workspace-already-exists";
    public const string SoloWorkspaceRequired = "https://grundet.app/errors/solo-workspace-required";
    public const string QuotaExceeded = "https://grundet.app/errors/quota-exceeded";
    public const string ReasonRequiresChange = "https://grundet.app/errors/reason-requires-change";
}
