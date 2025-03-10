namespace Drocsid.HenrikDennis2025.Core.Models
{
    public static class TokenStorage
    {
        private static string _jwtToken;
        private static Guid _userId;
        private static string _username;
        private static string _currentNodeEndpoint;
        private static string _registryEndpoint;
        private static DateTime _tokenExpiresAt;

        public static string JwtToken
        {
            get => _jwtToken;
            set => _jwtToken = value;
        }

        public static Guid UserId
        {
            get => _userId;
            set => _userId = value;
        }

        public static string Username
        {
            get => _username;
            set => _username = value;
        }

        public static string CurrentNodeEndpoint
        {
            get => _currentNodeEndpoint;
            set => _currentNodeEndpoint = value;
        }

        public static string RegistryEndpoint
        {
            get => _registryEndpoint;
            set => _registryEndpoint = value;
        }

        public static DateTime TokenExpiresAt
        {
            get => _tokenExpiresAt;
            set => _tokenExpiresAt = value;
        }

        public static void StoreConnectionInfo(string token, Guid userId, string username, string nodeEndpoint, string registryEndpoint, DateTime expiresAt)
        {
            _jwtToken = token;
            _userId = userId;
            _username = username;
            _currentNodeEndpoint = nodeEndpoint;
            _registryEndpoint = registryEndpoint;
            _tokenExpiresAt = expiresAt;
        }

        public static void ClearToken()
        {
            _jwtToken = null;
            _userId = Guid.Empty;
            _username = null;
            _currentNodeEndpoint = null;
            _registryEndpoint = null;
            _tokenExpiresAt = DateTime.MinValue;
        }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(_jwtToken);

        public static bool IsTokenExpired => _tokenExpiresAt < DateTime.UtcNow;

        public static TimeSpan TimeUntilExpiration => _tokenExpiresAt - DateTime.UtcNow;
    }
}