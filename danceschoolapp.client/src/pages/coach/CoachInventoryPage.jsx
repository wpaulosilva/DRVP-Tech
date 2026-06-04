import { useCallback, useEffect, useRef, useState } from 'react'
import { getMarketplace, getCategories } from '../../services/inventoryService'
import '../../styles/Inventory.css'

const PAGE_SIZE = 12

function fmtPrice(v) {
    if (v == null) return null
    return Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' })
}

export default function CoachInventoryPage() {
    const [items, setItems]           = useState([])
    const [total, setTotal]           = useState(0)
    const [page, setPage]             = useState(1)
    const [loading, setLoading]       = useState(false)
    const [error, setError]           = useState(null)
    const [search, setSearch]         = useState('')
    const [searchInput, setSearchInput] = useState('')
    const [categoryId, setCategoryId] = useState('')
    const [categories, setCategories] = useState([])
    const searchTimer                 = useRef(null)

    useEffect(() => {
        getCategories()
            .then(data => setCategories(Array.isArray(data) ? data : []))
            .catch(() => {})
    }, [])

    const load = useCallback(async () => {
        setLoading(true)
        setError(null)
        try {
            const result = await getMarketplace({ search, categoryId: categoryId || undefined, page, pageSize: PAGE_SIZE })
            setItems(result?.items ?? result?.Items ?? [])
            setTotal(result?.totalCount ?? result?.TotalCount ?? 0)
        } catch (e) {
            setError(e.message)
        } finally {
            setLoading(false)
        }
    }, [search, categoryId, page])

    useEffect(() => { load() }, [load])

    const handleSearchInput = (val) => {
        setSearchInput(val)
        clearTimeout(searchTimer.current)
        searchTimer.current = setTimeout(() => {
            setSearch(val)
            setPage(1)
        }, 400)
    }

    const totalPages = Math.ceil(total / PAGE_SIZE)

    return (
        <section className="dashboard-page-card">
            <div style={{ marginBottom: 20 }}>
                <h2 style={{ margin: 0 }}>Marketplace</h2>
                <p style={{ margin: '4px 0 0', color: '#6b7280', fontSize: '0.95rem' }}>
                    Artigos disponíveis — escola e comunidade.
                </p>
            </div>

            <div className="inv-filter-bar" style={{ marginTop: 8 }}>
                <input
                    className="inv-search-input"
                    placeholder="Pesquisar artigos..."
                    value={searchInput}
                    onChange={e => handleSearchInput(e.target.value)}
                />
                <select
                    className="inv-filter-select"
                    value={categoryId}
                    onChange={e => { setCategoryId(e.target.value); setPage(1) }}
                >
                    <option value="">Todas as categorias</option>
                    {categories.map(c => (
                        <option key={c.categoryId} value={c.categoryId}>{c.catgName}</option>
                    ))}
                </select>
            </div>

            {loading ? (
                <p className="inv-loading">A carregar...</p>
            ) : error ? (
                <p className="inv-error">{error}</p>
            ) : items.length === 0 ? (
                <div className="inv-empty">
                    <div className="inv-empty-icon">📦</div>
                    <p>Nenhum artigo encontrado.</p>
                </div>
            ) : (
                <>
                    <div className="inv-grid">
                        {items.map(card => {
                            const id         = card.itemId ?? card.ItemId
                            const img        = (card.images ?? card.Images ?? [])[0]?.imageUrl
                            const name       = card.name ?? card.Name
                            const desc       = card.description ?? card.Description
                            const cat        = card.category?.catgName ?? card.Category?.CatgName
                            const vc         = card.variantCount ?? card.VariantCount ?? 0
                            const fromSchool = card.fromSchool ?? card.FromSchool
                            return (
                                <div key={id} className="inv-card" style={{ cursor: 'default' }}>
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
                                        <div className="inv-card-meta">{vc} variante{vc !== 1 ? 's' : ''}</div>
                                        <span style={{ fontSize: '0.78rem', color: '#9ca3af' }}>
                                            {fromSchool ? 'Escola' : 'Comunidade'}
                                        </span>
                                    </div>
                                </div>
                            )
                        })}
                    </div>
                    {totalPages > 1 && (
                        <div className="inv-pagination">
                            <button className="inv-page-btn" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹</button>
                            <span className="inv-page-info">{page} / {totalPages}</span>
                            <button className="inv-page-btn" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>›</button>
                        </div>
                    )}
                </>
            )}
        </section>
    )
}
