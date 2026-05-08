import { useCallback, useEffect, useRef, useState } from 'react'
import Modal from '../../components/common/Modal'
import Tabs from '../../components/common/Tabs'
import {
    getItems, getItem, createSchoolItem, updateItem, deleteItem,
    uploadItemImage, removeItemImage,
    getVariants, createVariant, updateVariant, deleteVariant,
    getCategories,
    getRequisitions, reviewRequisition, returnRequisition,
} from '../../services/inventoryService'
import '../../styles/Inventory.css'

const PAGE_SIZE = 12

const TABS = [
    { value: 'school',       label: 'Escolar' },
    { value: 'community',    label: 'Comunidade' },
    { value: 'requisitions', label: 'Requisições' },
]

const REQ_STATUS = {
    0: { label: 'Pendente',  cls: 'inv-status-pill--pending' },
    1: { label: 'Aprovado',  cls: 'inv-status-pill--approved' },
    2: { label: 'Rejeitado', cls: 'inv-status-pill--rejected' },
    3: { label: 'Devolvido', cls: 'inv-status-pill--returned' },
}

const emptyItemForm = { name: '', description: '', idCategory: '', contactPhone: '', contactEmail: '', contactAddress: '' }
const emptyVariantForm = { color: '', size: '', quantity: 1, price: '' }

function stockPill(variantCount) {
    if (variantCount === 0) return <span className="inv-stock-pill inv-stock-pill--out">Sem stock</span>
    return <span className="inv-stock-pill inv-stock-pill--in">{variantCount} var.</span>
}

