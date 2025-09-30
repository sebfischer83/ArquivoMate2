namespace ArquivoMate2.Shared.Models.Sharing;

public class ShareTarget
{
    public ShareTargetType Type { get; set; } = ShareTargetType.User;

    public string Identifier { get; set; } = string.Empty;
}
