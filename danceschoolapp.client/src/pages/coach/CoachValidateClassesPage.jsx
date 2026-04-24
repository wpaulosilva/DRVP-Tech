import { useEffect, useState } from 'react'
import ClassValidationCard from '../../components/common/ClassValidationCard'
import Modal from '../../components/common/Modal'
import { getCoachValidate, coachAccept, coachReject, coachValidate } from '../../services/coachClassesService'
import '../../styles/ValidateClasses.css'

function CoachValidateClassesPage() {
    const [activeTab, setActiveTab] = useState('requests')
    const [aulas, setAulas] = useState([])
    const [loading, setLoading] = useState(false)
    const [stats, setStats] = useState({ requests: 0, validations: 0 })

    // Reject modal state
    const [rejectTarget, setRejectTarget] = useState(null)
    const [rejectReason, setRejectReason] = useState('')
    const [rejecting, setRejecting] = useState(false)

    const fetchAulas = async (tab) => {
        setLoading(true)
        try {
            const res = await getCoachValidate({ tab, page: 1, pageSize: 20 })
            const data = res?.Items ?? res?.items ?? res ?? []
            setAulas(data)
            setStats(prev => ({
                ...prev,
                requests: res?.RequestsCount ?? res?.requestsCount ?? (tab === 'requests' ? data.length : prev.requests),
                validations: res?.ValidationsCount ?? res?.validationsCount ?? (tab === 'validations' ? data.length : prev.validations),
            }))
        } catch (e) {
            console.error(e)
            setAulas([])
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => {
        const prefetchValidationsCount = async () => {
            try {
                const other = await getCoachValidate({ tab: 'validations', page: 1, pageSize: 1 })
                const otherData = other?.Items ?? other?.items ?? other ?? []
                setStats(prev => ({
                    ...prev,
                    validations: other?.ValidationsCount ?? other?.validationsCount ?? other?.totalCount ?? otherData.length,
                }))
            } catch {
                // ignore
            }
        }
        prefetchValidationsCount()
    }, [])

    useEffect(() => {
        fetchAulas(activeTab)
    }, [activeTab])

    const handleAccept = async (id) => {
        try {
            await coachAccept(id)
            fetchAulas(activeTab)
        } catch (e) {
            console.error(e)
        }
    }

    const openRejectModal = (id) => {
        const aula = aulas.find(a => (a.ClassId ?? a.classId ?? a.id) === id)
        setRejectTarget(aula || { id })
        setRejectReason('')
    }

    const submitReject = async (e) => {
        e.preventDefault()
        const id = rejectTarget?.ClassId ?? rejectTarget?.classId ?? rejectTarget?.id
        if (!id) return

        setRejecting(true)
        try {
            await coachReject(id, rejectReason || undefined)
            setRejectTarget(null)
            setRejectReason('')
            fetchAulas(activeTab)
        } catch (e) {
            console.error(e)
        } finally {
            setRejecting(false)
        }
    }

    const handleValidar = async (id, didTeach) => {
        try {
            await coachValidate(id, didTeach)
            fetchAulas(activeTab)
        } catch (e) {
            console.error(e)
        }
    }

    const isRequests = activeTab === 'requests'

    return (
        <section className="dashboard-page-card">
            <h2>Validar Aulas</h2>
            <p>Aceite pedidos de aula aprovados pela direção e valide aulas pendentes após o prazo de 48h.</p>

            <div className="validate-kpi-row" style={{ marginTop: '20px' }}>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Pedidos por aceitar</span>
                    <span className="validate-kpi-value validate-kpi-value--purple">
                        {loading ? '\u2014' : stats.requests}
                    </span>
                </div>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Validações pendentes</span>
                    <span className="validate-kpi-value validate-kpi-value--orange">
                        {loading ? '\u2014' : stats.validations}
                    </span>
                </div>
            </div>

            <div className="validate-tabs">
                <button
                    type="button"
                    className={`validate-tab ${activeTab === 'requests' ? 'validate-tab--active' : ''}`}
                    onClick={() => setActiveTab('requests')}
                >
                    Pedidos de Aula ({stats.requests})
                </button>
                <button
                    type="button"
                    className={`validate-tab ${activeTab === 'validations' ? 'validate-tab--active-orange' : ''}`}
                    onClick={() => setActiveTab('validations')}
                >
                    Validações ({stats.validations})
                </button>
            </div>

            {loading && (
                <div className="validate-empty">
                    <p>Carregando...</p>
                </div>
            )}

            {!loading && aulas.length === 0 && (
                <div className="validate-empty">
                    <div className="validate-empty-icon">{'\u2713'}</div>
                    <h3>{isRequests ? 'Sem pedidos pendentes' : 'Sem validações pendentes'}</h3>
                    <p>
                        {isRequests
                            ? 'Não há aulas aprovadas pela direção a aguardar a sua resposta.'
                            : 'Não há aulas em estado pendente para validar.'}
                    </p>
                </div>
            )}

            {!loading && aulas.map(aula => {
                const classId = aula.ClassId ?? aula.classId ?? aula.id

                return (
                    <ClassValidationCard
                        key={classId}
                        aula={aula}
                        tipo={isRequests ? 'coach-request' : 'professor'}
                        variant={isRequests ? 'purple' : 'orange'}
                        showParticipants
                        showCoachValidation={!isRequests}
                        showParentTally={!isRequests}
                        onConfirm={() => isRequests ? handleAccept(classId) : handleValidar(classId, true)}
                        onReject={() => isRequests ? openRejectModal(classId) : handleValidar(classId, false)}
                        confirmLabel={isRequests ? 'Aceitar Aula' : 'Realizada'}
                        rejectLabel={isRequests ? 'Recusar' : 'Não realizada'}
                    />
                )
            })}

            <Modal
                open={!!rejectTarget}
                title="Recusar pedido de aula"
                onClose={() => {
                    setRejectTarget(null)
                    setRejectReason('')
                }}
            >
                <form onSubmit={submitReject}>
                    {rejectTarget && (
                        <div className="reject-class-summary">
                            <div>
                                Modalidade: {rejectTarget?.ModalityName ?? rejectTarget?.modalityName ?? rejectTarget?.Modality ?? rejectTarget?.modality ?? '\u2014'}
                            </div>
                            <div>
                                Data: {rejectTarget?.StartDatetime ?? rejectTarget?.startDatetime
                                    ? new Date(rejectTarget.StartDatetime ?? rejectTarget.startDatetime).toLocaleString('pt-PT')
                                    : '\u2014'}
                            </div>
                            <div>
                                Alunos: {(rejectTarget?.Participants ?? rejectTarget?.participants ?? []).length}/
                                {rejectTarget?.MaxParticipants ?? rejectTarget?.maxParticipants ?? '?'}
                            </div>
                        </div>
                    )}

                    <label
                        style={{
                            display: 'block',
                            marginBottom: '8px',
                            fontSize: '0.92rem',
                            fontWeight: 600,
                            color: '#475569',
                        }}
                    >
                        Motivo *
                    </label>

                    <textarea
                        required
                        rows={4}
                        value={rejectReason}
                        onChange={(e) => setRejectReason(e.target.value)}
                        placeholder="Explique o motivo da recusa..."
                        className="reject-textarea"
                    />

                    <div className="modal-actions" style={{ marginTop: '16px' }}>
                        <button
                            type="button"
                            className="btn btn-secondary"
                            onClick={() => {
                                setRejectTarget(null)
                                setRejectReason('')
                            }}
                        >
                            Voltar
                        </button>
                        <button
                            type="submit"
                            className="btn btn-danger"
                            disabled={rejecting}
                        >
                            {rejecting ? 'A recusar...' : 'Confirmar recusa'}
                        </button>
                    </div>
                </form>
            </Modal>
        </section>
    )
}

export default CoachValidateClassesPage