import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'
import {
    getItem, updateItem, deleteItem,
    uploadItemImage, removeItemImage,
    getVariants, createVariant, updateVariant,
    getCategories,
    createRequisition,
} from '../../services/inventoryService'
import '../../styles/Inventory.css'

const emptyItemForm    = { name: '', description: '', idCategory: '', contactPhone: '', contactEmail: '', contactAddress: '' }
const emptyVariantForm = { color: '', size: '', quantity: 1, price: '' }
const emptyLoanForm    = { itemVariantId: '', quantity: 1, needFrom: '', needUntil: '', note: '' }

function fmtPrice(v) {
    if (v == null) return '—'
    return Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' })
}

export default function ParentItemDetailPage() {
    const { itemId } = useParams()
    const navigate   = useNavigate()
    const { user }   = useAuth()

    const [item, setItem]         = useState(null)
    const [loading, setLoading]   = useState(true)
    const [error, setError]       = useState(null)
    const [imgIndex, setImgIndex] = useState(0)

    // ── Owner edit state ──────────────────────────────────────────────────────
    const [editMode, setEditMode]     = useState(false)
    const [itemForm, setItemForm]     = useState(emptyItemForm)
    const [categories, setCategories] = useState([])
    const [savingItem, setSavingItem] = useState(false)
    const [itemError, setItemError]   = useState(null)

    // ── Image upload (owner) ──────────────────────────────────────────────────
    const fileRef                        = useRef(null)
    const [uploadingImg, setUploadingImg] = useState(false)

    // ── Variants (owner) ─────────────────────────────────────────────────────
    const [variants, setVariants]               = useState([])
    const [showAddVariant, setShowAddVariant]   = useState(false)
    const [variantForm, setVariantForm]         = useState(emptyVariantForm)
    const [editingVariant, setEditingVariant]   = useState(null)
    const [variantEditForm, setVariantEditForm] = useState(emptyVariantForm)
    const [variantError, setVariantError]       = useState(null)
    const [savingVariant, setSavingVariant]     = useState(false)

    // ── Loan form (school items) ──────────────────────────────────────────────
    const [loanForm, setLoanForm]     = useState(emptyLoanForm)
    const [loanError, setLoanError]   = useState(null)
    const [savingLoan, setSavingLoan] = useState(false)
    const [loanDone, setLoanDone]     = useState(false)

    // ── Load item ─────────────────────────────────────────────────────────────
    const load = useCallback(async () => {
        setLoading(true)
        setError(null)
        try {
            const data = await getItem(itemId)
            setItem(data)
            setImgIndex(0)
            setItemForm({
                name:           data.name ?? '',
                description:    data.description ?? '',
                idCategory:     data.category?.categoryId ?? '',
                contactPhone:   data.contactPhone ?? '',
                contactEmail:   data.contactEmail ?? '',
                contactAddress: data.contactAddress ?? '',
            })
            setVariants(data.variants ?? [])
        } catch (e) {
            setError(e.message)
        } finally {
            setLoading(false)
        }
    }, [itemId])

    useEffect(() => { load() }, [load])

    useEffect(() => {
        getCategories()
            .then(d => setCategories(Array.isArray(d) ? d : []))
            .catch(() => {})
    }, [])

    const isOwner = item && user && item.idOwner === user.userId

    // ── Save item edits ───────────────────────────────────────────────────────
    const handleSaveItem = async (e) => {
        e.preventDefault()
        setSavingItem(true)
        setItemError(null)
        try {
            const body = {}
            if (itemForm.name)           body.name = itemForm.name
            if (itemForm.description)    body.description = itemForm.description
            if (itemForm.idCategory)     body.idCategory = Number(itemForm.idCategory)
            if (itemForm.contactPhone)   body.contactPhone = itemForm.contactPhone
            if (itemForm.contactEmail)   body.contactEmail = itemForm.contactEmail
            if (itemForm.contactAddress) body.contactAddress = itemForm.contactAddress
            await updateItem(item.itemId, body)
            await load()
            setEditMode(false)
        } catch (e) {
            setItemError(e.message)
        } finally {
            setSavingItem(false)
        }
    }

    // ── Delete (owner) ────────────────────────────────────────────────────────
    const handleDelete = async () => {
        if (!window.confirm(`Remover anúncio "${item?.name}"?`)) return
        try {
            await deleteItem(item.itemId)
            navigate('/parent/inventario')
        } catch (e) {
            setItemError(e.message)
        }
    }

    // ── Image upload (owner) ──────────────────────────────────────────────────
    const handleImageUpload = async (e) => {
        const file = e.target.files?.[0]
        if (!file) return
        setUploadingImg(true)
        try {
            await uploadItemImage(item.itemId, file)
            await load()
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
            await removeItemImage(item.itemId, imageId)
            setImgIndex(0)
            await load()
        } catch (e) {
            setItemError(e.message)
        }
    }

    // ── Variant management (owner) ────────────────────────────────────────────
    const handleAddVariant = async (e) => {
        e.preventDefault()
        setSavingVariant(true)
        setVariantError(null)
        try {
            const body = {
                quantity: Number(variantForm.quantity),
                ...(variantForm.color ? { color: variantForm.color } : {}),
                ...(variantForm.size  ? { size:  variantForm.size  } : {}),
                ...(variantForm.price ? { price: Number(variantForm.price) } : {}),
            }
            await createVariant(item.itemId, body)
            const updated = await getVariants(item.itemId)
            setVariants(Array.isArray(updated) ? updated : [])
            setVariantForm(emptyVariantForm)
            setShowAddVariant(false)
        } catch (e) {
            setVariantError(e.message)
        } finally {
            setSavingVariant(false)
        }
    }

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
                ...(variantEditForm.size  ? { size:  variantEditForm.size  } : {}),
                price: variantEditForm.price ? Number(variantEditForm.price) : null,
            }
            await updateVariant(item.itemId, variantId, body)
            const updated = await getVariants(item.itemId)
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
            await updateVariant(item.itemId, v.variantId, { isActive: !v.isActive })
            const updated = await getVariants(item.itemId)
            setVariants(Array.isArray(updated) ? updated : [])
        } catch (e) {
            setVariantError(e.message)
        }
    }

    // ── Loan request (school items) ───────────────────────────────────────────
    const handleLoanSubmit = async (e) => {
        e.preventDefault()
        if (!loanForm.itemVariantId) { setLoanError('Selecione uma variante.'); return }
        setSavingLoan(true)
        setLoanError(null)
        try {
            const body = {
                itemVariantId: Number(loanForm.itemVariantId),
                quantity:      Number(loanForm.quantity),
                ...(loanForm.needFrom  ? { needFrom:  loanForm.needFrom  } : {}),
                ...(loanForm.needUntil ? { needUntil: loanForm.needUntil } : {}),
                ...(loanForm.note      ? { note:      loanForm.note      } : {}),
            }
            await createRequisition(body)
            setLoanDone(true)
            setLoanForm(emptyLoanForm)
        } catch (e) {
            setLoanError(e.message)
        } finally {
            setSavingLoan(false)
        }
    }

    // ── Loading / error ───────────────────────────────────────────────────────
    if (loading) return (
        <section className="dashboard-page-card">
            <p className="inv-loading">A carregar artigo...</p>
        </section>
    )

    if (error || !item) return (
        <section className="dashboard-page-card">
            <button type="button" className="inv-back-btn" onClick={() => navigate('/parent/inventario')}>
                ← Inventário
            </button>
            <p className="inv-error" style={{ marginTop: 16 }}>{error ?? 'Artigo não encontrado.'}</p>
        </section>
    )

    const images            = item.images ?? []
    const currentImg        = images[imgIndex]
    const activeVariants    = variants.filter(v => v.isActive !== false)
    const availableVariants = (item.variants ?? []).filter(v => v.isActive !== false && v.quantity > 0)
    const selectedLoanV     = availableVariants.find(v => v.variantId === Number(loanForm.itemVariantId))
    const isVisible      = item.isActive && (item.fromSchool ? availableVariants.length > 0 : activeVariants.length > 0)

    const setIF  = (k, v) => setItemForm(f => ({ ...f, [k]: v }))
    const setVF  = (k, v) => setVariantForm(f => ({ ...f, [k]: v }))
    const setVEF = (k, v) => setVariantEditForm(f => ({ ...f, [k]: v }))
    const setLF  = (k, v) => setLoanForm(f => ({ ...f, [k]: v }))

    return (
        <section className="dashboard-page-card">
            <div className="inv-detail-page">

                {/* ── Header ──────────────────────────────────────────────────── */}
                <div className="inv-detail-page-header">
                    <button type="button" className="inv-back-btn" onClick={() => navigate('/parent/inventario')}>
                        ← Inventário
                    </button>
                    <h2 className="inv-detail-page-title">{item.name}</h2>
                    {isOwner && (
                        <div className="inv-detail-page-actions">
                            <button type="button" className="btn btn-danger btn-sm" onClick={handleDelete}>
                                Remover anúncio
                            </button>
                        </div>
                    )}
                </div>

                {/* ── Status banner (owner only) ──────────────────────────────── */}
                {isOwner && (
                    isVisible ? (
                        <div className="inv-status-banner inv-status-banner--ok">
                            ✓ O seu anúncio está visível no marketplace com {activeVariants.length} variante{activeVariants.length !== 1 ? 's' : ''} ativa{activeVariants.length !== 1 ? 's' : ''}.
                        </div>
                    ) : (
                        <div className="inv-status-banner inv-status-banner--warn">
                            ⚠ O seu anúncio não está visível —{' '}
                            {!item.isActive
                                ? 'artigo desativado.'
                                : 'sem variantes ativas. Adicione pelo menos uma variante para aparecer no marketplace.'}
                        </div>
                    )
                )}

                {itemError && <div className="inv-form-error">{itemError}</div>}

                {/* ── Images + Metadata ────────────────────────────────────────── */}
                <div className="inv-two-col">

                    {/* Images */}
                    <div className="inv-section-card">
                        <p className="inv-section-title">Imagens</p>
                        {images.length > 0 ? (
                            <div className="inv-detail-images">
                                <img src={currentImg?.imageUrl} alt={item.name} className="inv-detail-main-img" />
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
                            </div>
                        ) : (
                            <div className="inv-detail-main-placeholder">{item.name[0]}</div>
                        )}
                        {isOwner && (
                            <div className="inv-detail-img-actions" style={{ marginTop: 12 }}>
                                <span className="inv-upload-label" onClick={() => fileRef.current?.click()}>
                                    {uploadingImg ? 'A carregar...' : '+ Adicionar imagem'}
                                </span>
                                {currentImg && (
                                    <button type="button" className="btn btn-danger btn-sm" onClick={() => handleRemoveImage(currentImg.imageId)}>
                                        Remover
                                    </button>
                                )}
                                <input ref={fileRef} type="file" accept=".jpg,.jpeg,.png" style={{ display: 'none' }} onChange={handleImageUpload} />
                            </div>
                        )}
                    </div>

                    {/* Metadata */}
                    <div className="inv-section-card">
                        <div className="inv-section-title">
                            Informações
                            {isOwner && !editMode && (
                                <div className="inv-section-title-actions">
                                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditMode(true)}>Editar</button>
                                </div>
                            )}
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
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Telefone de contacto</label>
                                    <input className="inv-form-input" value={itemForm.contactPhone} onChange={e => setIF('contactPhone', e.target.value)} />
                                </div>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Email de contacto</label>
                                    <input className="inv-form-input" type="email" value={itemForm.contactEmail} onChange={e => setIF('contactEmail', e.target.value)} />
                                </div>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Morada</label>
                                    <input className="inv-form-input" value={itemForm.contactAddress} onChange={e => setIF('contactAddress', e.target.value)} />
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
                                <span className="inv-meta-value">{item.fromSchool ? 'Escolar' : 'Comunidade'}</span>
                                <span className="inv-meta-label">Categoria</span>
                                <span className="inv-meta-value">{item.category?.catgName ?? '—'}</span>
                                {item.description && <>
                                    <span className="inv-meta-label">Descrição</span>
                                    <span className="inv-meta-value">{item.description}</span>
                                </>}
                                {!item.fromSchool && item.contactPhone && <>
                                    <span className="inv-meta-label">Telefone</span>
                                    <span className="inv-meta-value">{item.contactPhone}</span>
                                </>}
                                {!item.fromSchool && item.contactEmail && <>
                                    <span className="inv-meta-label">Email</span>
                                    <span className="inv-meta-value">{item.contactEmail}</span>
                                </>}
                                {!item.fromSchool && item.contactAddress && <>
                                    <span className="inv-meta-label">Morada</span>
                                    <span className="inv-meta-value">{item.contactAddress}</span>
                                </>}
                            </div>
                        )}
                    </div>
                </div>

                {/* ── Contact chips (community, non-owner) ────────────────────── */}
                {!item.fromSchool && !isOwner && (item.contactPhone || item.contactEmail || item.contactAddress) && (
                    <div className="inv-section-card">
                        <p className="inv-section-title">Contacto do vendedor</p>
                        <div className="inv-contact-row">
                            {item.contactPhone   && <span className="inv-contact-chip">📞 {item.contactPhone}</span>}
                            {item.contactEmail   && <span className="inv-contact-chip">✉ {item.contactEmail}</span>}
                            {item.contactAddress && <span className="inv-contact-chip">📍 {item.contactAddress}</span>}
                        </div>
                    </div>
                )}

                {/* ── Variants section ─────────────────────────────────────────── */}
                {isOwner && !item.fromSchool ? (

                    /* Owner manages variants */
                    <div className="inv-section-card">
                        <div className="inv-section-title">
                            Variantes
                            <span style={{ fontSize: '0.8rem', fontWeight: 400, color: '#6b7280', marginLeft: 4 }}>
                                ({variants.length} total, {activeVariants.length} ativa{activeVariants.length !== 1 ? 's' : ''})
                            </span>
                            <div className="inv-section-title-actions">
                                <button type="button" className="btn btn-primary btn-sm" onClick={() => { setVF('color', ''); setVF('size', ''); setVF('quantity', 1); setVF('price', ''); setVariantError(null); setShowAddVariant(v => !v) }}>
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
                                        {savingVariant ? 'A adicionar...' : 'Adicionar'}
                                    </button>
                                </div>
                            </form>
                        )}

                        {variants.length === 0 ? (
                            <p style={{ color: '#6b7280', fontSize: '0.9rem', margin: '8px 0 0' }}>
                                Nenhuma variante. Adicione pelo menos uma para o artigo aparecer no marketplace.
                            </p>
                        ) : (
                            <div style={{ overflowX: 'auto' }}>
                                <table className="inv-variants-table">
                                    <thead>
                                        <tr><th>Cor</th><th>Tam.</th><th>Qtd.</th><th>Preço</th><th>Estado</th><th></th></tr>
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
                                                        <span className={`inv-stock-pill ${v.isActive === false ? 'inv-stock-pill--out' : 'inv-stock-pill--in'}`}>
                                                            {v.isActive === false ? 'Inativa' : 'Ativa'}
                                                        </span>
                                                    </td>
                                                    <td>
                                                        <div className="inv-variant-actions">
                                                            <button type="button" className="btn btn-secondary btn-sm" onClick={() => startEditVariant(v)}>Editar</button>
                                                            <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleToggleVariant(v)}>
                                                                {v.isActive === false ? 'Ativar' : 'Desativar'}
                                                            </button>
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

                ) : (() => {
                    /* Read-only variant display */
                    const readVariants = item.fromSchool ? availableVariants : (item.variants ?? []).filter(v => v.isActive !== false)
                    if (readVariants.length === 0) return null
                    return (
                        <div className="inv-section-card">
                            <p className="inv-section-title">Variantes disponíveis</p>
                            <div style={{ overflowX: 'auto' }}>
                                <table className="inv-variants-table">
                                    <thead>
                                        <tr><th>Cor</th><th>Tam.</th><th>Qtd.</th><th>Preço</th></tr>
                                    </thead>
                                    <tbody>
                                        {readVariants.map(v => (
                                            <tr key={v.variantId}>
                                                <td>{v.color ?? '—'}</td>
                                                <td>{v.size ?? '—'}</td>
                                                <td>{v.quantity}</td>
                                                <td>{v.price != null ? fmtPrice(v.price) : '—'}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    )
                })()}

                {/* ── Loan / request form (non-owner) ──────────────────────────── */}
                {!isOwner && (
                    <div className="inv-section-card">
                        <p className="inv-section-title">{item.fromSchool ? 'Pedir Empréstimo' : 'Pedir Artigo'}</p>
                        {loanDone ? (
                            <div className="inv-status-banner inv-status-banner--ok">
                                ✓ Pedido enviado com sucesso! Pode acompanhar o estado em &quot;Pedidos&quot;.
                            </div>
                        ) : availableVariants.length === 0 ? (
                            <div className="inv-status-banner inv-status-banner--info">
                                Não existem variantes disponíveis neste momento.
                            </div>
                        ) : (
                            <form className="inv-loan-form" onSubmit={handleLoanSubmit}>
                                <div className="inv-form-group">
                                    <label className="inv-form-label">Variante *</label>
                                    <select
                                        className="inv-form-select"
                                        value={loanForm.itemVariantId}
                                        onChange={e => setLF('itemVariantId', e.target.value)}
                                        required
                                    >
                                        <option value="">Selecionar variante...</option>
                                        {availableVariants.map(v => (
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
                                        max={selectedLoanV?.quantity ?? 99}
                                        value={loanForm.quantity}
                                        onChange={e => setLF('quantity', e.target.value)}
                                        required
                                        style={{ maxWidth: 120 }}
                                    />
                                </div>
                                <div className="inv-form-row-2">
                                    <div className="inv-form-group">
                                        <label className="inv-form-label">Necessário de</label>
                                        <input className="inv-form-input" type="date" value={loanForm.needFrom} onChange={e => setLF('needFrom', e.target.value)} />
                                    </div>
                                    <div className="inv-form-group">
                                        <label className="inv-form-label">Necessário até</label>
                                        <input className="inv-form-input" type="date" value={loanForm.needUntil} onChange={e => setLF('needUntil', e.target.value)} />
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
                                <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                                    <button type="submit" className="btn btn-primary" disabled={savingLoan}>
                                        {savingLoan ? 'A enviar...' : item.fromSchool ? 'Pedir Empréstimo' : 'Enviar Pedido'}
                                    </button>
                                </div>
                            </form>
                        )}
                    </div>
                )}

            </div>
        </section>
    )
}
