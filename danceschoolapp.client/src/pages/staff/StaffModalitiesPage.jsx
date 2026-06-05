import { useEffect, useState, useCallback } from 'react'
import Modal from '../../components/common/Modal'
import { getModalities, createModality, updateModality, deactivateModality, activateModality, assignCoach, removeCoach } from '../../services/modalitiesService'
import { getCoachesAvailable } from '../../services/coachService'
import '../../styles/ManageCards.css'

// ─── Modality card ─────────────────────────────────────────────────────────────
function ModalityCard({ modality, coaches, onEdit, onToggleActive }) {
    const active = modality.IsActive ?? modality.isActive ?? true
    const name = modality.Name ?? modality.name ?? '—'
    const desc = modality.Description ?? modality.description ?? null
    const id = modality.ModalityId ?? modality.modalityId

    return (
        <div className={`mc-card${active ? '' : ' mc-card--inactive'}`}>
            <div>
                <div className="mc-card-top">
                    <p className="mc-card-name">{name}</p>
                    <span className={`mc-badge ${active ? 'mc-badge--active' : 'mc-badge--inactive'}`}>
                        {active ? 'Ativa' : 'Inativa'}
                    </span>
                </div>

                {desc && <p className="mc-card-desc">{desc}</p>}
            </div>

            <div className="mc-card-bottom">
                <div className="mc-tag-row">
                    <span className="mc-tag mc-tag--coach">
                        Professores: {coaches.length}
                    </span>
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
        </div>
    )
}

