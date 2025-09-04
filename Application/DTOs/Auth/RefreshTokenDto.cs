using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    public class RefreshTokenDto
    {
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; }

        [Required(ErrorMessage = "Access token is required")]
        public string AccessToken { get; set; }
    }
}
