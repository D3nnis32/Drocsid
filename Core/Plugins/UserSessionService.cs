using System.Net.Http.Json;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
    /// Implementation of user session service
    /// </summary>
    public class UserSessionService : IUserSessionService
    {
        private readonly string _apiBaseUrl;
        private readonly IPluginLogger _logger;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public UserSessionService(string apiBaseUrl, IPluginLogger logger)
        {
            _apiBaseUrl = apiBaseUrl ?? throw new ArgumentNullException(nameof(apiBaseUrl));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Current user ID
        /// </summary>
        public Guid CurrentUserId => TokenStorage.UserId;
        
        /// <summary>
        /// Current user's username
        /// </summary>
        public string CurrentUsername => TokenStorage.Username;
        
        /// <summary>
        /// JWT token for authentication
        /// </summary>
        public string AuthToken => TokenStorage.JwtToken;
        
        /// <summary>
        /// Base API URL for the current user session
        /// </summary>
        public string ApiBaseUrl => _apiBaseUrl;
        
        /// <summary>
        /// Get information about a channel
        /// </summary>
        public async Task<ChannelInfo> GetChannelInfoAsync(Guid channelId)
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
                    
                    var response = await httpClient.GetAsync($"{ApiBaseUrl}/api/channels/{channelId}");
                    response.EnsureSuccessStatusCode();
                    
                    var channel = await response.Content.ReadFromJsonAsync<ChannelInfo>();
                    return channel;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting channel info for {channelId}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Get information about a user
        /// </summary>
        public async Task<UserInfo> GetUserInfoAsync(Guid userId)
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
                    
                    var response = await httpClient.GetAsync($"{ApiBaseUrl}/api/users/{userId}");
                    response.EnsureSuccessStatusCode();
                    
                    var user = await response.Content.ReadFromJsonAsync<UserInfo>();
                    return user;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting user info for {userId}", ex);
                throw;
            }
        }
    }