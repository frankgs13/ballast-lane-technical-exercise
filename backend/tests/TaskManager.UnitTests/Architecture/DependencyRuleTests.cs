using System.Reflection;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Tasks;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.UnitTests.Architecture;

/// <summary>Turns the Clean Architecture dependency rule into a failing test instead of a convention.</summary>
public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(TaskItem).Assembly;
    private static readonly Assembly Application = typeof(ITaskService).Assembly;
    private static readonly Assembly Infrastructure = typeof(DatabaseOptions).Assembly;

    [Fact]
    public void Domain_DependsOnNothingFromTheSolution()
    {
        Assert.Empty(SolutionReferences(Domain));
    }

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        Assert.Equal(["TaskManager.Domain"], SolutionReferences(Application));
    }

    [Fact]
    public void Application_HasNoDataAccessOrWebDependencies()
    {
        var forbidden = new[] { "Microsoft.Data.SqlClient", "Microsoft.AspNetCore", "System.Data" };

        var references = Application.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(references, r => forbidden.Any(f => r.StartsWith(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApi()
    {
        Assert.DoesNotContain("TaskManager.Api", SolutionReferences(Infrastructure));
    }

    [Theory]
    [InlineData("EntityFramework")]
    [InlineData("Dapper")]
    [InlineData("MediatR")]
    public void NoProject_UsesForbiddenLibraries(string library)
    {
        var all = new[] { Domain, Application, Infrastructure, typeof(Program).Assembly }
            .SelectMany(a => a.GetReferencedAssemblies())
            .Select(a => a.Name!);

        Assert.DoesNotContain(all, name => name.Contains(library, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] SolutionReferences(Assembly assembly) =>
    [
        .. assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("TaskManager.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal),
    ];
}
