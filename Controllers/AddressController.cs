using AuthDemo.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly PincodeService _pincodeService;

        public AddressController(PincodeService pincodeService)
        {
            _pincodeService = pincodeService;
        }

        [HttpGet("pincode/{pincode}")]
        public async Task<IActionResult> GetAddressByPincode(
            string pincode)
        {
            if (string.IsNullOrWhiteSpace(pincode) ||
                pincode.Length != 6 ||
                !pincode.All(char.IsDigit))
            {
                return BadRequest(new
                {
                    message = "Please enter a valid 6-digit PIN code."
                });
            }

            var address =
                await _pincodeService.GetAddressByPincodeAsync(pincode);

            if (address == null)
            {
                return NotFound(new
                {
                    message = "No address found for this PIN code."
                });
            }

            return Ok(address);
        }
    }
}