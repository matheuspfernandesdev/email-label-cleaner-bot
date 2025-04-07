namespace LimparEmail.Domain.Entities;

public class BetweenDates(DateTime after, DateTime before)
{
    public string DayBefore { get; set; } = before.Day.ToString();
    public string MonthBefore { get; set; } = before.Month.ToString();
    public string YearBefore { get; set; } = before.Year.ToString();

    public string DayAfter { get; set; } = after.Day.ToString();
    public string MonthAfter { get; set; } = after.Month.ToString();
    public string YearAfter { get; set; } = after.Year.ToString();

    public string FormatUrl(string urlTemplate, string label)
    {
        return urlTemplate
            .Replace("{dayAfter}", DayAfter)
            .Replace("{monthAfter}", MonthAfter)
            .Replace("{yearAfter}", YearAfter)
            .Replace("{dayBefore}", DayBefore)
            .Replace("{monthBefore}", MonthBefore)
            .Replace("{yearBefore}", YearBefore)
            .Replace("{label}", label);
    }
}
