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

namespace Promises.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class CabinetController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IPromiseRepository _promiseRepository;
        private readonly IFriendsRepository _friendsRepository;
        private readonly IMessagesRepository _messagesRepository;
        private readonly IUserTracker<Notification> _userTracker;
            
        public CabinetController(
          UserManager<ApplicationUser> userManager,
          SignInManager<ApplicationUser> signInManager,
          IPromiseRepository promiseRepository,
          IFriendsRepository friendsRepository,
          IMessagesRepository messagesRepository,
          IUserTracker<Notification> userTracker)
        {
            _promiseRepository = promiseRepository;
            _userManager = userManager;
            _signInManager = signInManager;
            _friendsRepository = friendsRepository;
            _messagesRepository = messagesRepository;
            _userTracker = userTracker;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Messages()
        {
            return View();
        }

        [HttpGet]
        public async Task<FileStreamResult> GetAvatarImage()
        {
            var user = await _userManager.GetUserAsync(User);
            Stream stream = new MemoryStream(user.Avatar);
            return new FileStreamResult(stream, user.AvatarContentType);
        }

        [HttpGet("{friendId}/{friendEmail}")]
        public async Task<IActionResult> PrivateChat(string friendId, string friendEmail)
        {
            var owner = await _userManager.GetUserAsync(HttpContext.User);
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
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePromise(PromiseCreationModel promise)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await _userManager.GetUserAsync(HttpContext.User);
                    var userId = user.Id;
                    _promiseRepository.Add(new Promise { Content = promise.Content, UserId = userId });
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException /* ex */)
            {
                //Log the error (uncomment ex variable name and write a log.
                ModelState.AddModelError("", "Unable to save changes. " +
                    "Try again, and if the problem persists " +
                    "see your system administrator.");
            }
            return View(promise);
        }

        public IActionResult CreatePromise()
        {
            return View();
        }

        public IActionResult GlobalChat()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetOwner()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            
            return new OkObjectResult(new User
            {
                Email = user.Email,
                Id = user.Id                
            });
        }

        [HttpGet("{userId}")]
        public FileStreamResult GetAvatar(string userId)
        {
            var user = _userManager.Users.FirstOrDefault(u => u.Id == userId);
            Stream stream = new MemoryStream(user.Avatar);
            return new FileStreamResult(stream, user.AvatarContentType);
        }
        
        [HttpGet]
        public IActionResult GetLastMessagesHistory()
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            var history = _messagesRepository.GetLastMessagesHistory(userId)
                .OrderByDescending(m => m.ServerDateUtc);
            
            return new OkObjectResult(history);
        }

        private async Task<bool> IsOnline(string userId)
        {
            var onlineUsers = await _userTracker.UsersOnline();
            return onlineUsers.FirstOrDefault(u => u.Owner.Id == userId) != null;
        }

        [HttpGet("{email?}")]
        public IActionResult FindByEmail(string email = default(string))    
        {
            var userId = _userManager.GetUserId(HttpContext.User);

            //TODO: make User table
            var friends = _userManager.Users
                .Where(u =>
                    _friendsRepository.AreFriends(u.Id, userId) && u.Id != userId &&
                    (email == default(string) || u.Email.StartsWith(email))
                )
                .Select(u => new User { Email = u.Email, Id = u.Id })
                .ToList();

            friends.ForEach(async u => u.IsOnline = await IsOnline(u.Id));

            //other users except his friends
            var foundOtherUsers = _userManager.Users
                .Where(u => 
                    !_friendsRepository.AreFriends(u.Id, userId) && u.Id != userId &&
                    (email == default(string) || u.Email.StartsWith(email))
                )
                .Select(u => new User { Email = u.Email, Id = u.Id })
                .ToList();

            foundOtherUsers.ForEach(async u => u.IsOnline = await IsOnline(u.Id));


            if (foundOtherUsers == null)
            {
                return NotFound();
            }

            var model = new FriendsModel
            {
                Friends = friends,
                Others = foundOtherUsers
            };

            return new OkObjectResult(model);
        }

        [HttpGet("{friendUserId}")]
        public IActionResult AddFriend(string friendUserId)
        {
            if (!IsThereUser(friendUserId))
                return NotFound();

            var userId = _userManager.GetUserId(HttpContext.User);
            _friendsRepository.AddFriend(userId, friendUserId);
            return Ok();
        }
        
        [HttpGet]
        public IActionResult GetLastMessageInHistory(string personId)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            var message = _messagesRepository.FindLastMessage(userId, personId);

            return new OkObjectResult(message);
        }

        [HttpGet]
        public IActionResult GetMessageHistory(string personId)
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            var history = _messagesRepository.GetMessageHistory(userId, personId, MESSAGES_AMOUNT.ALL);

            return new OkObjectResult(history);
        }
        
        private bool IsThereUser(string id)
        {
            return default(ApplicationUser) != _userManager.Users.FirstOrDefault(u => u.Id == id);
        }

        [HttpGet("{FriendUserId}")]
        public IActionResult RemoveFriend(string FriendUserId)
        {
            if (!IsThereUser(FriendUserId))
                return NotFound();

            var userId = _userManager.GetUserId(HttpContext.User);
            _friendsRepository.RemoveFriend(userId, FriendUserId);
            return Ok();
        }

        public async Task<IActionResult> ManagePromises()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var userId = user.Id;
            var promises = _promiseRepository.Promises.Where(p => p.UserId == userId);
            
            var model = new ManagePromisesViewModel{ Promises = promises };
            return View(model);
        }
    }
}
