namespace AuthDemo.DTOs.Address
{
    public class PincodeAddressDto
    {
        public string Pincode { get; set; } = string.Empty;

        public string District { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public List<string> Areas { get; set; } = new();
    }
}