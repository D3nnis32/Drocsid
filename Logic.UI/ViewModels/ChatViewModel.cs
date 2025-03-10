using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Logic.UI.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly Channel _channel;
        private string _messageText;
        private bool _isSending;
        private string _errorMessage;
        private System.Windows.Threading.DispatcherTimer _refreshTimer;
        private bool _isAttaching;
        public Channel Channel => _channel;
        public ObservableCollection<Message> Messages { get; } = new ObservableCollection<Message>();

        // Collection to hold pending attachments
        public ObservableCollection<PendingAttachment> PendingAttachments { get; } = new ObservableCollection<PendingAttachment>();

        public string ChannelName => _channel?.Name ?? "Unknown Channel";

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged(nameof(MessageText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsSending
        {
            get => _isSending;
            set
            {
                _isSending = value;
                OnPropertyChanged(nameof(IsSending));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsAttaching
        {
            get => _isAttaching;
            set
            {
                _isAttaching = value;
                OnPropertyChanged(nameof(IsAttaching));
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

        public RelayCommand SendMessageCommand { get; }
        public RelayCommand RefreshMessagesCommand { get; }
        public RelayCommand AddAttachmentCommand { get; }
        public RelayCommand<PendingAttachment> RemoveAttachmentCommand { get; }
        public RelayCommand<Attachment> DownloadAttachmentCommand { get; }
        public RelayCommand OpenAddMembersWindowCommand { get; }

        public event EventHandler RequestOpenAddMembersWindow;

        public ChatViewModel(Channel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5186/") };

            SendMessageCommand = new RelayCommand(
                execute: SendMessage,
                canExecute: () => (!string.IsNullOrWhiteSpace(MessageText) || PendingAttachments.Count > 0) && !IsSending
            );

            OpenAddMembersWindowCommand = new RelayCommand(OpenAddMembersWindow);

            RefreshMessagesCommand = new RelayCommand(
                execute: () => Task.Run(async () => await LoadMessagesAsync())
            );

            AddAttachmentCommand = new RelayCommand(
                execute: AddAttachment,
                canExecute: () => !IsAttaching && !IsSending
            );

            RemoveAttachmentCommand = new RelayCommand<PendingAttachment>(
                execute: RemoveAttachment,
                canExecute: (attachment) => attachment != null && !IsSending
            );

            DownloadAttachmentCommand = new RelayCommand<Attachment>(
            execute: async (attachment) => await DownloadAttachmentAsync(attachment),
            canExecute: (attachment) => attachment != null
            );

            // Initialize timer on UI thread to avoid threading issues
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                // Set up a timer to refresh messages
                _refreshTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                _refreshTimer.Tick += (s, e) => Task.Run(async () => await LoadMessagesAsync());
                _refreshTimer.Start();
            });

            // Load initial messages
            Task.Run(async () => await LoadMessagesAsync());
        }
        private void OpenAddMembersWindow()
        {
            RequestOpenAddMembersWindow?.Invoke(this, EventArgs.Empty);
        }

        private async Task DownloadAttachmentAsync(Attachment attachment)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/files/{attachment.Id}/content");

                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = "Failed to download file.";
                    Console.WriteLine($"DEBUG: Download failed. Status: {response.StatusCode}");
                    return;
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var saveFileDialog = new SaveFileDialog
                {
                    FileName = attachment.Filename,
                    Filter = "All Files (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(saveFileDialog.FileName, fileBytes);
                    Console.WriteLine($"DEBUG: File downloaded: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception downloading file: {ex.Message}");
                ErrorMessage = "Error downloading file.";
            }
        }
        private void AddAttachment()
        {
            try
            {
                IsAttaching = true;

                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select File to Attach",
                    Multiselect = true,
                    Filter = "All Files (*.*)|*.*"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (var filename in openFileDialog.FileNames)
                    {
                        var fileInfo = new System.IO.FileInfo(filename);

                        PendingAttachments.Add(new PendingAttachment
                        {
                            FilePath = filename,
                            Filename = fileInfo.Name, // Changed from FileName to Filename
                            FileSize = fileInfo.Length
                        });
                    }

                    CommandManager.InvalidateRequerySuggested();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error adding attachment: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception adding attachment: {ex.Message}");
            }
            finally
            {
                IsAttaching = false;
            }
        }

        private void RemoveAttachment(PendingAttachment attachment)
        {
            if (attachment != null)
            {
                PendingAttachments.Remove(attachment);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SendMessage()
        {
            // Start the async operation
            _ = SendMessageAsync();
        }

        private async Task SendMessageAsync()
        {
            try
            {
                IsSending = true;
                ErrorMessage = string.Empty;

                if (string.IsNullOrEmpty(TokenStorage.JwtToken))
                {
                    ErrorMessage = "Not logged in. Please log in first.";
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                // First, upload any attachments
                var attachmentIds = new List<Guid>();

                foreach (var pendingAttachment in PendingAttachments)
                {
                    var attachment = await UploadFileAsync(pendingAttachment.FilePath);
                    if (attachment != null)
                    {
                        attachmentIds.Add(attachment.Id);
                    }
                }

                // Then create the message
                var request = new CreateMessageRequest
                {
                    Content = MessageText,
                    AttachmentIds = attachmentIds
                };

                var response = await _httpClient.PostAsJsonAsync($"api/channels/{_channel.Id}/messages", request);

                if (response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadFromJsonAsync<Message>();
                    Messages.Add(message);
                    MessageText = string.Empty; // Clear the input
                    PendingAttachments.Clear(); // Clear attachments
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to send message: {response.StatusCode}. {errorContent}";
                    Console.WriteLine($"DEBUG: {ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception sending message: {ex.Message}");
            }
            finally
            {
                IsSending = false;
            }
        }

        private async Task<Attachment> UploadFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    Console.WriteLine("DEBUG: File path is empty or null.");
                    return null;
                }

                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    Console.WriteLine("DEBUG: Extracted filename is empty.");
                    return null;
                }

                using var multipartContent = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(fileBytes);

                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeTypeFromFileName(fileName));
                multipartContent.Add(fileContent, "file", fileName);

                Console.WriteLine($"DEBUG: Sending file {fileName} with size {fileBytes.Length}");

                var response = await _httpClient.PostAsync("api/files", multipartContent);
                var errorContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"DEBUG: Upload response status: {response.StatusCode}, Content: {errorContent}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Attachment>();
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception uploading file: {ex.Message}");
                return null;
            }
        }


        private string GetMimeTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".html" => "text/html",
                ".htm" => "text/html",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".zip" => "application/zip",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream" // Default content type
            };
        }

        public async Task LoadMessagesAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(TokenStorage.JwtToken))
                {
                    Console.WriteLine("DEBUG: No JWT Token found. Cannot fetch messages.");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                var response = await _httpClient.GetAsync($"api/channels/{_channel.Id}/messages");

                if (response.IsSuccessStatusCode)
                {
                    var messages = await response.Content.ReadFromJsonAsync<Message[]>();
                    Console.WriteLine($"DEBUG: Loaded {messages.Length} messages.");

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Messages.Clear();
                        foreach (var message in messages)
                        {
                            if (message.Attachments != null && message.Attachments.Any())
                            {
                                Console.WriteLine($"DEBUG: Message {message.Id} has {message.Attachments.Count} attachments");
                                foreach (var attachment in message.Attachments)
                                {
                                    Console.WriteLine($"DEBUG: Attachment ID: {attachment.Id}, Filename: {attachment.Filename ?? "null"}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"DEBUG: Message {message.Id} has no attachments");
                            }
                            Messages.Add(message);
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"DEBUG: Failed to fetch messages. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception fetching messages: {ex.Message}");
            }
        }


        // Make sure to clean up resources
        public void Dispose()
        {
            _refreshTimer.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Class to represent a pending attachment (before upload)
    public class PendingAttachment
    {
        public string FilePath { get; set; }
        public string Filename { get; set; }
        public long FileSize { get; set; }

        public string DisplaySize
        {
            get
            {
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                return FileSize switch
                {
                    < KB => $"{FileSize} B",
                    < MB => $"{FileSize / KB:F1} KB",
                    < GB => $"{FileSize / MB:F1} MB",
                    _ => $"{FileSize / GB:F1} GB"
                };
            }
        }
    }
}