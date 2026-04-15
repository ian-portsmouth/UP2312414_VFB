using System;
using System.ComponentModel.DataAnnotations;

namespace FoodBankApp.Models
{
    public class UserListItem
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff";
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UserCreateViewModel
    {
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Staff";

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? Error { get; set; }
    }

    public class UserEditViewModel
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [StringLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Staff";

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public string? Error { get; set; }
    }

    public class UserResetPasswordViewModel
    {
        [Required]
        public Guid UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? Error { get; set; }
    }
}
