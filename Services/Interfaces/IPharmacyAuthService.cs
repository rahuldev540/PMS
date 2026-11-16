using AuthDemo.DTOs;

namespace AuthDemo.Services.Interfaces;

public interface IPharmacyAuthService
{
    Task<PharmacyLoginResponseDto?> LoginAsync(LoginDto dto);
}

