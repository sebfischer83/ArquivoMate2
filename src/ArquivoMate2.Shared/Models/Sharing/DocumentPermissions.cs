using System;
using System.Text.Json.Serialization;
using ArquivoMate2.Shared.Serialization;

namespace ArquivoMate2.Shared.Models.Sharing;

[Flags]
[JsonConverter(typeof(DocumentPermissionsJsonConverter))]
public enum DocumentPermissions
{
    None = 0,
    Read = 1 << 0,
    Edit = 1 << 1,
    Delete = 1 << 2,
    All = Read | Edit | Delete
}
