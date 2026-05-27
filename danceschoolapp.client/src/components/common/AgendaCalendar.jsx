import { useEffect, useMemo, useState } from 'react'
import '../../styles/StaffAgenda.css'

// ─── Constants ────────────────────────────────────────────────────────────────

const WEEKDAYS = [
    { short: 'SEG', label: 'Segunda' },
    { short: 'TER', label: 'Terça' },
    { short: 'QUA', label: 'Quarta' },
    { short: 'QUI', label: 'Quinta' },
    { short: 'SEX', label: 'Sexta' },
    { short: 'SÁB', label: 'Sábado' },
    { short: 'DOM', label: 'Domingo' },
]

const HOUR_START = 7
const HOUR_END = 22
const HOUR_HEIGHT = 48
const TOTAL_HOURS = HOUR_END - HOUR_START

const STATUS_MAP = {
    1: { label: 'Pedido', css: 'requested' },
    2: { label: 'Aprovado', css: 'approved' },
    3: { label: 'Cancelado', css: 'cancelled' },
    4: { label: 'Terminado', css: 'finished' },
    5: { label: 'Validado', css: 'validated' },
    6: { label: 'Pendente', css: 'pending' },
    7: { label: 'Aguarda Staff', css: 'coach-approved' },
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const pad = (n) => String(n).padStart(2, '0')
const isoOf = (date) => { const d = new Date(date); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` }
const addDays = (date, n) => { const d = new Date(date); d.setDate(d.getDate() + n); return d }
const formatTime = (date) => { if (!date) return '—'; const d = new Date(date); return `${pad(d.getHours())}:${pad(d.getMinutes())}` }
const formatDate = (date) => { if (!date) return '—'; const d = new Date(date); return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}` }
const minsFromStart = (date) => { const d = new Date(date); return d.getHours() * 60 + d.getMinutes() }

const weekStart = (date) => {
    const d = new Date(date)
    const day = d.getDay() || 7
    d.setDate(d.getDate() - (day - 1))
    d.setHours(0, 0, 0, 0)
    return d
}

const getField = (item, ...keys) => {
    for (const key of keys) {
        if (item?.[key] !== undefined && item?.[key] !== null && item?.[key] !== '') return item[key]
    }
    return '—'
}

const getDateValue = (item, ...keys) => { for (const k of keys) if (item?.[k]) return item[k]; return null }
const getStudioName = (item) => getField(item, 'StudioName', 'studioName', 'Studio', 'studio')
const getCoachName = (item) => getField(item, 'CoachName', 'coachName', 'ProfessorName', 'professorName')
const getModality = (item) => getField(item, 'ModalityName', 'modalityName', 'ClassName', 'className', 'Name', 'name')
const statusOf = (item) => { const raw = item.Status ?? item.status; return STATUS_MAP[raw] ?? { label: `Estado ${raw}`, css: 'approved' } }

const getStudentNames = (item) => {
    const s = item.StudentNames ?? item.studentNames ?? item.Students ?? item.students ?? []
    if (Array.isArray(s)) return s.length > 0 ? s.join(', ') : 'Sem alunos inscritos'
    if (typeof s === 'string' && s.trim()) return s
    return 'Sem alunos inscritos'
}

const overlaps = (a, b) => {
    const aS = getDateValue(a, 'StartDatetime', 'startDatetime')
    const aE = getDateValue(a, 'EndDatetime', 'endDatetime')
    const bS = getDateValue(b, 'StartDatetime', 'startDatetime')
    const bE = getDateValue(b, 'EndDatetime', 'endDatetime')
    if (!aS || !aE || !bS || !bE) return false
    return minsFromStart(aS) < minsFromStart(bE) && minsFromStart(bS) < minsFromStart(aE)
}

const withOverlapLayout = (items) => {
    const sorted = [...items].sort((a, b) =>
        new Date(getDateValue(a, 'StartDatetime', 'startDatetime')) -
        new Date(getDateValue(b, 'StartDatetime', 'startDatetime'))
    )
    return sorted.map((item) => {
        const overlapping = sorted.filter((o) => overlaps(item, o))
        const overlapTotal = Math.max(overlapping.length, 1)
        const overlapIndex = Math.max(overlapping.indexOf(item), 0)
        return { item, overlapIndex, overlapTotal }
    })
}

const eventStyle = (item, overlapIndex, overlapTotal) => {
    const start = getDateValue(item, 'StartDatetime', 'startDatetime')
    const end = getDateValue(item, 'EndDatetime', 'endDatetime')
    const startMin = minsFromStart(start)
    const endMin = minsFromStart(end)
    const offsetMin = startMin - HOUR_START * 60
    const durationMin = Math.max(endMin - startMin, 30)
    const top = (offsetMin / 60) * HOUR_HEIGHT
    const height = Math.max((durationMin / 60) * HOUR_HEIGHT, 34)
    const width = 100 / overlapTotal
    const left = width * overlapIndex
    return { top: `${top}px`, height: `${height}px`, left: `${left}%`, width: `calc(${width}% - 4px)` }
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function ClassDetailPopover({ item, showCoach, onClose }) {
    const start = getDateValue(item, 'StartDatetime', 'startDatetime')
    const end = getDateValue(item, 'EndDatetime', 'endDatetime')
    const status = statusOf(item)

    return (
        <div className="sa-detail-backdrop" onClick={onClose}>
            <div className="sa-detail-popover" onClick={(e) => e.stopPropagation()}>
                <button type="button" className="sa-detail-close" onClick={onClose}>×</button>

                <h3>Informações do Coaching</h3>

                <div className="sa-detail-main">
                    <p><strong>Modalidade:</strong> {getModality(item)}</p>

                    {showCoach && (
                        <p><strong>Professor:</strong> {getCoachName(item)}</p>
                    )}

                    <p><strong>Estúdio:</strong> {getStudioName(item)}</p>
                    <p><strong>Aluno(s):</strong>  {getStudentNames(item)}</p>
                    <p><strong>Data:</strong>    {formatDate(start)}</p>
                    <p><strong>Horário:</strong> {formatTime(start)} - {formatTime(end)}</p>

                    <p>
                        <strong>Vagas:</strong>{' '}
                        {getField(item, 'CurrentParticipants', 'currentParticipants')}
                        {' / '}
                        {getField(item, 'MaxParticipants', 'maxParticipants')}
                    </p>

                    <p>
                        <strong>Estado:</strong>{' '}
                        <span className={`sa-status-badge ${status.css}`}>{status.label}</span>
                    </p>
                </div>
            </div>
        </div>
    )
}

// ─── Main component ───────────────────────────────────────────────────────────

/**
 * AgendaCalendar — weekly timeline calendar shared by Staff and Coach pages.
 *
 * Props:
 *   title       {string}   Page heading shown at the top left.
 *   subtitle    {string}   Small description below the heading.
 *   fetchFn     {Function} Async (from, to) => items[]. Called on every week change.
 *   showCoach   {boolean}  Whether to show the coach filter column and detail field.
 *                          Staff = true, Coach = false (default).
 *   extraFilters {node}    Optional extra filter controls rendered after the built-in ones.
 */
function AgendaCalendar({ title, subtitle, fetchFn, showCoach = false, extraFilters }) {
    const [mondayDate, setMondayDate] = useState(() => weekStart(new Date()))
    const [items, setItems] = useState([])
    const [filterModality, setFilterModality] = useState('')
    const [filterStudio, setFilterStudio] = useState('')
    const [filterCoach, setFilterCoach] = useState('')
    const [loading, setLoading] = useState(true)
    const [pageError, setPageError] = useState(null)
    const [selectedClass, setSelectedClass] = useState(null)

    const weekDates = useMemo(() => WEEKDAYS.map((_, i) => addDays(mondayDate, i)), [mondayDate])
    const weekLabel = useMemo(() => `${formatDate(weekDates[0])} - ${formatDate(weekDates[6])}`, [weekDates])
    const hours = useMemo(() => Array.from({ length: TOTAL_HOURS + 1 }, (_, i) => HOUR_START + i), [])

    // Fetch on week change
    useEffect(() => {
        let cancelled = false

        async function load() {
            setLoading(true)
            setPageError(null)
            try {
                const response = await fetchFn(isoOf(weekDates[0]), isoOf(weekDates[6]))
                if (cancelled) return
                const list = Array.isArray(response)
                    ? response
                    : response?.items ?? response?.Items ?? []
                setItems(list)
            } catch (err) {
                if (!cancelled) { setPageError(err.message); setItems([]) }
            } finally {
                if (!cancelled) setLoading(false)
            }
        }

        load()
        return () => { cancelled = true }
    }, [mondayDate, fetchFn])

    // Filter options derived from loaded data
    const modalityOptions = useMemo(() => [...new Set(items.map(getModality).filter((n) => n !== '—'))].sort((a, b) => a.localeCompare(b, 'pt-PT')), [items])
    const studioOptions = useMemo(() => [...new Set(items.map(getStudioName).filter((n) => n !== '—'))].sort((a, b) => a.localeCompare(b, 'pt-PT')), [items])
    const coachOptions = useMemo(() => [...new Set(items.map(getCoachName).filter((n) => n !== '—'))].sort((a, b) => a.localeCompare(b, 'pt-PT')), [items])

    const filteredItems = useMemo(() => items.filter((item) => {
        if (filterModality && getModality(item) !== filterModality) return false
        if (filterStudio && getStudioName(item) !== filterStudio) return false
        if (filterCoach && getCoachName(item) !== filterCoach) return false
        return true
    }), [items, filterModality, filterStudio, filterCoach])

    const itemsByDate = useMemo(() => {
        const map = {}
        filteredItems.forEach((item) => {
            const start = getDateValue(item, 'StartDatetime', 'startDatetime')
            if (!start) return
            const iso = isoOf(start)
            if (!map[iso]) map[iso] = []
            map[iso].push(item)
        })
        return map
    }, [filteredItems])

    const hasFilters = filterModality || filterStudio || filterCoach
    const clearFilters = () => { setFilterModality(''); setFilterStudio(''); setFilterCoach('') }

    if (pageError) {
        return (
            <section className="dashboard-page-card">
                <h2>{title}</h2>
                <p className="sa-error">{pageError}</p>
            </section>
        )
    }

    return (
        <section className="dashboard-page-card">
            <div className="sa-timeline-card">

                {/* ── Header ── */}
                <div className="sa-timeline-header">
                    <div>
                        <h2>{title}</h2>
                        {subtitle && <p>{subtitle}</p>}
                        <strong>{weekLabel}</strong>
                    </div>

                    <div className="sa-timeline-actions">
                        {/* Modality filter */}
                        <select value={filterModality} onChange={(e) => setFilterModality(e.target.value)} className="sa-select">
                            <option value="">Todas as modalidades</option>
                            {modalityOptions.map((m) => <option key={m} value={m}>{m}</option>)}
                        </select>

                        {/* Studio filter */}
                        <select value={filterStudio} onChange={(e) => setFilterStudio(e.target.value)} className="sa-select">
                            <option value="">Todos os estúdios</option>
                            {studioOptions.map((s) => <option key={s} value={s}>{s}</option>)}
                        </select>

                        {/* Coach filter — only for staff */}
                        {showCoach && (
                            <select value={filterCoach} onChange={(e) => setFilterCoach(e.target.value)} className="sa-select">
                                <option value="">Todos os professores</option>
                                {coachOptions.map((c) => <option key={c} value={c}>{c}</option>)}
                            </select>
                        )}

                        {/* Extra filters slot */}
                        {extraFilters}

                        {/* Clear */}
                        {hasFilters && (
                            <button type="button" className="sa-clear-btn" onClick={clearFilters}>
                                Limpar
                            </button>
                        )}

                        {/* Week navigation */}
                        <button type="button" className="sa-timeline-nav-btn" onClick={() => setMondayDate((d) => addDays(d, -7))} title="Semana anterior">‹</button>
                        <button type="button" className="sa-timeline-nav-btn" onClick={() => setMondayDate((d) => addDays(d, 7))} title="Semana seguinte">›</button>
                    </div>
                </div>

                {/* ── Timeline grid ── */}
                <div className="sa-timeline-scroll">
                    <div className="sa-timeline-grid">
                        <div className="sa-timeline-corner" />

                        {weekDates.map((date, i) => {
                            const isToday = isoOf(date) === isoOf(new Date())
                            return (
                                <div key={isoOf(date)} className={`sa-timeline-day-head ${isToday ? 'today' : ''}`}>
                                    <span>{WEEKDAYS[i].short}</span>
                                    <strong>{date.getDate()}</strong>
                                </div>
                            )
                        })}

                        <div className="sa-timeline-hours">
                            {hours.map((h) => (
                                <div key={h} className="sa-timeline-hour-label" style={{ height: `${HOUR_HEIGHT}px` }}>
                                    {pad(h)}:00
                                </div>
                            ))}
                        </div>

                        {weekDates.map((date) => {
                            const iso = isoOf(date)
                            const positionedItems = withOverlapLayout(itemsByDate[iso] || [])

                            return (
                                <div key={iso} className="sa-timeline-day-col">
                                    {hours.map((h) => (
                                        <div key={h} className="sa-timeline-hour-line" style={{ height: `${HOUR_HEIGHT}px` }} />
                                    ))}

                                    {!loading && positionedItems.map(({ item, overlapIndex, overlapTotal }) => {
                                        const id = item.Id ?? item.id
                                        const status = statusOf(item)
                                        const start = getDateValue(item, 'StartDatetime', 'startDatetime')
                                        const end = getDateValue(item, 'EndDatetime', 'endDatetime')

                                        return (
                                            <button
                                                key={id}
                                                type="button"
                                                className={`sa-timeline-event ${status.css}`}
                                                style={eventStyle(item, overlapIndex, overlapTotal)}
                                                onClick={(e) => { e.preventDefault(); e.stopPropagation(); setSelectedClass(item) }}
                                            >
                                                <span>{formatTime(start)} - {formatTime(end)}</span>
                                                <strong>{getModality(item)}</strong>
                                                <small>{getStudioName(item)}</small>
                                                {showCoach && <small>{getCoachName(item)}</small>}
                                            </button>
                                        )
                                    })}
                                </div>
                            )
                        })}
                    </div>
                </div>

                {loading && <p className="sa-loading">A carregar agenda...</p>}
            </div>

            {/* ── Detail popover ── */}
            {selectedClass && (
                <ClassDetailPopover
                    item={selectedClass}
                    showCoach={showCoach}
                    onClose={() => setSelectedClass(null)}
                />
            )}
        </section>
    )
}

export default AgendaCalendar