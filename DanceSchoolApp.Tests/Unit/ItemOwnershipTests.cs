using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Inventory;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

[Trait("Category", "Unit")]
public class ItemOwnershipTests
{
    // ─── Helper ───────────────────────────────────────────────────────────────

    private static (ItemService service, int itemId) SeedItem(
        DanceSchoolApp.Server.Data.AppDbContext db,
        bool fromSchool,
        int ownerUserId)
    {
        var item = new Item
        {
            Name = "Test Item",
            FromSchool = fromSchool,
            IdOwner = ownerUserId,
            IsActive = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        db.Items.Add(item);
        db.SaveChanges();

        return (new ItemService(db), item.ItemId);
    }

    // ─── IsItemOwnerAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task IsItemOwner_CommunityItem_OwnerReturnsTrue()
    {
        var db = DbContextFactory.Create();
        var (service, itemId) = SeedItem(db, fromSchool: false, ownerUserId: 42);

        var result = await service.IsItemOwnerAsync(itemId, userId: 42);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsItemOwner_CommunityItem_OtherUserReturnsFalse()
    {
        var db = DbContextFactory.Create();
        var (service, itemId) = SeedItem(db, fromSchool: false, ownerUserId: 42);

        var result = await service.IsItemOwnerAsync(itemId, userId: 99);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsItemOwner_SchoolItem_AlwaysReturnsFalse()
    {
        var db = DbContextFactory.Create();
        var (service, itemId) = SeedItem(db, fromSchool: true, ownerUserId: 42);

        var result = await service.IsItemOwnerAsync(itemId, userId: 42);

        result.Should().BeFalse(
            because: "school items are managed by staff, never by the creating user");
    }

    [Fact]
    public async Task IsItemOwner_NonexistentItem_ThrowsKeyNotFound()
    {
        var db = DbContextFactory.Create();
        var service = new ItemService(db);

        var act = () => service.IsItemOwnerAsync(itemId: 999, userId: 1);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ─── Owner can manage own community item ──────────────────────────────────

    [Fact]
    public async Task Owner_CanUpdateOwnCommunityItem()
    {
        var db = DbContextFactory.Create();
        var (service, itemId) = SeedItem(db, fromSchool: false, ownerUserId: 10);

        await service.UpdateItemAsync(itemId,
            new Server.DTOs.Inventory.ItemUpdateRequest { Name = "Updated" });

        var updated = await service.GetItemAsync(itemId);
        updated.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task Owner_CanCreateVariantOnOwnItem()
    {
        var db = DbContextFactory.Create();
        var (service, itemId) = SeedItem(db, fromSchool: false, ownerUserId: 10);

        var variantId = await service.CreateVariantAsync(itemId,
            new Server.DTOs.Inventory.ItemVariantCreateRequest
            {
                Color = "Red",
                Size = "M",
                Quantity = 5,
                Price = 10.00m
            });

        variantId.Should().BeGreaterThan(0);

        var variants = await service.GetVariantsAsync(itemId);
        variants.Should().HaveCount(1);
        variants[0].Color.Should().Be("Red");
    }

    [Fact]
    public async Task Owner_CanDeactivateAndReactivateVariant()
    {
        var db = DbContextFactory.Create();
        var (service, itemId) = SeedItem(db, fromSchool: false, ownerUserId: 10);
        var variantId = await service.CreateVariantAsync(itemId,
            new Server.DTOs.Inventory.ItemVariantCreateRequest
            {
                Color = "Blue",
                Quantity = 3
            });

        // Deactivate via update
        await service.UpdateVariantAsync(itemId, variantId,
            new Server.DTOs.Inventory.ItemVariantUpdateRequest { IsActive = false });

        var variants = await service.GetVariantsAsync(itemId);
        variants.Single().IsActive.Should().BeFalse();

        // Reactivate via update
        await service.UpdateVariantAsync(itemId, variantId,
            new Server.DTOs.Inventory.ItemVariantUpdateRequest { IsActive = true });

        variants = await service.GetVariantsAsync(itemId);
        variants.Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Owner_CanAddAndRemoveImage()
    {
        var db = DbContextFactory.Create();
        var (service, itemId) = SeedItem(db, fromSchool: false, ownerUserId: 10);

        var imageId = await service.AddImageAsync(itemId,
            new Server.DTOs.Inventory.ItemImageAddRequest
            {
                ImageUrl = "https://example.com/photo.jpg"
            });

        imageId.Should().BeGreaterThan(0);

        var detail = await service.GetItemAsync(itemId);
        detail.Images.Should().HaveCount(1);

        await service.RemoveImageAsync(itemId, imageId);

        detail = await service.GetItemAsync(itemId);
        detail.Images.Should().BeEmpty();
    }
}
