using InternalChatbox.Data;
using InternalChatbox.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalChatbox.Controllers
{
    public class ChatController : Controller
    {
        private readonly AppDbContext _context;

        public ChatController(AppDbContext context)
        {
            _context = context;
        }


        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var currentUser = await _context.Users.FindAsync(userId);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            ViewBag.CurrentUserId = userId;
            ViewBag.UserName = currentUser.Name;
            ViewBag.UserRole = currentUser.Role;

            // Get all groups where user is a member, with their admin status
            var userMemberships = await _context.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Include(gm => gm.Group)
                .ToListAsync();

            // For system admins, get all groups with their members
            if (currentUser.Role == "Admin")
            {
                ViewBag.Groups = await _context.Groups
                    .Include(g => g.Members)
                    .ToListAsync();
            }
            else
            {
                // For regular users, get only their groups
                ViewBag.Groups = userMemberships
                    .Select(gm => gm.Group)
                    .ToList();
            }

            // Create dictionary of group IDs where user is admin
            ViewBag.UserAdminGroups = userMemberships
                .Where(gm => gm.IsAdmin)
                .Select(gm => gm.GroupId)
                .ToHashSet();

            ViewBag.Users = await _context.Users
                .Where(u => u.Id != userId)
                .ToListAsync();

            return View();
        }


        [HttpPost]
        public async Task<IActionResult> SendMessage(ChatMessage msg)
        {
            msg.SentAt = DateTime.UtcNow;
            _context.ChatMessages.Add(msg);
            await _context.SaveChangesAsync();
            return Json(new { messageId = msg.Id });

        }

        public async Task<IActionResult> GetMessages(int senderId, int? receiverId, int? groupId)
        {
            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m =>
                    (receiverId != null && ((m.SenderId == senderId && m.ReceiverId == receiverId) ||
                                           (m.SenderId == receiverId && m.ReceiverId == senderId))) ||
                    (groupId != null && m.GroupId == groupId))
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    messageId = m.Id, // Make sure this is included
                    senderId = m.SenderId,
                    senderName = m.Sender.Name,
                    receiverId = m.ReceiverId,
                    messageText = m.MessageText,
                    groupId = m.GroupId,
                    sentAt = m.SentAt
                })
                .ToListAsync();

            return Json(messages);
        }


        [HttpPost]
        public async Task<IActionResult> CreateGroup(string GroupName, List<int> UserIds)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var currentUser = await _context.Users.FindAsync(userId);

            if (currentUser == null)
                return Unauthorized();

            var newGroup = new ChatGroup { GroupName = GroupName };
            _context.Groups.Add(newGroup);
            await _context.SaveChangesAsync();

            // Add the creator to the group as admin
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = newGroup.Id,
                UserId = currentUser.Id,
                IsAdmin = true // Mark as admin
            });

            // Add selected users (excluding the creator if already added)
            foreach (var id in UserIds.Distinct())
            {
                if (id != currentUser.Id)
                {
                    _context.GroupMembers.Add(new GroupMember
                    {
                        GroupId = newGroup.Id,
                        UserId = id,
                        IsAdmin = false // Regular member
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }



        [HttpPost]
        public async Task<IActionResult> UpdateGroup(int groupId, string groupName, List<int> newMembers)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            // Check if user is admin
            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.IsAdmin);

            if (!isAdmin) return Unauthorized();

            // Update group name
            var group = await _context.Groups.FindAsync(groupId);
            if (group == null) return NotFound();

            group.GroupName = groupName;

            // Add new members
            foreach (var memberId in newMembers ?? new List<int>())
            {
                // Check if user is already in the group
                var existingMember = await _context.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberId);

                if (existingMember == null)
                {
                    _context.GroupMembers.Add(new GroupMember
                    {
                        GroupId = groupId,
                        UserId = memberId,
                        IsAdmin = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }


        [HttpGet]
        public async Task<IActionResult> EditGroup(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            // Check if user is admin of this group
            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == id && gm.UserId == userId && gm.IsAdmin);

            if (!isAdmin) return Unauthorized();

            var group = await _context.Groups
                .Include(g => g.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            // Get all users NOT in this group
            var nonMembers = await _context.Users
                .Where(u => !u.GroupMemberships.Any(gm => gm.GroupId == id))
                .ToListAsync();

            ViewBag.NonMembers = nonMembers;
            ViewBag.CurrentUserId = userId;
            return View(group);
        }


        [HttpPost]
        public async Task<IActionResult> UpdateGroupMembers(int groupId, string groupName, List<int> newMembers)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            // Check if user is admin
            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.IsAdmin);

            if (!isAdmin) return Unauthorized();

            // Update group name
            var group = await _context.Groups.FindAsync(groupId);
            if (group == null) return NotFound();
            group.GroupName = groupName;

            // Add new members
            foreach (var memberId in newMembers)
            {
                _context.GroupMembers.Add(new GroupMember
                {
                    GroupId = groupId,
                    UserId = memberId,
                    IsAdmin = false
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> RemoveGroupMember([FromBody] GroupMemberRequest request)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return Unauthorized();

            // Check if current user is admin
            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == currentUserId && gm.IsAdmin);

            if (!isAdmin) return Unauthorized();

            // Prevent removing yourself
            if (request.UserId == currentUserId)
            {
                return BadRequest("You cannot remove yourself from the group");
            }

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.UserId);

            if (member != null)
            {
                _context.GroupMembers.Remove(member);
                await _context.SaveChangesAsync();
                return Ok();
            }

            return NotFound();
        }

        public class GroupMemberRequest
        {
            public int UserId { get; set; }
            public int GroupId { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            // Check if user is admin of this group
            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.IsAdmin);

            if (!isAdmin)
                return Unauthorized();

            var group = await _context.Groups.FindAsync(groupId);
            if (group == null)
                return NotFound();

            var members = _context.GroupMembers.Where(g => g.GroupId == groupId);
            _context.GroupMembers.RemoveRange(members);

            // Also delete all messages in this group
            var messages = _context.ChatMessages.Where(m => m.GroupId == groupId);
            _context.ChatMessages.RemoveRange(messages);

            _context.Groups.Remove(group);

            await _context.SaveChangesAsync();

            return Ok();
        }


        // Add this method to your ChatController

        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message == null)
                return NotFound();

            // Only allow deletion if user is the sender or admin
            if (message.SenderId != userId && HttpContext.Session.GetString("UserRole") != "Admin")
                return Unauthorized();

            _context.ChatMessages.Remove(message);
            await _context.SaveChangesAsync();

            return Ok();
        }

    }
}
