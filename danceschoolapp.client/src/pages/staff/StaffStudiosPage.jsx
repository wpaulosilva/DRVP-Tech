import { useEffect, useState, useCallback } from 'react'
import Modal from '../../components/common/Modal'
import { getStudios, getStudio, createStudio, updateStudio, deactivateStudio, activateStudio } from '../../services/studiosService'
import { getModalities } from '../../services/modalitiesService'
import '../../styles/ManageCards.css'

// ─── Studio card ───────────────────────────────────────────────────────────────
function StudioCard({ studio, onEdit, onToggleActive }) {
    const active = studio.IsActive ?? studio.isActive ?? true
    const name = studio.Name ?? studio.name ?? '—'
    const capacity = studio.Capacity ?? studio.capacity ?? '—'
    const address = studio.Address ?? studio.address ?? null
    const modalityCount = studio.ModalityCount ?? studio.modalityCount ?? 0
    const id = studio.StudioId ?? studio.studioId

    return (
        <div className={`mc-card${active ? '' : ' mc-card--inactive'}`}>
            <div className="mc-card-top">
                <p className="mc-card-name">{name}</p>
                <span className={`mc-badge ${active ? 'mc-badge--active' : 'mc-badge--inactive'}`}>
                    {active ? 'Ativo' : 'Inativo'}
                </span>
            </div>
            <div className="mc-card-meta">
                <span>Capacidade: <strong>{capacity}</strong> pessoas</span>
                <span>Modalidades: <strong>{modalityCount}</strong></span>
                {address && <span>{address}</span>}
            </div>
            <div className="mc-card-actions">
                <button type="button" className="btn btn-secondary" onClick={() => onEdit(id)}>
                    Editar
                </button>
                <button
                    type="button"
                    className={`btn ${active ? 'btn-danger' : 'btn-secondary'}`}
                    onClick={() => onToggleActive(id, active)}
                >
                    {active ? 'Desativar' : 'Ativar'}
                </button>
            </div>
        </div>
    )
}

// ─── Studio form modal (create + edit) ─────────────────────────────────────────
function StudioFormModal({ open, studioId, modalities, onClose, onSaved }) {
    const isEdit = !!studioId
    const [form, setForm] = useState({ name: '', capacity: '', address: '' })
    const [selectedModalities, setSelectedModalities] = useState([])
    const [saving, setSaving] = useState(false)
    const [loading, setLoading] = useState(false)

    useEffect(() => {
        if (!open) return
        if (isEdit) {
            setLoading(true)
            getStudio(studioId)
                .then(data => {
                    setForm({
                        name: data.Name ?? data.name ?? '',
                        capacity: String(data.Capacity ?? data.capacity ?? ''),
                        address: data.Address ?? data.address ?? '',
                    })
                    const ids = (data.Modalities ?? data.modalities ?? []).map(
                        m => m.ModalityId ?? m.modalityId
                    )
                    setSelectedModalities(ids)
                })
                .catch(console.error)
                .finally(() => setLoading(false))
        } else {
            setForm({ name: '', capacity: '', address: '' })
            setSelectedModalities([])
        }
    }, [open, studioId, isEdit])

    const toggleModality = (id) => {
        setSelectedModalities(prev =>
            prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
        )
    }

    const handleSubmit = async (e) => {
        e.preventDefault()
        setSaving(true)
        try {
            const body = {
                name: form.name.trim(),
                capacity: Number(form.capacity),
                ...(form.address.trim() ? { address: form.address.trim() } : {}),
                modalityIds: selectedModalities,
            }
            if (isEdit) {
                await updateStudio(studioId, body)
            } else {
                await createStudio(body)
            }
            onSaved()
        } catch (e) {
            console.error(e)
        } finally {
            setSaving(false)
        }
    }

    return (
        <Modal open={open} title={isEdit ? 'Editar Estúdio' : 'Novo Estúdio'} onClose={onClose}>
            {loading ? (
                <p className="mc-loading">A carregar...</p>
            ) : (
                <form onSubmit={handleSubmit}>
                    <div className="mc-form-group">
                        <label className="mc-label">Nome *</label>
                        <input
                            className="mc-input"
                            required
                            value={form.name}
                            onChange={e => setForm(p => ({ ...p, name: e.target.value }))}
                            placeholder="Ex: Estúdio A"
                        />
                    </div>
                    <div className="mc-form-group">
                        <label className="mc-label">Capacidade (pessoas) *</label>
                        <input
                            className="mc-input"
                            required
                            type="number"
                            min={1}
                            value={form.capacity}
                            onChange={e => setForm(p => ({ ...p, capacity: e.target.value }))}
                            placeholder="Ex: 20"
                        />
                    </div>
                    <div className="mc-form-group">
                        <label className="mc-label">Morada</label>
                        <input
                            className="mc-input"
                            value={form.address}
                            onChange={e => setForm(p => ({ ...p, address: e.target.value }))}
                            placeholder="Opcional"
                        />
                    </div>
                    <div className="mc-form-group">
                        <label className="mc-label">Modalidades</label>
                        <div className="mc-checkbox-grid">
                            {modalities.map(m => {
                                const id = m.ModalityId ?? m.modalityId
                                const mName = m.Name ?? m.name
                                return (
                                    <label key={id} className="mc-checkbox-item">
                                        <input
                                            type="checkbox"
                                            checked={selectedModalities.includes(id)}
                                            onChange={() => toggleModality(id)}
                                        />
                                        {mName}
                                    </label>
                                )
                            })}
                            {modalities.length === 0 && (
                                <span style={{ fontSize: '0.88rem', color: '#9ca3af' }}>
                                    Sem modalidades disponíveis
                                </span>
                            )}
                        </div>
                    </div>
                    <div className="modal-actions" style={{ marginTop: '16px' }}>
                        <button type="button" className="btn btn-secondary" onClick={onClose}>
                            Cancelar
                        </button>
                        <button type="submit" className="btn btn-primary" disabled={saving}>
                            {saving ? 'A guardar...' : 'Guardar'}
                        </button>
                    </div>
                </form>
            )}
        </Modal>
    )
}

