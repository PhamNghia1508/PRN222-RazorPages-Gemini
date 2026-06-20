using System.Globalization;

namespace PRN222.Web.Models.Pagination;

public static class PagerRouteValueBuilder
{
    public static Dictionary<string, string?> Build(
        IReadOnlyDictionary<string, string?> currentValues,
        int page)
    {
        return BuildCore(currentValues, page, null);
    }

    public static Dictionary<string, string?> BuildForPageSize(
        IReadOnlyDictionary<string, string?> currentValues,
        int pageSize)
    {
        return BuildCore(currentValues, page: 1, pageSize);
    }

    private static Dictionary<string, string?> BuildCore(
        IReadOnlyDictionary<string, string?> currentValues,
        int page,
        int? pageSize)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in currentValues)
        {
            if (string.Equals(key, "page", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(key, "pageSize", StringComparison.OrdinalIgnoreCase) && pageSize.HasValue)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
            }
        }

        values["page"] = page.ToString(CultureInfo.InvariantCulture);

        if (pageSize.HasValue)
        {
            values["pageSize"] = pageSize.Value.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }
}