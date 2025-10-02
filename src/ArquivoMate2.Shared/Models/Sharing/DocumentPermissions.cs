using System;

namespace ArquivoMate2.Shared.Models.Sharing;

[Flags]
public enum DocumentPermissions
{
    None = 0,
    Read = 1 << 0,
    Edit = 1 << 1,
    Delete = 1 << 2,
    All = Read | Edit | Delete
}
