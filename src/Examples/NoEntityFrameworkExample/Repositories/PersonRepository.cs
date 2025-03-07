using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using NoEntityFrameworkExample.Data;
using NoEntityFrameworkExample.Models;

namespace NoEntityFrameworkExample.Repositories;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public sealed class PersonRepository : InMemoryResourceRepository<Person, long>
{
    public PersonRepository(IResourceGraph resourceGraph, IResourceFactory resourceFactory)
        : base(resourceGraph, resourceFactory)
    {
    }

    protected override IEnumerable<Person> GetDataSource()
    {
        return Database.People;
    }
}
