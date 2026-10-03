namespace IHaveBeen.Web.Components.Sharing;

public sealed class ShareDraft
{
    public string Label { get; set; } = "Friends";
    public string Hours { get; set; } = "168";
    public string CustomExpiration { get; set; } = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm");
}
