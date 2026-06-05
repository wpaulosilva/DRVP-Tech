import { useEffect, useState, useCallback } from 'react'
import Modal from '../../components/common/Modal'
import {
    getAllEvents,
    createEvent,
    updateEvent,
    activateEvent,
    deactivateEvent,
    deleteEvent
} from '../../services/eventsService'
import { getModalities } from '../../services/modalitiesService'
import { getCoaches } from '../../services/coachService'
import '../../styles/ManageCards.css'

const fmtDt = v => v
    ? new Date(v).toLocaleDateString('pt-PT', {
        day: '2-digit', month: 'short', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    })
    : '—'

const toLocal = v => v ? new Date(v).toISOString().slice(0, 16) : ''

function EventCard({ event, onEdit, onToggleActive, onDelete }) {
    const { eventId, title, description, startDatetime, endDatetime, isActive, createdByName, modalities = [], coaches = [] } = event

    return (
        <div className={`ev-card${isActive ? '' : ' ev-card--inactive'}`}>
            <div className="ev-card-body">
                <div className="ev-card-header">
                    <p className="ev-card-title">{title}</p>
                    <span className={`mc-badge ${isActive ? 'mc-badge--active' : 'mc-badge--inactive'}`}>
                        {isActive ? 'Ativo' : 'Inativo'}
                    </span>
                </div>

                {description && <p className="ev-card-desc">{description}</p>}

                <div className="ev-card-meta">
                    <span>📅 {fmtDt(startDatetime)} → {fmtDt(endDatetime)}</span>
                    {createdByName && <span>👤 {createdByName}</span>}
                </div>

                {(modalities.length > 0 || coaches.length > 0) && (
                    <div className="mc-tag-row">
                        {modalities.map(m => (
                            <span key={m.modalityId} className="mc-tag">{m.name}</span>
                        ))}
                        {coaches.map(c => (
                            <span key={c.coachId} className="mc-tag mc-tag--coach">🎓 {c.name}</span>
                        ))}
                    </div>
                )}

                <div className="ev-card-actions">
                    <button className="btn btn-secondary" onClick={() => onEdit(eventId)}>Editar</button>
                    <button
                        className={`btn ${isActive ? 'btn-secondary' : 'btn-primary'}`}
                        onClick={() => onToggleActive(eventId, isActive)}
                    >
                        {isActive ? 'Desativar' : 'Ativar'}
                    </button>
                    <button className="btn btn-danger" onClick={() => onDelete(eventId)}>Apagar</button>
                </div>
            </div>
        </div>
    )
}

function EventFormModal({ open, editEvent, onClose, onSaved }) {
    const isEdit = !!editEvent
    const emptyForm = { title: '', description: '', startDatetime: '', endDatetime: '' }

    const [form, setForm] = useState(emptyForm)
    const [selectedModalityIds, setSelectedModalityIds] = useState([])
    const [selectedCoaches, setSelectedCoaches] = useState([])
    const [coachPickId, setCoachPickId] = useState('')

    const [allModalities, setAllModalities] = useState([])
    const [allCoaches, setAllCoaches] = useState([])
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState('')

    useEffect(() => {
        if (!open) return

        getModalities().then(r => setAllModalities(Array.isArray(r) ? r : [])).catch(() => {})
        getCoaches().then(r => setAllCoaches(Array.isArray(r) ? r : [])).catch(() => {})

        if (isEdit) {
            setForm({
                title: editEvent.title ?? '',
                description: editEvent.description ?? '',
                startDatetime: toLocal(editEvent.startDatetime),
                endDatetime: toLocal(editEvent.endDatetime),
            })
            setSelectedModalityIds((editEvent.modalities ?? []).map(m => m.modalityId))
            setSelectedCoaches(editEvent.coaches ?? [])
        } else {
            setForm(emptyForm)
            setSelectedModalityIds([])
            setSelectedCoaches([])
        }

        setCoachPickId('')
        setError('')
    }, [open, editEvent])

    const set = (field, value) => setForm(prev => ({ ...prev, [field]: value }))

    const toggleModality = id => {
        setSelectedModalityIds(prev =>
            prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
        )
    }

    const addCoach = () => {
        if (!coachPickId) return
        const id = Number(coachPickId)
        if (selectedCoaches.some(c => c.coachId === id)) return
        const coach = allCoaches.find(c => c.coachId === id)
        if (coach) setSelectedCoaches(prev => [...prev, { coachId: coach.coachId, name: coach.name }])
        setCoachPickId('')
    }

    const removeCoach = id => setSelectedCoaches(prev => prev.filter(c => c.coachId !== id))

    const handleSubmit = async e => {
        e.preventDefault()
        if (selectedModalityIds.length === 0) { setError('Seleciona pelo menos uma modalidade.'); return }

        setSaving(true)
        setError('')
        try {
            const body = {
                title: form.title.trim(),
                description: form.description.trim() || null,
                startDatetime: form.startDatetime,
                endDatetime: form.endDatetime,
                modalityIds: selectedModalityIds,
                coachIds: selectedCoaches.map(c => c.coachId)
            }
            if (isEdit) await updateEvent(editEvent.eventId, body)
            else await createEvent(body)
            onSaved()
        } catch (err) {
            setError(err.message)
        } finally {
            setSaving(false)
        }
    }

    const availableCoaches = allCoaches.filter(c => !selectedCoaches.some(s => s.coachId === c.coachId))

    return (
        <Modal open={open} title={isEdit ? 'Editar Evento' : 'Novo Evento'} onClose={onClose}>
            <form onSubmit={handleSubmit}>
                <div className="mc-form-group">
                    <label className="mc-label">Título *</label>
                    <input className="mc-input" required maxLength={64} value={form.title}
                        onChange={e => set('title', e.target.value)} />
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Descrição</label>
                    <textarea className="mc-textarea" maxLength={256} rows={3} value={form.description}
                        onChange={e => set('description', e.target.value)} />
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Data de Início *</label>
                    <input className="mc-input" type="datetime-local" required value={form.startDatetime}
                        onChange={e => set('startDatetime', e.target.value)} />
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Data de Fim *</label>
                    <input className="mc-input" type="datetime-local" required value={form.endDatetime}
                        onChange={e => set('endDatetime', e.target.value)} />
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Modalidades *</label>
                    {allModalities.length === 0
                        ? <p style={{ fontSize: '0.85rem', color: 'var(--text-3)' }}>A carregar...</p>
                        : <div className="mc-checkbox-grid">
                            {allModalities.filter(m => m.isActive).map(m => (
                                <label key={m.modalityId} className="mc-checkbox-item">
                                    <input type="checkbox" checked={selectedModalityIds.includes(m.modalityId)}
                                        onChange={() => toggleModality(m.modalityId)} />
                                    {m.name}
                                </label>
                            ))}
                        </div>
                    }
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Professores</label>
                    {selectedCoaches.length > 0 && (
                        <div className="mc-tag-row" style={{ marginBottom: 8 }}>
                            {selectedCoaches.map(c => (
                                <span key={c.coachId} className="mc-tag mc-tag--coach">
                                    {c.name}
                                    <button type="button" className="mc-tag--remove"
                                        onClick={() => removeCoach(c.coachId)}>✕</button>
                                </span>
                            ))}
                        </div>
                    )}
                    <div className="mc-coach-assign">
                        <select className="mc-select" value={coachPickId}
                            onChange={e => setCoachPickId(e.target.value)}>
                            <option value="">— selecionar professor —</option>
                            {availableCoaches.map(c => (
                                <option key={c.coachId} value={c.coachId}>{c.name}</option>
                            ))}
                        </select>
                        <button type="button" className="btn btn-secondary" onClick={addCoach}>
                            Adicionar
                        </button>
                    </div>
                </div>

                {error && <p style={{ color: 'var(--danger)', fontSize: '0.88rem', margin: '0 0 12px' }}>{error}</p>}

                <div className="modal-actions">
                    <button type="button" className="btn btn-secondary" onClick={onClose}>Cancelar</button>
                    <button type="submit" className="btn btn-primary" disabled={saving}>
                        {saving ? 'A guardar...' : 'Guardar'}
                    </button>
                </div>
            </form>
        </Modal>
    )
}

