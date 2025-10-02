using System;
using ArquivoMate2.Shared.Models.Sharing;

namespace ArquivoMate2.Domain.Sharing;

public class ShareAutomationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string OwnerUserId { get; set; } = string.Empty;

    public ShareTarget Target { get; set; } = new();

    public ShareAutomationScope Scope { get; set; } = ShareAutomationScope.AllDocuments;

    private DocumentPermissions _permissions = DocumentPermissions.Read;

    public DocumentPermissions Permissions
    {
        get => _permissions;
        set => _permissions = NormalizePermissions(value);
    }

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
                Permissions = Permissions & ~DocumentPermissions.Edit;
            }
        }
    }

    private static DocumentPermissions NormalizePermissions(DocumentPermissions permissions)
    {
        if (permissions == DocumentPermissions.None)
        {
            return DocumentPermissions.Read;
        }

        return permissions.HasFlag(DocumentPermissions.Read)
            ? permissions
            : permissions | DocumentPermissions.Read;
    }
}
