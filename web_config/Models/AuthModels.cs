using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FoodBankApp.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }

        public string? Error { get; set; }
    }

    public class StaffOrderListItem
    {
        public Guid OrderId { get; set; }

        public string OrderCode { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; }

        public string Status { get; set; } = "Pending";
    }

    public class StaffOrderDetailsViewModel
    {
        public Guid OrderId { get; set; }

        public string OrderCode { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; }

        public string Status { get; set; } = "Pending";

        public List<OrderLineDetail> Lines { get; set; } = new();
    }

    public class OrderLineDetail
    {
        public string ItemName { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public int Quantity { get; set; }
    }
}
