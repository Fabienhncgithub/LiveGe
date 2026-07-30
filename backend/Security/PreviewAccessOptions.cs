namespace FrontiereLiveGe.Api.Security;

public sealed class PreviewAccessOptions
{
    public const string SectionName = "PreviewAccess";

    public bool Enabled { get; set; }
    public string Username { get; set; } = "frontiere";
    public string Password { get; set; } = string.Empty;
}
