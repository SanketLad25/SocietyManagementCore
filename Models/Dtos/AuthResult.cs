namespace SocietyManagementCore.Models.Dtos;

public record AuthResult(bool Success, string? Error, AuthResponse? Response);
