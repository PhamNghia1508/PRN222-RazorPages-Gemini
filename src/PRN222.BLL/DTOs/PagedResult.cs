namespace PRN222.BLL.DTOs;

public interface IPagedResult
{
  int Page { get; }
  int PageSize { get; }
  int TotalItems { get; }
  int TotalPages { get; }
  int FirstItemIndex { get; }
  int LastItemIndex { get; }
  bool HasPreviousPage { get; }
  bool HasNextPage { get; }
}

public sealed class PagedResult<T> : IPagedResult
{
  public static readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };
  public const int DefaultPageSize = 25;

  public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
  public int Page { get; init; }
  public int PageSize { get; init; }
  public int TotalItems { get; init; }
  public int TotalPages { get; init; }
  public int FirstItemIndex { get; init; }
  public int LastItemIndex { get; init; }

  public bool HasPreviousPage => Page > 1;
  public bool HasNextPage => TotalPages > 0 && Page < TotalPages;

  public static PagedResult<T> Create(IEnumerable<T> source, int page, int pageSize)
  {
    ArgumentNullException.ThrowIfNull(source);

    var items = source as IReadOnlyList<T> ?? source.ToList();
    return CreateFromList(items, page, NormalizePageSize(pageSize), items.Count);
  }

  public static PagedResult<T> CreateFromPage(
      IReadOnlyList<T> pageItems,
      int page,
      int pageSize,
      int totalItems)
  {
    ArgumentNullException.ThrowIfNull(pageItems);

    var normalizedPageSize = NormalizePageSize(pageSize);
    var totalPages = totalItems == 0
        ? 0
        : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
    var normalizedPage = NormalizePage(page, totalPages);
    var firstIndex = totalItems == 0 ? 0 : ((normalizedPage - 1) * normalizedPageSize) + 1;
    var lastIndex = totalItems == 0 ? 0 : Math.Min(firstIndex + pageItems.Count - 1, totalItems);

    return new PagedResult<T>
    {
      Items = pageItems,
      Page = normalizedPage,
      PageSize = normalizedPageSize,
      TotalItems = totalItems,
      TotalPages = totalPages,
      FirstItemIndex = firstIndex,
      LastItemIndex = lastIndex
    };
  }

  public static int NormalizePageSize(int pageSize) =>
      AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

  private static PagedResult<T> CreateFromList(IReadOnlyList<T> source, int page, int pageSize, int totalItems)
  {
    var totalPages = totalItems == 0
        ? 0
        : (int)Math.Ceiling(totalItems / (double)pageSize);

    var normalizedPage = NormalizePage(page, totalPages);
    var pageItems = totalItems == 0
        ? new List<T>()
        : source.Skip((normalizedPage - 1) * pageSize).Take(pageSize).ToList();

    var firstIndex = totalItems == 0 ? 0 : ((normalizedPage - 1) * pageSize) + 1;
    var lastIndex = totalItems == 0 ? 0 : Math.Min(firstIndex + pageItems.Count - 1, totalItems);

    return new PagedResult<T>
    {
      Items = pageItems,
      Page = normalizedPage,
      PageSize = pageSize,
      TotalItems = totalItems,
      TotalPages = totalPages,
      FirstItemIndex = firstIndex,
      LastItemIndex = lastIndex
    };
  }

  private static int NormalizePage(int page, int totalPages)
  {
    if (totalPages == 0)
    {
      return 1;
    }

    var normalizedPage = page < 1 ? 1 : page;

    if (normalizedPage > totalPages)
    {
      normalizedPage = totalPages;
    }

    return normalizedPage;
  }
}