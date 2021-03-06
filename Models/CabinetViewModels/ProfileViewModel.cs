﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Promises.Models.CabinetViewModels
{
    public class ProfileViewModel
    {
        public string Address { get; set; }
        public string Email { get; set; } 
        public bool IsYourProfile { get; set; }
        public string Id { get; set; }
        public string FriendStatus { get; set; }
        public bool IsOnline { get; set; }
        public byte[] Avatar { get; set; }
        public string AvatarContentType { get; set; }
    }
}
