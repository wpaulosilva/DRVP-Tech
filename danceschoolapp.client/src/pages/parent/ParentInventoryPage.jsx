import { useCallback, useEffect, useRef, useState } from 'react'
import Modal from '../../components/common/Modal'
import Tabs from '../../components/common/Tabs'
import { useAuth } from '../../context/useAuth'
import {
    getSchoolInventory, getCommunityInventory,
    getItem, createPersonalItem, updateItem, deleteItem,
    uploadItemImage,
    getVariants,
    getCategories,
    getRequisitions, createRequisition, cancelRequisition, returnRequisition,
} from '../../services/inventoryService'
import '../../styles/Inventory.css'

const PAGE_SIZE = 12

const TABS = [
    { value: 'school',    label: 'Escolar' },
    { value: 'community', label: 'Comunidade' },
]

const REQ_STATUS = {
    0: { label: 'Pendente',  cls: 'inv-status-pill--pending' },
    1: { label: 'Aprovado',  cls: 'inv-status-pill--approved' },
    2: { label: 'Rejeitado', cls: 'inv-status-pill--rejected' },
    3: { label: 'Devolvido', cls: 'inv-status-pill--returned' },
}

const emptyItemForm = { name: '', description: '', idCategory: '', contactPhone: '', contactEmail: '', contactAddress: '' }
const emptyLoanForm = { itemVariantId: '', quantity: 1, needFrom: '', needUntil: '', note: '' }

