import { useEffect, useMemo, useState } from 'react'
import Modal from '../../components/common/Modal'
import { getBlockedPeriods, createBlockedPeriod, updateBlockedPeriod, deleteBlockedPeriod } from '../../services/blockedPeriodsService'
import { getCoaches } from '../../services/coachService'
import { getStudios } from '../../services/studiosService'
import '../../styles/BlockedPeriods.css'

const SCOPE_LABELS = {
    1: 'Aulas Regulares',
    2: 'Estúdio',
    3: 'Professor',
    4: 'Evento / Interrupção',
    5: 'Feriado',
}

// Scope 0 (Indefinido) is excluded from creation — backend fallback only
const SCOPE_OPTIONS = [
    { value: 1, label: 'Aulas Regulares' },
    { value: 2, label: 'Estúdio' },
    { value: 3, label: 'Professor' },
    { value: 4, label: 'Evento / Interrupção' },
    { value: 5, label: 'Feriado' },
]

const SCOPE_BADGE = {
    0: 'bp-badge-0',
    1: 'bp-badge-1',
    2: 'bp-badge-2',
    3: 'bp-badge-3',
    4: 'bp-badge-4',
    5: 'bp-badge-5',
}

const SCOPE_DOT = {
    0: 'bp-dot-0',
    1: 'bp-dot-1',
    2: 'bp-dot-2',
    3: 'bp-dot-3',
    4: 'bp-dot-4',
    5: 'bp-dot-5',
}

const WEEKDAYS_SHORT = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']

// Normalize a blocked period from either casing
const normalizePeriod = (bp) => ({
    id: bp.blockedId ?? bp.blocked_id ?? bp.id ?? bp.Id,
    startDatetime: bp.startDatetime ?? bp.start_datetime ?? bp.StartDatetime,
    endDatetime: bp.endDatetime ?? bp.end_datetime ?? bp.EndDatetime,
    scope: bp.scope ?? bp.Scope ?? 0,
    coachId: bp.coachId ?? bp.CoachId,
    coachName: bp.coachName ?? bp.CoachName,
    studioId: bp.studioId ?? bp.StudioId,
    studioName: bp.studioName ?? bp.StudioName,
    reason: bp.reason ?? bp.Reason,
})

const padDateKey = (y, m, d) =>
    `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`

const formatTime = (datetimeStr) => {
    const d = new Date(datetimeStr)
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

const formatFullDate = (datetimeStr) =>
    new Date(datetimeStr).toLocaleDateString('pt-PT', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
    })

const formatMonthRange = (year, month) => {
    const from = `${year}-${String(month + 1).padStart(2, '0')}-01`
    const lastDay = new Date(year, month + 1, 0).getDate()
    const to = `${year}-${String(month + 1).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`
    return { from, to }
}

const emptyForm = {
    startDate: '',
    startTime: '',
    endDate: '',
    endTime: '',
    scope: 1,
    reason: '',
    coachId: '',
    studioId: '',
    allDay: false,
}

