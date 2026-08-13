using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace Rebel.Domain.Entities
{
    public class Product : IValidatableObject
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsAvailable { get; set; } = true;

        public bool IsPopular { get; set; }

        public bool IsSpicy { get; set; }

        public bool IsVegetarian { get; set; }

        public bool IsVegan { get; set; }

        public bool IsGlutenFree { get; set; }

        public bool ContainsNuts { get; set; }

        public bool IsLimited { get; set; }

        public bool IsPromo { get; set; }

        [StringLength(60)]
        [Display(Name = "Country of origin")]
        public string? OriginCountry { get; set; }

        [StringLength(80)]
        [Display(Name = "Beer style")]
        public string? BeerStyle { get; set; }

        [Range(0, 25)]
        [Display(Name = "Alcohol by volume")]
        public decimal? AlcoholByVolume { get; set; }

        [Range(1, 5)]
        [Display(Name = "Body")]
        public int? BodyLevel { get; set; }

        [Range(1, 5)]
        [Display(Name = "Bitterness")]
        public int? BitternessLevel { get; set; }

        [Range(1, 5)]
        [Display(Name = "Sweetness")]
        public int? SweetnessLevel { get; set; }

        [Range(1, 5)]
        [Display(Name = "Acidity")]
        public int? AcidityLevel { get; set; }

        [StringLength(300)]
        [Display(Name = "Flavour notes")]
        public string? FlavorNotes { get; set; }

        [StringLength(300)]
        [Display(Name = "Pairing tags")]
        public string? PairingTags { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAtUtc { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        public Category? Category { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            var normalizedName = Regex.Replace(Name.Trim(), @"\s+", " ");
            var words = normalizedName.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 2 || words.Length % 2 != 0)
            {
                yield break;
            }

            var half = words.Length / 2;
            var repeatsExactly = words
                .Take(half)
                .SequenceEqual(
                    words.Skip(half),
                    StringComparer.OrdinalIgnoreCase);

            if (repeatsExactly)
            {
                yield return new ValidationResult(
                    "Enter the product name once; it appears to be repeated.",
                    [nameof(Name)]);
            }
        }
    }
}
