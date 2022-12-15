using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using TestBuildingBlocks;

namespace JsonApiDotNetCoreTests.Startups;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public sealed class NoModelStateValidationStartup<TDbContext> : TestableStartup<TDbContext>
    where TDbContext : TestableDbContext
{
    protected override void SetJsonApiOptions(JsonApiOptions options)
    {
        base.SetJsonApiOptions(options);

        options.ValidateModelState = false;
    }
}
