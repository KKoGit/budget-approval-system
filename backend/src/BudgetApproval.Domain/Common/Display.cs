using System.Globalization;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Domain.Common;

/// <summary>
/// Formats values for text that people read: rule messages and audit history. Audit entries are stored as
/// written, so they must already read well; they are never re-rendered by the client.
/// </summary>
public static class Display
{
    /// <summary>US dollars with cents, independent of server culture: 75000 → "$75,000.00".</summary>
    public static string Money(decimal amount) =>
        amount.ToString("$#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>Human-readable category name, matching the labels used in the web app.</summary>
    public static string Category(BudgetCategory category) => category switch
    {
        BudgetCategory.ProfessionalServices => "Professional services",
        _ => category.ToString()
    };
}
