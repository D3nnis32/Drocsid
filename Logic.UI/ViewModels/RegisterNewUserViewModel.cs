using Drocsid.HenrikDennis2025.Core.DTO;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Input;

namespace Logic.UI.ViewModels
{
    public class RegisterNewUserViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;

        private string _username;
        private string _email;
        private string _password;
        private string _preferredRegion;
        private string _errorMessage;

        // Bound Properties
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(nameof(Email)); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(nameof(Password)); }
        }

        public string PreferredRegion
        {
            get => _preferredRegion;
            set { _preferredRegion = value; OnPropertyChanged(nameof(PreferredRegion)); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        // Commands
        public ICommand RegisterCommand { get; }
        public ICommand CloseWindowCommand { get; }

        // Event to close the window
        public event EventHandler RequestClose;

        public RegisterNewUserViewModel()
        {
            // Adjust BaseAddress or store in config if needed
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5261") };

            // Example if you have an auth token to pass:
            // _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", someToken);

            RegisterCommand = new RelayCommand(async () => await RegisterUser());
            CloseWindowCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
        }

        private async System.Threading.Tasks.Task RegisterUser()
        {
            ErrorMessage = string.Empty;
            try
            {
                var request = new CreateUserRequest
                {
                    Username = Username,
                    Email = Email,
                    Password = Password,
                    PreferredRegion = PreferredRegion
                };

                // Example registry endpoint
                var response = await _httpClient.PostAsJsonAsync("api/registry/users/register", request);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("User registered successfully!");
                    RequestClose?.Invoke(this, EventArgs.Empty); // Close the window
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to register user: {response.StatusCode}\n{errorContent}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Exception: {ex.Message}";
            }
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
