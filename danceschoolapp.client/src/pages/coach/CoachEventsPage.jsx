import { useEffect, useState } from 'react'
import { getActiveEvents } from '../../services/eventsService'
import '../../styles/ManageCards.css'

const fmt = {
    datetime: v => v
        ? new Date(v).toLocaleDateString('pt-PT', {
            day: '2-digit', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        })
        : '—',
}

function CoachEventsPage() {
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
                <div className="mc-grid">
                    {events.length === 0 && (
                        <div className="mc-empty"><p>Não existem eventos ativos de momento.</p></div>
                    )}
                    {events.map(ev => (
                        <div key={ev.eventId} className="mc-card">
                            <div className="mc-card-top">
                                <p className="mc-card-name">{ev.title}</p>
                            </div>
                            {ev.description && <p className="mc-card-desc">{ev.description}</p>}
                            <div className="mc-card-meta">
                                <span>Início: {fmt.datetime(ev.startDatetime)}</span>
                                <span>Fim: {fmt.datetime(ev.endDatetime)}</span>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </section>
    )
}

export default CoachEventsPage
