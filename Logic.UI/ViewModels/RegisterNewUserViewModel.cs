using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Logic.UI.ViewModels
{
    public class RegisterNewUserViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private string _username;
        private string _email;
        private string _password;
        private string _preferredRegion = "region1"; // Default value
        private bool _isRegistering;
        private string _errorMessage;
        private string _statusMessage;
        private int _registrationStep = 1;

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged(nameof(Email));
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string PreferredRegion
        {
            get => _preferredRegion;
            set
            {
                _preferredRegion = value;
                OnPropertyChanged(nameof(PreferredRegion));
            }
        }

        public bool IsRegistering
        {
            get => _isRegistering;
            set
            {
                _isRegistering = value;
                OnPropertyChanged(nameof(IsRegistering));
                CommandManager.InvalidateRequerySuggested();
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

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public int RegistrationStep
        {
            get => _registrationStep;
            set
            {
                _registrationStep = value;
                OnPropertyChanged(nameof(RegistrationStep));
                // Update status message based on step
                StatusMessage = $"Step {value} of 3: " + GetStepDescription(value);
            }
        }

        // Commands
        public RelayCommand RegisterCommand { get; }
        public RelayCommand CloseWindowCommand { get; }

        // Events
        public event EventHandler RequestClose;
        public event EventHandler<RegistrationCompletedEventArgs> RegistrationCompleted;

        public class RegistrationCompletedEventArgs : EventArgs
        {
            public string UserId { get; set; }
            public string Username { get; set; }
        }

        public RegisterNewUserViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5261/") }; // Registry URL
            StatusMessage = $"Step 1 of 3: {GetStepDescription(1)}";

            RegisterCommand = new RelayCommand(
                execute: Register,
                canExecute: () => !string.IsNullOrWhiteSpace(Username) &&
                                 !string.IsNullOrWhiteSpace(Email) &&
                                 !string.IsNullOrWhiteSpace(Password) &&
                                 !IsRegistering
            );

            CloseWindowCommand = new RelayCommand(
                execute: () => RequestClose?.Invoke(this, EventArgs.Empty)
            );
        }

        private string GetStepDescription(int step)
        {
            return step switch
            {
                1 => "Register user with registry",
                2 => "Log in and get node assignment",
                3 => "Sync with storage node",
                _ => "Completing registration"
            };
        }

        private void Register()
        {
            _ = CompleteRegistrationAsync();
        }

        private async Task CompleteRegistrationAsync()
        {
            try
            {
                IsRegistering = true;
                ErrorMessage = string.Empty;
                string userId = null;
                string token = null;
                string nodeUrl = null;

                // Step 1: Register user with registry
                RegistrationStep = 1;
                userId = await RegisterUserAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    // Registration failed, error message is set by RegisterUserAsync
                    return;
                }

                // Small delay for UX
                await Task.Delay(500);

                // Step 2: Get token and node assignment
                RegistrationStep = 2;
                (token, nodeUrl) = await GetTokenAndNodeAsync();
                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(nodeUrl))
                {
                    // Getting token failed, error message is set by GetTokenAndNodeAsync
                    return;
                }

                // Small delay for UX
                await Task.Delay(500);

                // Step 3: Verify token with storage node to sync
                RegistrationStep = 3;
                bool syncSuccess = await VerifyTokenWithNodeAsync(token, nodeUrl);
                if (!syncSuccess)
                {
                    ErrorMessage = "Failed to sync with storage node. Please try logging in later.";
                    return;
                }

                // Registration completed successfully
                Console.WriteLine("DEBUG: Registration completed successfully");

                // Notify about successful registration
                RegistrationCompleted?.Invoke(this, new RegistrationCompletedEventArgs
                {
                    UserId = userId,
                    Username = Username
                });

                // Close the registration window
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Registration error: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception during registration: {ex.Message}");
            }
            finally
            {
                IsRegistering = false;
            }
        }

        private async Task<string> RegisterUserAsync()
        {
            try
            {
                // Create user registration request
                var registerRequest = new
                {
                    username = Username,
                    email = Email,
                    password = Password,
                    preferredRegion = PreferredRegion
                };

                // Send registration request
                var response = await _httpClient.PostAsJsonAsync("api/registry/users/register", registerRequest);

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response to get the user ID
                    var result = await response.Content.ReadFromJsonAsync<dynamic>();
                    string userId = result.GetProperty("userId").GetString();

                    Console.WriteLine($"DEBUG: Step 1 - User registered successfully with ID: {userId}");
                    return userId;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Registration failed: {response.StatusCode}. {errorContent}";
                    Console.WriteLine($"DEBUG: Step 1 - {ErrorMessage}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Registration error: {ex.Message}";
                Console.WriteLine($"DEBUG: Step 1 - Exception during registration: {ex.Message}");
                return null;
            }
        }

        private async Task<(string token, string nodeUrl)> GetTokenAndNodeAsync()
        {
            try
            {
                // Create login request
                var loginRequest = new
                {
                    username = Username,
                    password = Password
                };

                // Send login request
                var response = await _httpClient.PostAsJsonAsync("api/gateway/connect", loginRequest);

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response to get token and node URL
                    var loginResponse = await response.Content.ReadFromJsonAsync<dynamic>();

                    string token = loginResponse.GetProperty("token").GetString();
                    string nodeEndpoint = loginResponse.GetProperty("nodeEndpoint").GetString();

                    Console.WriteLine($"DEBUG: Step 2 - Login successful. Token: {token.Substring(0, 20)}..., Node URL: {nodeEndpoint}");
                    return (token, nodeEndpoint);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Login failed: {response.StatusCode}. {errorContent}";
                    Console.WriteLine($"DEBUG: Step 2 - {ErrorMessage}");
                    return (null, null);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login error: {ex.Message}";
                Console.WriteLine($"DEBUG: Step 2 - Exception during login: {ex.Message}");
                return (null, null);
            }
        }

        private async Task<bool> VerifyTokenWithNodeAsync(string token, string nodeUrl)
        {
            try
            {
                using var nodeClient = new HttpClient();

                var verifyRequest = new
                {
                    token = token
                };

                var response = await nodeClient.PostAsJsonAsync($"{nodeUrl}/api/auth/verify-token", verifyRequest);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("DEBUG: Step 3 - Token verified with storage node successfully");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Token verification failed: {response.StatusCode}. {errorContent}";
                    Console.WriteLine($"DEBUG: Step 3 - {ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Token verification error: {ex.Message}";
                Console.WriteLine($"DEBUG: Step 3 - Exception during token verification: {ex.Message}");
                return false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}