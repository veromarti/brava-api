namespace Brava.Domain.Admins;

public enum AdminRole
{
    Admin,
    SuperAdmin,
}

public class Admin
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required AdminRole Role { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}