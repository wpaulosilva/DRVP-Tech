import { useEffect, useMemo, useState } from 'react'
import Modal from '../../components/common/Modal'
import {
    getMyCoachProfile,
    getCoachAvailability,
    createAvailability,
    updateAvailability,
    deleteAvailability,
} from '../../services/coachAvailabilityService'
import '../../styles/Availability.css'

const WEEKDAYS = [
    { value: 1, label: 'Segunda' },
    { value: 2, label: 'Terça' },
    { value: 3, label: 'Quarta' },
    { value: 4, label: 'Quinta' },
    { value: 5, label: 'Sexta' },
    { value: 6, label: 'Sábado' },
    { value: 7, label: 'Domingo' },
]

const HOUR_START = 6
const HOUR_END = 24
const HOUR_PX = 52
const TOTAL_HOURS = HOUR_END - HOUR_START

const todayIso = () => new Date().toISOString().slice(0, 10)

const formatPtDate = (iso) => {
    if (!iso) return '—'
    const [y, m, d] = iso.split('-')
    return `${d}/${m}/${y}`
}

const minutesFrom = (time) => {
    const [h, m] = time.split(':').map(Number)
    return h * 60 + m
}

// Field accessors — handles both camelCase and snake_case from the API
const getId = (item) => item.coachAvId ?? item.coachav_id ?? item.id ?? item.Id
const getWeekday = (item) => item.weekday ?? item.Weekday
const getStartTime = (item) => item.startTime ?? item.start_time ?? item.StartTime
const getEndTime = (item) => item.endTime ?? item.end_time ?? item.EndTime
const getValidFrom = (item) => item.validFrom ?? item.valid_from ?? item.ValidFrom
const getValidUntil = (item) => item.validUntil ?? item.valid_until ?? item.ValidUntil

const isActiveOn = (item, isoDate) => {
    const from = getValidFrom(item)
    const until = getValidUntil(item)
    if (from && isoDate < from) return false
    if (until && isoDate > until) return false
    return true
}

const emptyForm = { weekday: '', startTime: '', endTime: '', validFrom: '', validUntil: '' }

