using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FoodBankApp.Models
{
    public class OrderLine
    {
        [Display(Name = "Item ID")]
        public Guid ItemId { get; set; }

        [Display(Name = "Item Name")]
        public string ItemName { get; set; } = string.Empty;

        public string? Category { get; set; }

        [Display(Name = "Available Stock")]
        public int Available { get; set; }

        [Display(Name = "Quantity Requested")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or greater.")]
        public int Quantity { get; set; }

        public string? ImagePath { get; set; }
    }

    public class PublicOrderViewModel
    {
        [Required]
        [Display(Name = "Your Name")]
        public string ContactName { get; set; } = string.Empty;

        public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
    }
}