// ─── Modality form modal (create + edit) ───────────────────────────────────────
function ModalityFormModal({ open, modalityId, editModality, coachMap, allCoaches, onClose, onSaved }) {
    const isEdit = !!modalityId
    const [form, setForm] = useState({ name: '', description: '' })
    const [assignedCoaches, setAssignedCoaches] = useState([])
    const [addCoachId, setAddCoachId] = useState('')
    const [saving, setSaving] = useState(false)
    const [assignLoading, setAssignLoading] = useState(false)

    useEffect(() => {
        if (!open) return
        if (isEdit && editModality) {
            setForm({
                name: editModality.Name ?? editModality.name ?? '',
                description: editModality.Description ?? editModality.description ?? '',
            })
            setAssignedCoaches(coachMap[modalityId] ?? [])
        } else {
            setForm({ name: '', description: '' })
            setAssignedCoaches([])
        }
        setAddCoachId('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [open, modalityId])

    const handleAddCoach = async () => {
        const id = Number(addCoachId)
        if (!id || assignedCoaches.some(c => c.CoachId === id)) return
        setAssignLoading(true)
        try {
            await assignCoach(modalityId, id)
            const coach = allCoaches.find(c => (c.CoachId ?? c.coachId) === id)
            if (coach) {
                setAssignedCoaches(prev => [
                    ...prev,
                    { CoachId: coach.CoachId ?? coach.coachId, Name: coach.Name ?? coach.name },
                ])
            }
            setAddCoachId('')
        } catch (e) {
            console.error(e)
        } finally {
            setAssignLoading(false)
        }
    }

    const handleRemoveCoach = async (coachId) => {
        setAssignLoading(true)
        try {
            await removeCoach(modalityId, coachId)
            setAssignedCoaches(prev => prev.filter(c => c.CoachId !== coachId))
        } catch (e) {
            console.error(e)
        } finally {
            setAssignLoading(false)
        }
    }

    const handleSubmit = async (e) => {
        e.preventDefault()
        setSaving(true)
        try {
            const body = {
                name: form.name.trim(),
                ...(form.description.trim() ? { description: form.description.trim() } : {}),
            }
            if (isEdit) {
                await updateModality(modalityId, body)
            } else {
                await createModality(body)
            }
            onSaved()
        } catch (e) {
            console.error(e)
        } finally {
            setSaving(false)
        }
    }

    const availableToAdd = allCoaches.filter(
        c => !assignedCoaches.some(a => a.CoachId === (c.CoachId ?? c.coachId))
    )

    return (
        <Modal open={open} title={isEdit ? 'Editar Modalidade' : 'Nova Modalidade'} onClose={onClose}>
            <form onSubmit={handleSubmit}>
                <div className="mc-form-group">
                    <label className="mc-label">Nome *</label>
                    <input
                        className="mc-input"
                        required
                        value={form.name}
                        onChange={e => setForm(p => ({ ...p, name: e.target.value }))}
                        placeholder="Ex: Ballet"
                    />
                </div>
                <div className="mc-form-group">
                    <label className="mc-label">Descrição</label>
                    <textarea
                        className="mc-textarea"
                        value={form.description}
                        onChange={e => setForm(p => ({ ...p, description: e.target.value }))}
                        placeholder="Opcional"
                        rows={3}
                    />
                </div>

                {isEdit && (
                    <div className="mc-form-group">
                        <label className="mc-label">Professores</label>
                        {assignedCoaches.length > 0 && (
                            <div className="mc-tag-row" style={{ marginBottom: '8px' }}>
                                {assignedCoaches.map(c => (
                                    <span key={c.CoachId} className="mc-tag mc-tag--coach">
                                        {c.Name}
                                        <button
                                            type="button"
                                            className="mc-tag--remove"
                                            onClick={() => handleRemoveCoach(c.CoachId)}
                                            disabled={assignLoading}
                                            title="Remover"
                                        >
                                            ✕
                                        </button>
                                    </span>
                                ))}
                            </div>
                        )}
                        <div className="mc-coach-assign">
                            <select
                                className="mc-select"
                                value={addCoachId}
                                onChange={e => setAddCoachId(e.target.value)}
                            >
                                <option value="">Adicionar professor...</option>
                                {availableToAdd.map(c => {
                                    const cId = c.CoachId ?? c.coachId
                                    return (
                                        <option key={cId} value={cId}>
                                            {c.Name ?? c.name}
                                        </option>
                                    )
                                })}
                            </select>
                            <button
                                type="button"
                                className="btn btn-secondary"
                                onClick={handleAddCoach}
                                disabled={!addCoachId || assignLoading}
                            >
                                Adicionar
                            </button>
                        </div>
                    </div>
                )}

                <div className="modal-actions" style={{ marginTop: '16px' }}>
                    <button type="button" className="btn btn-secondary" onClick={onClose}>
                        Cancelar
                    </button>
                    <button type="submit" className="btn btn-primary" disabled={saving || assignLoading}>
                        {saving ? 'A guardar...' : 'Guardar'}
                    </button>
                </div>
            </form>
        </Modal>
    )
}

// ─── Confirm modal ─────────────────────────────────────────────────────────────
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
function StaffModalitiesPage() {
    const [modalities, setModalities] = useState([])
    const [allCoaches, setAllCoaches] = useState([])
    const [coachMap, setCoachMap] = useState({}) // modalityId -> [{CoachId, Name}]
    const [loading, setLoading] = useState(false)

    const [formOpen, setFormOpen] = useState(false)
    const [editModality, setEditModality] = useState(null)

    const [confirm, setConfirm] = useState(null) // { id, active }
    const [confirming, setConfirming] = useState(false)

    const fetchAll = useCallback(async () => {
        setLoading(true)
        try {
            const [mRes, cRes] = await Promise.all([getModalities(), getCoachesAvailable()])
            const mList = Array.isArray(mRes) ? mRes : (mRes?.items ?? mRes?.Items ?? [])
            const cList = Array.isArray(cRes) ? cRes : (cRes?.items ?? cRes?.Items ?? [])

            mList.sort((a, b) => (a.Name ?? a.name ?? '').localeCompare(b.Name ?? b.name ?? '', 'pt'))
            cList.sort((a, b) => (a.Name ?? a.name ?? '').localeCompare(b.Name ?? b.name ?? '', 'pt'))
            setModalities(mList)
            setAllCoaches(cList)

            // Build reverse map: modalityId -> [{CoachId, Name}]
            const map = {}
            cList.forEach(coach => {
                const coachModalities = coach.Modalities ?? coach.modalities ?? []
                coachModalities.forEach(m => {
                    const mId = m.ModalityId ?? m.modalityId
                    if (!map[mId]) map[mId] = []
                    map[mId].push({
                        CoachId: coach.CoachId ?? coach.coachId,
                        Name: coach.Name ?? coach.name,
                    })
                })
            })
            setCoachMap(map)
        } catch (e) {
            console.error(e)
        } finally {
            setLoading(false)
        }
    }, [])

    useEffect(() => { fetchAll() }, [fetchAll])

    const openCreate = () => { setEditModality(null); setFormOpen(true) }

    const openEdit = (id) => {
        const m = modalities.find(x => (x.ModalityId ?? x.modalityId) === id)
        setEditModality(m ?? null)
        setFormOpen(true)
    }

    const closeForm = () => { setFormOpen(false); setEditModality(null) }

    const handleSaved = () => {
        setFormOpen(false)
        setEditModality(null)
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
                await deactivateModality(confirm.id)
            } else {
                await activateModality(confirm.id)
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
                    <h2>Modalidades</h2>
                    <p style={{ margin: '4px 0 0', color: '#6b7280', fontSize: '0.95rem' }}>
                        Gerir modalidades de dança e professores associados.
                    </p>
                </div>
                <button type="button" className="btn btn-primary" onClick={openCreate}>
                    + Nova Modalidade
                </button>
            </div>

            {loading && <p className="mc-loading">A carregar...</p>}

            {!loading && (
                <div className="mc-grid">
                    {modalities.length === 0 && (
                        <div className="mc-empty">
                            <p>Ainda não existem modalidades registadas.</p>
                        </div>
                    )}
                    {modalities.map(m => {
                        const id = m.ModalityId ?? m.modalityId
                        return (
                            <ModalityCard
                                key={id}
                                modality={m}
                                coaches={coachMap[id] ?? []}
                                onEdit={openEdit}
                                onToggleActive={handleToggleActive}
                            />
                        )
                    })}
                </div>
            )}

            <ModalityFormModal
                open={formOpen}
                modalityId={editModality ? (editModality.ModalityId ?? editModality.modalityId) : null}
                editModality={editModality}
                coachMap={coachMap}
                allCoaches={allCoaches}
                onClose={closeForm}
                onSaved={handleSaved}
            />

            <ConfirmModal
                open={!!confirm}
                message={
                    confirm?.active
                        ? 'Tem a certeza que pretende desativar esta modalidade?'
                        : 'Tem a certeza que pretende ativar esta modalidade?'
                }
                onClose={() => setConfirm(null)}
                onConfirm={submitConfirm}
                loading={confirming}
            />
        </section>
    )
}

export default StaffModalitiesPage
