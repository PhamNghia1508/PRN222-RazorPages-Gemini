using FluentAssertions;
using PRN222.Web.Models.Pagination;

namespace PRN222.Tests.Web;

public class PagerRouteValueBuilderTests
{
  [Fact]
  public void Build_ShouldPreserveFiltersAndSetPage()
  {
    var current = new Dictionary<string, string?>
    {
      ["search"] = "rag",
      ["status"] = "Completed",
      ["pageSize"] = "25"
    };

    var result = PagerRouteValueBuilder.Build(current, page: 3);

    result["search"].Should().Be("rag");
    result["status"].Should().Be("Completed");
    result["pageSize"].Should().Be("25");
    result["page"].Should().Be("3");
  }

  [Fact]
  public void BuildForPageSize_ShouldResetToFirstPage()
  {
    var current = new Dictionary<string, string?>
    {
      ["courseId"] = "1",
      ["page"] = "4",
      ["pageSize"] = "25"
    };

    var result = PagerRouteValueBuilder.BuildForPageSize(current, pageSize: 50);

    result["courseId"].Should().Be("1");
    result["page"].Should().Be("1");
    result["pageSize"].Should().Be("50");
  }
}