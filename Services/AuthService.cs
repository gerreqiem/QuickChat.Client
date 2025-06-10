using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuickChat.Client.Models;

namespace QuickChat.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;

        public AuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://localhost:5001/");
        }

        public async Task<(bool Success, string Message)> RegisterAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Username and password are required.");
            }

            var content = new StringContent(JsonConvert.SerializeObject(new { username, password }), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/auth/register", content);
            return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Registration successful!" : "Registration failed.");
        }

        public async Task<(bool Success, string Token, string Message)> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, null, "Username and password are required.");
            }

            var content = new StringContent(JsonConvert.SerializeObject(new { username, password }), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/auth/login", content);

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, "Login failed.");
            }

            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(await response.Content.ReadAsStringAsync());
            return (true, tokenResponse.Token, "Login successful!");
        }

        public async Task<(bool Success, string Token, string Message)> UpdateUsernameAsync(Guid userId, string newUsername)
        {
            if (string.IsNullOrWhiteSpace(newUsername))
            {
                return (false, null, "New username cannot be empty.");
            }

            var content = new StringContent(JsonConvert.SerializeObject(new { NewUsername = newUsername }), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"api/users/{userId}", content);

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, "Failed to update username.");
            }

            var responseData = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            return (true, responseData.Token, "Username updated successfully! Please log in again with the new username.");
        }
    }
}