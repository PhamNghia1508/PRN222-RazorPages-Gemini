using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PRN222.Web.Pages.TestSet;

namespace PRN222.Tests.Web;

public class TestSetPageTests
{
    [Fact]
    public void Seed_ShouldRejectManualGroundTruthImport()
    {
        var page = new IndexModel();

        var result = page.OnPostSeedAsync(courseId: 1);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
