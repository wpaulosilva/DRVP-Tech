import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
    getItem, updateItem, deleteItem,
    uploadItemImage, removeItemImage,
    getVariants, createVariant, updateVariant, deleteVariant,
    getCategories,
    getRequisitions, reviewRequisition, returnRequisition,
} from '../../services/inventoryService'
import '../../styles/Inventory.css'

const REQ_STATUS = {
    0: { label: 'Pendente',  cls: 'inv-status-pill--pending' },
    1: { label: 'Aprovado',  cls: 'inv-status-pill--approved' },
    2: { label: 'Rejeitado', cls: 'inv-status-pill--rejected' },
    3: { label: 'Devolvido', cls: 'inv-status-pill--returned' },
}

const emptyVariantForm = { color: '', size: '', quantity: 1, price: '' }

function fmtDate(v) {
    if (!v) return '—'
    return new Date(v).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function fmtPrice(v) {
    if (v == null) return '—'
    return Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' })
}

export default function StaffItemDetailPage() {
    const { itemId } = useParams()
    const navigate   = useNavigate()

    // ── Item state ────────────────────────────────────────────────────────────
    const [item, setItem]           = useState(null)
    const [loading, setLoading]     = useState(true)
    const [error, setError]         = useState(null)
    const [imgIndex, setImgIndex]   = useState(0)

    // ── Edit metadata ─────────────────────────────────────────────────────────
    const [editMode, setEditMode]   = useState(false)
    const [itemForm, setItemForm]   = useState({})
    const [savingItem, setSavingItem] = useState(false)
    const [itemError, setItemError] = useState(null)
    const [categories, setCategories] = useState([])

    // ── Image upload ──────────────────────────────────────────────────────────
    const fileInputRef              = useRef(null)
    const [uploadingImg, setUploadingImg] = useState(false)

    // ── Variants ──────────────────────────────────────────────────────────────
    const [variants, setVariants]           = useState([])
    const [showAddVariant, setShowAddVariant] = useState(false)
    const [variantForm, setVariantForm]     = useState(emptyVariantForm)
    const [editingVariant, setEditingVariant] = useState(null)
    const [variantEditForm, setVariantEditForm] = useState(emptyVariantForm)
    const [variantError, setVariantError]   = useState(null)
    const [savingVariant, setSavingVariant] = useState(false)

    // ── Requisitions ──────────────────────────────────────────────────────────
    const [requisitions, setRequisitions]   = useState([])
    const [loadingReqs, setLoadingReqs]     = useState(false)
    const [reqFilter, setReqFilter]         = useState('')

    // ── Review modal ──────────────────────────────────────────────────────────
    const [showReview, setShowReview]       = useState(false)
    const [reviewTarget, setReviewTarget]   = useState(null)
    const [reviewForm, setReviewForm]       = useState({ approve: true, expectedReturnDate: '', note: '' })
    const [reviewError, setReviewError]     = useState(null)
    const [savingReview, setSavingReview]   = useState(false)

    // ── Return modal ──────────────────────────────────────────────────────────
    const [showReturn, setShowReturn]       = useState(false)
    const [returnTarget, setReturnTarget]   = useState(null)
    const [returnQty, setReturnQty]         = useState(1)
    const [returnError, setReturnError]     = useState(null)
    const [savingReturn, setSavingReturn]   = useState(false)

    // ── Load item ─────────────────────────────────────────────────────────────
    const loadItem = useCallback(async () => {
        setLoading(true)
        setError(null)
        try {
            const detail = await getItem(Number(itemId))
            setItem(detail)
            setImgIndex(0)
            setItemForm({
                name: detail.name ?? '',
                description: detail.description ?? '',
                idCategory: detail.category?.categoryId ?? '',
                contactPhone: detail.contactPhone ?? '',
                contactEmail: detail.contactEmail ?? '',
                contactAddress: detail.contactAddress ?? '',
            })
            const vars = await getVariants(Number(itemId))
            setVariants(Array.isArray(vars) ? vars : [])
        } catch (e) {
            setError(e.message)
        } finally {
            setLoading(false)
        }
    }, [itemId])

    useEffect(() => { loadItem() }, [loadItem])

    useEffect(() => {
        getCategories().then(data => setCategories(Array.isArray(data) ? data : [])).catch(() => {})
    }, [])

    const loadReqs = useCallback(async () => {
        setLoadingReqs(true)
        try {
            const data = await getRequisitions()
            const list = Array.isArray(data) ? data : []
            setRequisitions(list.filter(r => r.itemId === Number(itemId)))
        } catch (e) {
            console.error(e)
        } finally {
            setLoadingReqs(false)
        }
    }, [itemId])

    useEffect(() => { loadReqs() }, [loadReqs])

    // ── Save metadata ─────────────────────────────────────────────────────────
    const handleSaveItem = async (e) => {
        e.preventDefault()
        setSavingItem(true)
        setItemError(null)
        try {
            const body = {}
            if (itemForm.name)          body.name = itemForm.name
            if (itemForm.description)   body.description = itemForm.description
            if (itemForm.idCategory)    body.idCategory = Number(itemForm.idCategory)
            if (itemForm.contactPhone)  body.contactPhone = itemForm.contactPhone
            if (itemForm.contactEmail)  body.contactEmail = itemForm.contactEmail
            if (itemForm.contactAddress) body.contactAddress = itemForm.contactAddress
            await updateItem(Number(itemId), body)
            await loadItem()
            setEditMode(false)
        } catch (e) {
            setItemError(e.message)
        } finally {
            setSavingItem(false)
        }
    }

    // ── Deactivate item ───────────────────────────────────────────────────────
    const handleDeactivate = async () => {
        if (!window.confirm(`Desativar "${item?.name}"? O artigo ficará oculto ao público.`)) return
        try {
            await deleteItem(Number(itemId))
            navigate('/staff/inventario')
        } catch (e) {
            setItemError(e.message)
        }
    }

    // ── Image upload ──────────────────────────────────────────────────────────
    const handleImageUpload = async (e) => {
        const file = e.target.files?.[0]
        if (!file) return
        setUploadingImg(true)
        try {
            await uploadItemImage(Number(itemId), file)
            await loadItem()
        } catch (e) {
            setItemError(e.message)
        } finally {
            setUploadingImg(false)
            e.target.value = ''
        }
    }

    const handleRemoveImage = async (imageId) => {
        if (!window.confirm('Remover esta imagem?')) return
        try {
            await removeItemImage(Number(itemId), imageId)
            setImgIndex(0)
            await loadItem()
        } catch (e) {
            setItemError(e.message)
        }
    }

    // ── Add variant ───────────────────────────────────────────────────────────
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
            await createVariant(Number(itemId), body)
            const updated = await getVariants(Number(itemId))
            setVariants(Array.isArray(updated) ? updated : [])
            setVariantForm(emptyVariantForm)
            setShowAddVariant(false)
        } catch (e) {
            setVariantError(e.message)
        } finally {
            setSavingVariant(false)
        }
    }

    // ── Edit variant ──────────────────────────────────────────────────────────
    const startEditVariant = (v) => {
        setEditingVariant(v.variantId)
        setVariantEditForm({ color: v.color ?? '', size: v.size ?? '', quantity: v.quantity, price: v.price ?? '' })
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
            await updateVariant(Number(itemId), variantId, body)
            const updated = await getVariants(Number(itemId))
            setVariants(Array.isArray(updated) ? updated : [])
            setEditingVariant(null)
        } catch (e) {
            setVariantError(e.message)
        } finally {
            setSavingVariant(false)
        }
    }

    const handleToggleVariant = async (v) => {
        try {
            await updateVariant(Number(itemId), v.variantId, { isActive: !v.isActive })
            const updated = await getVariants(Number(itemId))
            setVariants(Array.isArray(updated) ? updated : [])
        } catch (e) {
            setVariantError(e.message)
        }
    }

    const handleDeleteVariant = async (variantId) => {
        if (!window.confirm('Eliminar esta variante permanentemente?')) return
        try {
            await deleteVariant(Number(itemId), variantId)
            const updated = await getVariants(Number(itemId))
            setVariants(Array.isArray(updated) ? updated : [])
        } catch (e) {
            setVariantError(e.message)
        }
    }

    // ── Review requisition ────────────────────────────────────────────────────
    const handleReview = async (e) => {
        e.preventDefault()
        setSavingReview(true)
        setReviewError(null)
        try {
            await reviewRequisition(reviewTarget.requisitionId, {
                approve: reviewForm.approve,
                ...(reviewForm.expectedReturnDate ? { expectedReturnDate: reviewForm.expectedReturnDate } : {}),
                ...(reviewForm.note ? { note: reviewForm.note } : {}),
            })
            setShowReview(false)
            loadReqs()
        } catch (e) {
            setReviewError(e.message)
        } finally {
            setSavingReview(false)
        }
    }

    // ── Return ────────────────────────────────────────────────────────────────
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

    // ── Derived ───────────────────────────────────────────────────────────────
    const activeVariants = variants.filter(v => v.isActive !== false && v.quantity > 0)
    const images         = item?.images ?? []
    const currentImg     = images[imgIndex]

    const filteredReqs = reqFilter !== ''
        ? requisitions.filter(r => String(r.status) === reqFilter)
        : requisitions

    const setIF = (k, v) => setItemForm(f => ({ ...f, [k]: v }))
    const setVF = (k, v) => setVariantForm(f => ({ ...f, [k]: v }))
    const setVEF = (k, v) => setVariantEditForm(f => ({ ...f, [k]: v }))

    if (loading) return (
        <section className="dashboard-page-card">
            <p className="inv-loading">A carregar artigo...</p>
        </section>
    )

    if (error) return (
        <section className="dashboard-page-card">
            <button type="button" className="inv-back-btn" onClick={() => navigate('/staff/inventario')}>← Inventário</button>
            <p className="inv-error" style={{ marginTop: 16 }}>{error}</p>
        </section>
    )

    return (
        <section className="dashboard-page-card">
            <div className="inv-detail-page">

                {/* ── Header ─────────────────────────────────────────────────── */}
                <div className="inv-detail-page-header">
                    <button type="button" className="inv-back-btn" onClick={() => navigate('/staff/inventario')}>
                        ← Inventário
                    </button>
                    <h2 className="inv-detail-page-title">{item?.name}</h2>
                    <div className="inv-detail-page-actions">
                        {item?.isActive && (
                            <button type="button" className="btn btn-danger btn-sm" onClick={handleDeactivate}>
                                Desativar
                            </button>
                        )}
                    </div>
                </div>

                {/* ── Status banner ───────────────────────────────────────────── */}
                {!item?.isActive ? (
                    <div className="inv-status-banner inv-status-banner--warn">
                        ⚠ Este artigo está desativado e não é visível ao público.
                    </div>
                ) : activeVariants.length === 0 ? (
                    <div className="inv-status-banner inv-status-banner--warn">
                        ⚠ Sem variantes com stock — este artigo não está visível ao público. Adicione pelo menos uma variante com quantidade superior a 0.
                    </div>
                ) : (
                    <div className="inv-status-banner inv-status-banner--ok">
                        ✓ Artigo ativo e visível ao público — {activeVariants.length} variante{activeVariants.length !== 1 ? 's' : ''} disponível{activeVariants.length !== 1 ? 'eis' : ''}.
                    </div>
                )}

                {itemError && <div className="inv-form-error">{itemError}</div>}

                {/* ── Images + Metadata ───────────────────────────────────────── */}
                <div className="inv-two-col">

                    {/* Images */}
                    <div className="inv-section-card">
                        <p className="inv-section-title">Imagens</p>
                        {images.length > 0 ? (
                            <div className="inv-detail-images">
                                <img src={currentImg?.imageUrl} alt={item?.name} className="inv-detail-main-img" />
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
                                    <span
                                        className="inv-upload-label"
                                        onClick={() => fileInputRef.current?.click()}
                                    >
                                        {uploadingImg ? 'A carregar...' : '+ Adicionar imagem'}
                                    </span>
                                    {currentImg && (
                                        <button
                                            type="button"
                                            className="btn btn-danger btn-sm"
                                            onClick={() => handleRemoveImage(currentImg.imageId)}
                                        >
                                            Remover
                                        </button>
                                    )}
                                </div>
                            </div>
                        ) : (
                            <div className="inv-detail-main-placeholder">{(item?.name ?? '?')[0]}</div>
                        )}
                        {images.length === 0 && (
                            <div style={{ marginTop: 12 }}>
                                <span
                                    className="inv-upload-label"
                                    onClick={() => fileInputRef.current?.click()}
                                >
                                    {uploadingImg ? 'A carregar...' : '+ Adicionar imagem'}
                                </span>
                            </div>
                        )}
                        <input
                            ref={fileInputRef}
                            type="file"
                            accept=".jpg,.jpeg,.png"
                            style={{ display: 'none' }}
                            onChange={handleImageUpload}
                        />
                    </div>

                    {/* Metadata */}
                    <div className="inv-section-card">
                        <div className="inv-section-title">
                            Informações
                            <div className="inv-section-title-actions">
                                {!editMode && (
                                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditMode(true)}>
                                        Editar
                                    </button>
                                )}
                            </div>
                        </div>

                        {editMode ? (
                            <form className="inv-modal-form" onSubmit={handleSaveItem}>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Nome *</label>
                                    <input className="inv-form-input" value={itemForm.name} onChange={e => setIF('name', e.target.value)} required />
                                </div>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Descrição</label>
                                    <textarea className="inv-form-textarea" rows={3} value={itemForm.description} onChange={e => setIF('description', e.target.value)} />
                                </div>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Categoria</label>
                                    <select className="inv-form-select" value={itemForm.idCategory} onChange={e => setIF('idCategory', e.target.value)}>
                                        <option value="">Sem categoria</option>
                                        {categories.map(c => <option key={c.categoryId} value={c.categoryId}>{c.catgName}</option>)}
                                    </select>
                                </div>
                                <div className="modal-actions">
                                    <button type="button" className="btn btn-secondary" onClick={() => setEditMode(false)}>Cancelar</button>
                                    <button type="submit" className="btn btn-primary" disabled={savingItem}>
                                        {savingItem ? 'A guardar...' : 'Guardar'}
                                    </button>
                                </div>
                            </form>
                        ) : (
                            <div className="inv-meta-grid">
                                <span className="inv-meta-label">Tipo</span>
                                <span className="inv-meta-value">{item?.fromSchool ? 'Escolar' : 'Comunidade'}</span>
                                <span className="inv-meta-label">Categoria</span>
                                <span className="inv-meta-value">{item?.category?.catgName ?? '—'}</span>
                                <span className="inv-meta-label">Descrição</span>
                                <span className="inv-meta-value">{item?.description ?? '—'}</span>
                                {item?.contactPhone && <>
                                    <span className="inv-meta-label">Telefone</span>
                                    <span className="inv-meta-value">{item.contactPhone}</span>
                                </>}
                                {item?.contactEmail && <>
                                    <span className="inv-meta-label">Email</span>
                                    <span className="inv-meta-value">{item.contactEmail}</span>
                                </>}
                                {item?.contactAddress && <>
                                    <span className="inv-meta-label">Morada</span>
                                    <span className="inv-meta-value">{item.contactAddress}</span>
                                </>}
                            </div>
                        )}
                    </div>
                </div>

                {/* ── Variants ────────────────────────────────────────────────── */}
                <div className="inv-section-card">
                    <div className="inv-section-title">
                        Variantes
                        <span style={{ fontSize: '0.8rem', fontWeight: 400, color: '#6b7280', marginLeft: 4 }}>
                            ({variants.length} total, {activeVariants.length} com stock)
                        </span>
                        <div className="inv-section-title-actions">
                            <button type="button" className="btn btn-primary btn-sm" onClick={() => { setShowAddVariant(v => !v); setVariantForm(emptyVariantForm) }}>
                                + Variante
                            </button>
                        </div>
                    </div>

                    {variantError && <div className="inv-form-error" style={{ marginBottom: 12 }}>{variantError}</div>}

                    {showAddVariant && (
                        <form className="inv-add-variant-form" onSubmit={handleAddVariant}>
                            <div className="inv-form-row-3">
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Cor</label>
                                    <input className="inv-form-input" value={variantForm.color} onChange={e => setVF('color', e.target.value)} placeholder="ex: Azul" />
                                </div>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Tamanho</label>
                                    <input className="inv-form-input" value={variantForm.size} onChange={e => setVF('size', e.target.value)} placeholder="ex: M" />
                                </div>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Preço (€)</label>
                                    <input className="inv-form-input" type="number" min="0" step="0.01" value={variantForm.price} onChange={e => setVF('price', e.target.value)} placeholder="Opcional" />
                                </div>
                            </div>
                            <div className="inv-form-group">
                                <label className="inv-form-label">Quantidade *</label>
                                <input className="inv-form-input" type="number" min="0" value={variantForm.quantity} onChange={e => setVF('quantity', e.target.value)} required style={{ maxWidth: 120 }} />
                            </div>
                            <div style={{ display: 'flex', gap: 8 }}>
                                <button type="button" className="btn btn-secondary btn-sm" onClick={() => setShowAddVariant(false)}>Cancelar</button>
                                <button type="submit" className="btn btn-primary btn-sm" disabled={savingVariant}>
                                    {savingVariant ? 'A guardar...' : 'Adicionar'}
                                </button>
                            </div>
                        </form>
                    )}

                    {variants.length === 0 ? (
                        <p style={{ color: '#6b7280', fontSize: '0.9rem', margin: '8px 0 0' }}>Nenhuma variante criada.</p>
                    ) : (
                        <div style={{ overflowX: 'auto' }}>
                            <table className="inv-variants-table">
                                <thead>
                                    <tr>
                                        <th>Cor</th>
                                        <th>Tam.</th>
                                        <th>Qtd.</th>
                                        <th>Preço</th>
                                        <th>Estado</th>
                                        <th></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {variants.map(v => (
                                        editingVariant === v.variantId ? (
                                            <tr key={v.variantId}>
                                                <td><input className="inv-form-input" value={variantEditForm.color} onChange={e => setVEF('color', e.target.value)} style={{ width: 80 }} /></td>
                                                <td><input className="inv-form-input" value={variantEditForm.size} onChange={e => setVEF('size', e.target.value)} style={{ width: 60 }} /></td>
                                                <td><input className="inv-form-input" type="number" min="0" value={variantEditForm.quantity} onChange={e => setVEF('quantity', e.target.value)} style={{ width: 70 }} /></td>
                                                <td><input className="inv-form-input" type="number" min="0" step="0.01" value={variantEditForm.price} onChange={e => setVEF('price', e.target.value)} style={{ width: 80 }} /></td>
                                                <td>—</td>
                                                <td>
                                                    <div className="inv-variant-actions">
                                                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditingVariant(null)}>✕</button>
                                                        <button type="button" className="btn btn-primary btn-sm" disabled={savingVariant} onClick={() => handleUpdateVariant(v.variantId)}>✓</button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ) : (
                                            <tr key={v.variantId} className={v.isActive === false ? 'inv-variant-inactive' : ''}>
                                                <td>{v.color ?? '—'}</td>
                                                <td>{v.size ?? '—'}</td>
                                                <td>{v.quantity}</td>
                                                <td>{v.price != null ? fmtPrice(v.price) : '—'}</td>
                                                <td>
                                                    <span className={`inv-stock-pill ${v.isActive === false ? 'inv-stock-pill--out' : v.quantity === 0 ? 'inv-stock-pill--low' : 'inv-stock-pill--in'}`}>
                                                        {v.isActive === false ? 'Inativo' : v.quantity === 0 ? 'Sem stock' : 'Ativo'}
                                                    </span>
                                                </td>
                                                <td>
                                                    <div className="inv-variant-actions">
                                                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => startEditVariant(v)}>Editar</button>
                                                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleToggleVariant(v)}>
                                                            {v.isActive === false ? 'Ativar' : 'Desativar'}
                                                        </button>
                                                        <button type="button" className="btn btn-danger btn-sm" onClick={() => handleDeleteVariant(v.variantId)}>Eliminar</button>
                                                    </div>
                                                </td>
                                            </tr>
                                        )
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>

                {/* ── Requisitions ────────────────────────────────────────────── */}
                <div className="inv-section-card">
                    <div className="inv-section-title">
                        Requisições
                        <div className="inv-section-title-actions">
                            <select className="inv-filter-select" style={{ fontSize: '0.82rem', padding: '4px 8px' }} value={reqFilter} onChange={e => setReqFilter(e.target.value)}>
                                <option value="">Todos</option>
                                <option value="0">Pendente</option>
                                <option value="1">Aprovado</option>
                                <option value="2">Rejeitado</option>
                                <option value="3">Devolvido</option>
                            </select>
                        </div>
                    </div>

                    {loadingReqs ? (
                        <p className="inv-loading">A carregar...</p>
                    ) : filteredReqs.length === 0 ? (
                        <p style={{ color: '#6b7280', fontSize: '0.9rem', margin: 0 }}>Nenhuma requisição{reqFilter ? ' com este estado' : ''} para este artigo.</p>
                    ) : (
                        <div className="inv-req-list">
                            {filteredReqs.map(r => {
                                const s = REQ_STATUS[r.status] ?? { label: String(r.status), cls: '' }
                                return (
                                    <div key={r.requisitionId} className="inv-req-card">
                                        <div className="inv-req-body">
                                            <p className="inv-req-title">
                                                por {r.parentName ?? `#${r.idParent}`}
                                                {(r.variantColor || r.variantSize) && (
                                                    <span style={{ fontWeight: 400, color: '#6b7280' }}> — {[r.variantColor, r.variantSize].filter(Boolean).join(' / ')}</span>
                                                )}
                                            </p>
                                            <div className="inv-req-meta">
                                                <span className="inv-req-meta-label">Qtd:</span>
                                                <span>{r.quantity}</span>
                                                <span className="inv-req-meta-label">Pedido em:</span>
                                                <span>{fmtDate(r.requestedAt)}</span>
                                                {r.needFrom && <>
                                                    <span className="inv-req-meta-label">Período:</span>
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
                                                    className="btn btn-primary btn-sm"
                                                    onClick={() => { setReviewTarget(r); setReviewForm({ approve: true, expectedReturnDate: '', note: '' }); setReviewError(null); setShowReview(true) }}
                                                >
                                                    Responder
                                                </button>
                                            )}
                                            {r.status === 1 && (
                                                <button
                                                    type="button"
                                                    className="btn btn-secondary btn-sm"
                                                    onClick={() => { setReturnTarget(r); setReturnQty(r.quantity); setReturnError(null); setShowReturn(true) }}
                                                >
                                                    Devolução
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                )
                            })}
                        </div>
                    )}
                </div>
            </div>

            {/* ══ Review Modal ═════════════════════════════════════════════════ */}
            {showReview && reviewTarget && (
                <div className="overlay">
                    <div className="modal">
                        <div className="modal-header">
                            <h3>Responder Requisição</h3>
                            <button type="button" className="modal-close-btn" onClick={() => setShowReview(false)}>✕</button>
                        </div>
                        <div className="modal-body">
                            <form className="inv-modal-form" onSubmit={handleReview}>
                                <p style={{ margin: 0 }}>
                                    <strong>{reviewTarget.parentName}</strong> — qtd. {reviewTarget.quantity}
                                    {reviewTarget.needFrom && <span style={{ color: '#6b7280' }}> ({fmtDate(reviewTarget.needFrom)}{reviewTarget.needUntil ? ` → ${fmtDate(reviewTarget.needUntil)}` : ''})</span>}
                                </p>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Decisão</label>
                                    <div style={{ display: 'flex', gap: 16 }}>
                                        <label style={{ display: 'flex', gap: 6, alignItems: 'center', cursor: 'pointer' }}>
                                            <input type="radio" checked={reviewForm.approve === true} onChange={() => setReviewForm(f => ({ ...f, approve: true }))} />
                                            Aprovar
                                        </label>
                                        <label style={{ display: 'flex', gap: 6, alignItems: 'center', cursor: 'pointer' }}>
                                            <input type="radio" checked={reviewForm.approve === false} onChange={() => setReviewForm(f => ({ ...f, approve: false }))} />
                                            Rejeitar
                                        </label>
                                    </div>
                                </div>
                                {reviewForm.approve && (
                                    <div className="inv-form-group">
                                        <label className="inv-form-label">Data prevista de devolução</label>
                                        <input className="inv-form-input" type="date" value={reviewForm.expectedReturnDate} onChange={e => setReviewForm(f => ({ ...f, expectedReturnDate: e.target.value }))} />
                                    </div>
                                )}
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Nota (opcional)</label>
                                    <textarea className="inv-form-textarea" rows={2} value={reviewForm.note} onChange={e => setReviewForm(f => ({ ...f, note: e.target.value }))} />
                                </div>
                                {reviewError && <div className="inv-form-error">{reviewError}</div>}
                                <div className="modal-actions">
                                    <button type="button" className="btn btn-secondary" onClick={() => setShowReview(false)}>Cancelar</button>
                                    <button type="submit" className="btn btn-primary" disabled={savingReview}>
                                        {savingReview ? 'A guardar...' : 'Confirmar'}
                                    </button>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            )}

            {/* ══ Return Modal ═════════════════════════════════════════════════ */}
            {showReturn && returnTarget && (
                <div className="overlay">
                    <div className="modal">
                        <div className="modal-header">
                            <h3>Registar Devolução</h3>
                            <button type="button" className="modal-close-btn" onClick={() => setShowReturn(false)}>✕</button>
                        </div>
                        <div className="modal-body">
                            <form className="inv-modal-form" onSubmit={handleReturn}>
                                <p style={{ margin: 0 }}>por <strong>{returnTarget.parentName}</strong></p>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Quantidade devolvida *</label>
                                    <input className="inv-form-input" type="number" min={1} max={returnTarget.quantity} value={returnQty} onChange={e => setReturnQty(e.target.value)} required style={{ maxWidth: 120 }} />
                                </div>
                                {returnError && <div className="inv-form-error">{returnError}</div>}
                                <div className="modal-actions">
                                    <button type="button" className="btn btn-secondary" onClick={() => setShowReturn(false)}>Cancelar</button>
                                    <button type="submit" className="btn btn-primary" disabled={savingReturn}>
                                        {savingReturn ? 'A registar...' : 'Confirmar'}
                                    </button>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            )}
        </section>
    )
}
