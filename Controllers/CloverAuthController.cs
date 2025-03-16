using CloverAuthAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CloverAuthAPI.Controllers;

[Route("api/auth")]
[ApiController]
public class CloverAuthController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly CloverSettings _cloverSettings;

    public CloverAuthController(HttpClient httpClient, IOptionsSnapshot<CloverSettings> cloverSettings)
    {
        _httpClient = httpClient;
        _cloverSettings = cloverSettings.Value;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        string redirectUrl = $"{_cloverSettings.CloverBaseUrl}/oauth/v2/authorize?client_id={_cloverSettings.ClientId}&response_type=code&redirect_uri={_cloverSettings.RedirectUrl}";
        return Redirect(redirectUrl);
    }

    [HttpGet("oauth_callback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Authorization code is missing.");

        string tokenUrl = $"{_cloverSettings.CloverBaseUrl}/oauth/v2/token";

        var requestData = new
        {
            client_id = _cloverSettings.ClientId,
            client_secret = _cloverSettings.ClientSecret,
            code = code,
            grant_type = "authorization_code",
            redirect_uri = _cloverSettings.RedirectUrl
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = requestContent
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseJson);

            string accessToken = jsonDocument.RootElement.GetProperty("access_token").GetString()!;
            long access_token_expiration = jsonDocument.RootElement.GetProperty("access_token_expiration").GetInt64()!;
            string refreshToken = jsonDocument.RootElement.GetProperty("refresh_token").GetString()!;
            long refresh_token_expiration = jsonDocument.RootElement.GetProperty("refresh_token_expiration").GetInt64()!;

            return Ok(new { access_token = accessToken,
                refresh_token = refreshToken, 
                access_token_expiration = access_token_expiration,
                refresh_token_expiration = refresh_token_expiration
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, $"Error retrieving access token: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return StatusCode(500, $"Error parsing JSON response: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Unexpected error: {ex.Message}");
        }
    }

    [HttpGet("refresh_token")]
    public async Task<IActionResult> RefreshToken([FromQuery] string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return BadRequest("Refresh token is missing.");

        string tokenUrl = $"{_cloverSettings.CloverBaseUrl}/oauth/v2/refresh";

        var requestData = new
        {
            client_id = _cloverSettings.ClientId,
            client_secret = _cloverSettings.ClientSecret,
            refresh_token = refreshToken,
            grant_type = "refresh_token"
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = requestContent
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseJson);

            string accessToken = jsonDocument.RootElement.GetProperty("access_token").GetString()!;
            string newRefreshToken = jsonDocument.RootElement.GetProperty("refresh_token").GetString()!;

            return Ok(new { access_token = accessToken, refresh_token = newRefreshToken });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, $"Error refreshing access token: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return StatusCode(500, $"Error parsing JSON response: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Unexpected error: {ex.Message}");
        }
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromHeader] string accessToken, [FromQuery] string merchantId)
    {
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(merchantId))
            return BadRequest("Cookie, Access token, and merchant ID are required.");

        var request = new HttpRequestMessage(HttpMethod.Get, $"{_cloverSettings.CloverBaseUrl}/v2/merchant/{merchantId}/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, $"Error retrieving orders: {ex.Message}");
        }
    }
}