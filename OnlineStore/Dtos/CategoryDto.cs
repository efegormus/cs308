namespace OnlineStore.Dtos;

// What the API returns for a category (not the EF entity itself). ParentId is null for a top-level category;
// the header builds its category menu from it (SCRUM-123).
public record CategoryDto(long Id, string Name, string? Description, long? ParentId = null);
