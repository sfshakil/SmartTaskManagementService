namespace SmartTaskManagement.Application.DTOs;

public record ImproveDescriptionRequest(string? Title, string Description);

public record ImproveDescriptionResponse(string OriginalDescription, string ImprovedDescription, string Model);
