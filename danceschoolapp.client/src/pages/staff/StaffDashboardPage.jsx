import { useEffect, useState } from 'react'
import PageCard from '../../components/common/PageCard'
import KpiCard from '../../components/common/KpiCard'
import { getStaffDashboardStats } from '../../services/dashboardService'
import '../../styles/AdminPage.css'
import '../../styles/DashboardCards.css'

function StaffDashboardPage() {
    const [stats, setStats] = useState(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    useEffect(() => {
        const fetchDashboard = async () => {
            try {
                const data = await getStaffDashboardStats()
                setStats(data)
            } catch (err) {
                setError(err.message)
            } finally {
                setLoading(false)
            }
        }

        fetchDashboard()
    }, [])

    const formatDate = (value) =>
        value
            ? new Date(value).toLocaleDateString('pt-PT', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
            })
            : '—'

    const formatTime = (value) =>
        value
            ? new Date(value).toLocaleTimeString('pt-PT', {
                hour: '2-digit',
                minute: '2-digit',
            })
            : '—'

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
                    label="Aulas Marcadas Este Mês"
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
                    tone="orange"
                />
            </div>

            <h3 className="dashboard-section-title">Próximos Eventos</h3>

            {loading ? (
                <p>A carregar...</p>
            ) : !stats?.upcomingEvents?.length ? (
                <p className="table-empty">Sem próximos eventos.</p>
            ) : (
                <div className="dashboard-cards-grid">
                    {stats.upcomingEvents.map((event) => (
                        <div key={event.eventId} className="dashboard-info-card">
                            <h4>{event.title}</h4>

                            <div className="dashboard-card-footer">
                                <span>{formatDate(event.startDatetime)}</span>
                                <span>{formatTime(event.startDatetime)}</span>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </PageCard>
    )
}

export default StaffDashboardPage