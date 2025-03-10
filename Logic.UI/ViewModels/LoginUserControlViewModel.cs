using Drocsid.HenrikDennis2025.Core.DTO;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Logic.UI.ViewModels
{
    public class LoginUserControlViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private string _userName;
        private string _password;
        private bool _isLoggingIn;
        private string _errorMessage;

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged(nameof(UserName));
                LoginCommand.InvalidateCanExecute();
            }
        }

        // We don't expose Password as a property because it's handled by the PasswordBox directly
        // for security reasons
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                // No need to raise property changed as this is set directly
                LoginCommand.InvalidateCanExecute();
            }
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set
            {
                _isLoggingIn = value;
                OnPropertyChanged(nameof(IsLoggingIn));
                LoginCommand.InvalidateCanExecute();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public RelayCommand LoginCommand { get; }
        
        // Event to signal successful login
        public event EventHandler LoginSuccessful;

        public RelayCommand OpenRegisterWindowCommand { get; }

        public event EventHandler RequestOpenRegisterWindow;
        public LoginUserControlViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5261/") };
            LoginCommand = new RelayCommand(
                execute: Login,
                canExecute: () => !string.IsNullOrWhiteSpace(UserName) &&
                                 !string.IsNullOrWhiteSpace(Password) &&
                                 !IsLoggingIn
            );
            OpenRegisterWindowCommand = new RelayCommand(OpenRegisterWindow);
        }

        public void Login()
        {
            // Start the async operation
            _ = LoginAsync();
        }
        private void OpenRegisterWindow()
        {
            // Fire an event that the code-behind will handle
            RequestOpenRegisterWindow?.Invoke(this, EventArgs.Empty);
        }
        private async Task LoginAsync()
        {
            try
            {
                IsLoggingIn = true;
                ErrorMessage = string.Empty;

                var request = new LoginRequest
                {
                    Username = UserName,
                    Password = Password
                };

                var response = await _httpClient.PostAsJsonAsync("api/gateway/connect", request);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

                    // Store the token and user info
                    TokenStorage.JwtToken = loginResponse.Token;
                    TokenStorage.UserId = loginResponse.UserId;
                    TokenStorage.Username = loginResponse.Username;

                    Console.WriteLine($"DEBUG: Login successful for user: {loginResponse.Username}");

                    // Notify subscribers about successful login
                    LoginSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? "Invalid username or password"
                        : $"Login failed: {response.StatusCode}. {errorContent}";

                    Console.WriteLine($"DEBUG: {ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception during login: {ex.Message}");
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // We're using the RelayCommand class instead of this custom implementation
}