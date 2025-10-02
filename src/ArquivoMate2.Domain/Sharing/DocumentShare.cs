using System;
using ArquivoMate2.Shared.Models.Sharing;

namespace ArquivoMate2.Domain.Sharing;

public class DocumentShare
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public ShareTarget Target { get; set; } = new();

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    public string? GrantedBy { get; set; }

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
