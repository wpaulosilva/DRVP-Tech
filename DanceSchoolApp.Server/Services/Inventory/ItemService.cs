using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Inventory;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Inventory
{
    public class ItemService
    {
        private readonly AppDbContext _context;

        public ItemService(AppDbContext context)
        {
            _context = context;
        }

        //  Item Queries 

        public async Task<PagedResult<ItemListResponse>> GetItemsAsync(
            bool? fromSchool, PagedQuery query, int? categoryId = null)
        {
            var dbQuery = _context.Items
                .Include(i => i.IdCategoryNavigation)
                .Include(i => i.ItemImages)
                .Where(i => i.IsActive)
                .AsQueryable();

            if (fromSchool.HasValue)
                dbQuery = dbQuery.Where(i => i.FromSchool == fromSchool.Value);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim().ToLower();
                dbQuery = dbQuery.Where(i => i.Name.ToLower().Contains(term));
            }

            if (categoryId.HasValue)
                dbQuery = dbQuery.Where(i => i.IdCategory == categoryId.Value);

            var total = await dbQuery.CountAsync();

            var items = await dbQuery
                .OrderByDescending(i => i.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(i => new ItemListResponse
                {
                    ItemId = i.ItemId,
                    Name = i.Name,
                    Description = i.Description,
                    FromSchool = i.FromSchool,
                    IdOwner = i.IdOwner,
                    IsActive = i.IsActive,
                    CreatedAt = i.CreatedAt,
                    Category = i.IdCategoryNavigation == null ? null : new ItemCategorySummaryResponse
                    {
                        CategoryId = i.IdCategoryNavigation.CategoryId,
                        CatgName = i.IdCategoryNavigation.CatgName
                    },
                    Images = i.ItemImages.Select(img => new ItemImageResponse
                    {
                        ImageId = img.ImageId,
                        ImageUrl = img.ImageUrl
                    }).ToList(),
                    VariantCount = i.ItemVariants.Count(v => v.IsActive == true)
                })
                .ToListAsync();

            return new PagedResult<ItemListResponse>
            {
                Items = items,
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<ItemDetailResponse> GetItemAsync(int id)
        {
            var item = await _context.Items
                .Include(i => i.IdCategoryNavigation)
                .Include(i => i.ItemImages)
                .Include(i => i.ItemVariants)
                .FirstOrDefaultAsync(i => i.ItemId == id);

            if (item is null)
                throw new KeyNotFoundException($"Item with id {id} was not found.");

            return MapToDetail(item);
        }

        public async Task<PagedResult<ItemListResponse>> GetItemsByCategoryAsync(
    int categoryId, bool? fromSchool, PagedQuery query)
        {
            var categoryExists = await _context.ItemCategories
                .AnyAsync(c => c.CategoryId == categoryId && c.IsActive);

            if (!categoryExists)
                throw new KeyNotFoundException($"Category with id {categoryId} was not found.");

            var dbQuery = _context.Items
                .Include(i => i.IdCategoryNavigation)
                .Include(i => i.ItemImages)
                .Where(i => i.IsActive && i.IdCategory == categoryId)
                .AsQueryable();

            if (fromSchool.HasValue)
                dbQuery = dbQuery.Where(i => i.FromSchool == fromSchool.Value);

            var total = await dbQuery.CountAsync();

            var items = await dbQuery
                .OrderByDescending(i => i.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(i => new ItemListResponse
                {
                    ItemId = i.ItemId,
                    Name = i.Name,
                    Description = i.Description,
                    FromSchool = i.FromSchool,
                    IdOwner = i.IdOwner,
                    IsActive = i.IsActive,
                    CreatedAt = i.CreatedAt,
                    Category = i.IdCategoryNavigation == null ? null : new ItemCategorySummaryResponse
                    {
                        CategoryId = i.IdCategoryNavigation.CategoryId,
                        CatgName = i.IdCategoryNavigation.CatgName
                    },
                    Images = i.ItemImages.Select(img => new ItemImageResponse
                    {
                        ImageId = img.ImageId,
                        ImageUrl = img.ImageUrl
                    }).ToList(),
                    VariantCount = i.ItemVariants.Count(v => v.IsActive == true)
                })
                .ToListAsync();

            return new PagedResult<ItemListResponse>
            {
                Items = items,
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        //  Item Commands

        public async Task<int> CreateItemAsync(ItemCreateRequest request, int ownerUserId, bool fromSchool)
        {
            var item = new Item
            {
                Name = request.Name,
                Description = request.Description,
                FromSchool = fromSchool,
                IdOwner = ownerUserId,
                IdCategory = request.IdCategory,
                ContactPhone = request.ContactPhone,
                ContactEmail = request.ContactEmail,
                ContactAddress = request.ContactAddress,
                CreatedAt = DateOnly.FromDateTime(DateTime.Now),
                IsActive = true
            };

            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            return item.ItemId;
        }

        public async Task UpdateItemAsync(int id, ItemUpdateRequest request)
        {
            var item = await _context.Items.FirstOrDefaultAsync(i => i.ItemId == id);

            if (item is null)
                throw new KeyNotFoundException($"Item with id {id} was not found.");

            if (request.Name is not null) item.Name = request.Name;
            if (request.Description is not null) item.Description = request.Description;
            if (request.IdCategory.HasValue) item.IdCategory = request.IdCategory;
            if (request.ContactPhone is not null) item.ContactPhone = request.ContactPhone;
            if (request.ContactEmail is not null) item.ContactEmail = request.ContactEmail;
            if (request.ContactAddress is not null) item.ContactAddress = request.ContactAddress;

            await _context.SaveChangesAsync();
        }

        public async Task DeactivateItemAsync(int id)
        {
            var rows = await _context.Items
                .Where(i => i.ItemId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));

            if (rows == 0)
                throw new KeyNotFoundException($"Item with id {id} was not found.");
        }

        //  Images 

        public async Task<int> AddImageAsync(int itemId, ItemImageAddRequest request)
        {
            var exists = await _context.Items.AnyAsync(i => i.ItemId == itemId);
            if (!exists)
                throw new KeyNotFoundException($"Item with id {itemId} was not found.");

            // Ensure we store only relative path (already expected by client)
            var image = new ItemImage
            {
                IdItem = itemId,
                ImageUrl = request.ImageUrl?.Trim()
            };

            _context.ItemImages.Add(image);
            await _context.SaveChangesAsync();

            return image.ImageId;
        }

        public async Task<int> AddImageFromFileAsync(int itemId, string relativePath)
        {
            var exists = await _context.Items.AnyAsync(i => i.ItemId == itemId);

            if (!exists)
                throw new KeyNotFoundException($"Item with id {itemId} was not found.");

            var image = new ItemImage
            {
                IdItem = itemId,
                ImageUrl = relativePath.Trim()
            };

            _context.ItemImages.Add(image);
            await _context.SaveChangesAsync();

            return image.ImageId;
        }

        public async Task RemoveImageAsync(int itemId, int imageId)
        {
            var image = await _context.ItemImages
                .FirstOrDefaultAsync(img => img.ImageId == imageId && img.IdItem == itemId);

            if (image is null)
                throw new KeyNotFoundException($"Image with id {imageId} not found on item {itemId}.");

            var imageCount = await _context.ItemImages.CountAsync(img => img.IdItem == itemId);
            if (imageCount <= 1)
                throw new InvalidOperationException(
                    "An item must have at least one image. Add a replacement image before removing this one.");

            _context.ItemImages.Remove(image);
            await _context.SaveChangesAsync();
        }

        //  Variants 

        public async Task<List<ItemVariantDetailResponse>> GetVariantsAsync(int itemId)
        {
            var exists = await _context.Items.AnyAsync(i => i.ItemId == itemId);
            if (!exists)
                throw new KeyNotFoundException($"Item with id {itemId} was not found.");

            return await _context.ItemVariants
                .Where(v => v.IdItem == itemId)
                .Select(v => new ItemVariantDetailResponse
                {
                    VariantId = v.VariantId,
                    IdItem = v.IdItem,
                    Color = v.Color,
                    Size = v.Size,
                    Quantity = v.Quantity,
                    Price = v.Price,
                    IsActive = v.IsActive
                }).ToListAsync();
        }

        public async Task<int> CreateVariantAsync(int itemId, ItemVariantCreateRequest request)
        {
            var exists = await _context.Items.AnyAsync(i => i.ItemId == itemId);
            if (!exists)
                throw new KeyNotFoundException($"Item with id {itemId} was not found.");

            var variant = new ItemVariant
            {
                IdItem = itemId,
                Color = request.Color,
                Size = request.Size,
                Quantity = request.Quantity,
                Price = request.Price,
                IsActive = true
            };

            _context.ItemVariants.Add(variant);
            await _context.SaveChangesAsync();

            return variant.VariantId;
        }

        public async Task UpdateVariantAsync(int itemId, int variantId, ItemVariantUpdateRequest request)
        {
            var variant = await _context.ItemVariants
                .FirstOrDefaultAsync(v => v.VariantId == variantId && v.IdItem == itemId);

            if (variant is null)
                throw new KeyNotFoundException($"Variant with id {variantId} not found on item {itemId}.");

            if (request.Color is not null) variant.Color = request.Color;
            if (request.Size is not null) variant.Size = request.Size;
            if (request.Quantity.HasValue) variant.Quantity = request.Quantity.Value;
            if (request.Price.HasValue) variant.Price = request.Price;
            if (request.IsActive.HasValue) variant.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteVariantAsync(int itemId, int variantId)
        {
            var variant = await _context.ItemVariants
                .FirstOrDefaultAsync(v => v.VariantId == variantId && v.IdItem == itemId);

            if (variant is null)
                throw new KeyNotFoundException($"Variant with id {variantId} not found on item {itemId}.");

            var hasActiveRequisitions = await _context.ItemRequisitions
                .AnyAsync(r => r.ItemVariantId == variantId && r.Status == 1);

            if (hasActiveRequisitions)
                throw new InvalidOperationException("Cannot delete a variant with active requisitions.");

            var variantCount = await _context.ItemVariants.CountAsync(v => v.IdItem == itemId);
            if (variantCount <= 1)
                throw new InvalidOperationException(
                    "An item must have at least one variant. Add a replacement variant before removing this one.");

            _context.ItemVariants.Remove(variant);
            await _context.SaveChangesAsync();
        }

        //  Ownership 

        /// <summary>
        /// Returns true when the given user owns the item (community item with
        /// matching IdOwner). Staff callers should bypass this check entirely.
        /// </summary>
        public async Task<bool> IsItemOwnerAsync(int itemId, int userId)
        {
            var item = await _context.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (item is null)
                throw new KeyNotFoundException($"Item with id {itemId} was not found.");

            return !item.FromSchool && item.IdOwner == userId;
        }

        //  Helpers 

        private static ItemDetailResponse MapToDetail(Item item) => new()
        {
            ItemId = item.ItemId,
            Name = item.Name,
            Description = item.Description,
            FromSchool = item.FromSchool,
            IdOwner = item.IdOwner,
            IsActive = item.IsActive,
            CreatedAt = item.CreatedAt,
            Category = item.IdCategoryNavigation == null ? null : new ItemCategorySummaryResponse
            {
                CategoryId = item.IdCategoryNavigation.CategoryId,
                CatgName = item.IdCategoryNavigation.CatgName
            },
            ContactPhone = item.ContactPhone,
            ContactEmail = item.ContactEmail,
            ContactAddress = item.ContactAddress,
            Images = item.ItemImages.Select(img => new ItemImageResponse
            {
                ImageId = img.ImageId,
                ImageUrl = img.ImageUrl
            }).ToList(),
            Variants = item.ItemVariants.Select(v => new ItemVariantSummaryResponse
            {
                VariantId = v.VariantId,
                Color = v.Color,
                Size = v.Size,
                Quantity = v.Quantity,
                Price = v.Price,
                IsActive = v.IsActive
            }).ToList()
        };
        public async Task<bool> IsSchoolItemAsync(int itemId)
        {
            var item = await _context.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (item is null)
                throw new KeyNotFoundException($"Item with id {itemId} was not found.");

            return item.FromSchool;
        }
    }
}
