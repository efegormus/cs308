namespace OnlineStore.Dtos;

// What the API returns for a category (not the EF entity itself).
public record CategoryDto(int Id, string Name, string? Description);
