import { useEffect, useState } from 'react'
import { getActiveEvents } from '../../services/eventsService'
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

function ParentEventsPage() {
    const [events, setEvents] = useState([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    useEffect(() => {
        getActiveEvents()
            .then(res => setEvents(Array.isArray(res) ? res : []))
            .catch(err => setError(err.message))
            .finally(() => setLoading(false))
    }, [])

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
                                <p className="ev-card-title">{ev.title}</p>

                                {ev.description && <p className="ev-card-desc">{ev.description}</p>}

                                <div className="ev-card-meta">
                                    <span>📅 {fmtDt(ev.startDatetime)} → {fmtDt(ev.endDatetime)}</span>
                                </div>

                                {ev.modalities?.length > 0 && (
                                    <div className="mc-tag-row">
                                        {ev.modalities.map(m => (
                                            <span key={m.modalityId} className="mc-tag">{m.name}</span>
                                        ))}
                                    </div>
                                )}

                                {ev.secretDescription && (
                                    <div>
                                        <p className="ev-secret-label">📋 Informação para participantes</p>
                                        <div className="ev-secret-box">
                                            <Linkified text={ev.secretDescription} />
                                        </div>
                                    </div>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </section>
    )
}

export default ParentEventsPage
