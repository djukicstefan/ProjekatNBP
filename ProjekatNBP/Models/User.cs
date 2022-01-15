﻿using System.Collections.Generic;

namespace ProjekatNBP.Models
{
	public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string City { get; set; }
        public List<int> Followers { get; set; }
    }
}
