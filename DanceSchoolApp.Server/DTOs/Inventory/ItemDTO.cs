using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.Inventory
{
    //  Shared sub-responses 

    public class ItemImageResponse
    {
        public int ImageId { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class ItemVariantSummaryResponse
    {
        public int VariantId { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ItemCategorySummaryResponse
    {
        public int CategoryId { get; set; }
        public string? CatgName { get; set; }
    }

    //  Item Responses 

    public class ItemListResponse
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool FromSchool { get; set; }
        public int? IdOwner { get; set; }
        public bool IsActive { get; set; }
        public DateOnly? CreatedAt { get; set; }
        public ItemCategorySummaryResponse? Category { get; set; }
        public List<ItemImageResponse> Images { get; set; } = new();
        public int VariantCount { get; set; }
    }

    public class ItemDetailResponse
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool FromSchool { get; set; }
        public int? IdOwner { get; set; }
        public bool IsActive { get; set; }
        public DateOnly? CreatedAt { get; set; }
        public ItemCategorySummaryResponse? Category { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactAddress { get; set; }
        public List<ItemImageResponse> Images { get; set; } = new();
        public List<ItemVariantSummaryResponse> Variants { get; set; } = new();
    }

    //  Item Requests 

    public class ItemCreateRequest
    {
        [Required]
        [MaxLength(128)]
        public string Name { get; set; } = null!;

        [MaxLength(256)]
        public string? Description { get; set; }

        public int? IdCategory { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        [EmailAddress]
        [MaxLength(254)]
        public string? ContactEmail { get; set; }

        [MaxLength(256)]
        public string? ContactAddress { get; set; }
    }

    public class ItemUpdateRequest
    {
        [MaxLength(128)]
        public string? Name { get; set; }

        [MaxLength(256)]
        public string? Description { get; set; }

        public int? IdCategory { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        [EmailAddress]
        [MaxLength(254)]
        public string? ContactEmail { get; set; }

        [MaxLength(256)]
        public string? ContactAddress { get; set; }
    }

    //  ItemVariant Responses 

    public class ItemVariantDetailResponse
    {
        public int VariantId { get; set; }
        public int IdItem { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
        public bool? IsActive { get; set; }
    }

    //  ItemVariant Requests 

    public class ItemVariantCreateRequest
    {
        [MaxLength(32)]
        public string? Color { get; set; }

        [MaxLength(32)]
        public string? Size { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Price { get; set; }
    }

    public class ItemVariantUpdateRequest
    {
        [MaxLength(32)]
        public string? Color { get; set; }

        [MaxLength(32)]
        public string? Size { get; set; }

        [Range(0, int.MaxValue)]
        public int? Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Price { get; set; }

        public bool? IsActive { get; set; }
    }

    //  ItemContact Requests 

    public class ItemContactUpsertRequest
    {
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        [MaxLength(254)]
        public string? Email { get; set; }

        [MaxLength(128)]
        public string? Address { get; set; }
    }

    //  ItemImage Requests 

    public class ItemImageAddRequest
    {
        [Required]
        [MaxLength(256)]
        public string ImageUrl { get; set; } = null!;
    }

    // Upload response used by upload endpoint
    public class UploadResultResponse
    {
        public string Path { get; set; } = null!; // relative path to saved file (e.g. /uploads/abc.jpg)
    }

    //  ItemRequisition Responses 

    public class ItemRequisitionListResponse
    {
        public int RequisitionId { get; set; }
        public int ItemId { get; set; }
        public bool FromSchool { get; set; }
        public int ItemVariantId { get; set; }
        public string? ItemName { get; set; }
        public string? ItemImageUrl { get; set; }
        public string? VariantColor { get; set; }
        public string? VariantSize { get; set; }
        public int IdParent { get; set; }
        public string? ParentName { get; set; }
        public int Quantity { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? NeedFrom { get; set; }
        public DateTime? NeedUntil { get; set; }
        public DateOnly? ExpectedReturnDate { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public byte Status { get; set; }
        public string? Note { get; set; }
    }

    public class ItemRequisitionDetailResponse : ItemRequisitionListResponse
    {
        public int? ReturnQuantity { get; set; }
    }

    //  ItemRequisition Requests 

    public class ItemRequisitionCreateRequest
    {
        [Required]
        public int ItemVariantId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        // When the parent wants the item from
        public DateTime? NeedFrom { get; set; }

        // When the parent wants the item until
        public DateTime? NeedUntil { get; set; }

        [MaxLength(256)]
        public string? Note { get; set; }
    }

    public class ItemRequisitionReviewRequest
    {
        [Required]
        public bool Approve { get; set; }

        // Staff sets the return deadline only on approval
        public DateOnly? ExpectedReturnDate { get; set; }

        [MaxLength(256)]
        public string? Note { get; set; }
    }

    public class ItemRequisitionReturnRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int ReturnQuantity { get; set; }

        [MaxLength(256)]
        public string? ReturnNote { get; set; }  // describes condition/defects on return
    }

    //  ItemCategory Requests 

    public class ItemCategoryCreateRequest
    {
        [Required]
        [MaxLength(128)]
        public string CatgName { get; set; } = null!;
    }
}