// ─── Confirm modal (activate / deactivate) ─────────────────────────────────────
function ConfirmModal({ open, message, onClose, onConfirm, loading }) {
    return (
        <Modal open={open} title="Confirmar" onClose={onClose}>
            <p className="mc-confirm-text">{message}</p>
            <div className="modal-actions">
                <button type="button" className="btn btn-secondary" onClick={onClose}>
                    Cancelar
                </button>
                <button type="button" className="btn btn-danger" onClick={onConfirm} disabled={loading}>
                    {loading ? 'A processar...' : 'Confirmar'}
                </button>
            </div>
        </Modal>
    )
}

// ─── Page ──────────────────────────────────────────────────────────────────────
function StaffStudiosPage() {
    const [studios, setStudios] = useState([])
    const [modalities, setModalities] = useState([])
    const [loading, setLoading] = useState(false)

    const [formOpen, setFormOpen] = useState(false)
    const [editId, setEditId] = useState(null)

    const [confirm, setConfirm] = useState(null) // { id, active }
    const [confirming, setConfirming] = useState(false)

    const fetchAll = useCallback(async () => {
        setLoading(true)
        try {
            const [s, m] = await Promise.all([getStudios(), getModalities()])
            const studioList = Array.isArray(s) ? s : (s?.items ?? s?.Items ?? [])
            studioList.sort((a, b) => (a.Name ?? a.name ?? '').localeCompare(b.Name ?? b.name ?? '', 'pt'))
            setStudios(studioList)
            const modalityList = (Array.isArray(m) ? m : (m?.items ?? m?.Items ?? [])).filter(
                x => x.IsActive ?? x.isActive ?? true
            )
            modalityList.sort((a, b) => (a.Name ?? a.name ?? '').localeCompare(b.Name ?? b.name ?? '', 'pt'))
            setModalities(modalityList)
        } catch (e) {
            console.error(e)
        } finally {
            setLoading(false)
        }
    }, [])

    useEffect(() => { fetchAll() }, [fetchAll])

    const openCreate = () => { setEditId(null); setFormOpen(true) }
    const openEdit = (id) => { setEditId(id); setFormOpen(true) }
    const closeForm = () => { setFormOpen(false); setEditId(null) }

    const handleSaved = () => {
        setFormOpen(false)
        fetchAll()
    }

    const handleToggleActive = (id, isCurrentlyActive) => {
        setConfirm({ id, active: isCurrentlyActive })
    }

    const submitConfirm = async () => {
        if (!confirm) return
        setConfirming(true)
        try {
            if (confirm.active) {
                await deactivateStudio(confirm.id)
            } else {
                await activateStudio(confirm.id)
            }
            setConfirm(null)
            fetchAll()
        } catch (e) {
            console.error(e)
        } finally {
            setConfirming(false)
        }
    }

    return (
        <section className="dashboard-page-card">
            <div className="mc-header">
                <div>
                    <h2>Estúdios</h2>
                    <p style={{ margin: '4px 0 0', color: '#6b7280', fontSize: '0.95rem' }}>
                        Gerir estúdios e associar modalidades.
                    </p>
                </div>
                <button type="button" className="btn btn-primary" onClick={openCreate}>
                    + Novo Estúdio
                </button>
            </div>

            {loading && <p className="mc-loading">A carregar...</p>}

            {!loading && (
                <div className="mc-grid">
                    {studios.length === 0 && (
                        <div className="mc-empty">
                            <p>Ainda não existem estúdios registados.</p>
                        </div>
                    )}
                    {studios.map(s => (
                        <StudioCard
                            key={s.StudioId ?? s.studioId}
                            studio={s}
                            onEdit={openEdit}
                            onToggleActive={handleToggleActive}
                        />
                    ))}
                </div>
            )}

            <StudioFormModal
                open={formOpen}
                studioId={editId}
                modalities={modalities}
                onClose={closeForm}
                onSaved={handleSaved}
            />

            <ConfirmModal
                open={!!confirm}
                message={
                    confirm?.active
                        ? 'Tem a certeza que pretende desativar este estúdio?'
                        : 'Tem a certeza que pretende ativar este estúdio?'
                }
                onClose={() => setConfirm(null)}
                onConfirm={submitConfirm}
                loading={confirming}
            />
        </section>
    )
}

export default StaffStudiosPage
