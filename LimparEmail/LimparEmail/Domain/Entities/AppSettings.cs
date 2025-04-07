namespace LimparEmail.Domain.Entities;

public class AppSettings
{
    public string Environment { get; set; } = "Development"; 
    public bool SendEmail { get; set; } = false;
    public string Label { get; set; } = string.Empty;
    public string ProfileFolder { get; set; } = string.Empty;
    public string RecipientEmails { get; set; } = string.Empty;
    public string UrlBase { get; set; } = string.Empty;
    public string UrlBaseBetweenDates { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
}