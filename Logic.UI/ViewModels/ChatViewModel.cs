﻿using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Models;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;
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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Logic.UI.ViewModels.Services;
using FileInfo = System.IO.FileInfo;

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
        private bool _isVoiceChatAvailable;
        private bool _isWhiteboardAvailable;
        private bool _isPluginActive;
        private ContentControl _activePluginContent;
        private string _activePluginSessionId;
        private string _activePluginType;

        public Channel Channel => _channel;
        public ObservableCollection<Message> Messages { get; } = new ObservableCollection<Message>();

        // Collection to hold pending attachments
        public ObservableCollection<PendingAttachment> PendingAttachments { get; } =
            new ObservableCollection<PendingAttachment>();

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

        public bool IsVoiceChatAvailable
        {
            get => _isVoiceChatAvailable;
            set
            {
                _isVoiceChatAvailable = value;
                OnPropertyChanged(nameof(IsVoiceChatAvailable));
            }
        }

        public bool IsWhiteboardAvailable
        {
            get => _isWhiteboardAvailable;
            set
            {
                _isWhiteboardAvailable = value;
                OnPropertyChanged(nameof(IsWhiteboardAvailable));
            }
        }

        public bool IsPluginActive
        {
            get => _isPluginActive;
            set
            {
                _isPluginActive = value;
                OnPropertyChanged(nameof(IsPluginActive));
            }
        }

        public ContentControl ActivePluginContent
        {
            get => _activePluginContent;
            set
            {
                _activePluginContent = value;
                OnPropertyChanged(nameof(ActivePluginContent));
                IsPluginActive = (value != null);
            }
        }

        public RelayCommand SendMessageCommand { get; }
        public RelayCommand RefreshMessagesCommand { get; }
        public ICommand ShowPluginsCommand { get; }
        public ICommand StartVoiceChatCommand { get; }
        public ICommand StartWhiteboardCommand { get; }
        public RelayCommand AddAttachmentCommand { get; }
        public RelayCommand<PendingAttachment> RemoveAttachmentCommand { get; }
        public RelayCommand<Attachment> DownloadAttachmentCommand { get; }
        public RelayCommand OpenAddMembersWindowCommand { get; }

        public event EventHandler RequestOpenAddMembersWindow;
        public event EventHandler RequestShowPluginsWindow;
        public event EventHandler<PluginSessionEventArgs> PluginSessionStarted;

        public ChatViewModel(Channel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _httpClient = new HttpClient
                { BaseAddress = new Uri(TokenStorage.CurrentNodeEndpoint ?? "http://localhost:5186/") };

            SendMessageCommand = new RelayCommand(
                execute: SendMessage,
                canExecute: () =>
                    (!string.IsNullOrWhiteSpace(MessageText) || PendingAttachments.Count > 0) && !IsSending
            );

            ShowPluginsCommand = new RelayCommand(OnShowPluginsDialog);

            StartVoiceChatCommand = new RelayCommand(
                execute: StartVoiceChat,
                canExecute: () => IsVoiceChatAvailable && !IsPluginActive
            );

            StartWhiteboardCommand = new RelayCommand(
                execute: StartWhiteboard,
                canExecute: () => IsWhiteboardAvailable && !IsPluginActive
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
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
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

            // Check for available plugins
            CheckAvailablePlugins();
        }

        private async void CheckAvailablePlugins()
        {
            try
            {
                // Add the auth token
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                // Check for voice chat plugin
                var voiceChatResponse =
                    await _httpClient.GetAsync("api/plugins/channel/" + _channel.Id + "/communication");
                if (voiceChatResponse.IsSuccessStatusCode)
                {
                    var plugins = await voiceChatResponse.Content.ReadFromJsonAsync<PluginInfo[]>();
                    IsVoiceChatAvailable = plugins != null && plugins.Length > 0;
                }

                // Check for whiteboard plugin
                var whiteboardResponse =
                    await _httpClient.GetAsync("api/plugins/channel/" + _channel.Id + "/collaboration");
                if (whiteboardResponse.IsSuccessStatusCode)
                {
                    var plugins = await whiteboardResponse.Content.ReadFromJsonAsync<PluginInfo[]>();
                    IsWhiteboardAvailable = plugins != null && plugins.Length > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for plugins: {ex.Message}");
                // Don't show an error message to the user, just log it
            }
        }

        private async void StartVoiceChat()
        {
            try
            {
                if (IsPluginActive)
                {
                    await EndActivePluginSession();
                }
                
                // Add the auth token
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                
                // Get the first voice chat plugin
                var response = await _httpClient.GetAsync("api/plugins/channel/" + _channel.Id + "/communication");
                Console.WriteLine($"DEBUG: Voice chat plugin response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var plugins = await response.Content.ReadFromJsonAsync<PluginInfo[]>();
                    Console.WriteLine($"DEBUG: Found {plugins?.Length ?? 0} voice chat plugins");
                    
                    if (plugins != null && plugins.Length > 0)
                    {
                        // Start a voice chat session - UPDATED ENDPOINT
                        var startResponse = await _httpClient.PostAsync(
                            $"api/channels/{_channel.Id}/communication/{plugins[0].Id}/start?mode=Audio", 
                            null);
                        
                        Console.WriteLine($"DEBUG: Start voice chat response status: {startResponse.StatusCode}");
                        
                        if (startResponse.IsSuccessStatusCode)
                        {
                            var sessionInfo = await startResponse.Content.ReadFromJsonAsync<PluginSessionInfo>();
                            Console.WriteLine($"DEBUG: Created voice chat session with ID: {sessionInfo.SessionId}");
                            
                            _activePluginSessionId = sessionInfo.SessionId;
                            _activePluginType = "voice";
                            
                            // Raise event to notify the view to create the plugin UI
                            PluginSessionStarted?.Invoke(this, new PluginSessionEventArgs 
                            { 
                                SessionId = sessionInfo.SessionId,
                                PluginType = "voice",
                                UiComponent = sessionInfo.UiComponent
                            });
                        }
                        else
                        {
                            var error = await startResponse.Content.ReadAsStringAsync();
                            ErrorMessage = $"Failed to start voice chat: {error}";
                            Console.WriteLine($"DEBUG: Voice chat start error: {error}");
                        }
                    }
                    else
                    {
                        ErrorMessage = "No voice chat plugins are available.";
                        Console.WriteLine("DEBUG: No voice chat plugins found");
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to get voice chat plugins: {error}";
                    Console.WriteLine($"DEBUG: Voice chat plugin query error: {error}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error starting voice chat: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception in StartVoiceChat: {ex}");
            }
        }

        private async void StartWhiteboard()
        {
            try
            {
                if (IsPluginActive)
                {
                    await EndActivePluginSession();
                }
                
                // Add the auth token
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                
                // Get the first whiteboard plugin
                var response = await _httpClient.GetAsync("api/plugins/channel/" + _channel.Id + "/collaboration");
                Console.WriteLine($"DEBUG: Whiteboard plugin response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var plugins = await response.Content.ReadFromJsonAsync<PluginInfo[]>();
                    Console.WriteLine($"DEBUG: Found {plugins?.Length ?? 0} whiteboard plugins");
                    
                    if (plugins != null && plugins.Length > 0)
                    {
                        // Start a whiteboard session - UPDATED ENDPOINT
                        var startResponse = await _httpClient.PostAsync(
                            $"api/channels/{_channel.Id}/collaboration/{plugins[0].Id}/start", 
                            null);
                        
                        Console.WriteLine($"DEBUG: Start whiteboard response status: {startResponse.StatusCode}");
                        
                        if (startResponse.IsSuccessStatusCode)
                        {
                            var sessionInfo = await startResponse.Content.ReadFromJsonAsync<PluginSessionInfo>();
                            Console.WriteLine($"DEBUG: Created whiteboard session with ID: {sessionInfo.SessionId}");
                            
                            _activePluginSessionId = sessionInfo.SessionId;
                            _activePluginType = "whiteboard";
                            
                            // Raise event to notify the view to create the plugin UI
                            PluginSessionStarted?.Invoke(this, new PluginSessionEventArgs 
                            { 
                                SessionId = sessionInfo.SessionId,
                                PluginType = "whiteboard",
                                UiComponent = sessionInfo.UiComponent
                            });
                        }
                        else
                        {
                            var error = await startResponse.Content.ReadAsStringAsync();
                            ErrorMessage = $"Failed to start whiteboard: {error}";
                            Console.WriteLine($"DEBUG: Whiteboard start error: {error}");
                        }
                    }
                    else
                    {
                        ErrorMessage = "No whiteboard plugins are available.";
                        Console.WriteLine("DEBUG: No whiteboard plugins found");
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to get whiteboard plugins: {error}";
                    Console.WriteLine($"DEBUG: Whiteboard plugin query error: {error}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error starting whiteboard: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception in StartWhiteboard: {ex}");
            }
        }

        private async Task EndActivePluginSession()
        {
            if (string.IsNullOrEmpty(_activePluginSessionId))
                return;
        
            try
            {
                // Add the auth token
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
        
                // End the session based on the plugin type - UPDATED ENDPOINTS
                string endpoint = _activePluginType == "voice" 
                    ? $"api/channels/{_channel.Id}/communication/session/{_activePluginSessionId}/end"
                    : $"api/channels/{_channel.Id}/collaboration/session/{_activePluginSessionId}/end";
        
                Console.WriteLine($"DEBUG: Ending {_activePluginType} session with ID: {_activePluginSessionId}");
        
                var response = await _httpClient.PostAsync(endpoint, null);
                Console.WriteLine($"DEBUG: End session response status: {response.StatusCode}");
        
                // Clear the active plugin
                _activePluginSessionId = null;
                _activePluginType = null;
                ActivePluginContent = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error ending plugin session: {ex.Message}");
                // Don't show an error message to the user, just log it
            }
        }

        private void OnShowPluginsDialog()
        {
            try
            {
                // Instead of creating the window here, raise an event for the view to handle
                RequestShowPluginsWindow?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error opening plugins dialog: {ex.Message}";
            }
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
                        Console.WriteLine($"DEBUG: Successfully uploaded attachment: {attachment.Id}, {attachment.Filename}");
                        attachmentIds.Add(attachment.Id);
                    }
                    else
                    {
                        Console.WriteLine($"DEBUG: Failed to upload attachment: {pendingAttachment.Filename}");
                    }
                }

                // Then create the message
                var request = new CreateMessageRequest
                {
                    Content = MessageText,
                    AttachmentIds = attachmentIds
                };

                Console.WriteLine($"DEBUG: Sending message with {attachmentIds.Count} attachments");

                var response = await _httpClient.PostAsJsonAsync($"api/channels/{_channel.Id}/messages", request);

                if (response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadFromJsonAsync<Message>();
                    Console.WriteLine($"DEBUG: Message created successfully with {message?.Attachments?.Count ?? 0} attachments");
                    
                    // Ensure we have the attachments included
                    if (message != null)
                    {
                        if (message.Attachments == null)
                        {
                            message.Attachments = new List<Attachment>();
                        }
                        
                        // Add the message to the list
                        Messages.Add(message);
                    }
                    
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

                var fileInfo = new FileInfo(filePath);
                
                if (!fileInfo.Exists)
                {
                    Console.WriteLine($"DEBUG: File does not exist: {filePath}");
                    return null;
                }
                
                var fileName = Path.GetFileName(filePath);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    Console.WriteLine("DEBUG: Extracted filename is empty.");
                    return null;
                }

                // Create multipart form content
                using var multipartContent = new MultipartFormDataContent();
                
                // Create stream content directly from file stream
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var fileContent = new StreamContent(fileStream);
                
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeTypeFromFileName(fileName));
                multipartContent.Add(fileContent, "file", fileName);

                Console.WriteLine($"DEBUG: Sending file {fileName} with size {fileInfo.Length}");

                var response = await _httpClient.PostAsync("api/files", multipartContent);
                
                Console.WriteLine($"DEBUG: Upload response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var attachment = await response.Content.ReadFromJsonAsync<Attachment>();
                    Console.WriteLine($"DEBUG: File uploaded successfully. ID: {attachment?.Id}, Name: {attachment?.Filename}");
                    return attachment;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"DEBUG: Upload failed. Status: {response.StatusCode}, Error: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception uploading file: {ex.Message}");
                Console.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
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
                    Console.WriteLine($"DEBUG: Loaded {messages?.Length ?? 0} messages.");

                    // Check the messages for debugging purposes
                    if (messages != null)
                    {
                        foreach (var message in messages)
                        {
                            if (message.Attachments != null && message.Attachments.Any())
                            {
                                Console.WriteLine($"DEBUG: Message {message.Id} has {message.Attachments.Count} attachments");
                                foreach (var attachment in message.Attachments)
                                {
                                    Console.WriteLine($"DEBUG: Attachment ID: {attachment.Id}, Filename: {attachment.Filename ?? "null"}, Path: {attachment.Path ?? "null"}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"DEBUG: Message {message.Id} has no attachments");
                                // Initialize attachments collection if it's null
                                message.Attachments = new List<Attachment>();
                            }
                        }
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Messages.Clear();
                        if (messages != null)
                        {
                            foreach (var message in messages)
                            {
                                Messages.Add(message);
                            }
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"DEBUG: Failed to fetch messages. Status: {response.StatusCode}");
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"DEBUG: Response content: {content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception fetching messages: {ex.Message}");
                Console.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
            }
        }


        // Make sure to clean up resources
        public void Dispose()
        {
            _refreshTimer.Stop();

            // End any active plugin session
            if (!string.IsNullOrEmpty(_activePluginSessionId))
            {
                _ = EndActivePluginSession();
            }
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

    // Plugin info model
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

    // Plugin session info model
    public class PluginSessionInfo
    {
        public string SessionId { get; set; }
        public UiComponent UiComponent { get; set; }
    }

    // Event args for plugin session events
    public class PluginSessionEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public string PluginType { get; set; }
        public UiComponent UiComponent { get; set; }
    }
}