﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Promises.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Promises.Models.CabinetViewModels;
using Promises.Abstract;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Promises.Hubs;
using SkiaSharp;

namespace Promises.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class CabinetController : Controller
    {
        private const int MIN_IMAGE_QUALITY = 0;
        private const int MAX_IMAGE_QUALITY = 100;
        private const string OWNER_DEFAULT = null;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPromiseRepository _promiseRepository;
        private readonly IMessagesRepository _messagesRepository;
        private readonly IFriendsRepository _friendsRepository;
        private readonly IUserTracker<Notification> _userTracker;
        private async Task<ApplicationUser> Owner() => await _userManager.GetUserAsync(User);

        public CabinetController(
          UserManager<ApplicationUser> userManager,
          IPromiseRepository promiseRepository,
          IFriendsRepository friendsRepository,
          IMessagesRepository messagesRepository,
          IUserTracker<Notification> userTracker)
        {
            _promiseRepository = promiseRepository;
            _userManager = userManager;
            _messagesRepository = messagesRepository;
            _userTracker = userTracker;
            _friendsRepository = friendsRepository;
        }

        public async Task<IActionResult> Profile(string userId = OWNER_DEFAULT)
        {
            var user = (userId == OWNER_DEFAULT) ?
                await Owner() : _userManager.Users.FirstOrDefault(u => u.Id == userId);

            var ownerId = (await Owner()).Id;

            var friendStatus = "";
            
            if (_friendsRepository.AreFriends(userId, ownerId))
                friendStatus = "friend";
            else if (_friendsRepository.ArePendingFriends(userId, ownerId))
                friendStatus = "subscribed";
            else
                friendStatus = "";

            var model = new ProfileViewModel
            {
                Email = user.Email,
                Address = user.Address,
                IsYourProfile = userId == OWNER_DEFAULT,
                Id = user.Id,
                FriendStatus = friendStatus,
                IsOnline = await IsOnline(userId),
                Avatar = user.Avatar,
                AvatarContentType = user.AvatarContentType
            };
            return View(model);
        }

        public IActionResult Messages()
        {
            return View();
        }

        [HttpGet]
        public async Task<int> GetUnreadAmount()
        {
            var user = await Owner();
            return _messagesRepository.GetUnreadAmount(user.Id);
        }

        [HttpGet("{width}x{height}/{quality?}/{userId?}")]
        public async Task<FileStreamResult> GetAvatarImage(int width, int height, 
            int quality = MAX_IMAGE_QUALITY, string userId = OWNER_DEFAULT)
        {
            Stream output = new MemoryStream();
            
            var user = (userId == OWNER_DEFAULT) ? 
                await Owner() : _userManager.Users.FirstOrDefault(u => u.Id == userId);
            
            using (var original = SKBitmap.Decode(user.Avatar))
            {
                using (var resized = original.Resize(new SKImageInfo(width, height), 
                    SKBitmapResizeMethod.Lanczos3))
                {
                    if (resized == null)
                        return null;

                    using (var image = SKImage.FromBitmap(resized))
                    {
                        image.Encode(SKEncodedImageFormat.Jpeg, quality).SaveTo(output);
                        output.Seek(0, SeekOrigin.Begin);
                        return new FileStreamResult(output, user.AvatarContentType);
                    }
                }
            }
        }

        [HttpGet("{friendId}/{friendEmail}")]
        public async Task<IActionResult> PrivateChat(string friendId, string friendEmail)
        {
            var owner = await Owner();
            var friend = _userManager.Users.FirstOrDefault(u => u.Id == friendId);
            var ownerId = owner.Id;

            var isUserOnline = await IsOnline(friendId);

            var model = new PrivateChatViewModel
            {
                Friend = new User {
                    Id = friendId,
                    Email = friendEmail,
                    IsOnline = isUserOnline,
                    Avatar = friend.Avatar,
                    AvatarContentType = friend.AvatarContentType
                },
            };
            
            _messagesRepository.MarkHistoryAsRead(ownerId, friendId);
            
            return View(model);
        }

        public IActionResult Friends()
        {
            return View();
        }

        
        private async Task<bool> IsOnline(string userId)
        {
            var onlineUsers = await _userTracker.UsersOnline();
            return onlineUsers.FirstOrDefault(u => u.Owner.Id == userId) != null;
        }
    }
}
