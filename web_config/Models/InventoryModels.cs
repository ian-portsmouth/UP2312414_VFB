using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FoodBankApp.Models
{
    public class InventoryItem
    {
        public Guid ItemId { get; set; }

        [Display(Name = "Item Name")]
        public string ItemName { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string? ImagePath { get; set; }

        [Display(Name = "Stock")]
        public int StockQuantity { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }

    public class StockIndexViewModel
    {
        public List<InventoryItem> Items { get; set; } = new();
        public List<string> Categories { get; set; } = new();

        public string? Search { get; set; }
        public string? Category { get; set; }
        public bool ShowInactive { get; set; }
    }

    public class ItemUpsertViewModel
    {
        public Guid? ItemId { get; set; }

        [Required]
        [StringLength(120)]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; } = string.Empty;

        [StringLength(60)]
        public string? Category { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock must be zero or greater.")]
        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public string? ImagePath { get; set; }

        [Display(Name = "Item Photo")]
        public IFormFile? ImageFile { get; set; }
    }

    public class AddStockViewModel
    {
        [Required]
        public Guid ItemId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        [Display(Name = "Current Stock")]
        public int CurrentStock { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Add amount must be at least 1.")]
        [Display(Name = "Add Stock")]
        public int AddAmount { get; set; }
    }
}
