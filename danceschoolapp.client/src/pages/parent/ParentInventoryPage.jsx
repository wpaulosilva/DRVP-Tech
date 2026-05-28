import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'
import Modal from '../../components/common/Modal'
import Tabs from '../../components/common/Tabs'
import {
    createPersonalItem,
    getCategories,
    getRequisitions, cancelRequisition, returnRequisition,
} from '../../services/inventoryService'
import '../../styles/Inventory.css'

const PAGE_SIZE = 12

const TABS = [
    { value: 'marketplace', label: 'Marketplace' },
    { value: 'me', label: 'Meus Artigos' },
]

const REQ_STATUS = {
    0: { label: 'Pendente',  cls: 'inv-status-pill--pending' },
    1: { label: 'Aprovado',  cls: 'inv-status-pill--approved' },
    2: { label: 'Rejeitado', cls: 'inv-status-pill--rejected' },
    3: { label: 'Devolvido', cls: 'inv-status-pill--returned' },
}

const emptyItemForm = { name: '', description: '', idCategory: '', contactPhone: '', contactEmail: '', contactAddress: '' }

function fmtDate(v) {
    if (!v) return '—'
    return new Date(v).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function fmtPrice(v) {
    if (v == null) return null
    return Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' })
}

export default function ParentInventoryPage() {
    const navigate = useNavigate()

    const { user } = useAuth()
    const [tab, setTab] = useState('marketplace')

    // ── Shared filter state ───────────────────────────────────────────────────
    const [search, setSearch]             = useState('')
    const [searchInput, setSearchInput]   = useState('')
    const [categoryId, setCategoryId]     = useState('')
    const [categories, setCategories]     = useState([])
    const searchTimer                     = useRef(null)

    // (school tab removed)

    // ── Marketplace (unified) ───────────────────────────────────────────────
    const [marketplaceItems, setMarketplaceItems]     = useState([])
    const [marketplaceTotal, setMarketplaceTotal]     = useState(0)
    const [marketplacePage, setMarketplacePage]       = useState(1)
    const [loadingMarketplace, setLoadingMarketplace] = useState(false)
    const [marketplaceError, setMarketplaceError]     = useState(null)
    const [debugVisible, setDebugVisible] = useState(false)
    const [lastMyItemsResponse, setLastMyItemsResponse] = useState(null)
    const [lastMarketplaceResponse, setLastMarketplaceResponse] = useState(null)

    // (community tab removed)

    // ── My requisitions ───────────────────────────────────────────────────────
    const [requisitions, setRequisitions] = useState([])
    const [loadingReqs, setLoadingReqs]   = useState(false)

    // ── Return modal ──────────────────────────────────────────────────────────
    const [showReturn, setShowReturn]     = useState(false)
    const [returnTarget, setReturnTarget] = useState(null)
    const [returnQty, setReturnQty]       = useState(1)
    const [returnNote, setReturnNote]     = useState('')
    const [returnError, setReturnError]   = useState(null)
    const [savingReturn, setSavingReturn] = useState(false)

    // ── Announce (create personal item) modal ─────────────────────────────────
    const [showAnnounce, setShowAnnounce]     = useState(false)
    const [announceForm, setAnnounceForm]     = useState(emptyItemForm)
    const [announceError, setAnnounceError]   = useState(null)
    const [savingAnnounce, setSavingAnnounce] = useState(false)

    // ── Load categories once ──────────────────────────────────────────────────
    useEffect(() => {
        getCategories()
            .then(data => setCategories(Array.isArray(data) ? data : []))
            .catch(() => {})
    }, [])

    // (school loader removed)

    // ── Load marketplace (all items) ──────────────────────────────────────────
    const loadMarketplace = useCallback(async () => {
        // load for both marketplace and personal (me) tab
        if (tab !== 'marketplace' && tab !== 'me') return
        setLoadingMarketplace(true)
        setMarketplaceError(null)
        try {
            if (tab === 'marketplace') {
                const { getMarketplace } = await import('../../services/inventoryService')
                const result = await getMarketplace({ search, categoryId: categoryId || undefined, page: marketplacePage, pageSize: PAGE_SIZE })
                const list = result?.items ?? result?.Items ?? []
                setMarketplaceItems(list)
                setMarketplaceTotal(result?.totalCount ?? result?.TotalCount ?? 0)
            } else {
                const { getMyItems } = await import('../../services/inventoryService')
                const result = await getMyItems({ search, categoryId: categoryId || undefined, page: marketplacePage, pageSize: PAGE_SIZE })
                setLastMyItemsResponse(result ?? null)
                const list = result?.items ?? result?.Items ?? []
                // Debug logs to diagnose missing personal items
                try {
                    console.debug('[INV][me] user:', user)
                    console.debug('[INV][me] getMyItems result status:', result)
                    console.debug('[INV][me] list length:', list.length)
                    if (list.length > 0) console.debug('[INV][me] first item owners:', list.map(i => ({ id: i.itemId ?? i.ItemId, owner: i.idOwner ?? i.IdOwner })))
                } catch (e) {}
                // If server returned nothing but user exists, fallback to scanning marketplace and filter by owner
                if ((list.length === 0 || result == null) && user && user.UserId) {
                    try {
                        const { getMarketplace } = await import('../../services/inventoryService')
                        // request larger page to increase chance to find personal items
                        const all = await getMarketplace({ search, categoryId: categoryId || undefined, page: 1, pageSize: 1000 })
                        setLastMarketplaceResponse(all ?? null)
                        const allList = all?.items ?? all?.Items ?? []
                        try { console.debug('[INV][me] fallback marketplace size:', allList.length) } catch (e) {}
                        const myId = Number(user.UserId ?? user.userId ?? user.id)
                        const filtered = allList.filter(i => Number(i.idOwner ?? i.IdOwner ?? i.ownerId ?? i.OwnerId ?? 0) === myId)
                        try { console.debug('[INV][me] filtered count:', filtered.length, 'myId:', myId, 'sample owners:', filtered.map(i => ({ id: i.itemId ?? i.ItemId, owner: i.idOwner ?? i.IdOwner }))) } catch (e) {}
                        setMarketplaceItems(filtered)
                        setMarketplaceTotal(filtered.length)
                    } catch (ex) {
                        setMarketplaceItems([])
                        setMarketplaceTotal(0)
                    }
                } else {
                    setMarketplaceItems(list)
                    setMarketplaceTotal(result?.totalCount ?? result?.TotalCount ?? 0)
                }
            }
        } catch (e) {
            setMarketplaceError(e.message)
        } finally {
            setLoadingMarketplace(false)
        }
    }, [tab, search, categoryId, marketplacePage])

    useEffect(() => { loadMarketplace() }, [loadMarketplace])

    // (community loader removed)

    // ── Load my requisitions ──────────────────────────────────────────────────
    const loadReqs = useCallback(async () => {
        setLoadingReqs(true)
        try {
            const data = await getRequisitions()
            setRequisitions(Array.isArray(data) ? data : [])
        } catch (e) {
            console.error(e)
        } finally {
            setLoadingReqs(false)
        }
    }, [])

    useEffect(() => { loadReqs() }, [loadReqs])

    // ── Tab change ────────────────────────────────────────────────────────────
    const handleTabChange = (t) => {
        setTab(t)
        setSearch('')
        setSearchInput('')
        setCategoryId('')
        setMarketplacePage(1)
    }

    // ── Search debounce ───────────────────────────────────────────────────────
    const handleSearchInput = (val) => {
        setSearchInput(val)
        clearTimeout(searchTimer.current)
        searchTimer.current = setTimeout(() => {
            setSearch(val)
            setMarketplacePage(1)
        }, 400)
    }

    // ── Cancel requisition ────────────────────────────────────────────────────
    const handleCancelReq = async (req) => {
        if (!window.confirm(`Cancelar requisição de "${req.itemName}"?`)) return
        try {
            await cancelRequisition(req.requisitionId)
            loadReqs()
        } catch (e) {
            alert(e.message)
        }
    }

    // ── Open return modal ─────────────────────────────────────────────────────
    const openReturn = (req) => {
        setReturnTarget(req)
        setReturnQty(req.quantity)
        setReturnNote('')
        setReturnError(null)
        setShowReturn(true)
    }

    // ── Submit return ─────────────────────────────────────────────────────────
    const handleReturnSubmit = async (e) => {
        e.preventDefault()
        setSavingReturn(true)
        setReturnError(null)
        try {
            await returnRequisition(returnTarget.requisitionId, {
                returnQuantity: Number(returnQty),
                ...(returnNote ? { returnNote } : {}),
            })
            setShowReturn(false)
            loadReqs()
        } catch (e) {
            setReturnError(e.message)
        } finally {
            setSavingReturn(false)
        }
    }

    // ── Submit announce (create personal item) ────────────────────────────────
    const handleAnnounceSubmit = async (e) => {
        e.preventDefault()
        if (!announceForm.name.trim()) { setAnnounceError('Nome é obrigatório.'); return }
        setSavingAnnounce(true)
        setAnnounceError(null)
        try {
            const body = {
                name: announceForm.name,
                ...(announceForm.description    ? { description: announceForm.description }       : {}),
                ...(announceForm.idCategory     ? { idCategory: Number(announceForm.idCategory) } : {}),
                ...(announceForm.contactPhone   ? { contactPhone: announceForm.contactPhone }     : {}),
                ...(announceForm.contactEmail   ? { contactEmail: announceForm.contactEmail }     : {}),
                ...(announceForm.contactAddress ? { contactAddress: announceForm.contactAddress } : {}),
            }
            const res = await createPersonalItem(body)
            setShowAnnounce(false)
            setAnnounceForm(emptyItemForm)
            const newId = res?.itemId ?? res?.ItemId
            if (newId) navigate(`/parent/inventario/${newId}`)
            else setMarketplacePage(1)
        } catch (e) {
            setAnnounceError(e.message)
        } finally {
            setSavingAnnounce(false)
        }
    }

    // ── Derived ───────────────────────────────────────────────────────────────
    const marketplacePages = Math.ceil(marketplaceTotal / PAGE_SIZE)
    const myId = Number(user?.UserId ?? user?.userId ?? user?.id ?? 0)
    const myItems = marketplaceItems.filter(card => {
        const owner = Number(card.idOwner ?? card.IdOwner ?? card.ownerId ?? card.OwnerId ?? 0)
        return owner && myId && owner === myId
    })

    const setAF = (k, v) => setAnnounceForm(f => ({ ...f, [k]: v }));

    return (
        <section className="dashboard-page-card">
            <div style={{ marginBottom: 20 }}>
                <h2 style={{ margin: 0 }}>Inventário</h2>
                <p style={{ margin: '4px 0 0', color: '#6b7280', fontSize: '0.95rem' }}>
                    Marketplace  — procurar e ver artigos disponíveis.
                </p>
            </div>

            <Tabs tabs={TABS} activeTab={tab} onTabChange={handleTabChange} />

            {/* debug panel removed */}

            {/* legacy school tab removed */}

            {/* ═══════════════════════════ MARKETPLACE / MEUS ARTIGOS TAB ═════════════════════════════ */}
            {(tab === 'marketplace' || tab === 'me') && (
                <>
            <div className="inv-filter-bar" style={{ marginTop: 8 }}>
                {tab === 'marketplace' ? (
                    <>
                        <input
                            className="inv-search-input"
                            placeholder="Pesquisar artigos no marketplace..."
                            value={searchInput}
                            onChange={e => handleSearchInput(e.target.value)}
                        />
                        <select
                            className="inv-filter-select"
                            value={categoryId}
                            onChange={e => { setCategoryId(e.target.value); setMarketplacePage(1) }}
                        >
                            <option value="">Todas as categorias</option>
                            {categories.map(c => (
                                <option key={c.categoryId} value={c.categoryId}>{c.catgName}</option>
                            ))}
                        </select>
                    </>
                ) : (
                    // tab === 'me' — sem filtros, apenas botão para anunciar
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                        <button
                            type="button"
                            className="btn btn-primary"
                            onClick={() => { setAnnounceForm(emptyItemForm); setAnnounceError(null); setShowAnnounce(true) }}
                        >
                            + Anunciar
                        </button>
                    </div>
                )}
            </div>

                    {loadingMarketplace ? (
                        <p className="inv-loading">A carregar...</p>
                    ) : marketplaceError ? (
                        <p className="inv-error">{marketplaceError}</p>
                    ) : marketplaceItems.length === 0 ? (
                        <div className="inv-empty">
                            <div className="inv-empty-icon">📦</div>
                            <p>Nenhum artigo encontrado no marketplace.</p>
                        </div>
                    ) : (
                        <>
                            <div className="inv-grid">
                                {(tab === 'me' ? myItems : marketplaceItems).map(card => {
                                    const id   = card.itemId ?? card.ItemId ?? card.itemId
                                    const img  = (card.images ?? card.Images ?? [])[0]?.imageUrl ?? card.imageUrl
                                    const name = card.name ?? card.Name ?? card.itemName
                                    const desc = card.description ?? card.Description
                                    const cat  = card.category?.catgName ?? card.Category?.CatgName ?? card.categoryName
                                    const vc   = card.variantCount ?? card.VariantCount ?? 0
                                    const fromSchool = card.fromSchool ?? card.FromSchool
                                    return (
                                        <div key={id} className="inv-card" onClick={() => navigate(`/parent/inventario/${id}`)}>
                                            {fromSchool && <div className="inv-school-badge">ENT'ARTES</div>}
                                            {img
                                                ? <img src={img} alt={name} className="inv-card-img" />
                                                : <div className="inv-card-img-placeholder">{(name ?? '?')[0]}</div>
                                            }
                                            <div className="inv-card-body">
                                                {cat && <span className="inv-card-category">{cat}</span>}
                                                <p className="inv-card-name">{name}</p>
                                                {desc && <p className="inv-card-desc">{desc}</p>}
                                            </div>
                                            <div className="inv-card-footer">
                                                <div className="inv-card-meta">{vc} var.</div>
                                                <span style={{ fontSize: '0.8rem', color: '#9ca3af' }}>Ver →</span>
                                            </div>
                                        </div>
                                    )
                                })}
                            </div>
                            {Math.ceil(marketplaceTotal / PAGE_SIZE) > 1 && (
                                <div className="inv-pagination">
                                    <button className="inv-page-btn" disabled={marketplacePage <= 1} onClick={() => setMarketplacePage(p => p - 1)}>‹</button>
                                    <span className="inv-page-info">{marketplacePage} / {Math.ceil(marketplaceTotal / PAGE_SIZE)}</span>
                                    <button className="inv-page-btn" disabled={marketplacePage >= Math.ceil(marketplaceTotal / PAGE_SIZE)} onClick={() => setMarketplacePage(p => p + 1)}>›</button>
                                </div>
                            )}
                        </>
                    )}
                </>
            )}

            {/* legacy community tab removed */}

            {/* ═══════════════════════ RETURN MODAL ══════════════════════════════════════ */}
            <Modal
                open={showReturn}
                title="Registar Devolução"
                onClose={() => setShowReturn(false)}
            >
                {returnTarget && (
                    <form className="inv-modal-form" onSubmit={handleReturnSubmit}>
                        <p style={{ margin: 0 }}>
                            <strong>{returnTarget.itemName}</strong>
                            {(returnTarget.variantColor || returnTarget.variantSize) && (
                                <span style={{ color: '#6b7280' }}>
                                    {' — '}{[returnTarget.variantColor, returnTarget.variantSize].filter(Boolean).join(' / ')}
                                </span>
                            )}
                        </p>

                        <div className="inv-form-group">
                            <label className="inv-form-label">Quantidade devolvida *</label>
                            <input
                                className="inv-form-input"
                                type="number"
                                min={1}
                                max={returnTarget.quantity}
                                value={returnQty}
                                onChange={e => setReturnQty(e.target.value)}
                                required
                            />
                        </div>

                        <div className="inv-form-group">
                            <label className="inv-form-label">Observação (opcional)</label>
                            <textarea
                                className="inv-form-textarea"
                                rows={3}
                                placeholder="Estado do artigo, danos, etc."
                                value={returnNote}
                                onChange={e => setReturnNote(e.target.value)}
                            />
                        </div>

                        {returnError && <div className="inv-form-error">{returnError}</div>}

                        <div className="modal-actions">
                            <button type="button" className="btn btn-secondary" onClick={() => setShowReturn(false)}>Cancelar</button>
                            <button type="submit" className="btn btn-primary" disabled={savingReturn}>
                                {savingReturn ? 'A registar...' : 'Confirmar Devolução'}
                            </button>
                        </div>
                    </form>
                )}
            </Modal>

            {/* ═══════════════════════ ANNOUNCE MODAL ════════════════════════════════════ */}
            <Modal
                open={showAnnounce}
                title="Anunciar Artigo"
                onClose={() => setShowAnnounce(false)}
            >
                <form className="inv-modal-form" onSubmit={handleAnnounceSubmit}>
                    <div className="inv-form-group">
                        <label className="inv-form-label">Nome *</label>
                        <input
                            className="inv-form-input"
                            value={announceForm.name}
                            onChange={e => setAF('name', e.target.value)}
                            required
                        />
                    </div>
                    <div className="inv-form-group">
                        <label className="inv-form-label">Descrição</label>
                        <textarea
                            className="inv-form-textarea"
                            rows={3}
                            placeholder="Condição, dimensões, motivo da venda..."
                            value={announceForm.description}
                            onChange={e => setAF('description', e.target.value)}
                        />
                    </div>
                    <div className="inv-form-group">
                        <label className="inv-form-label">Categoria</label>
                        <select
                            className="inv-form-select"
                            value={announceForm.idCategory}
                            onChange={e => setAF('idCategory', e.target.value)}
                        >
                            <option value="">Sem categoria</option>
                            {categories.map(c => (
                                <option key={c.categoryId} value={c.categoryId}>{c.catgName}</option>
                            ))}
                        </select>
                    </div>

                    <label className="inv-form-label" style={{ margin: '8px 0 6px' }}>Contacto</label>
                    <div className="inv-form-row-3">
                        <div className="inv-form-group">
                            <label className="inv-form-label">Telefone</label>
                            <input className="inv-form-input" value={announceForm.contactPhone} onChange={e => setAF('contactPhone', e.target.value)} />
                        </div>
                        <div className="inv-form-group">
                            <label className="inv-form-label">Email</label>
                            <input className="inv-form-input" type="email" value={announceForm.contactEmail} onChange={e => setAF('contactEmail', e.target.value)} />
                        </div>
                        <div className="inv-form-group">
                            <label className="inv-form-label">Morada</label>
                            <input className="inv-form-input" value={announceForm.contactAddress} onChange={e => setAF('contactAddress', e.target.value)} />
                        </div>
                    </div>

                    {announceError && <div className="inv-form-error">{announceError}</div>}

                    <div className="modal-actions">
                        <button type="button" className="btn btn-secondary" onClick={() => setShowAnnounce(false)}>Cancelar</button>
                        <button type="submit" className="btn btn-primary" disabled={savingAnnounce}>
                            {savingAnnounce ? 'A publicar...' : 'Publicar'}
                        </button>
                    </div>
                </form>
            </Modal>
        </section>
    )
}
