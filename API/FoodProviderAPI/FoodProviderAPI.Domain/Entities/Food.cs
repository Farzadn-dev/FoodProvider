using System.ComponentModel.DataAnnotations;

namespace FoodProviderAPI.Domain.Entities
{
    public class Food
    {
        [Required]
        public required string Name { get; set; }

        [Required]
        public required string Category { get; set; }
    }
}
