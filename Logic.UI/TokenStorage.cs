using System;

namespace Logic.UI.ViewModels
{
    public static class TokenStorage
    {
        private static string _jwtToken;
        private static Guid _userId;
        private static string _username;

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

        public static void ClearToken()
        {
            _jwtToken = null;
            _userId = Guid.Empty;
            _username = null;
        }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(_jwtToken);
    }
}