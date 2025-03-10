using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

[ApiController]
    [Route("api/channels/{channelId}/[controller]")]
    [Authorize]
    public class MessagesController : BaseController
    {
        private readonly IMessageService _messageService;
        private readonly IChannelService _channelService;
        private readonly IFileStorageService _fileStorageService;

        public MessagesController(
            IMessageService messageService, 
            IChannelService channelService,
            IFileStorageService fileStorageService,
            ILogger<MessagesController> logger) 
            : base(logger)
        {
            _messageService = messageService;
            _channelService = channelService;
            _fileStorageService = fileStorageService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Message>>> GetMessages(Guid channelId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            // Check if the user is a member of the channel
            var userId = GetCurrentUserId();
            var channel = await _channelService.GetChannelByIdAsync(channelId);
            
            if (channel == null)
            {
                return NotFound();
            }

            if (!channel.MemberIds.Contains(userId))
            {
                return Forbid();
            }

            var messages = await _messageService.GetChannelMessagesAsync(channelId, skip, take);
            return Ok(messages);
        }

        [HttpPost]
        public async Task<ActionResult<Message>> CreateMessage(Guid channelId, CreateMessageRequest request)
        {
            // Check if the user is a member of the channel
            var userId = GetCurrentUserId();
            var channel = await _channelService.GetChannelByIdAsync(channelId);
    
            if (channel == null)
            {
                return NotFound();
            }

            if (!channel.MemberIds.Contains(userId))
            {
                return Forbid();
            }

            // Create a list to hold attachment objects
            var attachments = new List<Attachment>();
    
            // If there are attachment IDs, fetch the complete attachment information
            if (request.AttachmentIds != null && request.AttachmentIds.Any())
            {
                // Use your file/attachment service to get complete attachment info
                foreach (var attachmentId in request.AttachmentIds)
                {
                    // Fetch the complete attachment information from your file service
                    var attachmentInfo = await _fileStorageService.GetFileInfoAsync(attachmentId.ToString());
            
                    if (attachmentInfo != null)
                    {
                        attachments.Add(new Attachment 
                        { 
                            Id = attachmentId,
                            Filename = attachmentInfo.Filename,
                            ContentType = attachmentInfo.ContentType,
                            Path = attachmentInfo.Path,
                            Size = attachmentInfo.Size,
                            UploadedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            var message = new Message
            {
                ChannelId = channelId,
                SenderId = userId,
                Content = request.Content,
                Attachments = attachments
            };

            var createdMessage = await _messageService.CreateMessageAsync(message);
            return Ok(createdMessage);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(Guid channelId, Guid id)
        {
            // Check if the message exists
            var message = await _messageService.GetMessageByIdAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            // Check if the message belongs to the specified channel
            if (message.ChannelId != channelId)
            {
                return BadRequest("Message does not belong to the specified channel");
            }

            // Check if the user is the sender of the message
            var userId = GetCurrentUserId();
            if (message.SenderId != userId)
            {
                return Forbid();
            }

            await _messageService.DeleteMessageAsync(id);
            return NoContent();
        }
    }