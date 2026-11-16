using AuthDemo.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthDemo.Helpers;

public class JwtHelper
{
    private readonly IConfiguration _configuration;

    public JwtHelper(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Role))
            throw new InvalidOperationException("User role is missing.");

        var jwtKey = _configuration["PharmacyJwt:Key"]
            ?? throw new InvalidOperationException(
                "PharmacyJwt:Key is missing.");

        var issuer = _configuration["PharmacyJwt:Issuer"]
            ?? "PharmacyOnlyApi";

        var audience = _configuration["PharmacyJwt:Audience"]
            ?? "PharmacyOnlyClients";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, user.Role),
            new("role", user.Role),
            new("userId", user.Id.ToString()),
            new("AccountStatus", user.AccountStatus)
        };

        // Only pharmacy-scoped roles receive tenant claims.
        // A global Super Admin must never receive pharmacy/branch claims.
        if (user.Role != "PharmacySuperAdmin")
        {
            claims.Add(new Claim("IsPharmacyAssigned",
                IsPharmacyAssigned(user).ToString().ToLowerInvariant()));

            if (user.PharmacyId.HasValue)
            {
                claims.Add(new Claim("PharmacyId", user.PharmacyId.Value.ToString()));
            }

            if (user.BranchId.HasValue)
            {
                claims.Add(new Claim(
                    "BranchId",
                    user.BranchId.Value.ToString()));
            }
        }

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private static bool IsPharmacyAssigned(User user)
    {
        if (user.Role == "PharmacySuperAdmin")
            return true;

        if (user.Role == "PharmacyAdmin")
        {
            return user.AccountStatus == "Active"
                && user.PharmacyId.HasValue;
        }

        if (user.Role == "Pharmacist")
        {
            return user.AccountStatus == "Active"
                && user.PharmacyId.HasValue
                && user.BranchId.HasValue;
        }

        return false;
    }
}
