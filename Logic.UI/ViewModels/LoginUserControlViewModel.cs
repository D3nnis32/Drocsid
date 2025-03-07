using Drocsid.HenrikDennis2025.Api.Controllers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Logic.UI.ViewModels
{
    public class LoginUserControlViewModel : INotifyPropertyChanged
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        private readonly HttpClient _httpClient;

        private string _userName;
        private string _password;
        private string _errorMessage;

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged(nameof(UserName));
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value; 
                OnPropertyChanged(nameof(Password));
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
        public LoginUserControlViewModel(MainWindowViewModel mainWindowVM) 
        {
            _mainWindowViewModel = mainWindowVM;
            //Das muss angepasst werden an die API
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5186/") };

            LoginCommand = new RelayCommand(async (obj) => await LoginAsync());
        }

        private async Task LoginAsync()
        {
            ErrorMessage = string.Empty;

            var request = new LoginRequest
            {
                Username = UserName,
                Password = Password
            };
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    _mainWindowViewModel.NavigateToChat(); //Muss implementiert werden
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Login failed. Connection to Server failed. Try again.";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
