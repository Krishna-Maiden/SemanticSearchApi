public class UserIntent
{
    public string RawQuery { get; set; } = string.Empty;
    public string FocusField { get; set; } = string.Empty;
    public string? ChartType { get; set; }
    public string? TimeFilter { get; set; }
    public string? Product { get; set; }
    public CompanyMentions? CompanyMentions { get; set; }
}

public class CompanyMentions
{
    public string? Exporter { get; set; }
    public string? Importer { get; set; }
}
