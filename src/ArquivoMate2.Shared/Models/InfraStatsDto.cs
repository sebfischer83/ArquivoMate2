using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public sealed class TableSizeInfo
    {
        public string TableName { get; init; } = string.Empty;
        public string TotalSize { get; init; } = string.Empty;
        public long TotalSizeBytes { get; init; }
        public string TableSize { get; init; } = string.Empty;
        public string IndexSize { get; init; } = string.Empty;
        public long ApproxRows { get; init; }
    }

    public sealed record DatabaseStatsDto
    {
        public string PgVersion { get; init; } = string.Empty;
        public List<TableSizeInfo> TopTables { get; init; } = new();
        public List<TableSizeInfo> MartenTables { get; init; } = new();
        public string MartenVersion { get; init; } = string.Empty;
    }

    public sealed class InfraStatsDto
    {
        public object RedisInfo { get; init; } = new { };
        public Dictionary<string, long> KeyCounts { get; init; } = new();
        public object? MeiliHealth { get; init; }
        public object? MeiliIndexStats { get; init; }
        public object? MeiliVersion { get; init; }
        public DatabaseStatsDto? Database { get; init; }
    }
}
