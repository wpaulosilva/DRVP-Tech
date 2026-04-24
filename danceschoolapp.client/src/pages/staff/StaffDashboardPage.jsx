import { useEffect, useState } from 'react'
import PageCard from '../../components/common/PageCard'
import KpiCard from '../../components/common/KpiCard'
import { getStaffDashboardStats } from '../../services/dashboardService'
import '../../styles/AdminPage.css'

function StaffDashboardPage() {
    const [stats, setStats] = useState(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    useEffect(() => {
        const fetch = async () => {
            try {
                const data = await getStaffDashboardStats()
                setStats(data)
            } catch (err) {
                setError(err.message)
            } finally {
                setLoading(false)
            }
        }
        fetch()
    }, [])

    return (
        <PageCard>
            <h2>Painel de Direção</h2>
            <p>Gestão completa da escola Ent&apos;Artes.</p>

            {error && <p className="admin-error">{error}</p>}

            <div className="kpi-grid">
                <KpiCard
                    label="Encarregados"
                    value={stats?.totalParents}
                    loading={loading}
                />
                <KpiCard
                    label="Professores"
                    value={stats?.totalCoaches}
                    loading={loading}
                />
                <KpiCard
                    label="Estudantes Ativos"
                    value={stats?.totalActiveStudents}
                    loading={loading}
                />
                <KpiCard
                    label="Eventos Ativos"
                    value={stats?.totalActiveEvents}
                    loading={loading}
                />
            </div>

            <div className="kpi-grid">
                <KpiCard
                    label="Aulas Marcadas (Mês)"
                    value={stats?.classesScheduledThisMonth}
                    loading={loading}
                />
                <KpiCard
                    label="Aulas Realizadas"
                    value={stats?.classesCompletedThisMonth}
                    loading={loading}
                />
                <KpiCard
                    label="Pendentes Validação"
                    value={stats?.classesPendingValidation}
                    loading={loading}
                />
            </div>

            {stats?.upcomingEvents?.length > 0 && (
                <div className="events-strip">
                    <h3 className="events-strip-title">Próximos Eventos</h3>
                    <div className="events-strip-grid">
                        {stats.upcomingEvents.map((event) => (
                            <div key={event.eventId} className="event-card">
                                <h4>{event.title}</h4>
                                {event.startDatetime && (
                                    <span className="event-date">
                                        {new Date(event.startDatetime).toLocaleDateString('pt-PT', {
                                            day: '2-digit',
                                            month: 'short',
                                            year: 'numeric',
                                        })}
                                        {' '}
                                        {new Date(event.startDatetime).toLocaleTimeString('pt-PT', {
                                            hour: '2-digit',
                                            minute: '2-digit',
                                        })}
                                    </span>
                                )}
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </PageCard>
    )
}

export default StaffDashboardPage
