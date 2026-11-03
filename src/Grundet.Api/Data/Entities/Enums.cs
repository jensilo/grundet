namespace Grundet.Api.Data.Entities;

public enum TeamRole
{
    Lead,
    Contributor
}

public enum TeamPlan
{
    Solo,
    Power,
    Expert,
    TeamS,
    TeamM,
    TeamL,
    TeamAlpha,
    TeamUnlimited
}

public enum RequirementWorkItemLimitStatus
{
    WithinLimit,
    ApproachingLimit,
    OverLimitGrace,
    EnforcementActive
}

public enum RequirementStatus
{
    Draft,
    Active,
    Archived,
    WontDo
}

public enum RequirementPriority
{
    Must,
    Should,
    Could,
    Wont
}

public enum RequirementChange
{
    Created,
    Updated,
    Deleted
}

public enum WorkItemStatus
{
    Todo,
    InProgress,
    Done,
    WontDo
}

public enum WorkItemPriority
{
    Low,
    Medium,
    High
}

public enum WorkItemType
{
    Feature,
    Bug,
    Task
}

public enum WorkItemChange
{
    Created,
    Updated,
    Deleted
}

public enum WorkItemLinkChange
{
    Linked,
    Unlinked
}

public enum AlphaWaitlistAccountType
{
    Personal,
    Business
}

public enum AlphaWaitlistTeamSize
{
    TwoToFive,
    SixToFifteen,
    SixteenToFifty,
    FiftyPlus
}

public enum AlphaWaitlistLineOfWork
{
    ProductDevelopment,
    AgencyConsultancy,
    NonDevelopment,
    Other
}

public enum AlphaWaitlistStatus
{
    Pending,
    Approved,
    Declined
}