function fmtDate(v) {
    if (!v) return '—'
    return new Date(v).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function fmtPrice(v) {
    if (v == null) return null
    return Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' })
}

export default function ParentInventoryPage() {
    const { user } = useAuth()

    const [tab, setTab] = useState('school')

    // ── Shared filter state ───────────────────────────────────────────────────
    const [search, setSearch]           = useState('')
    const [searchInput, setSearchInput] = useState('')
    const [categoryId, setCategoryId]   = useState('')
    const [categories, setCategories]   = useState([])
    const searchTimer                   = useRef(null)

    // ── School tab ────────────────────────────────────────────────────────────
    const [schoolItems, setSchoolItems]     = useState([])
    const [schoolTotal, setSchoolTotal]     = useState(0)
    const [schoolPage, setSchoolPage]       = useState(1)
    const [loadingSchool, setLoadingSchool] = useState(false)
    const [schoolError, setSchoolError]     = useState(null)

    // ── Community tab ─────────────────────────────────────────────────────────
    const [commItems, setCommItems]       = useState([])
    const [commTotal, setCommTotal]       = useState(0)
    const [commPage, setCommPage]         = useState(1)
    const [loadingComm, setLoadingComm]   = useState(false)
    const [commListError, setCommListError] = useState(null)
    const [maxPrice, setMaxPrice]         = useState('')
    const [maxPriceInput, setMaxPriceInput] = useState('')
    const priceTimer                      = useRef(null)

    // ── My requisitions ───────────────────────────────────────────────────────
    const [requisitions, setRequisitions]   = useState([])
    const [loadingReqs, setLoadingReqs]     = useState(false)

    // ── Loan modal ────────────────────────────────────────────────────────────
    const [showLoan, setShowLoan]                   = useState(false)
    const [loanCard, setLoanCard]                   = useState(null)
    const [loanVariants, setLoanVariants]           = useState([])
    const [loadingLoanVars, setLoadingLoanVars]     = useState(false)
    const [loanForm, setLoanForm]                   = useState(emptyLoanForm)
    const [loanError, setLoanError]                 = useState(null)
    const [savingLoan, setSavingLoan]               = useState(false)

    // ── Return modal ──────────────────────────────────────────────────────────
    const [showReturn, setShowReturn]       = useState(false)
    const [returnTarget, setReturnTarget]   = useState(null)
    const [returnQty, setReturnQty]         = useState(1)
    const [returnNote, setReturnNote]       = useState('')
    const [returnError, setReturnError]     = useState(null)
    const [savingReturn, setSavingReturn]   = useState(false)

    // ── Community item detail modal ───────────────────────────────────────────
    const [showCommDetail, setShowCommDetail]         = useState(false)
    const [commDetail, setCommDetail]                 = useState(null)
    const [commDetailLoading, setCommDetailLoading]   = useState(false)
    const [commDetailError, setCommDetailError]       = useState(null)
    const [commEditMode, setCommEditMode]             = useState(false)
    const [commForm, setCommForm]                     = useState(emptyItemForm)
    const [savingComm, setSavingComm]                 = useState(false)
    const [commImgIndex, setCommImgIndex]             = useState(0)
    const commFileRef                                 = useRef(null)
    const [uploadingCommImg, setUploadingCommImg]     = useState(false)

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

    // ── Load school items ─────────────────────────────────────────────────────
    const loadSchool = useCallback(async () => {
        if (tab !== 'school') return
        setLoadingSchool(true)
        setSchoolError(null)
        try {
            const result = await getSchoolInventory({
                categoryId: categoryId || undefined,
                search,
                page: schoolPage,
                pageSize: PAGE_SIZE,
            })
            setSchoolItems(result?.items ?? [])
            setSchoolTotal(result?.totalCount ?? 0)
        } catch (e) {
            setSchoolError(e.message)
        } finally {
            setLoadingSchool(false)
        }
    }, [tab, search, categoryId, schoolPage])

    useEffect(() => { loadSchool() }, [loadSchool])

    // ── Load community items ──────────────────────────────────────────────────
    const loadCommunity = useCallback(async () => {
        if (tab !== 'community') return
        setLoadingComm(true)
        setCommListError(null)
        try {
            const result = await getCommunityInventory({
                categoryId: categoryId || undefined,
                search,
                maxPrice: maxPrice || undefined,
                page: commPage,
                pageSize: PAGE_SIZE,
            })
            setCommItems(result?.items ?? [])
            setCommTotal(result?.totalCount ?? 0)
        } catch (e) {
            setCommListError(e.message)
        } finally {
            setLoadingComm(false)
        }
    }, [tab, search, categoryId, maxPrice, commPage])

    useEffect(() => { loadCommunity() }, [loadCommunity])

    // ── Load my requisitions ──────────────────────────────────────────────────
    const loadReqs = useCallback(async () => {
        if (tab !== 'school') return
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

    // ── Tab change ────────────────────────────────────────────────────────────
    const handleTabChange = (t) => {
        setTab(t)
        setSearch('')
        setSearchInput('')
        setCategoryId('')
        setMaxPrice('')
        setMaxPriceInput('')
        setSchoolPage(1)
        setCommPage(1)
    }

    // ── Search debounce ───────────────────────────────────────────────────────
    const handleSearchInput = (val) => {
        setSearchInput(val)
        clearTimeout(searchTimer.current)
        searchTimer.current = setTimeout(() => {
            setSearch(val)
            setSchoolPage(1)
            setCommPage(1)
        }, 400)
    }

    // ── Max price debounce ────────────────────────────────────────────────────
    const handleMaxPriceInput = (val) => {
        setMaxPriceInput(val)
        clearTimeout(priceTimer.current)
        priceTimer.current = setTimeout(() => {
            setMaxPrice(val)
            setCommPage(1)
        }, 400)
    }

    // ── Open loan modal ───────────────────────────────────────────────────────
    const openLoan = async (card) => {
        setLoanCard(card)
        setLoanForm(emptyLoanForm)
        setLoanError(null)
        setLoanVariants([])
        setShowLoan(true)
        setLoadingLoanVars(true)
        try {
            const vars = await getVariants(card.itemId)
            const available = Array.isArray(vars)
                ? vars.filter(v => v.isActive !== false && v.quantity > 0)
                : []
            setLoanVariants(available)
        } catch {
            setLoanError('Não foi possível carregar as variantes deste artigo.')
        } finally {
            setLoadingLoanVars(false)
        }
    }

    // ── Submit loan request ───────────────────────────────────────────────────
    const handleLoanSubmit = async (e) => {
        e.preventDefault()
        if (!loanForm.itemVariantId) { setLoanError('Selecione uma variante.'); return }
        setSavingLoan(true)
        setLoanError(null)
        try {
            const body = {
                itemVariantId: Number(loanForm.itemVariantId),
                quantity: Number(loanForm.quantity),
                ...(loanForm.needFrom  ? { needFrom: loanForm.needFrom }   : {}),
                ...(loanForm.needUntil ? { needUntil: loanForm.needUntil } : {}),
                ...(loanForm.note      ? { note: loanForm.note }           : {}),
            }
            await createRequisition(body)
            setShowLoan(false)
            loadReqs()
        } catch (e) {
            setLoanError(e.message)
        } finally {
            setSavingLoan(false)
        }
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

    // ── Open community item detail ─────────────────────────────────────────────
    const openCommDetail = async (card) => {
        setCommDetail(null)
        setCommDetailError(null)
        setCommEditMode(false)
        setCommImgIndex(0)
        setShowCommDetail(true)
        setCommDetailLoading(true)
        try {
            const detail = await getItem(card.itemId)
            setCommDetail(detail)
            setCommForm({
                name: detail.name ?? '',
                description: detail.description ?? '',
                idCategory: detail.category?.categoryId ?? '',
                contactPhone: detail.contactPhone ?? '',
                contactEmail: detail.contactEmail ?? '',
                contactAddress: detail.contactAddress ?? '',
            })
        } catch (e) {
            setCommDetailError(e.message)
        } finally {
            setCommDetailLoading(false)
        }
    }

    // ── Save community item edit ───────────────────────────────────────────────
    const handleSaveComm = async (e) => {
        e.preventDefault()
        setSavingComm(true)
        setCommDetailError(null)
        try {
            const body = {}
            if (commForm.name)          body.name = commForm.name
            if (commForm.description)   body.description = commForm.description
            if (commForm.idCategory)    body.idCategory = Number(commForm.idCategory)
            if (commForm.contactPhone)  body.contactPhone = commForm.contactPhone
            if (commForm.contactEmail)  body.contactEmail = commForm.contactEmail
            if (commForm.contactAddress) body.contactAddress = commForm.contactAddress
            await updateItem(commDetail.itemId, body)
            const updated = await getItem(commDetail.itemId)
            setCommDetail(updated)
            setCommEditMode(false)
            loadCommunity()
        } catch (e) {
            setCommDetailError(e.message)
        } finally {
            setSavingComm(false)
        }
    }

    // ── Delete community item ─────────────────────────────────────────────────
    const handleDeleteComm = async () => {
        if (!window.confirm(`Remover anúncio "${commDetail?.name}"?`)) return
        try {
            await deleteItem(commDetail.itemId)
            setShowCommDetail(false)
            loadCommunity()
        } catch (e) {
            setCommDetailError(e.message)
        }
    }

    // ── Upload image to own community item ────────────────────────────────────
    const handleCommImgUpload = async (e) => {
        const file = e.target.files?.[0]
        if (!file) return
        setUploadingCommImg(true)
        try {
            await uploadItemImage(commDetail.itemId, file)
            const updated = await getItem(commDetail.itemId)
            setCommDetail(updated)
            loadCommunity()
        } catch (e) {
            setCommDetailError(e.message)
        } finally {
            setUploadingCommImg(false)
            e.target.value = ''
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
            await createPersonalItem(body)
            setShowAnnounce(false)
            setAnnounceForm(emptyItemForm)
            loadCommunity()
        } catch (e) {
            setAnnounceError(e.message)
        } finally {
            setSavingAnnounce(false)
        }
    }

    // ── Derived ───────────────────────────────────────────────────────────────
    const schoolPages = Math.ceil(schoolTotal / PAGE_SIZE)
    const commPages   = Math.ceil(commTotal / PAGE_SIZE)
    const isOwnItem   = commDetail && user && commDetail.idOwner === user.userId

    const selectedLoanVariant = loanVariants.find(v => v.variantId === Number(loanForm.itemVariantId))

    const setLF = (k, v) => setLoanForm(f => ({ ...f, [k]: v }))
    const setCF = (k, v) => setCommForm(f => ({ ...f, [k]: v }))
    const setAF = (k, v) => setAnnounceForm(f => ({ ...f, [k]: v }))

    return (
        <section className="dashboard-page-card">
            <div style={{ marginBottom: 20 }}>
                <h2 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700, color: '#1f2937' }}>Inventário</h2>
                <p style={{ margin: '4px 0 0', color: '#6b7280', fontSize: '0.95rem' }}>
                    Consultar artigos da escola e artigos da comunidade.
                </p>
            </div>

            <Tabs tabs={TABS} activeTab={tab} onTabChange={handleTabChange} />

            {/* ═══════════════════════════════ ESCOLAR TAB ═══════════════════════════════ */}
            {tab === 'school' && (
                <>
                    {/* Filters */}
                    <div className="inv-filter-bar">
                        <input
                            className="inv-search-input"
                            placeholder="Pesquisar artigos..."
                            value={searchInput}
                            onChange={e => handleSearchInput(e.target.value)}
                        />
                        <select
                            className="inv-filter-select"
                            value={categoryId}
                            onChange={e => { setCategoryId(e.target.value); setSchoolPage(1) }}
                        >
                            <option value="">Todas as categorias</option>
                            {categories.map(c => (
                                <option key={c.categoryId} value={c.categoryId}>{c.catgName}</option>
                            ))}
                        </select>
                    </div>

                    {/* Item grid */}
                    {loadingSchool ? (
                        <p className="inv-loading">A carregar...</p>
                    ) : schoolError ? (
                        <p className="inv-error">{schoolError}</p>
                    ) : schoolItems.length === 0 ? (
                        <div className="inv-empty">
                            <div className="inv-empty-icon">📦</div>
                            <p>Nenhum artigo encontrado.</p>
                        </div>
                    ) : (
                        <>
                            <div className="inv-grid">
                                {schoolItems.map(card => (
                                    <div key={card.itemId} className="inv-card" onClick={() => openLoan(card)}>
                                        {card.imageUrl
                                            ? <img src={card.imageUrl} alt={card.name} className="inv-card-img" />
                                            : <div className="inv-card-img-placeholder">{(card.name ?? '?')[0]}</div>
                                        }
                                        <div className="inv-card-body">
                                            {card.categoryName && <span className="inv-card-category">{card.categoryName}</span>}
                                            <p className="inv-card-name">{card.name}</p>
                                            {card.description && <p className="inv-card-desc">{card.description}</p>}
                                        </div>
                                        <div className="inv-card-footer">
                                            <div className="inv-card-meta">
                                                {card.lowestPrice != null && <span className="inv-price">{fmtPrice(card.lowestPrice)}</span>}
                                                {card.variantCount != null && (
                                                    <span className={`inv-stock-pill ${card.variantCount === 0 ? 'inv-stock-pill--out' : 'inv-stock-pill--in'}`}>
                                                        {card.variantCount === 0 ? 'Sem stock' : `${card.variantCount} var.`}
                                                    </span>
                                                )}
                                            </div>
                                            <span style={{ fontSize: '0.8rem', color: '#9ca3af' }}>Pedir →</span>
                                        </div>
                                    </div>
                                ))}
                            </div>
                            {schoolPages > 1 && (
                                <div className="inv-pagination">
                                    <button className="inv-page-btn" disabled={schoolPage <= 1} onClick={() => setSchoolPage(p => p - 1)}>‹</button>
                                    <span className="inv-page-info">{schoolPage} / {schoolPages}</span>
                                    <button className="inv-page-btn" disabled={schoolPage >= schoolPages} onClick={() => setSchoolPage(p => p + 1)}>›</button>
                                </div>
                            )}
                        </>
                    )}

                    {/* My Requisitions */}
                    <div className="inv-my-req-section">
                        <h3 className="inv-my-req-title">As minhas requisições</h3>
                        {loadingReqs ? (
                            <p className="inv-loading">A carregar...</p>
                        ) : requisitions.length === 0 ? (
                            <div className="inv-empty" style={{ padding: '24px' }}>
                                <p style={{ margin: 0 }}>Ainda não tem requisições.</p>
                            </div>
                        ) : (
                            <div className="inv-req-list">
                                {requisitions.map(r => {
                                    const s = REQ_STATUS[r.status] ?? { label: String(r.status), cls: '' }
                                    return (
                                        <div key={r.requisitionId} className="inv-req-card">
                                            {r.itemImageUrl
                                                ? <img src={r.itemImageUrl} alt={r.itemName} className="inv-req-img" />
                                                : <div className="inv-req-img-placeholder">{(r.itemName ?? '?')[0]}</div>
                                            }
                                            <div className="inv-req-body">
                                                <p className="inv-req-title">
                                                    {r.itemName}
                                                    {(r.variantColor || r.variantSize) && (
                                                        <span style={{ fontWeight: 400, color: '#6b7280' }}>
                                                            {' — '}{[r.variantColor, r.variantSize].filter(Boolean).join(' / ')}
                                                        </span>
                                                    )}
                                                </p>
                                                <div className="inv-req-meta">
                                                    <span className="inv-req-meta-label">Qtd:</span>
                                                    <span>{r.quantity}</span>
                                                    <span className="inv-req-meta-label">Pedido em:</span>
                                                    <span>{fmtDate(r.requestedAt)}</span>
                                                    {r.needFrom && <>
                                                        <span className="inv-req-meta-label">Necessário de:</span>
                                                        <span>{fmtDate(r.needFrom)}{r.needUntil ? ` → ${fmtDate(r.needUntil)}` : ''}</span>
                                                    </>}
                                                    {r.expectedReturnDate && <>
                                                        <span className="inv-req-meta-label">Devolver até:</span>
                                                        <span>{fmtDate(r.expectedReturnDate)}</span>
                                                    </>}
                                                    {r.note && <>
                                                        <span className="inv-req-meta-label">Nota:</span>
                                                        <span>{r.note}</span>
                                                    </>}
                                                </div>
                                            </div>
                                            <div className="inv-req-actions">
                                                <span className={`inv-status-pill ${s.cls}`}>{s.label}</span>
                                                {r.status === 0 && (
                                                    <button
                                                        type="button"
                                                        className="btn btn-danger btn-sm"
                                                        onClick={() => handleCancelReq(r)}
                                                    >
                                                        Cancelar
                                                    </button>
                                                )}
                                                {r.status === 1 && (
                                                    <button
                                                        type="button"
                                                        className="btn btn-secondary btn-sm"
                                                        onClick={() => openReturn(r)}
                                                    >
                                                        Devolver
                                                    </button>
                                                )}
                                            </div>
                                        </div>
                                    )
                                })}
                            </div>
                        )}
                    </div>
                </>
            )}

            {/* ═══════════════════════════════ COMUNIDADE TAB ════════════════════════════ */}
            {tab === 'community' && (
                <>
                    <div className="inv-filter-bar">
                        <input
                            className="inv-search-input"
                            placeholder="Pesquisar artigos..."
                            value={searchInput}
                            onChange={e => handleSearchInput(e.target.value)}
                        />
                        <select
                            className="inv-filter-select"
                            value={categoryId}
                            onChange={e => { setCategoryId(e.target.value); setCommPage(1) }}
                        >
                            <option value="">Todas as categorias</option>
                            {categories.map(c => (
                                <option key={c.categoryId} value={c.categoryId}>{c.catgName}</option>
                            ))}
                        </select>
                        <input
                            className="inv-price-input"
                            type="number"
                            placeholder="Preço máx."
                            min="0"
                            step="0.01"
                            value={maxPriceInput}
                            onChange={e => handleMaxPriceInput(e.target.value)}
                        />
                        <button
                            type="button"
                            className="btn btn-primary"
                            onClick={() => { setAnnounceForm(emptyItemForm); setAnnounceError(null); setShowAnnounce(true) }}
                        >
                            + Anunciar
                        </button>
                    </div>

                    {loadingComm ? (
                        <p className="inv-loading">A carregar...</p>
                    ) : commListError ? (
                        <p className="inv-error">{commListError}</p>
                    ) : commItems.length === 0 ? (
                        <div className="inv-empty">
                            <div className="inv-empty-icon">🏷️</div>
                            <p>Nenhum artigo encontrado.</p>
                        </div>
                    ) : (
                        <>
                            <div className="inv-grid">
                                {commItems.map(card => (
                                    <div key={card.itemId} className="inv-card" onClick={() => openCommDetail(card)}>
                                        {card.imageUrl
                                            ? <img src={card.imageUrl} alt={card.name} className="inv-card-img" />
                                            : <div className="inv-card-img-placeholder">{(card.name ?? '?')[0]}</div>
                                        }
                                        <div className="inv-card-body">
                                            {card.categoryName && <span className="inv-card-category">{card.categoryName}</span>}
                                            <p className="inv-card-name">{card.name}</p>
                                            {card.description && <p className="inv-card-desc">{card.description}</p>}
                                        </div>
                                        <div className="inv-card-footer">
                                            <div className="inv-card-meta">
                                                {card.lowestPrice != null && <span className="inv-price">{fmtPrice(card.lowestPrice)}</span>}
                                                {card.ownerName && <span className="inv-owner-name">{card.ownerName}</span>}
                                            </div>
                                        </div>
                                        {(card.contactPhone || card.contactEmail) && (
                                            <div className="inv-contact-row" style={{ padding: '0 14px 12px' }}>
                                                {card.contactPhone && <span className="inv-contact-chip">📞 {card.contactPhone}</span>}
                                                {card.contactEmail && <span className="inv-contact-chip">✉ {card.contactEmail}</span>}
                                            </div>
                                        )}
                                    </div>
                                ))}
                            </div>
                            {commPages > 1 && (
                                <div className="inv-pagination">
                                    <button className="inv-page-btn" disabled={commPage <= 1} onClick={() => setCommPage(p => p - 1)}>‹</button>
                                    <span className="inv-page-info">{commPage} / {commPages}</span>
                                    <button className="inv-page-btn" disabled={commPage >= commPages} onClick={() => setCommPage(p => p + 1)}>›</button>
                                </div>
                            )}
                        </>
                    )}
                </>
            )}

            {/* ═══════════════════════ LOAN MODAL ════════════════════════════════════════ */}
            <Modal
                open={showLoan}
                title={`Pedir Empréstimo — ${loanCard?.name ?? ''}`}
                onClose={() => setShowLoan(false)}
            >
                {loanCard?.imageUrl && (
                    <img
                        src={loanCard.imageUrl}
                        alt={loanCard.name}
                        style={{ width: '100%', maxHeight: 180, objectFit: 'contain', borderRadius: 10, marginBottom: 16 }}
                    />
                )}
                {loadingLoanVars ? (
                    <p className="inv-loading">A carregar variantes...</p>
                ) : loanVariants.length === 0 ? (
                    <div className="inv-form-error">
                        Não existem variantes disponíveis em stock para este artigo.
                    </div>
                ) : (
                    <form className="inv-modal-form" onSubmit={handleLoanSubmit}>
                        <div className="inv-form-group">
                            <label className="inv-form-label">Variante *</label>
                            <select
                                className="inv-form-select"
                                value={loanForm.itemVariantId}
                                onChange={e => setLF('itemVariantId', e.target.value)}
                                required
                            >
                                <option value="">Selecionar variante...</option>
                                {loanVariants.map(v => (
                                    <option key={v.variantId} value={v.variantId}>
                                        {[v.color, v.size].filter(Boolean).join(' / ') || `Variante #${v.variantId}`}
                                        {' '}— {v.quantity} disponível
                                        {v.price != null ? ` · ${fmtPrice(v.price)}` : ''}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div className="inv-form-group">
                            <label className="inv-form-label">Quantidade *</label>
                            <input
                                className="inv-form-input"
                                type="number"
                                min={1}
                                max={selectedLoanVariant?.quantity ?? 99}
                                value={loanForm.quantity}
                                onChange={e => setLF('quantity', e.target.value)}
                                required
                            />
                        </div>

                        <div className="inv-form-row-2">
                            <div className="inv-form-group">
                                <label className="inv-form-label">Necessário de</label>
                                <input
                                    className="inv-form-input"
                                    type="date"
                                    value={loanForm.needFrom}
                                    onChange={e => setLF('needFrom', e.target.value)}
                                />
                            </div>
                            <div className="inv-form-group">
                                <label className="inv-form-label">Necessário até</label>
                                <input
                                    className="inv-form-input"
                                    type="date"
                                    value={loanForm.needUntil}
                                    onChange={e => setLF('needUntil', e.target.value)}
                                />
                            </div>
                        </div>

                        <div className="inv-form-group">
                            <label className="inv-form-label">Nota (opcional)</label>
                            <textarea
                                className="inv-form-textarea"
                                rows={3}
                                placeholder="Motivo, detalhes adicionais..."
                                value={loanForm.note}
                                onChange={e => setLF('note', e.target.value)}
                            />
                        </div>

                        {loanError && <div className="inv-form-error">{loanError}</div>}

                        <div className="modal-actions">
                            <button type="button" className="btn btn-secondary" onClick={() => setShowLoan(false)}>Cancelar</button>
                            <button type="submit" className="btn btn-primary" disabled={savingLoan}>
                                {savingLoan ? 'A enviar...' : 'Pedir Empréstimo'}
                            </button>
                        </div>
                    </form>
                )}
            </Modal>

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

            {/* ═══════════════════ COMMUNITY ITEM DETAIL MODAL ═══════════════════════════ */}
            <Modal
                open={showCommDetail}
                title={commDetail?.name ?? 'Artigo'}
                onClose={() => { setShowCommDetail(false); setCommEditMode(false) }}
            >
                {commDetailLoading ? (
                    <p className="inv-loading">A carregar...</p>
                ) : commDetailError && !commDetail ? (
                    <p className="inv-error">{commDetailError}</p>
                ) : commDetail ? (
                    <>
                        {commDetailError && <div className="inv-form-error" style={{ marginBottom: 12 }}>{commDetailError}</div>}

                        {/* Image gallery */}
                        {commDetail.images?.length > 0 && (
                            <div className="inv-detail-images" style={{ marginBottom: 16 }}>
                                <img
                                    src={commDetail.images[commImgIndex]?.imageUrl}
                                    alt={commDetail.name}
                                    className="inv-detail-main-img"
                                />
                                {commDetail.images.length > 1 && (
                                    <div className="inv-detail-thumbs">
                                        {commDetail.images.map((img, i) => (
                                            <img
                                                key={img.imageId}
                                                src={img.imageUrl}
                                                alt=""
                                                className={`inv-detail-thumb${i === commImgIndex ? ' inv-detail-thumb--active' : ''}`}
                                                onClick={() => setCommImgIndex(i)}
                                            />
                                        ))}
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Contact chips */}
                        {(commDetail.contactPhone || commDetail.contactEmail || commDetail.contactAddress) && (
                            <div className="inv-contact-row" style={{ marginBottom: 14 }}>
                                {commDetail.contactPhone   && <span className="inv-contact-chip">📞 {commDetail.contactPhone}</span>}
                                {commDetail.contactEmail   && <span className="inv-contact-chip">✉ {commDetail.contactEmail}</span>}
                                {commDetail.contactAddress && <span className="inv-contact-chip">📍 {commDetail.contactAddress}</span>}
                            </div>
                        )}

                        {/* Variants (read-only for community) */}
                        {commDetail.variants?.filter(v => v.isActive !== false).length > 0 && (
                            <div className="inv-variants-section" style={{ marginBottom: 14 }}>
                                <p className="inv-variants-title">Variantes disponíveis</p>
                                <table className="inv-variants-table">
                                    <thead>
                                        <tr>
                                            <th>Cor</th>
                                            <th>Tam.</th>
                                            <th>Qtd.</th>
                                            <th>Preço</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {commDetail.variants
                                            .filter(v => v.isActive !== false)
                                            .map(v => (
                                                <tr key={v.variantId}>
                                                    <td>{v.color ?? '—'}</td>
                                                    <td>{v.size ?? '—'}</td>
                                                    <td>{v.quantity}</td>
                                                    <td>{v.price != null ? fmtPrice(v.price) : '—'}</td>
                                                </tr>
                                            ))
                                        }
                                    </tbody>
                                </table>
                            </div>
                        )}

                        {/* Owner-only controls */}
                        {isOwnItem && (
                            commEditMode ? (
                                <form className="inv-modal-form" onSubmit={handleSaveComm}>
                                    <div className="inv-form-group">
                                        <label className="inv-form-label">Nome *</label>
                                        <input
                                            className="inv-form-input"
                                            value={commForm.name}
                                            onChange={e => setCF('name', e.target.value)}
                                            required
                                        />
                                    </div>
                                    <div className="inv-form-group">
                                        <label className="inv-form-label">Descrição</label>
                                        <textarea
                                            className="inv-form-textarea"
                                            rows={3}
                                            value={commForm.description}
                                            onChange={e => setCF('description', e.target.value)}
                                        />
                                    </div>
                                    <div className="inv-form-group">
                                        <label className="inv-form-label">Categoria</label>
                                        <select
                                            className="inv-form-select"
                                            value={commForm.idCategory}
                                            onChange={e => setCF('idCategory', e.target.value)}
                                        >
                                            <option value="">Sem categoria</option>
                                            {categories.map(c => (
                                                <option key={c.categoryId} value={c.categoryId}>{c.catgName}</option>
                                            ))}
                                        </select>
                                    </div>
                                    <div className="inv-form-row-3">
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Telefone</label>
                                            <input className="inv-form-input" value={commForm.contactPhone} onChange={e => setCF('contactPhone', e.target.value)} />
                                        </div>
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Email</label>
                                            <input className="inv-form-input" type="email" value={commForm.contactEmail} onChange={e => setCF('contactEmail', e.target.value)} />
                                        </div>
                                        <div className="inv-form-group">
                                            <label className="inv-form-label">Morada</label>
                                            <input className="inv-form-input" value={commForm.contactAddress} onChange={e => setCF('contactAddress', e.target.value)} />
                                        </div>
                                    </div>
                                    <div className="modal-actions">
                                        <button type="button" className="btn btn-secondary" onClick={() => setCommEditMode(false)}>Cancelar</button>
                                        <button type="submit" className="btn btn-primary" disabled={savingComm}>
                                            {savingComm ? 'A guardar...' : 'Guardar'}
                                        </button>
                                    </div>
                                </form>
                            ) : (
                                <>
                                    {/* Image upload */}
                                    <div style={{ marginBottom: 12 }}>
                                        <input
                                            ref={commFileRef}
                                            type="file"
                                            accept=".jpg,.jpeg,.png"
                                            style={{ display: 'none' }}
                                            onChange={handleCommImgUpload}
                                        />
                                        <span
                                            className="inv-upload-label"
                                            onClick={() => commFileRef.current?.click()}
                                        >
                                            {uploadingCommImg ? 'A carregar...' : '+ Adicionar imagem'}
                                        </span>
                                    </div>
                                    <div className="modal-actions">
                                        <button type="button" className="btn btn-danger" onClick={handleDeleteComm}>
                                            Remover anúncio
                                        </button>
                                        <button type="button" className="btn btn-primary" onClick={() => setCommEditMode(true)}>
                                            Editar
                                        </button>
                                    </div>
                                </>
                            )
                        )}
                    </>
                ) : null}
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

                    <p style={{ margin: '4px 0 0', fontWeight: 600, fontSize: '0.9rem', color: '#374151' }}>Contacto</p>
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
