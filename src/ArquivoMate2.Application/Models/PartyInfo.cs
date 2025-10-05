using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Models
{

    public class PartyInfo
    {
        public required string UserId { get; init; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string HouseNumber { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        public string SearchText
        {
            get
            {
                return $"{FirstName} {LastName} {CompanyName} {Street} {HouseNumber} {PostalCode} {City}".ToLowerInvariant();
            }
        }

        public Guid Id { get; set; }
    }
}
