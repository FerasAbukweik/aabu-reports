namespace aabu_reports.core.Enums;

// explicit values: the number is what is stored in the DB, so reordering or inserting
// in the middle must never change an existing category
public enum ReportCategory {
    Electrical = 0,
    Plumbing = 1,
    Roads = 2,
    Furniture = 3,
    Cleaning = 4,
    Other = 5,
}
