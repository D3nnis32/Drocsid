using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

[ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public AuthController(IUserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            // Validate credentials
            var isValid = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
            if (!isValid)
            {
                return Unauthorized("Invalid username or password");
            }

            // Get user details
            var users = await _userService.FindUsersAsync(u => u.Username == request.Username);
            var user = users.FirstOrDefault();
            if (user == null)
            {
                return Unauthorized("User not found");
            }

            // Generate JWT token
            var token = GenerateJwtToken(user.Id.ToString(), user.Username);

            return new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Username = user.Username
            };
        }

        private string GenerateJwtToken(string userId, string username)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"]));

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
    }