function ConfirmModal({ open, message, onClose, onConfirm, loading }) {
    return (
        <Modal open={open} title="Confirmar" onClose={onClose}>
            <p className="mc-confirm-text">{message}</p>
            <div className="modal-actions">
                <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
                <button className="btn btn-danger" onClick={onConfirm} disabled={loading}>
                    {loading ? 'A processar...' : 'Confirmar'}
                </button>
            </div>
        </Modal>
    )
}

function StaffEventsPage() {
    const [events, setEvents] = useState([])
    const [loading, setLoading] = useState(false)
    const [formOpen, setFormOpen] = useState(false)
    const [editEvent, setEditEvent] = useState(null)
    const [confirm, setConfirm] = useState(null)
    const [confirming, setConfirming] = useState(false)

    const fetchAll = useCallback(async () => {
        setLoading(true)
        try {
            const res = await getAllEvents()
            setEvents(Array.isArray(res) ? res : [])
        } catch (e) {
            console.error(e)
        } finally {
            setLoading(false)
        }
    }, [])

    useEffect(() => { fetchAll() }, [fetchAll])

    const openEdit = id => {
        setEditEvent(events.find(e => e.eventId === id) ?? null)
        setFormOpen(true)
    }

    const handleConfirm = async () => {
        if (!confirm) return
        setConfirming(true)
        try {
            if (confirm.type === 'toggle') {
                confirm.isActive ? await deactivateEvent(confirm.id) : await activateEvent(confirm.id)
            } else {
                await deleteEvent(confirm.id)
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
                    <h2>Gestão de Eventos</h2>
                    <p style={{ margin: '4px 0 0', color: 'var(--text-2)', fontSize: '0.95rem' }}>
                        Administrar e agendar eventos escolares.
                    </p>
                </div>
                <button className="btn btn-primary" onClick={() => { setEditEvent(null); setFormOpen(true) }}>
                    Novo Evento
                </button>
            </div>

            {loading && <p className="mc-loading">A carregar eventos...</p>}

            {!loading && (
                <div className="ev-list">
                    {events.length === 0 && (
                        <div className="mc-empty"><p>Nenhum evento registado.</p></div>
                    )}
                    {events.map(ev => (
                        <EventCard
                            key={ev.eventId}
                            event={ev}
                            onEdit={openEdit}
                            onToggleActive={(id, isActive) => setConfirm({ id, type: 'toggle', isActive })}
                            onDelete={id => setConfirm({ id, type: 'delete' })}
                        />
                    ))}
                </div>
            )}

            <EventFormModal
                open={formOpen}
                editEvent={editEvent}
                onClose={() => { setFormOpen(false); setEditEvent(null) }}
                onSaved={() => { setFormOpen(false); setEditEvent(null); fetchAll() }}
            />

            <ConfirmModal
                open={!!confirm}
                loading={confirming}
                message={
                    confirm?.type === 'delete'
                        ? 'Tem a certeza de que deseja eliminar permanentemente este evento?'
                        : `Tem a certeza de que deseja ${confirm?.isActive ? 'desativar' : 'ativar'} este evento?`
                }
                onClose={() => setConfirm(null)}
                onConfirm={handleConfirm}
            />
        </section>
    )
}

export default StaffEventsPage