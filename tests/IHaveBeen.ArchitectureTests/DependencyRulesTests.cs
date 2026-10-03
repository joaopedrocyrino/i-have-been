using System.Reflection;
using System.Xml.Linq;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.ArchitectureTests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Domain_has_no_application_infrastructure_or_framework_dependency() => AssertCoreReferences(typeof(TravelLog).Assembly, []);
    [Fact]
    public void Application_depends_only_on_domain_and_the_base_class_library() => AssertCoreReferences(typeof(CreateTravelLogHandler).Assembly, ["IHaveBeen.Domain"]);
    [Theory]
    [InlineData("IHaveBeen.Domain", "")]
    [InlineData("IHaveBeen.Application", "IHaveBeen.Domain")]
    [InlineData("IHaveBeen.Infrastructure", "IHaveBeen.Application")]
    public void Project_reference_directions_are_enforced(string project, string allowed)
    {
        var document = XDocument.Load(Path.Combine(Root(), "src", project, project + ".csproj"));
        var projects = document.Descendants("ProjectReference").Select(x => Path.GetFileNameWithoutExtension(x.Attribute("Include")!.Value)).ToArray();
        if (allowed.Length == 0) Assert.Empty(projects); else Assert.Equal([allowed], projects);
        if (project != "IHaveBeen.Infrastructure") { Assert.Empty(document.Descendants("PackageReference")); Assert.Empty(document.Descendants("FrameworkReference")); }
    }
    [Fact]
    public void Web_features_do_not_query_databases_or_reference_physical_adapters()
    {
        foreach (var path in Directory.EnumerateFiles(Path.Combine(Root(), "src", "IHaveBeen.Web", "Features"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            foreach (var forbidden in new[] { "IHaveBeen.Infrastructure", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Identity", "Amazon.", "AppDbContext", "GarageObjectStorage" })
                Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }
    [Fact]
    public void Domain_state_cannot_be_set_from_outer_layers()
    {
        foreach (var type in typeof(TravelLog).Assembly.GetTypes().Where(t => t.IsClass && t.Namespace is { } ns && (ns.EndsWith("TravelLogs") || ns.EndsWith("Media") || ns.EndsWith("Sharing") || ns.EndsWith("Experiences")) && !t.Name.EndsWith("Details") && t.Name != "MediaTypes"))
            foreach (var property in type.GetProperties()) Assert.False(property.SetMethod?.IsPublic ?? false, type.Name + "." + property.Name + " exposes a public setter");
    }
    private static void AssertCoreReferences(Assembly assembly, string[] allowed)
    {
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            Assert.False(reference.Name!.StartsWith("Microsoft.", StringComparison.Ordinal) || reference.Name.StartsWith("Amazon", StringComparison.Ordinal));
            if (reference.Name.StartsWith("IHaveBeen.", StringComparison.Ordinal)) Assert.Contains(reference.Name, allowed);
        }
    }
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) { if (File.Exists(Path.Combine(current.FullName, "IHaveBeen.slnx"))) return current.FullName; current = current.Parent; }
        throw new InvalidOperationException("Run architecture tests from the source checkout.");
    }
}
