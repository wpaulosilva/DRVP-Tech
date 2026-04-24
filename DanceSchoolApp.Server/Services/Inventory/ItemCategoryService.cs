using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Inventory;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Inventory
{
    public class ItemCategoryService
    {
        private readonly AppDbContext _context;

        public ItemCategoryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ItemCategorySummaryResponse>> GetAllAsync()
        {
            return await _context.ItemCategories
                .Where(c => c.IsActive)
                .Select(c => new ItemCategorySummaryResponse
                {
                    CategoryId = c.CategoryId,
                    CatgName = c.CatgName
                }).ToListAsync();
        }

        public async Task<ItemCategorySummaryResponse> GetByIdAsync(int id)
        {
            var category = await _context.ItemCategories
                .Where(c => c.CategoryId == id && c.IsActive)
                .Select(c => new ItemCategorySummaryResponse
                {
                    CategoryId = c.CategoryId,
                    CatgName = c.CatgName
                })
                .FirstOrDefaultAsync();

            if (category is null)
                throw new KeyNotFoundException($"Category with id {id} was not found.");

            return category;
        }

        public async Task<int> CreateAsync(ItemCategoryCreateRequest request)
        {
            bool exists = await _context.ItemCategories
                .AnyAsync(c => c.CatgName == request.CatgName && c.IsActive);

            if (exists)
                throw new InvalidOperationException($"A category named '{request.CatgName}' already exists.");

            var category = new ItemCategory
            {
                CatgName = request.CatgName,
                IsActive = true
            };

            _context.ItemCategories.Add(category);
            await _context.SaveChangesAsync();

            return category.CategoryId;
        }

        public async Task DeactivateAsync(int id)
        {
            var rows = await _context.ItemCategories
                .Where(c => c.CategoryId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));

            if (rows == 0)
                throw new KeyNotFoundException($"Category with id {id} was not found.");
        }
    }
}