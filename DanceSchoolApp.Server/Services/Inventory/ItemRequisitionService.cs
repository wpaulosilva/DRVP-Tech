using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Inventory;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Inventory
{
    // Requisition status constants
    public static class RequisitionStatus
    {
        public const byte Pending = 0;
        public const byte Approved = 1;
        public const byte Rejected = 2;
        public const byte Returned = 3;
    }

    public class ItemRequisitionService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public ItemRequisitionService(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        //  Queries

        /// <summary>Returns all requisitions (staff view).</summary>
        public async Task<List<ItemRequisitionListResponse>> GetAllAsync()
        {
            var reqs = await _context.ItemRequisitions
                .Include(r => r.ItemVariant)
                    .ThenInclude(v => v.IdItemNavigation)
                        .ThenInclude(i => i.ItemImages)
                .Include(r => r.IdParentNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return reqs.Select(MapToList).ToList();
        }

        /// <summary>Returns only the requisitions that belong to a given parent user.</summary>
        public async Task<List<ItemRequisitionListResponse>> GetByParentAsync(int parentUserId)
        {
            var reqs = await _context.ItemRequisitions
                .Include(r => r.ItemVariant)
                    .ThenInclude(v => v.IdItemNavigation)
                        .ThenInclude(i => i.ItemImages)
                .Include(r => r.IdParentNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .Where(r => r.IdParent == parentUserId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return reqs.Select(MapToList).ToList();
        }

        public async Task<ItemRequisitionDetailResponse> GetByIdAsync(int id)
        {
            var req = await _context.ItemRequisitions
                .Include(r => r.ItemVariant)
                    .ThenInclude(v => v.IdItemNavigation)
                .FirstOrDefaultAsync(r => r.RequisitionId == id);

            if (req is null)
                throw new KeyNotFoundException($"Requisition with id {id} was not found.");

            return MapToDetail(req);
        }

        //  Commands 

        public async Task<int> CreateAsync(ItemRequisitionCreateRequest request, int parentUserId)
        {
            var variant = await _context.ItemVariants
                .Include(v => v.IdItemNavigation) // ← adicionar este Include
                .FirstOrDefaultAsync(v => v.VariantId == request.ItemVariantId && v.IsActive == true);

            if (variant is null)
                throw new KeyNotFoundException($"Variant with id {request.ItemVariantId} was not found or is inactive.");

            //  Validação de datas
            var today = DateTime.Now.Date;

            if (request.NeedFrom.HasValue && request.NeedFrom.Value.Date < today)
                throw new InvalidOperationException(
                    "NeedFrom cannot be in the past.");

            if (request.NeedUntil.HasValue && request.NeedUntil.Value.Date < today)
                throw new InvalidOperationException(
                    "NeedUntil cannot be in the past.");

            if (request.NeedFrom.HasValue && request.NeedUntil.HasValue
                && request.NeedUntil.Value.Date < request.NeedFrom.Value.Date)
                throw new InvalidOperationException(
                    "NeedUntil cannot be before NeedFrom.");


            if (variant.Quantity < request.Quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock. Available: {variant.Quantity}, requested: {request.Quantity}.");

            var requisition = new ItemRequisition
            {
                ItemVariantId = request.ItemVariantId,
                IdParent = parentUserId,
                Quantity = request.Quantity,
                RequestedAt = DateTime.Now,
                NeedFrom = request.NeedFrom,
                NeedUntil = request.NeedUntil,
                Note = request.Note,
                Status = RequisitionStatus.Pending
            };

            _context.ItemRequisitions.Add(requisition);
            await _context.SaveChangesAsync();

            return requisition.RequisitionId;
        }

        /// <summary>Staff approves or rejects a pending requisition.</summary>
        public async Task ReviewAsync(int id, ItemRequisitionReviewRequest request)
        {
            var requisition = await _context.ItemRequisitions
                .Include(r => r.ItemVariant)
                    .ThenInclude(v => v.IdItemNavigation)
                .FirstOrDefaultAsync(r => r.RequisitionId == id);

            if (requisition is null)
                throw new KeyNotFoundException($"Requisition with id {id} was not found.");

            if (requisition.Status != RequisitionStatus.Pending)
                throw new InvalidOperationException("Only pending requisitions can be reviewed.");

            if (!requisition.ItemVariant.IdItemNavigation.FromSchool)
                throw new InvalidOperationException("Community item requisitions must be reviewed by the item owner, not staff.");

            if (request.Approve)
            {
                // Deduct stock
                if (requisition.ItemVariant.Quantity < requisition.Quantity)
                    throw new InvalidOperationException("Insufficient stock at time of approval.");

                requisition.ItemVariant.Quantity -= requisition.Quantity;
                requisition.Status = RequisitionStatus.Approved;
                requisition.ExpectedReturnDate = request.ExpectedReturnDate;
                // Note: NeedFrom already holds the parent's requested start date.
            }
            else
            {
                requisition.Status = RequisitionStatus.Rejected;
            }

            if (request.Note is not null)
                requisition.Note = request.Note;

            await _context.SaveChangesAsync();

            if (request.Approve)
            {
                await _notificationService.SendAsync(
                    userId: requisition.IdParent,
                    title: "Requisição Aprovada",
                    message: request.ExpectedReturnDate.HasValue
                        ? $"A sua requisição de '{requisition.ItemVariant.IdItemNavigation.Name}' foi aprovada. Por favor, devolva até {request.ExpectedReturnDate.Value:dd/MM/yyyy}."
                        : $"A sua requisição de '{requisition.ItemVariant.IdItemNavigation.Name}' foi aprovada.",
                    type: NotificationType.Success,
                    entityType: "ItemRequisition",
                    entityId: requisition.RequisitionId);
            }
            else
            {
                await _notificationService.SendAsync(
                    userId: requisition.IdParent,
                    title: "Requisição Rejeitada",
                    message: $"A sua requisição de '{requisition.ItemVariant.IdItemNavigation.Name}' não foi aprovada.",
                    type: NotificationType.Warning,
                    entityType: "ItemRequisition",
                    entityId: requisition.RequisitionId);
            }
        }

        /// <summary>Parent registers the return of borrowed items.</summary>
        public async Task ReturnAsync(int id, ItemRequisitionReturnRequest request)
        {
            var requisition = await _context.ItemRequisitions
                .Include(r => r.ItemVariant)
                .FirstOrDefaultAsync(r => r.RequisitionId == id);

            if (requisition is null)
                throw new KeyNotFoundException($"Requisition with id {id} was not found.");

            if (requisition.Status != RequisitionStatus.Approved)
                throw new InvalidOperationException("Only approved requisitions can be returned.");

            if (request.ReturnQuantity > requisition.Quantity)
                throw new InvalidOperationException(
                    $"Return quantity ({request.ReturnQuantity}) exceeds requisition quantity ({requisition.Quantity}).");

            // Restore stock
            requisition.ItemVariant.Quantity += request.ReturnQuantity;
            requisition.ReturnQuantity = request.ReturnQuantity;
            requisition.ReturnedAt = DateTime.Now;
            requisition.Status = RequisitionStatus.Returned;
            if (request.ReturnNote is not null) requisition.Note = request.ReturnNote;

            await _context.SaveChangesAsync();
        }

        public async Task CancelAsync(int id, int requestingUserId, bool isStaff)
        {
            var requisition = await _context.ItemRequisitions
                .FirstOrDefaultAsync(r => r.RequisitionId == id);

            if (requisition is null)
                throw new KeyNotFoundException($"Requisition with id {id} was not found.");

            if (!isStaff && requisition.IdParent != requestingUserId)
                throw new UnauthorizedAccessException("You do not have permission to cancel this requisition.");

            if (requisition.Status != RequisitionStatus.Pending)
                throw new InvalidOperationException("Only pending requisitions can be cancelled.");

            _context.ItemRequisitions.Remove(requisition);
            await _context.SaveChangesAsync();
        }

        /// <summary>Returns all pending/active requisitions made against items owned by the given parent.</summary>
        public async Task<List<ItemRequisitionListResponse>> GetReceivedAsync(int ownerUserId)
        {
            var reqs = await _context.ItemRequisitions
                .Include(r => r.ItemVariant)
                    .ThenInclude(v => v.IdItemNavigation)
                        .ThenInclude(i => i.ItemImages)
                .Include(r => r.IdParentNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .Where(r => r.ItemVariant.IdItemNavigation.IdOwner == ownerUserId
                         && !r.ItemVariant.IdItemNavigation.FromSchool)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return reqs.Select(MapToList).ToList();
        }

        /// <summary>Item owner (parent) approves or rejects a requisition for one of their community items.</summary>
        public async Task OwnerReviewAsync(int id, ItemRequisitionReviewRequest request, int ownerUserId)
        {
            var requisition = await _context.ItemRequisitions
                .Include(r => r.ItemVariant)
                    .ThenInclude(v => v.IdItemNavigation)
                .FirstOrDefaultAsync(r => r.RequisitionId == id);

            if (requisition is null)
                throw new KeyNotFoundException($"Requisition with id {id} was not found.");

            if (requisition.ItemVariant.IdItemNavigation.FromSchool)
                throw new InvalidOperationException("School item requisitions are reviewed by staff.");

            if (requisition.ItemVariant.IdItemNavigation.IdOwner != ownerUserId)
                throw new UnauthorizedAccessException("You can only review requisitions for your own items.");

            if (requisition.Status != RequisitionStatus.Pending)
                throw new InvalidOperationException("Only pending requisitions can be reviewed.");

            if (request.Approve)
            {
                if (requisition.ItemVariant.Quantity < requisition.Quantity)
                    throw new InvalidOperationException("Insufficient quantity at time of approval.");

                requisition.ItemVariant.Quantity -= requisition.Quantity;
                requisition.Status = RequisitionStatus.Approved;
                requisition.ExpectedReturnDate = request.ExpectedReturnDate;
            }
            else
            {
                requisition.Status = RequisitionStatus.Rejected;
            }

            if (request.Note is not null)
                requisition.Note = request.Note;

            await _context.SaveChangesAsync();

            if (request.Approve)
            {
                await _notificationService.SendAsync(
                    userId: requisition.IdParent,
                    title: "Pedido Aprovado",
                    message: request.ExpectedReturnDate.HasValue
                        ? $"O seu pedido de '{requisition.ItemVariant.IdItemNavigation.Name}' foi aprovado. Por favor, devolva até {request.ExpectedReturnDate.Value:dd/MM/yyyy}."
                        : $"O seu pedido de '{requisition.ItemVariant.IdItemNavigation.Name}' foi aprovado.",
                    type: NotificationType.Success,
                    entityType: "ItemRequisition",
                    entityId: requisition.RequisitionId);
            }
            else
            {
                await _notificationService.SendAsync(
                    userId: requisition.IdParent,
                    title: "Pedido Rejeitado",
                    message: $"O seu pedido de '{requisition.ItemVariant.IdItemNavigation.Name}' não foi aprovado.",
                    type: NotificationType.Warning,
                    entityType: "ItemRequisition",
                    entityId: requisition.RequisitionId);
            }
        }

        //  Helpers

        private static ItemRequisitionListResponse MapToList(ItemRequisition r)
        {
            var p = r.IdParentNavigation?.PersonInfo;
            return new ItemRequisitionListResponse
            {
                RequisitionId = r.RequisitionId,
                ItemId        = r.ItemVariant.IdItemNavigation.ItemId,
                FromSchool    = r.ItemVariant.IdItemNavigation.FromSchool,
                ItemVariantId = r.ItemVariantId,
                ItemName      = r.ItemVariant.IdItemNavigation.Name,
                ItemImageUrl  = r.ItemVariant.IdItemNavigation.ItemImages.FirstOrDefault()?.ImageUrl,
                VariantColor  = r.ItemVariant.Color,
                VariantSize   = r.ItemVariant.Size,
                IdParent      = r.IdParent,
                ParentName    = p is not null
                    ? $"{p.FirstName} {p.LastName}".Trim()
                    : r.IdParentNavigation?.Username,
                Quantity          = r.Quantity,
                RequestedAt       = r.RequestedAt,
                NeedFrom          = r.NeedFrom,
                NeedUntil         = r.NeedUntil,
                ExpectedReturnDate = r.ExpectedReturnDate,
                ReturnedAt        = r.ReturnedAt,
                Status            = r.Status,
                Note              = r.Note
            };
        }

        private static ItemRequisitionDetailResponse MapToDetail(ItemRequisition r) => new()
        {
            RequisitionId = r.RequisitionId,
            ItemVariantId = r.ItemVariantId,
            ItemName = r.ItemVariant.IdItemNavigation.Name,
            VariantColor = r.ItemVariant.Color,
            VariantSize = r.ItemVariant.Size,
            IdParent = r.IdParent,
            Quantity = r.Quantity,
            RequestedAt = r.RequestedAt,
            NeedFrom = r.NeedFrom,
            NeedUntil = r.NeedUntil,
            ExpectedReturnDate = r.ExpectedReturnDate,
            ReturnedAt = r.ReturnedAt,
            Status = r.Status,
            Note = r.Note,
            ReturnQuantity = r.ReturnQuantity
        };
    }
}