function CoachAvailabilityPage() {
    const [coachId, setCoachId] = useState(null)
    const [items, setItems] = useState([])
    const [loading, setLoading] = useState(true)
    const [saving, setSaving] = useState(false)
    const [pageError, setPageError] = useState(null)
    const [referenceDate, setReferenceDate] = useState(todayIso())
    const [showModal, setShowModal] = useState(false)
    const [editingId, setEditingId] = useState(null)
    const [form, setForm] = useState(emptyForm)
    const [formError, setFormError] = useState(null)

    useEffect(() => {
        async function load() {
            setLoading(true)
            try {
                const profile = await getMyCoachProfile()
                const id = profile?.coachId ?? profile?.CoachId ?? profile?.id ?? profile?.Id
                setCoachId(id)
                if (id) {
                    const data = await getCoachAvailability(id)
                    setItems(Array.isArray(data) ? data : (data?.items ?? data?.Items ?? []))
                }
            } catch (e) {
                setPageError(e.message)
            } finally {
                setLoading(false)
            }
        }
        load()
    }, [])

    const visible = useMemo(
        () => items.filter((item) => isActiveOn(item, referenceDate)),
        [items, referenceDate],
    )

    const refreshList = async (id) => {
        const cid = id ?? coachId
        if (!cid) return
        const data = await getCoachAvailability(cid)
        setItems(Array.isArray(data) ? data : (data?.items ?? data?.Items ?? []))
    }

    const openAdd = (weekday) => {
        setForm({ ...emptyForm, weekday: weekday ? String(weekday) : '' })
        setEditingId(null)
        setFormError(null)
        setShowModal(true)
    }

    const openEdit = (item) => {
        setForm({
            weekday: String(getWeekday(item)),
            startTime: getStartTime(item) ?? '',
            endTime: getEndTime(item) ?? '',
            validFrom: getValidFrom(item) ?? '',
            validUntil: getValidUntil(item) ?? '',
        })
        setEditingId(getId(item))
        setFormError(null)
        setShowModal(true)
    }

    const closeModal = () => {
        setShowModal(false)
        setEditingId(null)
        setForm(emptyForm)
        setFormError(null)
    }

    const validate = () => {
        if (!form.weekday) return 'Selecione o dia da semana.'
        if (!form.startTime || !form.endTime) return 'Indique a hora de início e fim.'
        if (form.startTime >= form.endTime) return 'A hora de fim deve ser superior à hora de início.'
        if (form.validFrom && form.validUntil && form.validFrom > form.validUntil) {
            return 'A data de fim de validade deve ser posterior à de início.'
        }
        const wd = Number(form.weekday)
        const conflict = items.find(
            (item) =>
                getWeekday(item) === wd &&
                getId(item) !== editingId &&
                form.startTime < getEndTime(item) &&
                form.endTime > getStartTime(item) &&
                (!form.validUntil || !getValidFrom(item) || form.validUntil >= getValidFrom(item)) &&
                (!form.validFrom || !getValidUntil(item) || form.validFrom <= getValidUntil(item)),
        )
        if (conflict) {
            return `Conflito com bloco existente (${getStartTime(conflict)}–${getEndTime(conflict)}). Os blocos no mesmo dia não podem sobrepor-se.`
        }
        return null
    }

    const handleSubmit = async (e) => {
        e.preventDefault()
        const err = validate()
        if (err) { setFormError(err); return }
        setSaving(true)
        try {
            const payload = {
                coachId,
                weekday: Number(form.weekday),
                startTime: form.startTime,
                endTime: form.endTime,
                ...(form.validFrom ? { validFrom: form.validFrom } : {}),
                ...(form.validUntil ? { validUntil: form.validUntil } : {}),
            }
            if (editingId !== null) {
                await updateAvailability(editingId, payload)
            } else {
                await createAvailability(payload)
            }
            await refreshList()
            closeModal()
        } catch (e) {
            setFormError(e.message)
        } finally {
            setSaving(false)
        }
    }

    const handleDelete = async (id) => {
        try {
            await deleteAvailability(id)
            await refreshList()
        } catch (e) {
            console.error(e)
        }
    }

    const blockStyle = (item) => {
        const startOffsetMin = minutesFrom(getStartTime(item)) - HOUR_START * 60
        const durationMin = minutesFrom(getEndTime(item)) - minutesFrom(getStartTime(item))
        const top = (startOffsetMin / 60) * HOUR_PX
        const height = Math.max((durationMin / 60) * HOUR_PX, 22)
        return { top: `${top}px`, height: `${height}px` }
    }

    const hours = Array.from({ length: TOTAL_HOURS + 1 }, (_, i) => HOUR_START + i)

    if (loading) {
        return (
            <section className="dashboard-page-card">
                <h2>Disponibilidade</h2>
                <p className="av-loading">A carregar...</p>
            </section>
        )
    }

    if (pageError) {
        return (
            <section className="dashboard-page-card">
                <h2>Disponibilidade</h2>
                <p className="av-error">{pageError}</p>
            </section>
        )
    }

    return (
        <section className="dashboard-page-card">
            {/* Header */}
            <div className="av-header">
                <div>
                    <h2>Disponibilidade</h2>
                    <p className="av-subtitle">
                        Defina os blocos semanais recorrentes em que está disponível para dar coaching.
                    </p>
                </div>
                <button type="button" className="btn btn-primary" onClick={() => openAdd()}>
                    Adicionar Bloco
                </button>
            </div>

            {/* Date filter */}
            <div className="av-filter-card">
                <label className="av-filter-label">Ver disponibilidade ativa em:</label>
                <input
                    type="date"
                    className="av-date-input"
                    value={referenceDate}
                    onChange={(e) => setReferenceDate(e.target.value)}
                />
                <span className="av-filter-hint">
                    Mostra os blocos cuja validade cobre esta data.
                </span>
            </div>

            {/* Weekly grid — desktop */}
            <div className="av-grid-card av-desktop-only">
                <div
                    className="av-grid-header"
                    style={{ gridTemplateColumns: `56px repeat(${WEEKDAYS.length}, 1fr)` }}
                >
                    <div />
                    {WEEKDAYS.map((w) => (
                        <div key={w.value} className="av-grid-day-label">
                            {w.label}
                        </div>
                    ))}
                </div>

                <div
                    className="av-grid-body"
                    style={{ gridTemplateColumns: `56px repeat(${WEEKDAYS.length}, 1fr)` }}
                >
                    {/* Hour labels column */}
                    <div className="av-hours-col" style={{ height: `${TOTAL_HOURS * HOUR_PX}px` }}>
                        {hours.slice(0, -1).map((h, idx) => (
                            <div
                                key={h}
                                className="av-hour-label"
                                style={{ top: `${idx * HOUR_PX - 6}px` }}
                            >
                                {String(h).padStart(2, '0')}:00
                            </div>
                        ))}
                    </div>

                    {/* Day columns */}
                    {WEEKDAYS.map((w) => {
                        const dayBlocks = visible.filter((item) => getWeekday(item) === w.value)
                        return (
                            <div
                                key={w.value}
                                className="av-day-col"
                                style={{ height: `${TOTAL_HOURS * HOUR_PX}px` }}
                                onDoubleClick={() => openAdd(w.value)}
                            >
                                {hours.slice(0, -1).map((_, idx) => (
                                    <div
                                        key={idx}
                                        className="av-hour-line"
                                        style={{ top: `${idx * HOUR_PX}px` }}
                                    />
                                ))}
                                {dayBlocks.map((b) => (
                                    <button
                                        key={getId(b)}
                                        type="button"
                                        className="av-block"
                                        style={blockStyle(b)}
                                        onClick={() => openEdit(b)}
                                        title={`${getStartTime(b)}–${getEndTime(b)}\nValidade: ${formatPtDate(getValidFrom(b))} → ${formatPtDate(getValidUntil(b))}`}
                                    >
                                        <span className="av-block-time">
                                            {getStartTime(b)}–{getEndTime(b)}
                                        </span>
                                        <span className="av-block-validity">
                                            {formatPtDate(getValidFrom(b))} → {formatPtDate(getValidUntil(b))}
                                        </span>
                                    </button>
                                ))}
                            </div>
                        )
                    })}
                </div>

                <p className="av-grid-hint">
                    Dica: clique num bloco para editar, ou faça duplo clique numa coluna para adicionar nesse dia.
                </p>
            </div>

            {/* Mobile list */}
            <div className="av-mobile-list">
                {WEEKDAYS.map((w) => {
                    const dayBlocks = visible
                        .filter((item) => getWeekday(item) === w.value)
                        .sort((a, b) => getStartTime(a).localeCompare(getStartTime(b)))
                    if (dayBlocks.length === 0) return null
                    return (
                        <div key={w.value} className="av-mobile-day-card">
                            <h3 className="av-mobile-day-title">{w.label}</h3>
                            {dayBlocks.map((b) => (
                                <div key={getId(b)} className="av-mobile-block">
                                    <div>
                                        <p className="av-mobile-block-time">
                                            {getStartTime(b)} – {getEndTime(b)}
                                        </p>
                                        <p className="av-mobile-block-validity">
                                            {formatPtDate(getValidFrom(b))} → {formatPtDate(getValidUntil(b))}
                                        </p>
                                    </div>
                                    <div className="av-mobile-actions">
                                        <button
                                            type="button"
                                            className="btn btn-secondary btn-sm"
                                            onClick={() => openEdit(b)}
                                        >
                                            Editar
                                        </button>
                                        <button
                                            type="button"
                                            className="btn btn-danger btn-sm"
                                            onClick={() => handleDelete(getId(b))}
                                        >
                                            Eliminar
                                        </button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )
                })}
                {visible.length === 0 && (
                    <div className="av-empty">
                        <p>Sem blocos de disponibilidade ativos para esta data.</p>
                    </div>
                )}
            </div>

            {/* Add / Edit Modal */}
            <Modal
                open={showModal}
                title={editingId !== null ? 'Editar Bloco de Disponibilidade' : 'Adicionar Bloco de Disponibilidade'}
                onClose={closeModal}
            >
                <form onSubmit={handleSubmit} className="av-modal-form">
                    <div className="av-form-group">
                        <label className="av-form-label" htmlFor="av-weekday">Dia da semana *</label>
                        <select
                            id="av-weekday"
                            className="av-select"
                            value={form.weekday}
                            onChange={(e) => setForm({ ...form, weekday: e.target.value })}
                            required
                        >
                            <option value="">Selecione...</option>
                            {WEEKDAYS.map((w) => (
                                <option key={w.value} value={w.value}>{w.label}</option>
                            ))}
                        </select>
                    </div>

                    <div className="av-form-row-2">
                        <div className="av-form-group">
                            <label className="av-form-label" htmlFor="av-start">Hora de início *</label>
                            <input
                                id="av-start"
                                type="time"
                                className="av-time-input"
                                required
                                value={form.startTime}
                                onChange={(e) => setForm({ ...form, startTime: e.target.value })}
                            />
                        </div>
                        <div className="av-form-group">
                            <label className="av-form-label" htmlFor="av-end">Hora de fim *</label>
                            <input
                                id="av-end"
                                type="time"
                                className="av-time-input"
                                required
                                value={form.endTime}
                                onChange={(e) => setForm({ ...form, endTime: e.target.value })}
                            />
                        </div>
                    </div>

                    <div className="av-form-row-2">
                        <div className="av-form-group">
                            <label className="av-form-label" htmlFor="av-from">Início de validade</label>
                            <input
                                id="av-from"
                                type="date"
                                className="av-date-input"
                                value={form.validFrom}
                                onChange={(e) => setForm({ ...form, validFrom: e.target.value })}
                            />
                        </div>
                        <div className="av-form-group">
                            <label className="av-form-label" htmlFor="av-until">Fim de validade</label>
                            <input
                                id="av-until"
                                type="date"
                                className="av-date-input"
                                value={form.validUntil}
                                onChange={(e) => setForm({ ...form, validUntil: e.target.value })}
                            />
                        </div>
                    </div>

                    <p className="av-form-hint">
                        Pode adicionar vários blocos no mesmo dia (ex.: 09:00–11:00 e 14:00–16:00).
                        Deixe a validade em branco para um bloco recorrente sem data limite.
                    </p>

                    {formError && <div className="av-form-error">{formError}</div>}

                    <div className="modal-actions">
                        {editingId !== null && (
                            <button
                                type="button"
                                className="btn btn-danger"
                                onClick={() => { handleDelete(editingId); closeModal() }}
                            >
                                Eliminar
                            </button>
                        )}
                        <button type="button" className="btn btn-secondary" onClick={closeModal}>
                            Cancelar
                        </button>
                        <button type="submit" className="btn btn-primary" disabled={saving}>
                            {saving ? 'A guardar...' : editingId !== null ? 'Guardar' : 'Adicionar'}
                        </button>
                    </div>
                </form>
            </Modal>
        </section>
    )
}

export default CoachAvailabilityPage
