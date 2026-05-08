import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import Modal from '../../components/common/Modal'
import Tabs from '../../components/common/Tabs'
import {
    getItems, createSchoolItem,
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

function stockPill(variantCount) {
    if (variantCount === 0) return <span className="inv-stock-pill inv-stock-pill--out">Sem stock</span>
    return <span className="inv-stock-pill inv-stock-pill--in">{variantCount} var.</span>
}

function fmtDate(v) {
    if (!v) return '—'
    return new Date(v).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

export default function StaffInventoryPage() {
    const navigate = useNavigate()
    const [tab, setTab] = useState('school')

    // ── Item list state ──────────────────────────────────────────────────────
    const [items, setItems]               = useState([])
    const [totalCount, setTotalCount]     = useState(0)
    const [page, setPage]                 = useState(1)
    const [search, setSearch]             = useState('')
    const [searchInput, setSearchInput]   = useState('')
    const [categoryId, setCategoryId]     = useState('')
    const [categories, setCategories]     = useState([])
    const [loadingItems, setLoadingItems] = useState(false)
    const [itemError, setItemError]       = useState(null)
    const searchTimer                     = useRef(null)

    // ── Create item modal ────────────────────────────────────────────────────
    const [showCreate, setShowCreate]     = useState(false)
    const [createForm, setCreateForm]     = useState(emptyItemForm)
    const [createError, setCreateError]   = useState(null)
    const [savingCreate, setSavingCreate] = useState(false)

    // ── Requisitions state ───────────────────────────────────────────────────
    const [requisitions, setRequisitions]         = useState([])
    const [reqStatusFilter, setReqStatusFilter]   = useState('')
    const [loadingReqs, setLoadingReqs]           = useState(false)

    // ── Review modal ─────────────────────────────────────────────────────────
    const [showReview, setShowReview]     = useState(false)
    const [reviewTarget, setReviewTarget] = useState(null)
    const [reviewForm, setReviewForm]     = useState({ approve: true, expectedReturnDate: '', note: '' })
    const [reviewError, setReviewError]   = useState(null)
    const [savingReview, setSavingReview] = useState(false)

    // ── Return modal ─────────────────────────────────────────────────────────
    const [showReturn, setShowReturn]     = useState(false)
    const [returnTarget, setReturnTarget] = useState(null)
    const [returnQty, setReturnQty]       = useState(1)
    const [returnError, setReturnError]   = useState(null)
    const [savingReturn, setSavingReturn] = useState(false)

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
            const newId = res?.itemId ?? res?.ItemId
            if (newId) navigate(`/staff/inventario/${newId}`)
            else loadItems()
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
                                    <div key={id} className="inv-card" onClick={() => navigate(`/staff/inventario/${id}`)}>
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