function fmtDate(v) {
    if (!v) return '—'
    return new Date(v).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function fmtPrice(v) {
    if (v == null) return '—'
    return Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' })
}

export default function StaffInventoryPage() {
    const [tab, setTab] = useState('school')

    // ── Item list state ──────────────────────────────────────────────────────
    const [items, setItems]           = useState([])
    const [totalCount, setTotalCount] = useState(0)
    const [page, setPage]             = useState(1)
    const [search, setSearch]         = useState('')
    const [searchInput, setSearchInput] = useState('')
    const [categoryId, setCategoryId] = useState('')
    const [categories, setCategories] = useState([])
    const [loadingItems, setLoadingItems] = useState(false)
    const [itemError, setItemError]   = useState(null)
    const searchTimer = useRef(null)

    // ── Requisitions state ───────────────────────────────────────────────────
    const [requisitions, setRequisitions] = useState([])
    const [reqStatusFilter, setReqStatusFilter] = useState('')
    const [loadingReqs, setLoadingReqs]   = useState(false)

    // ── Detail modal ─────────────────────────────────────────────────────────
    const [selectedItem, setSelectedItem]   = useState(null)
    const [showDetail, setShowDetail]       = useState(false)
    const [detailLoading, setDetailLoading] = useState(false)
    const [imgIndex, setImgIndex]           = useState(0)
    const [editMode, setEditMode]           = useState(false)
    const [itemForm, setItemForm]           = useState(emptyItemForm)
    const [detailError, setDetailError]     = useState(null)
    const [savingItem, setSavingItem]       = useState(false)

    // ── Variants ─────────────────────────────────────────────────────────────
    const [variants, setVariants]           = useState([])
    const [showAddVariant, setShowAddVariant] = useState(false)
    const [variantForm, setVariantForm]     = useState(emptyVariantForm)
    const [editingVariant, setEditingVariant] = useState(null)
    const [variantEditForm, setVariantEditForm] = useState(emptyVariantForm)
    const [variantError, setVariantError]   = useState(null)
    const [savingVariant, setSavingVariant] = useState(false)

    // ── Create item modal ────────────────────────────────────────────────────
    const [showCreate, setShowCreate] = useState(false)
    const [createForm, setCreateForm] = useState(emptyItemForm)
    const [createError, setCreateError] = useState(null)
    const [savingCreate, setSavingCreate] = useState(false)

    // ── Image upload ─────────────────────────────────────────────────────────
    const fileInputRef    = useRef(null)
    const [uploadingImg, setUploadingImg] = useState(false)

    // ── Review modal (requisitions) ──────────────────────────────────────────
    const [showReview, setShowReview]     = useState(false)
    const [reviewTarget, setReviewTarget] = useState(null)
    const [reviewForm, setReviewForm]     = useState({ approve: true, expectedReturnDate: '', note: '' })
    const [reviewError, setReviewError]   = useState(null)
    const [savingReview, setSavingReview] = useState(false)

    // ── Return modal ─────────────────────────────────────────────────────────
    const [showReturn, setShowReturn]       = useState(false)
    const [returnTarget, setReturnTarget]   = useState(null)
    const [returnQty, setReturnQty]         = useState(1)
    const [returnError, setReturnError]     = useState(null)
    const [savingReturn, setSavingReturn]   = useState(false)

    // ── Load categories once ─────────────────────────────────────────────────
    useEffect(() => {
        getCategories().then(data => {
            setCategories(Array.isArray(data) ? data : [])
        }).catch(() => {})
    }, [])

    // ── Load items when tab / filters / page change ──────────────────────────
    const loadItems = useCallback(async () => {
        if (tab === 'requisitions') return
        setLoadingItems(true)
        setItemError(null)
        try {
            const fromSchool = tab === 'school'
            const result = await getItems({ fromSchool, search, categoryId: categoryId || undefined, page, pageSize: PAGE_SIZE })
            const list = result?.items ?? result?.Items ?? []
            setItems(list)
            setTotalCount(result?.totalCount ?? result?.TotalCount ?? 0)
        } catch (e) {
            setItemError(e.message)
        } finally {
            setLoadingItems(false)
        }
    }, [tab, search, categoryId, page])

    useEffect(() => { loadItems() }, [loadItems])

    // ── Load requisitions ────────────────────────────────────────────────────
    const loadReqs = useCallback(async () => {
        if (tab !== 'requisitions') return
        setLoadingReqs(true)
        try {
            const data = await getRequisitions()
            setRequisitions(Array.isArray(data) ? data : [])
        } catch (e) {
            console.error(e)
        } finally {
            setLoadingReqs(false)
        }
    }, [tab])

    useEffect(() => { loadReqs() }, [loadReqs])

    // ── Tab change resets filters ────────────────────────────────────────────
    const handleTabChange = (t) => {
        setTab(t)
        setPage(1)
        setSearch('')
        setSearchInput('')
        setCategoryId('')
    }

    // ── Search debounce ──────────────────────────────────────────────────────
    const handleSearchInput = (val) => {
        setSearchInput(val)
        clearTimeout(searchTimer.current)
        searchTimer.current = setTimeout(() => {
            setSearch(val)
            setPage(1)
        }, 400)
    }

    // ── Open item detail ─────────────────────────────────────────────────────
    const openDetail = async (itemId) => {
        setDetailLoading(true)
        setShowDetail(true)
        setEditMode(false)
        setDetailError(null)
        setShowAddVariant(false)
        setEditingVariant(null)
        try {
            const detail = await getItem(itemId)
            setSelectedItem(detail)
            setImgIndex(0)
            setItemForm({
                name: detail.name ?? '',
                description: detail.description ?? '',
                idCategory: detail.category?.categoryId ?? '',
                contactPhone: detail.contactPhone ?? '',
                contactEmail: detail.contactEmail ?? '',
                contactAddress: detail.contactAddress ?? '',
            })
            const varList = detail.variants ?? []
            setVariants(varList)
        } catch (e) {
            setDetailError(e.message)
        } finally {
            setDetailLoading(false)
        }
    }

    // ── Save item edits ──────────────────────────────────────────────────────
    const handleSaveItem = async (e) => {
        e.preventDefault()
        setSavingItem(true)
        setDetailError(null)
        try {
            const body = {}
            if (itemForm.name)         body.name = itemForm.name
            if (itemForm.description)  body.description = itemForm.description
            if (itemForm.idCategory)   body.idCategory = Number(itemForm.idCategory)
            if (itemForm.contactPhone) body.contactPhone = itemForm.contactPhone
            if (itemForm.contactEmail) body.contactEmail = itemForm.contactEmail
            if (itemForm.contactAddress) body.contactAddress = itemForm.contactAddress
            await updateItem(selectedItem.itemId, body)
            await openDetail(selectedItem.itemId)
            setEditMode(false)
            loadItems()
        } catch (e) {
            setDetailError(e.message)
        } finally {
            setSavingItem(false)
        }
    }

    // ── Deactivate item ──────────────────────────────────────────────────────
    const handleDeactivate = async () => {
        if (!window.confirm(`Desativar "${selectedItem?.name}"?`)) return
        try {
            await deleteItem(selectedItem.itemId)
            setShowDetail(false)
            loadItems()
        } catch (e) {
            setDetailError(e.message)
        }
    }

    // ── Image upload ─────────────────────────────────────────────────────────
    const handleImageUpload = async (e) => {
        const file = e.target.files?.[0]
        if (!file) return
        setUploadingImg(true)
        try {
            await uploadItemImage(selectedItem.itemId, file)
            await openDetail(selectedItem.itemId)
            loadItems()
        } catch (e) {
            setDetailError(e.message)
        } finally {
            setUploadingImg(false)
            e.target.value = ''
        }
    }

    const handleRemoveImage = async (imageId) => {
        if (!window.confirm('Remover esta imagem?')) return
        try {
            await removeItemImage(selectedItem.itemId, imageId)
            await openDetail(selectedItem.itemId)
            loadItems()
        } catch (e) {
            setDetailError(e.message)
        }
    }

    // ── Add variant ──────────────────────────────────────────────────────────
    const handleAddVariant = async (e) => {
        e.preventDefault()
        setSavingVariant(true)
        setVariantError(null)
        try {
            const body = {
                quantity: Number(variantForm.quantity),
                ...(variantForm.color ? { color: variantForm.color } : {}),
                ...(variantForm.size  ? { size: variantForm.size }   : {}),
                ...(variantForm.price ? { price: Number(variantForm.price) } : {}),
            }
            await createVariant(selectedItem.itemId, body)
            const updated = await getVariants(selectedItem.itemId)
            setVariants(Array.isArray(updated) ? updated : [])
            setVariantForm(emptyVariantForm)
            setShowAddVariant(false)
            loadItems()
        } catch (e) {
            setVariantError(e.message)
        } finally {
            setSavingVariant(false)
        }
    }

    // ── Edit variant ─────────────────────────────────────────────────────────
    const startEditVariant = (v) => {
        setEditingVariant(v.variantId)
        setVariantEditForm({
            color: v.color ?? '',
            size: v.size ?? '',
            quantity: v.quantity,
            price: v.price ?? '',
        })
    }

    const handleUpdateVariant = async (variantId) => {
        setSavingVariant(true)
        setVariantError(null)
        try {
            const body = {
                quantity: Number(variantEditForm.quantity),
                ...(variantEditForm.color ? { color: variantEditForm.color } : {}),
                ...(variantEditForm.size  ? { size: variantEditForm.size }   : {}),
                price: variantEditForm.price ? Number(variantEditForm.price) : null,
            }
            await updateVariant(selectedItem.itemId, variantId, body)
            const updated = await getVariants(selectedItem.itemId)
            setVariants(Array.isArray(updated) ? updated : [])
            setEditingVariant(null)
            loadItems()
        } catch (e) {
            setVariantError(e.message)
        } finally {
            setSavingVariant(false)
        }
    }

    const handleToggleVariantActive = async (v) => {
        try {
            await updateVariant(selectedItem.itemId, v.variantId, { isActive: !v.isActive })
            const updated = await getVariants(selectedItem.itemId)
            setVariants(Array.isArray(updated) ? updated : [])
            loadItems()
        } catch (e) {
            setVariantError(e.message)
        }
    }

    const handleDeleteVariant = async (variantId) => {
        if (!window.confirm('Eliminar esta variante?')) return
        try {
            await deleteVariant(selectedItem.itemId, variantId)
            const updated = await getVariants(selectedItem.itemId)
            setVariants(Array.isArray(updated) ? updated : [])
            loadItems()
        } catch (e) {
            setVariantError(e.message)
        }
    }

    // ── Create school item ────────────────────────────────────────────────────
    const handleCreateItem = async (e) => {
        e.preventDefault()
        if (!createForm.name.trim()) { setCreateError('Nome obrigatório.'); return }
        setSavingCreate(true)
        setCreateError(null)
        try {
            const body = {
                name: createForm.name,
                ...(createForm.description ? { description: createForm.description } : {}),
                ...(createForm.idCategory  ? { idCategory: Number(createForm.idCategory) } : {}),
                ...(createForm.contactPhone ? { contactPhone: createForm.contactPhone } : {}),
                ...(createForm.contactEmail ? { contactEmail: createForm.contactEmail } : {}),
                ...(createForm.contactAddress ? { contactAddress: createForm.contactAddress } : {}),
            }
            const res = await createSchoolItem(body)
            setShowCreate(false)
            setCreateForm(emptyItemForm)
            loadItems()
            // Open the new item to add images and variants
            const newId = res?.itemId ?? res?.ItemId
            if (newId) openDetail(newId)
        } catch (e) {
            setCreateError(e.message)
        } finally {
            setSavingCreate(false)
        }
    }

    // ── Requisition review ────────────────────────────────────────────────────
    const handleReview = async (e) => {
        e.preventDefault()
        setSavingReview(true)
        setReviewError(null)
        try {
            const body = {
                approve: reviewForm.approve,
                ...(reviewForm.expectedReturnDate ? { expectedReturnDate: reviewForm.expectedReturnDate } : {}),
                ...(reviewForm.note ? { note: reviewForm.note } : {}),
            }
            await reviewRequisition(reviewTarget.requisitionId, body)
            setShowReview(false)
            loadReqs()
        } catch (e) {
            setReviewError(e.message)
        } finally {
            setSavingReview(false)
        }
    }

    // ── Record return ─────────────────────────────────────────────────────────
    const handleReturn = async (e) => {
        e.preventDefault()
        setSavingReturn(true)
        setReturnError(null)
        try {
            await returnRequisition(returnTarget.requisitionId, { returnQuantity: Number(returnQty) })
            setShowReturn(false)
            loadReqs()
        } catch (e) {
            setReturnError(e.message)
        } finally {
            setSavingReturn(false)
        }
    }

    // ── Filtered requisitions ─────────────────────────────────────────────────
    const filteredReqs = reqStatusFilter !== ''
        ? requisitions.filter(r => String(r.status ?? r.Status) === reqStatusFilter)
        : requisitions

    const totalPages = Math.ceil(totalCount / PAGE_SIZE)

    // ── Current images for detail modal ──────────────────────────────────────
    const images = selectedItem?.images ?? []
    const currentImage = images[imgIndex]

    return (
        <section className="dashboard-page-card">
            {/* Header */}
            <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
                <h2 style={{ margin: 0 }}>Inventário</h2>
                {tab !== 'requisitions' && (
                    <button type="button" className="btn btn-primary" onClick={() => { setCreateForm(emptyItemForm); setCreateError(null); setShowCreate(true) }}>
                        + Novo Artigo Escolar
                    </button>
                )}
            </div>

            <Tabs tabs={TABS} activeTab={tab} onTabChange={handleTabChange} />

            {/* Item grid tabs */}
            {tab !== 'requisitions' && (
                <>
                    {/* Filters */}
                    <div className="inv-filter-bar" style={{ marginTop: 16 }}>
                        <input
                            type="text"
                            className="inv-search-input"
                            placeholder="Pesquisar artigo..."
                            value={searchInput}
                            onChange={e => handleSearchInput(e.target.value)}
                        />
                        <select
                            className="inv-filter-select"
                            value={categoryId}
                            onChange={e => { setCategoryId(e.target.value); setPage(1) }}
                        >
                            <option value="">Todas categorias</option>
                            {categories.map(c => (
                                <option key={c.categoryId ?? c.CategoryId} value={c.categoryId ?? c.CategoryId}>
                                    {c.catgName ?? c.CatgName}
                                </option>
                            ))}
                        </select>
                    </div>

                    {/* Grid */}
                    {loadingItems ? (
                        <p className="inv-loading">A carregar...</p>
                    ) : itemError ? (
                        <p className="inv-error">{itemError}</p>
                    ) : items.length === 0 ? (
                        <div className="inv-empty">
                            <div className="inv-empty-icon">📦</div>
                            <p>Nenhum artigo encontrado.</p>
                        </div>
                    ) : (
                        <div className="inv-grid">
                            {items.map(item => {
                                const id   = item.itemId ?? item.ItemId
                                const img  = (item.images ?? item.Images ?? [])[0]?.imageUrl
                                const name = item.name ?? item.Name ?? ''
                                const desc = item.description ?? item.Description
                                const cat  = item.category?.catgName ?? item.Category?.CatgName
                                const vc   = item.variantCount ?? item.VariantCount ?? 0
                                return (
                                    <div key={id} className="inv-card" onClick={() => openDetail(id)}>
                                        {img
                                            ? <img src={img} alt={name} className="inv-card-img" />
                                            : <div className="inv-card-img-placeholder">{name[0]}</div>
                                        }
                                        <div className="inv-card-body">
                                            {cat && <span className="inv-card-category">{cat}</span>}
                                            <p className="inv-card-name">{name}</p>
                                            {desc && <p className="inv-card-desc">{desc}</p>}
                                        </div>
                                        <div className="inv-card-footer">
                                            <div className="inv-card-meta">{stockPill(vc)}</div>
                                            <span style={{ fontSize: '0.8rem', color: '#9ca3af' }}>Ver →</span>
                                        </div>
                                    </div>
                                )
                            })}
                        </div>
                    )}

                    {/* Pagination */}
                    {totalPages > 1 && (
                        <div className="inv-pagination">
                            <button className="inv-page-btn" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹</button>
                            <span className="inv-page-info">{page} / {totalPages}</span>
                            <button className="inv-page-btn" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>›</button>
                        </div>
                    )}
                </>
            )}

            {/* Requisitions tab */}
            {tab === 'requisitions' && (
                <>
                    <div className="inv-req-header" style={{ marginTop: 16 }}>
                        <select
                            className="inv-filter-select"
                            value={reqStatusFilter}
                            onChange={e => setReqStatusFilter(e.target.value)}
                        >
                            <option value="">Todos os estados</option>
                            <option value="0">Pendente</option>
                            <option value="1">Aprovado</option>
                            <option value="2">Rejeitado</option>
                            <option value="3">Devolvido</option>
                        </select>
                    </div>

                    {loadingReqs ? <p className="inv-loading">A carregar...</p> : (
                        filteredReqs.length === 0 ? (
                            <div className="inv-empty">
                                <div className="inv-empty-icon">📋</div>
                                <p>Nenhum pedido de requisição.</p>
                            </div>
                        ) : (
                            <div className="inv-req-list">
                                {filteredReqs.map(r => {
                                    const rid    = r.requisitionId ?? r.RequisitionId
                                    const status = r.status ?? r.Status ?? 0
                                    const stInfo = REQ_STATUS[status] ?? REQ_STATUS[0]
                                    const imgUrl = r.itemImageUrl ?? r.ItemImageUrl
                                    const itemName = r.itemName ?? r.ItemName ?? '—'
                                    const color = r.variantColor ?? r.VariantColor
                                    const size  = r.variantSize ?? r.VariantSize
                                    const parentName = r.parentName ?? r.ParentName ?? `EE #${r.idParent ?? r.IdParent}`
                                    return (
                                        <div key={rid} className="inv-req-card">
                                            {imgUrl
                                                ? <img src={imgUrl} alt={itemName} className="inv-req-img" />
                                                : <div className="inv-req-img-placeholder">{itemName[0]}</div>
                                            }
                                            <div className="inv-req-body">
                                                <p className="inv-req-title">
                                                    {itemName}
                                                    {(color || size) && <span style={{ fontWeight: 400, color: '#6b7280' }}> — {[color, size].filter(Boolean).join(' / ')}</span>}
                                                </p>
                                                <p className="inv-req-sub">por {parentName}</p>
                                                <div className="inv-req-meta">
                                                    <span className="inv-req-meta-label">Qtd:</span>
                                                    <span>{r.quantity ?? r.Quantity}</span>
                                                    <span className="inv-req-meta-label">Pedido em:</span>
                                                    <span>{fmtDate(r.requestedAt ?? r.RequestedAt)}</span>
                                                    {(r.needFrom ?? r.NeedFrom) && <>
                                                        <span className="inv-req-meta-label">Necessário de:</span>
                                                        <span>{fmtDate(r.needFrom ?? r.NeedFrom)} → {fmtDate(r.needUntil ?? r.NeedUntil)}</span>
                                                    </>}
                                                    {(r.expectedReturnDate ?? r.ExpectedReturnDate) && <>
                                                        <span className="inv-req-meta-label">Devolução prevista:</span>
                                                        <span>{fmtDate(r.expectedReturnDate ?? r.ExpectedReturnDate)}</span>
                                                    </>}
                                                    {(r.note ?? r.Note) && <>
                                                        <span className="inv-req-meta-label">Nota:</span>
                                                        <span>{r.note ?? r.Note}</span>
                                                    </>}
                                                </div>
                                            </div>
                                            <div className="inv-req-actions">
                                                <span className={`inv-status-pill ${stInfo.cls}`}>{stInfo.label}</span>
                                                {status === 0 && (
                                                    <button
                                                        type="button"
                                                        className="btn btn-primary btn-sm"
                                                        onClick={() => { setReviewTarget(r); setReviewForm({ approve: true, expectedReturnDate: '', note: '' }); setReviewError(null); setShowReview(true) }}
                                                    >
                                                        Responder
                                                    </button>
                                                )}
                                                {status === 1 && (
                                                    <button
                                                        type="button"
                                                        className="btn btn-secondary btn-sm"
                                                        onClick={() => { setReturnTarget(r); setReturnQty(r.quantity ?? r.Quantity ?? 1); setReturnError(null); setShowReturn(true) }}
                                                    >
                                                        Registar Devolução
                                                    </button>
                                                )}
                                            </div>
                                        </div>
                                    )
                                })}
                            </div>
                        )
                    )}
                </>
            )}

            {/* ── ITEM DETAIL MODAL ─────────────────────────────────────────────── */}
            <Modal open={showDetail} title={selectedItem?.name ?? 'Artigo'} onClose={() => { setShowDetail(false); setEditMode(false) }}>
                {detailLoading ? (
                    <p className="inv-loading">A carregar...</p>
                ) : selectedItem ? (
                    <>
                        {detailError && <div className="inv-form-error" style={{ marginBottom: 12 }}>{detailError}</div>}

                        <div className="inv-detail-layout">
                            {/* Images */}
                            <div className="inv-detail-images">
                                {currentImage
                                    ? <img src={currentImage.imageUrl} alt={selectedItem.name} className="inv-detail-main-img" />
                                    : <div className="inv-detail-main-placeholder">{(selectedItem.name ?? '?')[0]}</div>
                                }
                                {images.length > 1 && (
                                    <div className="inv-detail-thumbs">
                                        {images.map((img, i) => (
                                            <img
                                                key={img.imageId}
                                                src={img.imageUrl}
                                                alt=""
                                                className={`inv-detail-thumb${i === imgIndex ? ' inv-detail-thumb--active' : ''}`}
                                                onClick={() => setImgIndex(i)}
                                            />
                                        ))}
                                    </div>
                                )}
                                <div className="inv-detail-img-actions">
                                    <label className="inv-upload-label">
                                        {uploadingImg ? 'A carregar...' : '+ Imagem'}
                                        <input type="file" ref={fileInputRef} accept=".jpg,.jpeg,.png" style={{ display: 'none' }} onChange={handleImageUpload} />
                                    </label>
                                    {images.length > 1 && currentImage && (
                                        <button type="button" className="btn btn-danger btn-sm" onClick={() => handleRemoveImage(currentImage.imageId)}>
                                            Remover imagem
                                        </button>
                                    )}
                                </div>
                            </div>

                            {/* Info */}
                            <div className="inv-detail-info">
                                {!editMode ? (
                                    <>
                                        <div className="inv-detail-meta-grid">
                                            <span className="inv-detail-meta-label">Categoria:</span>
                                            <span>{selectedItem.category?.catgName ?? '—'}</span>
                                            <span className="inv-detail-meta-label">Tipo:</span>
                                            <span>{selectedItem.fromSchool ? 'Escolar' : 'Comunidade'}</span>
                                            {selectedItem.contactPhone && <>
                                                <span className="inv-detail-meta-label">Telefone:</span>
                                                <span>{selectedItem.contactPhone}</span>
                                            </>}
                                            {selectedItem.contactEmail && <>
                                                <span className="inv-detail-meta-label">Email:</span>
                                                <span>{selectedItem.contactEmail}</span>
                                            </>}
                                            {selectedItem.contactAddress && <>
                                                <span className="inv-detail-meta-label">Morada:</span>
                                                <span>{selectedItem.contactAddress}</span>
                                            </>}
                                        </div>
                                        {selectedItem.description && (
                                            <p className="inv-detail-desc">{selectedItem.description}</p>
                                        )}
                                        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
                                            <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditMode(true)}>Editar</button>
                                            <button type="button" className="btn btn-danger btn-sm" onClick={handleDeactivate}>Desativar</button>
                                        </div>
                                    </>
                                ) : (
                                    <form onSubmit={handleSaveItem} className="inv-modal-form">
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Nome *</label>
                                            <input className="inv-form-input" value={itemForm.name} onChange={e => setItemForm(f => ({ ...f, name: e.target.value }))} required />
                                        </div>
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Descrição</label>
                                            <textarea className="inv-form-textarea" value={itemForm.description} onChange={e => setItemForm(f => ({ ...f, description: e.target.value }))} />
                                        </div>
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Categoria</label>
                                            <select className="inv-form-select" value={itemForm.idCategory} onChange={e => setItemForm(f => ({ ...f, idCategory: e.target.value }))}>
                                                <option value="">Sem categoria</option>
                                                {categories.map(c => (
                                                    <option key={c.categoryId ?? c.CategoryId} value={c.categoryId ?? c.CategoryId}>
                                                        {c.catgName ?? c.CatgName}
                                                    </option>
                                                ))}
                                            </select>
                                        </div>
                                        <div className="inv-form-row-2">
                                            <div className="inv-form-group">
                                                <label className="inv-form-label">Telefone</label>
                                                <input className="inv-form-input" value={itemForm.contactPhone} onChange={e => setItemForm(f => ({ ...f, contactPhone: e.target.value }))} />
                                            </div>
                                            <div className="inv-form-group">
                                                <label className="inv-form-label">Email</label>
                                                <input type="email" className="inv-form-input" value={itemForm.contactEmail} onChange={e => setItemForm(f => ({ ...f, contactEmail: e.target.value }))} />
                                            </div>
                                        </div>
                                        <div className="modal-actions">
                                            <button type="button" className="btn btn-secondary" onClick={() => setEditMode(false)}>Cancelar</button>
                                            <button type="submit" className="btn btn-primary" disabled={savingItem}>{savingItem ? 'A guardar...' : 'Guardar'}</button>
                                        </div>
                                    </form>
                                )}
                            </div>
                        </div>

                        {/* Variants */}
                        <div className="inv-variants-section" style={{ marginTop: 24 }}>
                            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
                                <p className="inv-variants-title">Variantes ({variants.length})</p>
                                <button type="button" className="btn btn-secondary btn-sm" onClick={() => { setShowAddVariant(!showAddVariant); setVariantError(null) }}>
                                    {showAddVariant ? 'Cancelar' : '+ Adicionar'}
                                </button>
                            </div>

                            {variantError && <div className="inv-form-error" style={{ marginBottom: 10 }}>{variantError}</div>}

                            {variants.length > 0 && (
                                <table className="inv-variants-table">
                                    <thead>
                                        <tr>
                                            <th>Cor</th><th>Tamanho</th><th>Qtd</th><th>Preço</th><th>Estado</th><th></th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {variants.map(v => {
                                            const vid = v.variantId ?? v.VariantId
                                            const isInactive = v.isActive === false
                                            const isEditing = editingVariant === vid
                                            return (
                                                <tr key={vid} className={isInactive ? 'inv-variant-inactive' : ''}>
                                                    <td>{isEditing ? <input className="inv-form-input" style={{ minWidth: 70 }} value={variantEditForm.color} onChange={e => setVariantEditForm(f => ({ ...f, color: e.target.value }))} /> : (v.color ?? v.Color ?? '—')}</td>
                                                    <td>{isEditing ? <input className="inv-form-input" style={{ minWidth: 70 }} value={variantEditForm.size} onChange={e => setVariantEditForm(f => ({ ...f, size: e.target.value }))} /> : (v.size ?? v.Size ?? '—')}</td>
                                                    <td>{isEditing ? <input type="number" className="inv-form-input" style={{ minWidth: 60 }} value={variantEditForm.quantity} min={0} onChange={e => setVariantEditForm(f => ({ ...f, quantity: e.target.value }))} /> : (v.quantity ?? v.Quantity)}</td>
                                                    <td>{isEditing ? <input type="number" className="inv-form-input" style={{ minWidth: 80 }} value={variantEditForm.price} min={0} step="0.01" onChange={e => setVariantEditForm(f => ({ ...f, price: e.target.value }))} /> : fmtPrice(v.price ?? v.Price)}</td>
                                                    <td>
                                                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleToggleVariantActive(v)}>
                                                            {isInactive ? 'Ativar' : 'Pausar'}
                                                        </button>
                                                    </td>
                                                    <td>
                                                        <div className="inv-variant-actions">
                                                            {isEditing
                                                                ? <>
                                                                    <button type="button" className="btn btn-primary btn-sm" disabled={savingVariant} onClick={() => handleUpdateVariant(vid)}>OK</button>
                                                                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditingVariant(null)}>✕</button>
                                                                  </>
                                                                : <>
                                                                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => startEditVariant(v)}>Editar</button>
                                                                    <button type="button" className="btn btn-danger btn-sm" onClick={() => handleDeleteVariant(vid)}>✕</button>
                                                                  </>
                                                            }
                                                        </div>
                                                    </td>
                                                </tr>
                                            )
                                        })}
                                    </tbody>
                                </table>
                            )}

                            {showAddVariant && (
                                <form onSubmit={handleAddVariant} className="inv-add-variant-form">
                                    <div className="inv-form-row-3">
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Cor</label>
                                            <input className="inv-form-input" value={variantForm.color} onChange={e => setVariantForm(f => ({ ...f, color: e.target.value }))} placeholder="Ex: Azul" />
                                        </div>
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Tamanho</label>
                                            <input className="inv-form-input" value={variantForm.size} onChange={e => setVariantForm(f => ({ ...f, size: e.target.value }))} placeholder="Ex: M" />
                                        </div>
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Quantidade *</label>
                                            <input type="number" className="inv-form-input" value={variantForm.quantity} min={0} required onChange={e => setVariantForm(f => ({ ...f, quantity: e.target.value }))} />
                                        </div>
                                    </div>
                                    <div className="inv-form-group">
                                        <label className="inv-form-label">Preço (€)</label>
                                        <input type="number" className="inv-form-input" value={variantForm.price} min={0} step="0.01" onChange={e => setVariantForm(f => ({ ...f, price: e.target.value }))} placeholder="Opcional" />
                                    </div>
                                    <div className="modal-actions">
                                        <button type="button" className="btn btn-secondary" onClick={() => setShowAddVariant(false)}>Cancelar</button>
                                        <button type="submit" className="btn btn-primary" disabled={savingVariant}>{savingVariant ? 'A adicionar...' : 'Adicionar'}</button>
                                    </div>
                                </form>
                            )}
                        </div>
                    </>
                ) : detailError ? <p className="inv-error">{detailError}</p> : null}
            </Modal>

            {/* ── CREATE ITEM MODAL ─────────────────────────────────────────────── */}
            <Modal open={showCreate} title="Novo Artigo Escolar" onClose={() => setShowCreate(false)}>
                <form onSubmit={handleCreateItem} className="inv-modal-form">
                    <div className="inv-form-group">
                        <label className="inv-form-label">Nome *</label>
                        <input className="inv-form-input" value={createForm.name} required onChange={e => setCreateForm(f => ({ ...f, name: e.target.value }))} />
                    </div>
                    <div className="inv-form-group">
                        <label className="inv-form-label">Descrição</label>
                        <textarea className="inv-form-textarea" value={createForm.description} onChange={e => setCreateForm(f => ({ ...f, description: e.target.value }))} />
                    </div>
                    <div className="inv-form-group">
                        <label className="inv-form-label">Categoria</label>
                        <select className="inv-form-select" value={createForm.idCategory} onChange={e => setCreateForm(f => ({ ...f, idCategory: e.target.value }))}>
                            <option value="">Sem categoria</option>
                            {categories.map(c => (
                                <option key={c.categoryId ?? c.CategoryId} value={c.categoryId ?? c.CategoryId}>
                                    {c.catgName ?? c.CatgName}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div className="inv-form-row-2">
                        <div className="inv-form-group">
                            <label className="inv-form-label">Telefone de contacto</label>
                            <input className="inv-form-input" value={createForm.contactPhone} onChange={e => setCreateForm(f => ({ ...f, contactPhone: e.target.value }))} />
                        </div>
                        <div className="inv-form-group">
                            <label className="inv-form-label">Email de contacto</label>
                            <input type="email" className="inv-form-input" value={createForm.contactEmail} onChange={e => setCreateForm(f => ({ ...f, contactEmail: e.target.value }))} />
                        </div>
                    </div>
                    {createError && <div className="inv-form-error">{createError}</div>}
                    <div className="modal-actions">
                        <button type="button" className="btn btn-secondary" onClick={() => setShowCreate(false)}>Cancelar</button>
                        <button type="submit" className="btn btn-primary" disabled={savingCreate}>{savingCreate ? 'A criar...' : 'Criar'}</button>
                    </div>
                </form>
            </Modal>

            {/* ── REVIEW REQUISITION MODAL ──────────────────────────────────────── */}
            <Modal open={showReview} title="Responder a Requisição" onClose={() => setShowReview(false)}>
                <form onSubmit={handleReview} className="inv-modal-form">
                    <p style={{ margin: '0 0 12px', fontSize: '0.95rem', color: '#374151' }}>
                        <strong>{reviewTarget?.itemName ?? '—'}</strong>
                        {(reviewTarget?.variantColor || reviewTarget?.variantSize) && (
                            <span style={{ color: '#6b7280' }}> — {[reviewTarget?.variantColor, reviewTarget?.variantSize].filter(Boolean).join(' / ')}</span>
                        )}
                        <br /><span style={{ fontSize: '0.85rem', color: '#6b7280' }}>Qtd: {reviewTarget?.quantity}</span>
                    </p>
                    <div style={{ display: 'flex', gap: 16, marginBottom: 4 }}>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', fontSize: '0.9rem' }}>
                            <input type="radio" checked={reviewForm.approve === true} onChange={() => setReviewForm(f => ({ ...f, approve: true }))} />
                            Aprovar
                        </label>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', fontSize: '0.9rem' }}>
                            <input type="radio" checked={reviewForm.approve === false} onChange={() => setReviewForm(f => ({ ...f, approve: false }))} />
                            Rejeitar
                        </label>
                    </div>
                    {reviewForm.approve && (
                        <div className="inv-form-group">
                            <label className="inv-form-label">Data prevista de devolução</label>
                            <input type="date" className="inv-form-input" value={reviewForm.expectedReturnDate} onChange={e => setReviewForm(f => ({ ...f, expectedReturnDate: e.target.value }))} />
                        </div>
                    )}
                    <div className="inv-form-group">
                        <label className="inv-form-label">Nota (opcional)</label>
                        <textarea className="inv-form-textarea" value={reviewForm.note} onChange={e => setReviewForm(f => ({ ...f, note: e.target.value }))} rows={2} />
                    </div>
                    {reviewError && <div className="inv-form-error">{reviewError}</div>}
                    <div className="modal-actions">
                        <button type="button" className="btn btn-secondary" onClick={() => setShowReview(false)}>Cancelar</button>
                        <button type="submit" className="btn btn-primary" disabled={savingReview}>{savingReview ? 'A guardar...' : 'Confirmar'}</button>
                    </div>
                </form>
            </Modal>

            {/* ── RETURN MODAL ─────────────────────────────────────────────────── */}
            <Modal open={showReturn} title="Registar Devolução" onClose={() => setShowReturn(false)}>
                <form onSubmit={handleReturn} className="inv-modal-form">
                    <p style={{ margin: '0 0 12px', fontSize: '0.95rem', color: '#374151' }}>
                        Requisição de <strong>{returnTarget?.itemName ?? '—'}</strong> (Qtd: {returnTarget?.quantity})
                    </p>
                    <div className="inv-form-group">
                        <label className="inv-form-label">Quantidade devolvida *</label>
                        <input type="number" className="inv-form-input" value={returnQty} min={1} max={returnTarget?.quantity ?? 99} required onChange={e => setReturnQty(e.target.value)} />
                    </div>
                    {returnError && <div className="inv-form-error">{returnError}</div>}
                    <div className="modal-actions">
                        <button type="button" className="btn btn-secondary" onClick={() => setShowReturn(false)}>Cancelar</button>
                        <button type="submit" className="btn btn-primary" disabled={savingReturn}>{savingReturn ? 'A guardar...' : 'Confirmar Devolução'}</button>
                    </div>
                </form>
            </Modal>
        </section>
    )
}
