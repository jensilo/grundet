namespace Grundet.Api.Middleware;

public sealed class SkipOnboardingGuardMetadata;

public static class OnboardingGuardEndpointConventionBuilderExtensions
{
    public static TBuilder SkipOnboardingGuard<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new SkipOnboardingGuardMetadata());
        return builder;
    }
}
