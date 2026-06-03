import { useEffect, useState } from 'react'
import ClassValidationCard from '../../components/common/ClassValidationCard'
import Modal from '../../components/common/Modal'
import Button from '../../components/common/Button'
import Select from '../../components/common/Select'
import { getValidateClasses, staffApprove, staffReject, staffValidate, updateClassDetails, getDefaultPrice } from '../../services/staffService'
import { getStudios } from '../../services/studiosService'
import '../../styles/ValidateClasses.css'

function StaffValidateClassesPage() {
    const [activeTab, setActiveTab] = useState('requested')
    const [items, setItems] = useState([])
    const [loading, setLoading] = useState(false)
    const [stats, setStats] = useState({ requested: 0, pending: 0 })

    // Studios for edit modal
    const [studios, setStudios] = useState([])

    // Reject modal state
    const [rejectTarget, setRejectTarget] = useState(null)
    const [rejectReason, setRejectReason] = useState('')
    const [rejecting, setRejecting] = useState(false)

    // Edit modal state
    const [editTarget, setEditTarget] = useState(null)
    const [editStudioId, setEditStudioId] = useState('')
    const [editStartDate, setEditStartDate] = useState('')
    const [editStartTime, setEditStartTime] = useState('')
    const [editEndTime, setEditEndTime] = useState('')
    const [editPerParticipantPrice, setEditPerParticipantPrice] = useState('')
    const [editError, setEditError] = useState('')
    const [editSaving, setEditSaving] = useState(false)

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

    useEffect(() => {
        getStudios()
            .then(data => setStudios(Array.isArray(data) ? data : (data?.items ?? data?.Items ?? [])))
            .catch(() => {})
    }, [])

    const openEditModal = (aula) => {
        setEditTarget(aula)
        const startDt = aula.StartDatetime ?? aula.startDatetime ?? ''
        const endDt   = aula.EndDatetime   ?? aula.endDatetime   ?? ''
        setEditStudioId(String(aula.StudioId ?? aula.studioId ?? ''))
        setEditStartDate(startDt ? startDt.slice(0, 10) : '')
        setEditStartTime(startDt ? startDt.slice(11, 16) : '')
        setEditEndTime(endDt ? endDt.slice(11, 16) : '')
        const existingPrice = aula?.PerParticipantPrice ?? aula?.perParticipantPrice ?? null
        if (existingPrice !== null && existingPrice !== undefined) {
            setEditPerParticipantPrice(String(existingPrice))
        } else {
            // fetch default price for the date and prefill
            const dateStr = startDt ? startDt.slice(0, 10) : ''
            getDefaultPrice(dateStr)
                .then(res => {
                    const price = res?.price ?? res?.Price ?? res?.price ?? null
                    if (price !== null && price !== undefined) setEditPerParticipantPrice(String(price))
                })
                .catch(() => {})
        }
        setEditError('')
    }

    const submitEdit = async (e) => {
        e.preventDefault()
        if (!editStartDate || !editStartTime || !editEndTime) {
            setEditError('Preencha data e horários.')
            return
        }
        if (editEndTime <= editStartTime) {
            setEditError('A hora de fim deve ser depois da hora de início.');
            return
        }
        // Ensure a price is provided (cannot submit empty price)
        if (editPerParticipantPrice === null || editPerParticipantPrice === undefined || String(editPerParticipantPrice).trim() === '') {
            setEditError('Insira o preço por participante.')
            return
        }
        const parsedPrice = Number(String(editPerParticipantPrice).replace(',', '.'))
        if (Number.isNaN(parsedPrice) || parsedPrice < 0) {
            setEditError('Insira um preço válido (>= 0).')
            return
        }
        const id = editTarget?.ClassId ?? editTarget?.classId
        setEditSaving(true); setEditError('')
        try {
            const body = {
                startDatetime: `${editStartDate}T${editStartTime}:00`,
                endDatetime:   `${editStartDate}T${editEndTime}:00`,
            }
            if (editStudioId) body.studioId = Number(editStudioId)
            // perParticipantPrice is required now and will always be sent
            body.perParticipantPrice = parsedPrice
            await updateClassDetails(id, body)
            setEditTarget(null)
            fetchData()
        } catch (err) {
            setEditError(err.message)
        } finally {
            setEditSaving(false)
        }
    }

    const handleApprove = async (id) => {
        try {
            // attempt to read price from current item
            const aula = items.find(a => (a.ClassId ?? a.classId ?? a.id) === id)
            const price = aula?.PerParticipantPrice ?? aula?.perParticipantPrice ?? null
            const fn = staffApprove(id)
            if (typeof fn === 'function') {
                await fn(price)
            } else {
                await staffApprove(id)
            }
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
            await staffReject(id, rejectReason || undefined)
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
            const aula = items.find(a => (a.ClassId ?? a.classId ?? a.id) === id)
            const price = aula?.PerParticipantPrice ?? aula?.perParticipantPrice ?? null
            await staffValidate(id, true, price)
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
            <h2>Coachings</h2>
            <p>Aprovar coachings requisitados e validar coachings pendentes após o prazo de 48h.</p>

            {/* KPI cards */}
            <div className="validate-kpi-row" style={{ marginTop: '20px' }}>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Requisitados</span>
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
                    Requisitados
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
                            ? 'Não há pedidos de coaching aguardando aprovação.'
                            : 'Não há coachings pendentes de validação.'}
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
                    onEdit={isRequested ? openEditModal : undefined}
                    confirmLabel={isRequested ? 'Aceitar Coaching' : 'Validar'}
                    rejectLabel={isRequested ? 'Recusar Coaching' : 'Cancelar'}
                />
            ))}

            {/* Edit Modal */}
            <Modal
                open={!!editTarget}
                title="Editar Detalhes do Coaching"
                onClose={() => setEditTarget(null)}
            >
                <form onSubmit={submitEdit}>
                    {editTarget && (
                        <div className="reject-class-summary">
                            <div><strong>Modalidade:</strong> {editTarget.ModalityName ?? editTarget.modalityName ?? '—'}</div>
                            <div><strong>Coach:</strong> {editTarget.CoachName ?? editTarget.coachName ?? '—'}</div>
                        </div>
                    )}

                    <div className="modal-field" style={{ marginTop: '16px' }}>
                        <label className="modal-label">Estúdio</label>
                        <Select
                            value={editStudioId}
                            onChange={setEditStudioId}
                            options={[
                                { value: '', label: 'Manter atual' },
                                ...studios
                                    .filter(s => s.isActive ?? s.IsActive ?? true)
                                    .map(s => ({
                                        value: String(s.StudioId ?? s.studioId),
                                        label: s.Name ?? s.name ?? '',
                                    }))
                            ]}
                        />
                    </div>

                    <div className="modal-field" style={{ marginTop: '12px' }}>
                        <label className="modal-label">Data</label>
                        <input
                            type="date"
                            className="input"
                            value={editStartDate}
                            onChange={e => setEditStartDate(e.target.value)}
                            required
                        />
                    </div>

                    <div style={{ display: 'flex', gap: '12px', marginTop: '12px' }}>
                        <div className="modal-field" style={{ flex: 1 }}>
                            <label className="modal-label">Hora início</label>
                            <input
                                type="time"
                                className="input"
                                value={editStartTime}
                                onChange={e => setEditStartTime(e.target.value)}
                                required
                            />
                        </div>
                        <div className="modal-field" style={{ flex: 1 }}>
                            <label className="modal-label">Hora fim</label>
                            <input
                                type="time"
                                className="input"
                                value={editEndTime}
                                onChange={e => setEditEndTime(e.target.value)}
                                required
                            />
                        </div>
                    </div>

                    <div className="modal-field" style={{ marginTop: '12px' }}>
                        <label className="modal-label">Preço por participante (EUR)</label>
                        <input
                            type="number"
                            step="0.01"
                            min="0"
                            className="input"
                            value={editPerParticipantPrice}
                            onChange={e => setEditPerParticipantPrice(e.target.value)}
                            placeholder="Manter padrão da app se vazio"
                        />
                        <p style={{ marginTop: '6px', fontSize: '0.85rem', color: '#9CA3AF' }}>
                            Deixe vazio para usar o preço padrão definido em app settings.
                        </p>
                    </div>

                    {editError && <p style={{ color: 'var(--danger)', fontSize: '0.875rem', marginTop: '8px' }}>{editError}</p>}

                    <div className="modal-actions" style={{ marginTop: '16px' }}>
                        <Button type="button" variant="secondary" onClick={() => setEditTarget(null)}>
                            Cancelar
                        </Button>
                        <Button type="submit" variant="primary" disabled={editSaving}>
                            {editSaving ? 'A guardar...' : 'Guardar Alterações'}
                        </Button>
                    </div>
                </form>
            </Modal>

            {/* Reject Modal */}
            <Modal
                open={!!rejectTarget}
                title="Recusar Coaching"
                onClose={() => { setRejectTarget(null); setRejectReason('') }}
            >
                <form onSubmit={submitReject}>
                    <p>
                        Indique o motivo para recusar o coaching de{' '}
                        <strong>
                            {rejectTarget?.ModalityName ?? rejectTarget?.modalityName ?? rejectTarget?.Modality ?? rejectTarget?.modality ?? 'este coaching'}
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
