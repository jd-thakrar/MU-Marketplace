using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public enum UserRole : byte
    {
        Student = 0,
        Admin   = 1
    }

    public enum UserStatus : byte
    {
        Active    = 0,
        Suspended = 1
    }

    public class User
    {
        public int UserId { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [Required, StringLength(150)]
        [Index("UQ_Users_Email", IsUnique = true)]
        public string Email { get; set; }

        [StringLength(30)]
        public string EnrollmentNo { get; set; }

        [StringLength(100)]
        public string Department { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [Required, StringLength(255)]
        public string PasswordHash { get; set; }

        public UserRole Role { get; set; }

        public UserStatus Status { get; set; }

        [StringLength(255)]
        public string ResetTokenHash { get; set; }

        public DateTime? ResetTokenExpiry { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public User()
        {
            Role = UserRole.Student;
            Status = UserStatus.Active;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
