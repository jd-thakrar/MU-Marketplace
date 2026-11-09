using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MU_Marketplace.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public string EnrollmentNo { get; set; }

        public string Department { get; set; }

        public string PhoneNumber { get; set; }

        // Never store or display the plain-text password.
        public string PasswordHash { get; set; }

        // 0 = Student, 1 = Admin
        public byte Role { get; set; }

        // 0 = Active, 1 = Suspended
        public byte Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }
    }
}