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
import '../../styles/ManageCards.css'

const fmt = {
    datetime: v => v
        ? new Date(v).toLocaleDateString('pt-PT', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        })
        : '—',
}

function EventCard({ event, onEdit, onToggleActive, onDelete }) {
    const {
        eventId,
        title,
        description,
        startDatetime,
        endDatetime,
        isActive,
        createdByName
    } = event

    return (
        <div className={`mc-card${isActive ? '' : ' mc-card--inactive'}`}>
            <div className="mc-card-top">
                <p className="mc-card-name">{title}</p>
                <span className={`mc-badge ${isActive ? 'mc-badge--active' : 'mc-badge--inactive'}`}>
                    {isActive ? 'Ativo' : 'Inativo'}
                </span>
            </div>

            {description && <p className="mc-card-desc">{description}</p>}

            <div className="mc-card-meta">
                <span>Início: {fmt.datetime(startDatetime)}</span>
                <span>Fim: {fmt.datetime(endDatetime)}</span>
                {createdByName && <span>Criado por: {createdByName}</span>}
            </div>

            <div className="mc-card-actions">
                <button className="btn btn-secondary" onClick={() => onEdit(eventId)}>
                    Editar
                </button>

                <button
                    className={`btn ${isActive ? 'btn-secondary' : 'btn-primary'}`}
                    onClick={() => onToggleActive(eventId, isActive)}
                >
                    {isActive ? 'Desativar' : 'Ativar'}
                </button>

                <button className="btn btn-danger" onClick={() => onDelete(eventId)}>
                    Apagar
                </button>
            </div>
        </div>
    )
}

function EventFormModal({ open, editEvent, onClose, onSaved }) {
    const isEdit = !!editEvent
    const empty = {
        title: '',
        description: '',
        startDatetime: '',
        endDatetime: ''
    }

    const [form, setForm] = useState(empty)
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState('')

    useEffect(() => {
        if (!open) return

        if (isEdit) {
            const toLocal = v => v ? new Date(v).toISOString().slice(0, 16) : ''

            setForm({
                title: editEvent.title ?? '',
                description: editEvent.description ?? '',
                startDatetime: toLocal(editEvent.startDatetime),
                endDatetime: toLocal(editEvent.endDatetime)
            })
        } else {
            setForm(empty)
        }

        setError('')
    }, [open, editEvent])

    const set = (field, value) => {
        setForm(prev => ({ ...prev, [field]: value }))
    }

    const handleSubmit = async e => {
        e.preventDefault()
        setSaving(true)
        setError('')

        try {
            const body = {
                title: form.title.trim(),
                description: form.description.trim() || null,
                startDatetime: form.startDatetime,
                endDatetime: form.endDatetime
            }

            if (isEdit) {
                await updateEvent(editEvent.eventId, body)
            } else {
                await createEvent(body)
            }

            onSaved()
        } catch (err) {
            setError(err.message)
        } finally {
            setSaving(false)
        }
    }

    return (
        <Modal open={open} title={isEdit ? 'Editar Evento' : 'Novo Evento'} onClose={onClose}>
            <form onSubmit={handleSubmit}>
                <div className="mc-form-group">
                    <label className="mc-label">Título *</label>
                    <input
                        className="mc-input"
                        required
                        maxLength={64}
                        value={form.title}
                        onChange={e => set('title', e.target.value)}
                    />
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Descrição</label>
                    <textarea
                        className="mc-textarea"
                        maxLength={256}
                        rows={3}
                        value={form.description}
                        onChange={e => set('description', e.target.value)}
                    />
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Data de Início *</label>
                    <input
                        className="mc-input"
                        type="datetime-local"
                        required
                        value={form.startDatetime}
                        onChange={e => set('startDatetime', e.target.value)}
                    />
                </div>

                <div className="mc-form-group">
                    <label className="mc-label">Data de Fim *</label>
                    <input
                        className="mc-input"
                        type="datetime-local"
                        required
                        value={form.endDatetime}
                        onChange={e => set('endDatetime', e.target.value)}
                    />
                </div>

                {error && (
                    <p style={{ color: 'var(--danger)', fontSize: '0.88rem', margin: '0 0 12px' }}>
                        {error}
                    </p>
                )}

                <div className="modal-actions">
                    <button type="button" className="btn btn-secondary" onClick={onClose}>
                        Cancelar
                    </button>

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
                <button className="btn btn-secondary" onClick={onClose}>
                    Cancelar
                </button>

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

    useEffect(() => {
        fetchAll()
    }, [fetchAll])

    const openCreate = () => {
        setEditEvent(null)
        setFormOpen(true)
    }

    const openEdit = id => {
        setEditEvent(events.find(e => e.eventId === id) ?? null)
        setFormOpen(true)
    }

    const closeForm = () => {
        setFormOpen(false)
        setEditEvent(null)
    }

    const handleSaved = () => {
        closeForm()
        fetchAll()
    }

    const handleToggleActive = (id, isActive) => {
        setConfirm({
            id,
            type: 'toggle',
            isActive
        })
    }

    const handleDelete = id => {
        setConfirm({
            id,
            type: 'delete'
        })
    }

    const handleConfirm = async () => {
        if (!confirm) return

        setConfirming(true)

        try {
            if (confirm.type === 'toggle') {
                if (confirm.isActive) {
                    await deactivateEvent(confirm.id)
                } else {
                    await activateEvent(confirm.id)
                }
            } else if (confirm.type === 'delete') {
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

                <button className="btn btn-primary" onClick={openCreate}>
                    Novo Evento
                </button>
            </div>

            {loading && <p className="mc-loading">A carregar eventos...</p>}

            {!loading && (
                <div className="mc-grid">
                    {events.length === 0 && (
                        <div className="mc-empty">
                            <p>Nenhum evento registado.</p>
                        </div>
                    )}

                    {events.map(ev => (
                        <EventCard
                            key={ev.eventId}
                            event={ev}
                            onEdit={openEdit}
                            onToggleActive={handleToggleActive}
                            onDelete={handleDelete}
                        />
                    ))}
                </div>
            )}

            <EventFormModal
                open={formOpen}
                editEvent={editEvent}
                onClose={closeForm}
                onSaved={handleSaved}
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