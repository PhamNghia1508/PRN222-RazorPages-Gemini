using FluentAssertions;
using PRN222.BLL.DTOs;

namespace PRN222.Tests.Web;

public class PagedResultTests
{
  [Fact]
  public void Create_ShouldClampInvalidPageAndPageSize()
  {
    var source = Enumerable.Range(1, 60).ToList();

    var result = PagedResult<int>.Create(source, page: -3, pageSize: 999);

    result.Page.Should().Be(1);
    result.PageSize.Should().Be(25);
    result.TotalItems.Should().Be(60);
    result.TotalPages.Should().Be(3);
    result.Items.Should().Equal(Enumerable.Range(1, 25));
    result.FirstItemIndex.Should().Be(1);
    result.LastItemIndex.Should().Be(25);
  }

  [Fact]
  public void Create_ShouldClampPageBeyondTotalPages()
  {
    var source = Enumerable.Range(1, 12).ToList();

    var result = PagedResult<int>.Create(source, page: 10, pageSize: 10);

    result.Page.Should().Be(2);
    result.TotalPages.Should().Be(2);
    result.Items.Should().Equal(11, 12);
    result.FirstItemIndex.Should().Be(11);
    result.LastItemIndex.Should().Be(12);
    result.HasPreviousPage.Should().BeTrue();
    result.HasNextPage.Should().BeFalse();
  }

  [Fact]
  public void Create_ShouldUsePageOneForEmptySource()
  {
    var result = PagedResult<int>.Create(Array.Empty<int>(), page: 5, pageSize: 10);

    result.Page.Should().Be(1);
    result.TotalItems.Should().Be(0);
    result.TotalPages.Should().Be(0);
    result.Items.Should().BeEmpty();
    result.FirstItemIndex.Should().Be(0);
    result.LastItemIndex.Should().Be(0);
  }
}