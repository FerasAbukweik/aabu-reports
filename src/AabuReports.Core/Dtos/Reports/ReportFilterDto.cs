using System.Linq.Expressions;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Reports;

/// <summary>
/// every way to filter reports, shared by the report list endpoints (table).
/// A null (or empty) field means "no filter" for it, and the filled fields are combined with AND.
/// The text fields (Search, Id, Title, Description) match when the text is contained in the value, they do not have to be equal.
/// </summary>
public class ReportFilterDto
{
    public ReportStateEnum? State { get; set; }
    public ReportCategory? Category { get; set; }
    public ReportSeverity? Severity { get; set; }

    /// <summary> one text for the search box: it is found in the report id (with or without the # and the dashes), the title of the report's location or the description </summary>
    public string? Search { get; set; }

    /// <summary> the id of the report contains this text (with or without the # and the dashes) </summary>
    public string? Id { get; set; }

    /// <summary> the title of the report's location contains this text </summary>
    public string? Title { get; set; }

    /// <summary> the description of the report contains this text </summary>
    public string? Description { get; set; }

    /// <summary> the report was rated with exactly this rate (1-5) </summary>
    public int? Rate { get; set; }

    /// <summary> only the reports of this location </summary>
    public Guid? LocationId { get; set; }

    /// <summary> reports sent on or after this date </summary>
    public DateTime? FromDate { get; set; }

    /// <summary> reports sent on or before this date </summary>
    public DateTime? ToDate { get; set; }

    /// <summary> the filters as a predicate for the queries (a filter that is not set always passes) </summary>
    public Expression<Func<Report, bool>> ToPredicate()
    {
        // plain locals, so the query gets them as parameters
        var state = State;
        var category = Category;
        var severity = Severity;
        var rate = Rate;
        var locationId = LocationId;
        var from = FromDate;
        var to = ToDate;
        var id = NormalizeId(Id);
        var searchId = NormalizeId(Search);
        var search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        var title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim();
        var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        return r =>
            (state == null || r.State == state) &&
            (category == null || r.Category == category) &&
            (severity == null || r.Severity == severity) &&
            (rate == null || r.Rate == rate) &&
            (locationId == null || r.LocationId == locationId) &&
            (from == null || r.ReportDate >= from) &&
            (to == null || r.ReportDate <= to) &&
            (id == null || r.Id.ToString().Replace("-", "").Contains(id)) &&
            (title == null || r.Location!.Title.Contains(title)) &&
            (description == null || r.Description.Contains(description)) &&
            (search == null ||
             r.Location!.Title.Contains(search) ||
             r.Description.Contains(search) ||
             (searchId != null && r.Id.ToString().Replace("-", "").Contains(searchId)));
    }

    /// <summary> "#3F2A9C1E" or "3f2a-9c1e" -> "3F2A9C1E" (how the id looks without the dashes), null if nothing is left.
    /// also used to look a report up by the short number the user sees (GET /report/{id}) </summary>
    public static string? NormalizeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Replace("#", "").Replace("-", "").Replace(" ", "");

        return normalized.Length == 0 ? null : normalized;
    }
}
