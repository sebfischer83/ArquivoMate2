using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Domain.Document
{
    public sealed class SearchDocument
    {
        public Guid Id { get; set; }

        public string Content { get; set; } = string.Empty;

        public IEnumerable<string> Keywords { get; set; } = Enumerable.Empty<string>();
        public string Summary { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        // New: non-owner users with access (shares, group shares)
        public IReadOnlyCollection<string> AllowedUserIds { get; set; } = Array.Empty<string>();

        public static SearchDocument FromDocument(Document document, IReadOnlyCollection<string>? allowedUserIds = null)
        {
            return new SearchDocument
            {
                Id = document.Id,
                Content = document.Content,
                Keywords = document.Keywords,
                Summary = document.Summary,
                Title = document.Title,
                UserId = document.UserId,
                AllowedUserIds = allowedUserIds ?? Array.Empty<string>()
            };
        }
    }
}
