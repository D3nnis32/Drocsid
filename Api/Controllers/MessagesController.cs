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

        public MessagesController(
            IMessageService messageService, 
            IChannelService channelService,
            ILogger<MessagesController> logger) 
            : base(logger)
        {
            _messageService = messageService;
            _channelService = channelService;
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

            var message = new Message
            {
                ChannelId = channelId,
                SenderId = userId,
                Content = request.Content,
                Attachments = request.AttachmentIds?.Select(id => new Attachment { Id = id }).ToList() ?? new List<Attachment>()
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