using System;

namespace ArquivoMate2.Shared.Models.Sharing;

public class CreateDocumentShareRequest
{
    public ShareTarget Target { get; set; } = new();

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
