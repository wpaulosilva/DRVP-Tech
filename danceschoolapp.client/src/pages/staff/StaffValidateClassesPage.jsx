import { useEffect, useState } from 'react'
import ClassValidationCard from '../../components/common/ClassValidationCard'
import Modal from '../../components/common/Modal'
import { getValidateClasses, staffApprove, staffReject, staffValidate } from '../../services/staffService'
import '../../styles/ValidateClasses.css'

function StaffValidateClassesPage() {
    const [activeTab, setActiveTab] = useState('requested')
    const [items, setItems] = useState([])
    const [loading, setLoading] = useState(false)
    const [stats, setStats] = useState({ requested: 0, pending: 0 })

    // Reject modal state
    const [rejectTarget, setRejectTarget] = useState(null)
    const [rejectReason, setRejectReason] = useState('')
    const [rejecting, setRejecting] = useState(false)

    const fetchData = async () => {
        setLoading(true)
        try {
            const res = await getValidateClasses({ tab: activeTab, page: 1, pageSize: 20 })
            const data = res?.Items ?? res?.items ?? res ?? []
            setItems(data)
            setStats(prev => ({
                requested: res?.RequestedCount ?? res?.requestedCount ?? (activeTab === 'requested' ? data.length : prev.requested),
                pending: res?.PendingCount ?? res?.pendingCount ?? (activeTab === 'pending' ? data.length : prev.pending),
            }))
        } catch (e) {
            console.error(e)
            setItems([])
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => {
        fetchData()
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [activeTab])

    const handleApprove = async (id) => {
        try {
            await staffApprove(id)
            fetchData()
        } catch (e) { console.error(e) }
    }

    const openRejectModal = (id) => {
        const aula = items.find(a => (a.ClassId ?? a.classId ?? a.id) === id)
        setRejectTarget(aula || { id })
        setRejectReason('')
    }

    const submitReject = async (e) => {
        e.preventDefault()
        const id = rejectTarget?.ClassId ?? rejectTarget?.classId ?? rejectTarget?.id
        if (!id) return
        setRejecting(true)
        try {
            await staffReject(id)
            setRejectTarget(null)
            setRejectReason('')
            fetchData()
        } catch (e) {
            console.error(e)
        } finally {
            setRejecting(false)
        }
    }

    const handleValidate = async (id) => {
        try {
            await staffValidate(id, true)
            fetchData()
        } catch (e) { console.error(e) }
    }

    const handleCancel = async (id) => {
        try {
            await staffValidate(id, false)
            fetchData()
        } catch (e) { console.error(e) }
    }

    const isRequested = activeTab === 'requested'

    return (
        <section className="dashboard-page-card">
            <h2>Validações de Aulas</h2>
            <p>Aprovar aulas requisitadas e validar aulas pendentes após o prazo de 48h.</p>

            {/* KPI cards */}
            <div className="validate-kpi-row" style={{ marginTop: '20px' }}>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Requisitadas</span>
                    <span className="validate-kpi-value validate-kpi-value--purple">
                        {loading ? '\u2014' : stats.requested}
                    </span>
                </div>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Pendentes</span>
                    <span className="validate-kpi-value validate-kpi-value--amber">
                        {loading ? '\u2014' : stats.pending}
                    </span>
                </div>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Resolvidas esta semana</span>
                    <span className="validate-kpi-value validate-kpi-value--green">
                        {loading ? '\u2014' : '\u2014'}
                    </span>
                </div>
            </div>

            {/* Tabs */}
            <div className="validate-tabs">
                <button
                    type="button"
                    className={`validate-tab ${activeTab === 'requested' ? 'validate-tab--active' : ''}`}
                    onClick={() => setActiveTab('requested')}
                >
                    Requisitadas
                </button>
                <button
                    type="button"
                    className={`validate-tab ${activeTab === 'pending' ? 'validate-tab--active' : ''}`}
                    onClick={() => setActiveTab('pending')}
                >
                    Pendentes
                </button>
            </div>

            {/* Content */}
            {loading && (
                <div className="validate-empty">
                    <p>Carregando...</p>
                </div>
            )}

            {!loading && items.length === 0 && (
                <div className="validate-empty">
                    <div className="validate-empty-icon">{'\u2713'}</div>
                    <h3>Tudo em ordem!</h3>
                    <p>
                        {isRequested
                            ? 'Não há pedidos de aulas aguardando aprovação.'
                            : 'Não há aulas pendentes de validação.'}
                    </p>
                </div>
            )}

            {!loading && items.map(aula => (
                <ClassValidationCard
                    key={aula.ClassId ?? aula.classId ?? aula.id}
                    aula={aula}
                    tipo={isRequested ? 'coach-request' : 'staff'}
                    variant={isRequested ? 'purple' : 'amber'}
                    showParticipants
                    showCoachValidation={!isRequested}
                    showParentTally={!isRequested}
                    onConfirm={(id) => isRequested ? handleApprove(id) : handleValidate(id)}
                    onReject={(id) => isRequested ? openRejectModal(id) : handleCancel(id)}
                    confirmLabel={isRequested ? 'Aceitar Aula' : 'Validar'}
                    rejectLabel={isRequested ? 'Recusar Aula' : 'Cancelar'}
                />
            ))}

            {/* Reject Modal */}
            <Modal
                open={!!rejectTarget}
                title="Recusar Aula"
                onClose={() => { setRejectTarget(null); setRejectReason('') }}
            >
                <form onSubmit={submitReject}>
                    <p>
                        Indique o motivo para recusar a aula de{' '}
                        <strong>
                            {rejectTarget?.ModalityName ?? rejectTarget?.modalityName ?? rejectTarget?.Modality ?? rejectTarget?.modality ?? 'esta aula'}
                        </strong>.
                    </p>

                    {rejectTarget && (
                        <div className="reject-class-summary">
                            <div>Estúdio: {rejectTarget?.StudioName ?? rejectTarget?.studioName ?? rejectTarget?.Studio ?? rejectTarget?.studio ?? '\u2014'}</div>
                            <div>Data: {rejectTarget?.StartDatetime ?? rejectTarget?.startDatetime ? new Date(rejectTarget.StartDatetime ?? rejectTarget.startDatetime).toLocaleString('pt-PT') : '\u2014'}</div>
                        </div>
                    )}

                    <label style={{ display: 'block', marginBottom: '8px', fontSize: '0.92rem', fontWeight: 600, color: '#475569' }}>
                        Motivo *
                    </label>
                    <textarea
                        required
                        rows={4}
                        value={rejectReason}
                        onChange={(e) => setRejectReason(e.target.value)}
                        placeholder="Indique o motivo da recusa..."
                        className="reject-textarea"
                    />

                    <div className="modal-actions" style={{ marginTop: '16px' }}>
                        <button
                            type="button"
                            className="btn btn-secondary"
                            onClick={() => { setRejectTarget(null); setRejectReason('') }}
                        >
                            Cancelar
                        </button>
                        <button
                            type="submit"
                            className="btn btn-danger"
                            disabled={rejecting}
                        >
                            {rejecting ? 'A recusar...' : 'Recusar Aula'}
                        </button>
                    </div>
                </form>
            </Modal>
        </section>
    )
}

export default StaffValidateClassesPage