function StaffBlockedPeriodsPage() {
    const today = new Date()
    const [currentMonth, setCurrentMonth] = useState(new Date(today.getFullYear(), today.getMonth(), 1))
    const [periods, setPeriods] = useState([])
    const [coaches, setCoaches] = useState([])
    const [studios, setStudios] = useState([])
    const [loading, setLoading] = useState(true)
    const [pageError, setPageError] = useState(null)
    const [selectedDay, setSelectedDay] = useState(null)

    // Modal state
    const [showCreate, setShowCreate] = useState(false)
    const [showEdit, setShowEdit] = useState(false)
    const [showDelete, setShowDelete] = useState(false)
    const [editTarget, setEditTarget] = useState(null)
    const [form, setForm] = useState(emptyForm)
    const [saving, setSaving] = useState(false)
    const [formError, setFormError] = useState(null)

    const year = currentMonth.getFullYear()
    const month = currentMonth.getMonth()
    const daysInMonth = new Date(year, month + 1, 0).getDate()
    const firstDayOfWeek = new Date(year, month, 1).getDay() // 0 = Sunday

    const monthLabel = currentMonth.toLocaleDateString('pt-PT', { month: 'long', year: 'numeric' })

    const loadPeriods = async () => {
        const { from, to } = formatMonthRange(year, month)
        const data = await getBlockedPeriods({ from, to })
        const raw = Array.isArray(data) ? data : (data?.items ?? data?.Items ?? [])
        setPeriods(raw.map(normalizePeriod))
    }

    useEffect(() => {
        async function init() {
            setLoading(true)
            try {
                const [coachData, studioData] = await Promise.all([
                    getCoaches(),
                    getStudios(),
                ])
                const rawCoaches = Array.isArray(coachData) ? coachData : (coachData?.items ?? coachData?.Items ?? [])
                const rawStudios = Array.isArray(studioData) ? studioData : (studioData?.items ?? studioData?.Items ?? [])
                setCoaches(rawCoaches)
                setStudios(rawStudios.filter((s) => s.active ?? s.Active ?? true))
                await loadPeriods()
            } catch (e) {
                setPageError(e.message)
            } finally {
                setLoading(false)
            }
        }
        init()
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])

    useEffect(() => {
        loadPeriods()
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [year, month])

    // Map periods per calendar day
    const periodsPerDay = useMemo(() => {
        const map = {}
        for (let d = 1; d <= daysInMonth; d++) {
            const dk = padDateKey(year, month, d)
            const dayStart = new Date(year, month, d, 0, 0, 0)
            const dayEnd = new Date(year, month, d, 23, 59, 59)
            map[dk] = periods.filter((bp) => {
                const bpStart = new Date(bp.startDatetime)
                const bpEnd = new Date(bp.endDatetime)
                return bpStart <= dayEnd && bpEnd >= dayStart
            })
        }
        return map
    }, [periods, year, month, daysInMonth])

    const selectedDayPeriods = useMemo(() => {
        if (!selectedDay) return []
        return (periodsPerDay[selectedDay] ?? []).sort((a, b) =>
            a.startDatetime.localeCompare(b.startDatetime),
        )
    }, [periodsPerDay, selectedDay])

    const todayKey = padDateKey(today.getFullYear(), today.getMonth(), today.getDate())

    // Form helpers
    const setField = (key, value) => setForm((prev) => ({ ...prev, [key]: value }))

    const handleAllDayToggle = () => {
        const next = !form.allDay
        setForm((prev) => ({
            ...prev,
            allDay: next,
            startTime: next ? '00:00' : '',
            endTime: next ? '23:59' : '',
        }))
    }

    const resetForm = (prefillDate) => {
        setForm({
            ...emptyForm,
            startDate: prefillDate ?? '',
            endDate: prefillDate ?? '',
        })
        setFormError(null)
    }

    const openCreate = () => {
        resetForm(selectedDay ?? '')
        setShowCreate(true)
    }

    const openEdit = (bp) => {
        const start = new Date(bp.startDatetime)
        const end = new Date(bp.endDatetime)
        const isFullDay =
            start.getHours() === 0 && start.getMinutes() === 0 &&
            end.getHours() === 23 && end.getMinutes() === 59

        setForm({
            startDate: padDateKey(start.getFullYear(), start.getMonth(), start.getDate()),
            startTime: `${String(start.getHours()).padStart(2, '0')}:${String(start.getMinutes()).padStart(2, '0')}`,
            endDate: padDateKey(end.getFullYear(), end.getMonth(), end.getDate()),
            endTime: `${String(end.getHours()).padStart(2, '0')}:${String(end.getMinutes()).padStart(2, '0')}`,
            scope: bp.scope,
            reason: bp.reason ?? '',
            coachId: bp.coachId ? String(bp.coachId) : '',
            studioId: bp.studioId ? String(bp.studioId) : '',
            allDay: isFullDay,
        })
        setEditTarget(bp)
        setFormError(null)
        setShowEdit(true)
    }

    const buildPayload = () => ({
        scope: Number(form.scope),
        startDatetime: `${form.startDate}T${form.startTime || '00:00'}:00`,
        endDatetime: `${form.endDate}T${form.endTime || '23:59'}:00`,
        ...(form.reason ? { reason: form.reason } : {}),
        ...(Number(form.scope) === 3 && form.coachId ? { coachId: Number(form.coachId) } : {}),
        ...(Number(form.scope) === 2 && form.studioId ? { studioId: Number(form.studioId) } : {}),
    })

    const validateForm = () => {
        if (!form.startDate || !form.endDate) return 'Preencha as datas de início e fim.'
        if (!form.allDay && (!form.startTime || !form.endTime)) return 'Preencha as horas de início e fim.'
        if (Number(form.scope) === 3 && !form.coachId) return 'Selecione um professor.'
        if (Number(form.scope) === 2 && !form.studioId) return 'Selecione um estúdio.'
        return null
    }

    const handleCreate = async (e) => {
        e.preventDefault()
        const err = validateForm()
        if (err) { setFormError(err); return }
        setSaving(true)
        try {
            await createBlockedPeriod(buildPayload())
            await loadPeriods()
            setShowCreate(false)
        } catch (e) {
            setFormError(e.message)
        } finally {
            setSaving(false)
        }
    }

    const handleEdit = async (e) => {
        e.preventDefault()
        const err = validateForm()
        if (err) { setFormError(err); return }
        setSaving(true)
        try {
            await updateBlockedPeriod(editTarget.id, buildPayload())
            await loadPeriods()
            setShowEdit(false)
        } catch (e) {
            setFormError(e.message)
        } finally {
            setSaving(false)
        }
    }

    const handleDelete = async () => {
        setSaving(true)
        try {
            await deleteBlockedPeriod(editTarget.id)
            await loadPeriods()
            setShowDelete(false)
            setEditTarget(null)
        } catch (e) {
            console.error(e)
        } finally {
            setSaving(false)
        }
    }

    // Shared form fields rendered in both create & edit modals
    const renderFormFields = () => (
        <>
            <div className="bp-form-row-2">
                <div className="bp-form-group">
                    <label className="bp-form-label" htmlFor="bp-start-date">Data de início *</label>
                    <input
                        id="bp-start-date"
                        type="date"
                        className="bp-date-input"
                        value={form.startDate}
                        onChange={(e) => setField('startDate', e.target.value)}
                        required
                    />
                </div>
                <div className="bp-form-group">
                    <label className="bp-form-label" htmlFor="bp-end-date">Data de fim *</label>
                    <input
                        id="bp-end-date"
                        type="date"
                        className="bp-date-input"
                        value={form.endDate}
                        onChange={(e) => setField('endDate', e.target.value)}
                        required
                    />
                </div>
            </div>

            <div className="bp-allday-row">
                <input
                    id="bp-allday"
                    type="checkbox"
                    className="bp-allday-checkbox"
                    checked={form.allDay}
                    onChange={handleAllDayToggle}
                />
                <label htmlFor="bp-allday" className="bp-allday-label">Dia todo</label>
            </div>

            <div className="bp-form-row-2">
                <div className="bp-form-group">
                    <label className="bp-form-label" htmlFor="bp-start-time">Hora de início *</label>
                    <input
                        id="bp-start-time"
                        type="time"
                        className="bp-time-input"
                        value={form.startTime}
                        onChange={(e) => setField('startTime', e.target.value)}
                        disabled={form.allDay}
                        required={!form.allDay}
                    />
                </div>
                <div className="bp-form-group">
                    <label className="bp-form-label" htmlFor="bp-end-time">Hora de fim *</label>
                    <input
                        id="bp-end-time"
                        type="time"
                        className="bp-time-input"
                        value={form.endTime}
                        onChange={(e) => setField('endTime', e.target.value)}
                        disabled={form.allDay}
                        required={!form.allDay}
                    />
                </div>
            </div>

            <div className="bp-form-group">
                <label className="bp-form-label" htmlFor="bp-scope">Tipo de bloqueio *</label>
                <select
                    id="bp-scope"
                    className="bp-select"
                    value={form.scope}
                    onChange={(e) => setForm({ ...form, scope: Number(e.target.value), coachId: '', studioId: '' })}
                    required
                >
                    {SCOPE_OPTIONS.map((opt) => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                </select>
            </div>

            {Number(form.scope) === 3 && (
                <div className="bp-form-group">
                    <label className="bp-form-label" htmlFor="bp-coach">Professor *</label>
                    <select
                        id="bp-coach"
                        className="bp-select"
                        value={form.coachId}
                        onChange={(e) => setField('coachId', e.target.value)}
                        required
                    >
                        <option value="">Selecionar professor...</option>
                        {coaches.map((c) => {
                            const id = c.coachId ?? c.CoachId ?? c.id ?? c.Id
                            const name = c.name ?? c.Name ?? `#${id}`
                            return <option key={id} value={id}>{name}</option>
                        })}
                    </select>
                </div>
            )}

            {Number(form.scope) === 2 && (
                <div className="bp-form-group">
                    <label className="bp-form-label" htmlFor="bp-studio">Estúdio *</label>
                    <select
                        id="bp-studio"
                        className="bp-select"
                        value={form.studioId}
                        onChange={(e) => setField('studioId', e.target.value)}
                        required
                    >
                        <option value="">Selecionar estúdio...</option>
                        {studios.map((s) => {
                            const id = s.studioId ?? s.id ?? s.Id
                            const name = s.name ?? s.Name ?? `#${id}`
                            return <option key={id} value={id}>{name}</option>
                        })}
                    </select>
                </div>
            )}

            <div className="bp-form-group">
                <label className="bp-form-label" htmlFor="bp-reason">Razão (opcional)</label>
                <input
                    id="bp-reason"
                    type="text"
                    className="bp-text-input"
                    placeholder="Ex: Manutenção, feriado, formação..."
                    value={form.reason}
                    onChange={(e) => setField('reason', e.target.value)}
                />
            </div>

            {formError && (
                <div className="bp-form-error">{formError}</div>
            )}
        </>
    )

    if (loading) {
        return (
            <section className="dashboard-page-card">
                <h2>Bloqueios de Horário</h2>
                <p className="bp-loading">A carregar...</p>
            </section>
        )
    }

    if (pageError) {
        return (
            <section className="dashboard-page-card">
                <h2>Bloqueios de Horário</h2>
                <p className="bp-error">{pageError}</p>
            </section>
        )
    }

    return (
        <section className="dashboard-page-card">
            {/* Header */}
            <div className="bp-header">
                <div>
                    <h2>Bloqueios de Horário</h2>
                    <p className="bp-subtitle">Gerir períodos bloqueados para estúdios, professores e eventos.</p>
                </div>
                <button
                    type="button"
                    className="btn btn-primary"
                    onClick={openCreate}
                >
                    Criar Bloqueio
                </button>
            </div>

            {/* Legend */}
            <div className="bp-legend">
                {SCOPE_OPTIONS.map((opt) => (
                    <div key={opt.value} className="bp-legend-item">
                        <div className={`bp-legend-dot ${SCOPE_DOT[opt.value]}`} />
                        <span className="bp-legend-label">{opt.label}</span>
                    </div>
                ))}
            </div>

            {/* Monthly calendar */}
            <div className="bp-calendar-card">
                <div className="bp-calendar-nav">
                    <h3 className="bp-calendar-month">{monthLabel}</h3>
                    <div className="bp-calendar-nav-btns">
                        <button
                            type="button"
                            className="bp-nav-btn"
                            onClick={() => setCurrentMonth(new Date(year, month - 1, 1))}
                            aria-label="Mês anterior"
                        >
                            ‹
                        </button>
                        <button
                            type="button"
                            className="bp-nav-btn"
                            onClick={() => setCurrentMonth(new Date(year, month + 1, 1))}
                            aria-label="Mês seguinte"
                        >
                            ›
                        </button>
                    </div>
                </div>

                <div className="bp-cal-grid">
                    {WEEKDAYS_SHORT.map((d) => (
                        <div key={d} className="bp-cal-weekday">{d}</div>
                    ))}

                    {/* Empty cells before first day */}
                    {Array.from({ length: firstDayOfWeek }).map((_, i) => (
                        <div key={`empty-${i}`} className="bp-cal-empty" />
                    ))}

                    {/* Day cells */}
                    {Array.from({ length: daysInMonth }).map((_, i) => {
                        const day = i + 1
                        const dk = padDateKey(year, month, day)
                        const dayPeriods = periodsPerDay[dk] ?? []
                        const isToday = dk === todayKey
                        const isSelected = dk === selectedDay

                        return (
                            <div
                                key={dk}
                                className={[
                                    'bp-cal-day',
                                    isSelected ? 'bp-cal-day--selected' : '',
                                    isToday && !isSelected ? 'bp-cal-day--today' : '',
                                ].join(' ')}
                                onClick={() => setSelectedDay(isSelected ? null : dk)}
                            >
                                <div className="bp-cal-day-num">{day}</div>
                                {dayPeriods.length > 0 && (
                                    <div className="bp-cal-indicators">
                                        {dayPeriods.slice(0, 3).map((bp) => (
                                            <div
                                                key={bp.id}
                                                className={`bp-cal-indicator ${SCOPE_BADGE[bp.scope] ?? 'bp-badge-0'}`}
                                                title={`${SCOPE_LABELS[bp.scope] ?? 'Indefinido'}${bp.reason ? `: ${bp.reason}` : ''}`}
                                            >
                                                {SCOPE_LABELS[bp.scope] ?? 'Indefinido'}
                                            </div>
                                        ))}
                                        {dayPeriods.length > 3 && (
                                            <div className="bp-cal-more">+{dayPeriods.length - 3}</div>
                                        )}
                                    </div>
                                )}
                            </div>
                        )
                    })}
                </div>

                {selectedDay && (
                    <p className="bp-calendar-selection-hint">
                        {new Date(selectedDay + 'T12:00:00').toLocaleDateString('pt-PT', {
                            weekday: 'long',
                            day: 'numeric',
                            month: 'long',
                        })}
                    </p>
                )}
            </div>

            {/* Selected day detail */}
            {selectedDay && (
                <div>
                    <h3 className="bp-detail-heading">
                        {new Date(selectedDay + 'T12:00:00').toLocaleDateString('pt-PT', {
                            weekday: 'long',
                            day: 'numeric',
                            month: 'long',
                            year: 'numeric',
                        })}
                    </h3>

                    {selectedDayPeriods.length > 0 ? (
                        <div className="bp-detail-list">
                            {selectedDayPeriods.map((bp) => (
                                <div key={bp.id} className="bp-period-card">
                                    <div className="bp-period-card-left">
                                        <div className="bp-period-card-top">
                                            <span className={`bp-scope-badge ${SCOPE_BADGE[bp.scope] ?? 'bp-badge-0'}`}>
                                                {SCOPE_LABELS[bp.scope] ?? 'Indefinido'}
                                            </span>
                                        </div>
                                        {bp.reason && (
                                            <p className="bp-period-reason">{bp.reason}</p>
                                        )}
                                        <div className="bp-period-meta">
                                            <span className="bp-period-meta-label">Início:</span>
                                            <span>{formatFullDate(bp.startDatetime)} {formatTime(bp.startDatetime)}</span>
                                            <span className="bp-period-meta-label">Fim:</span>
                                            <span>{formatFullDate(bp.endDatetime)} {formatTime(bp.endDatetime)}</span>
                                            {bp.scope === 3 && bp.coachName && (
                                                <>
                                                    <span className="bp-period-meta-label">Professor:</span>
                                                    <span>{bp.coachName}</span>
                                                </>
                                            )}
                                            {bp.scope === 2 && bp.studioName && (
                                                <>
                                                    <span className="bp-period-meta-label">Estúdio:</span>
                                                    <span>{bp.studioName}</span>
                                                </>
                                            )}
                                        </div>
                                    </div>
                                    <div className="bp-period-card-actions">
                                        <button
                                            type="button"
                                            className="btn btn-secondary btn-sm"
                                            onClick={() => openEdit(bp)}
                                        >
                                            Editar
                                        </button>
                                        <button
                                            type="button"
                                            className="btn btn-danger btn-sm"
                                            onClick={() => { setEditTarget(bp); setShowDelete(true) }}
                                        >
                                            Remover
                                        </button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="bp-empty">
                            <p>Sem bloqueios neste dia.</p>
                        </div>
                    )}
                </div>
            )}

            {/* Create Modal */}
            <Modal
                open={showCreate}
                title="Criar Bloqueio de Horário"
                onClose={() => setShowCreate(false)}
            >
                <form onSubmit={handleCreate} className="bp-modal-form">
                    {renderFormFields()}
                    <div className="modal-actions">
                        <button type="button" className="btn btn-secondary" onClick={() => setShowCreate(false)}>
                            Cancelar
                        </button>
                        <button type="submit" className="btn btn-primary" disabled={saving}>
                            {saving ? 'A criar...' : 'Criar'}
                        </button>
                    </div>
                </form>
            </Modal>

            {/* Edit Modal */}
            <Modal
                open={showEdit}
                title="Editar Bloqueio de Horário"
                onClose={() => setShowEdit(false)}
            >
                <form onSubmit={handleEdit} className="bp-modal-form">
                    {renderFormFields()}
                    <div className="modal-actions">
                        <button type="button" className="btn btn-secondary" onClick={() => setShowEdit(false)}>
                            Cancelar
                        </button>
                        <button type="submit" className="btn btn-primary" disabled={saving}>
                            {saving ? 'A guardar...' : 'Guardar'}
                        </button>
                    </div>
                </form>
            </Modal>

            {/* Delete Confirm Modal */}
            <Modal
                open={showDelete}
                title="Remover Bloqueio"
                onClose={() => setShowDelete(false)}
            >
                <p className="bp-delete-text">
                    Tem a certeza de que deseja remover este bloqueio
                    {editTarget?.reason ? ` "${editTarget.reason}"` : ''}? Esta ação não pode ser desfeita.
                </p>
                <div className="modal-actions">
                    <button type="button" className="btn btn-secondary" onClick={() => setShowDelete(false)}>
                        Cancelar
                    </button>
                    <button type="button" className="btn btn-danger" onClick={handleDelete} disabled={saving}>
                        {saving ? 'A remover...' : 'Remover'}
                    </button>
                </div>
            </Modal>
        </section>
    )
}

export default StaffBlockedPeriodsPage
