namespace ECommerce.Core.DTOs.Admin;

// What an admin sees about a customer. Built with Select, so the password hash is never even read.
public record CustomerDto(int Id, string FullName, string Email, string? Phone, int OrderCount, DateTime CreatedAt);
