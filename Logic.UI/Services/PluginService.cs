using System.Net.Http;
using System.Net.Http.Json;

namespace Logic.UI.ViewModels.Services;

public class PluginService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public PluginService(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public async Task<List<PluginInfo>> GetAvailablePluginsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<PluginInfo>>($"{_baseUrl}/api/plugins") 
                   ?? new List<PluginInfo>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching plugins: {ex.Message}");
            return new List<PluginInfo>();
        }
    }

    public async Task<bool> LoadPluginAsync(string pluginName)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/plugins/load?pluginName={Uri.EscapeDataString(pluginName)}", 
                null);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading plugin: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/plugins/{pluginId}/unload", 
                null);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error unloading plugin: {ex.Message}");
            return false;
        }
    }
}

// Model that matches the API's PluginInfo class
public class PluginInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public string Author { get; set; }
    public string State { get; set; }
    public string Type { get; set; }
}