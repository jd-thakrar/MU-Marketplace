
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
        [Index("IX_Users_Email", IsUnique = true)]
        public string Email { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [Required, StringLength(255)]
        public string PasswordHash { get; set; }

        public UserRole Role { get; set; }

        public UserStatus Status { get; set; }
    }
}
