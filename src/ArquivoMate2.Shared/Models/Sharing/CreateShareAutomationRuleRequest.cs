using System;

namespace ArquivoMate2.Shared.Models.Sharing;

public class CreateShareAutomationRuleRequest
{
    public ShareTarget Target { get; set; } = new();

    public ShareAutomationScope Scope { get; set; } = ShareAutomationScope.AllDocuments;

    public DocumentPermissions Permissions { get; set; } = DocumentPermissions.Read;

    [Obsolete("Use Permissions")]
    public bool CanEdit
    {
        get => Permissions.HasFlag(DocumentPermissions.Edit);
        set
        {
            if (value)
            {
                Permissions |= DocumentPermissions.Edit | DocumentPermissions.Read;
            }
            else
            {
                Permissions &= ~DocumentPermissions.Edit;
            }
        }
    }
}
