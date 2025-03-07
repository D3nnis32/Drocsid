using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

[ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChannelsController : ControllerBase
    {
        private readonly IChannelService _channelService;

        public ChannelsController(IChannelService channelService)
        {
            _channelService = channelService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Channel>>> GetUserChannels()
        {
            var userId = GetCurrentUserId();
            var channels = await _channelService.GetUserChannelsAsync(userId);
            return Ok(channels);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Channel>> GetChannel(Guid id)
        {
            var channel = await _channelService.GetChannelByIdAsync(id);
            if (channel == null)
            {
                return NotFound();
            }

            // Check if user is member of the channel
            var userId = GetCurrentUserId();
            if (!channel.MemberIds.Contains(userId))
            {
                return Forbid();
            }

            return Ok(channel);
        }

        [HttpPost]
        public async Task<ActionResult<Channel>> CreateChannel(CreateChannelRequest request)
        {
            var userId = GetCurrentUserId();
            
            var channel = new Channel
            {
                Name = request.Name,
                Type = request.Type,
                MemberIds = new List<Guid> { userId } // Creator is automatically a member
            };

            var createdChannel = await _channelService.CreateChannelAsync(channel);
            return CreatedAtAction(nameof(GetChannel), new { id = createdChannel.Id }, createdChannel);
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request)
        {
            var currentUserId = GetCurrentUserId();
            
            // First check if the current user is a member of the channel
            var channel = await _channelService.GetChannelByIdAsync(id);
            if (channel == null)
            {
                return NotFound();
            }

            if (!channel.MemberIds.Contains(currentUserId))
            {
                return Forbid();
            }

            var result = await _channelService.AddUserToChannelAsync(id, request.UserId);
            if (!result)
            {
                return BadRequest("Failed to add user to channel");
            }

            return NoContent();
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            
            // Check if the current user is a member of the channel
            var channel = await _channelService.GetChannelByIdAsync(id);
            if (channel == null)
            {
                return NotFound();
            }

            if (!channel.MemberIds.Contains(currentUserId))
            {
                return Forbid();
            }

            // Users can remove themselves or need to be admin to remove others
            // For simplicity, we skip admin check here

            var result = await _channelService.RemoveUserFromChannelAsync(id, userId);
            if (!result)
            {
                return BadRequest("Failed to remove user from channel");
            }

            return NoContent();
        }

        private Guid GetCurrentUserId()
        {
            // In a real application, get this from the authenticated user claims
            // For now, return a dummy user ID
            return new Guid("11111111-1111-1111-1111-111111111111");
        }
    }