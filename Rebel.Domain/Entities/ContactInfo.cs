using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rebel.Domain.Entities
{
    public class ContactInfo
{
    public Guid Id { get; set; }
    public string Address { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string WorkingHours { get; set; } = null!;
    public string? MapEmbedUrl { get; set; }
    public string? WoltUrl { get; set; }
    public string? KorpaUrl { get; set; }
}
}
