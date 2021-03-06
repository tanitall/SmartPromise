﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Promises.Models
{
    //TODO restructure entire database
    public class User
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public string Id { get; set; }
        public bool IsOnline { get; set; }
        public byte[] Avatar { get; set; }
        public string AvatarContentType { get; set; }
    }
}
