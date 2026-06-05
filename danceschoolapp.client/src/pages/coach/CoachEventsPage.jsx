import { useEffect, useState } from 'react'
import { getActiveEvents, getEventById, updateEventSecretDescription } from '../../services/eventsService'
import { useAuth } from '../../context/useAuth'
import Modal from '../../components/common/Modal'
import '../../styles/ManageCards.css'

const fmtDt = v => v
    ? new Date(v).toLocaleDateString('pt-PT', {
        day: '2-digit', month: 'short', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    })
    : '—'

// Turns URLs in plain text into clickable <a> elements
function Linkified({ text }) {
    if (!text) return null
    const urlRegex = /(https?:\/\/[^\s]+)/g
    const parts = text.split(urlRegex)
    return (
        <>
            {parts.map((part, i) =>
                urlRegex.test(part)
                    ? <a key={i} href={part} target="_blank" rel="noopener noreferrer">{part}</a>
                    : part
            )}
        </>
    )
}

function SecretDescriptionModal({ open, eventId, current, onClose, onSaved }) {
    const [text, setText] = useState('')
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState('')

    useEffect(() => {
        if (open) { setText(current ?? ''); setError('') }
    }, [open, current])

    const handleSubmit = async e => {
        e.preventDefault()
        setSaving(true)
        setError('')
        try {
            await updateEventSecretDescription(eventId, text.trim() || null)
            onSaved(text.trim() || null)
        } catch (err) {
            setError(err.message)
        } finally {
            setSaving(false)
        }
    }

    return (
        <Modal open={open} title="Descrição Secreta" onClose={onClose}>
            <p style={{ fontSize: '0.88rem', color: 'var(--text-2)', marginBottom: 12 }}>
                Visível apenas para pais de alunos com a modalidade deste evento. Podes incluir links para fatos ou instruções.
            </p>
            <form onSubmit={handleSubmit}>
                <div className="mc-form-group">
                    <textarea
                        className="mc-textarea"
                        rows={8}
                        placeholder="Ex: O fato está disponível em https://loja.exemplo.pt/fato-ballet ..."
                        value={text}
                        onChange={e => setText(e.target.value)}
                    />
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

function CoachEventsPage() {
    const { user } = useAuth()
    const [events, setEvents] = useState([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    // Secret description modal state
    const [secretModal, setSecretModal] = useState(null) // { eventId, current }

    useEffect(() => {
        getActiveEvents()
            .then(res => setEvents(Array.isArray(res) ? res : []))
            .catch(err => setError(err.message))
            .finally(() => setLoading(false))
    }, [])

    const isAssigned = ev => (ev.coaches ?? []).some(c => c.coachId === user?.userId)

    const handleSecretSaved = (eventId, newText) => {
        setEvents(prev => prev.map(ev =>
            ev.eventId === eventId ? { ...ev, secretDescription: newText } : ev
        ))
        setSecretModal(null)
    }

    return (
        <section className="dashboard-page-card">
            <div className="mc-header">
                <div>
                    <h2>Eventos</h2>
                    <p style={{ margin: '4px 0 0', color: 'var(--text-2)', fontSize: '0.95rem' }}>
                        Eventos da escola.
                    </p>
                </div>
            </div>

            {loading && <p className="mc-loading">A carregar...</p>}
            {error && <p style={{ color: 'var(--danger)' }}>{error}</p>}

            {!loading && !error && (
                <div className="ev-list">
                    {events.length === 0 && (
                        <div className="mc-empty"><p>Não existem eventos ativos de momento.</p></div>
                    )}
                    {events.map(ev => (
                        <div key={ev.eventId} className="ev-card">
                            <div className="ev-card-body">
                                <div className="ev-card-header">
                                    <p className="ev-card-title">{ev.title}</p>
                                    {isAssigned(ev) && (
                                        <span className="mc-badge mc-badge--active">Atribuído</span>
                                    )}
                                </div>

                                {ev.description && <p className="ev-card-desc">{ev.description}</p>}

                                <div className="ev-card-meta">
                                    <span>📅 {fmtDt(ev.startDatetime)} → {fmtDt(ev.endDatetime)}</span>
                                </div>

                                {(ev.modalities?.length > 0) && (
                                    <div className="mc-tag-row">
                                        {ev.modalities.map(m => (
                                            <span key={m.modalityId} className="mc-tag">{m.name}</span>
                                        ))}
                                    </div>
                                )}

                                {ev.secretDescription && (
                                    <div>
                                        <p className="ev-secret-label">📋 Informação interna</p>
                                        <div className="ev-secret-box">
                                            <Linkified text={ev.secretDescription} />
                                        </div>
                                    </div>
                                )}

                                {isAssigned(ev) && (
                                    <div className="ev-card-actions">
                                        <button
                                            className="btn btn-secondary"
                                            onClick={() => setSecretModal({ eventId: ev.eventId, current: ev.secretDescription })}
                                        >
                                            ✏️ Editar descrição secreta
                                        </button>
                                    </div>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {secretModal && (
                <SecretDescriptionModal
                    open={!!secretModal}
                    eventId={secretModal.eventId}
                    current={secretModal.current}
                    onClose={() => setSecretModal(null)}
                    onSaved={newText => handleSecretSaved(secretModal.eventId, newText)}
                />
            )}
        </section>
    )
}

export default CoachEventsPage
