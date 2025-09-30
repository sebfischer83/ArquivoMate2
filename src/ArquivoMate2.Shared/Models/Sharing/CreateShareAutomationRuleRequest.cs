namespace ArquivoMate2.Shared.Models.Sharing;

public class CreateShareAutomationRuleRequest
{
    public ShareTarget Target { get; set; } = new();

    public ShareAutomationScope Scope { get; set; } = ShareAutomationScope.AllDocuments;
}
