using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Admin;

// Create and rename. The slug is made from the name on the server.
public class SaveCategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;
}
