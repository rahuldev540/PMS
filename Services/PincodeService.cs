using System.Text.Json;
using AuthDemo.DTOs.Address;

namespace AuthDemo.Services
{
    public class PincodeService
    {
        private readonly HttpClient _httpClient;

        public PincodeService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<PincodeAddressDto?> GetAddressByPincodeAsync(
            string pincode)
        {
            if (string.IsNullOrWhiteSpace(pincode) ||
                pincode.Length != 6 ||
                !pincode.All(char.IsDigit))
            {
                return null;
            }

            var response = await _httpClient.GetAsync(
                $"https://api.postalpincode.in/pincode/{pincode}"
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            if (root.GetArrayLength() == 0)
                return null;

            var result = root[0];

            if (!result.TryGetProperty("Status", out var status) ||
                status.GetString() != "Success")
            {
                return null;
            }

            if (!result.TryGetProperty(
                    "PostOffice",
                    out var postOffices) ||
                postOffices.ValueKind != JsonValueKind.Array ||
                postOffices.GetArrayLength() == 0)
            {
                return null;
            }

            var first = postOffices[0];

            var areas = postOffices
                .EnumerateArray()
                .Select(x =>
                    x.TryGetProperty("Name", out var name)
                        ? name.GetString()
                        : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            return new PincodeAddressDto
            {
                Pincode = pincode,

                District = first.TryGetProperty(
                    "District", out var district)
                    ? district.GetString() ?? ""
                    : "",

                State = first.TryGetProperty(
                    "State", out var state)
                    ? state.GetString() ?? ""
                    : "",

                Country = first.TryGetProperty(
                    "Country", out var country)
                    ? country.GetString() ?? "India"
                    : "India",

                Areas = areas!
            };
        }
    }
}