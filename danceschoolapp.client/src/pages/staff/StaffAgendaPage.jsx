import { useEffect, useMemo, useState } from 'react'
import '../../styles/StaffAgenda.css'
import { getAgenda } from '../../services/staffService'
import { getStudios } from '../../services/studiosService'

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

const pad = (n) => String(n).padStart(2, '0')

const isoOf = (date) => {
    const d = new Date(date)
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

const weekStart = (date) => {
    const d = new Date(date)
    const day = d.getDay() || 7

    d.setDate(d.getDate() - (day - 1))
    d.setHours(0, 0, 0, 0)

    return d
}

const addDays = (date, days) => {
    const d = new Date(date)
    d.setDate(d.getDate() + days)
    return d
}

const formatTime = (date) => {
    if (!date) return '—'

    const d = new Date(date)
    return `${pad(d.getHours())}:${pad(d.getMinutes())}`
}

const formatDate = (date) => {
    if (!date) return '—'

    const d = new Date(date)
    return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`
}

const minutesFromDayStart = (date) => {
    const d = new Date(date)
    return d.getHours() * 60 + d.getMinutes()
}

const getField = (item, ...keys) => {
    for (const key of keys) {
        if (item?.[key] !== undefined && item?.[key] !== null && item?.[key] !== '') {
            return item[key]
        }
    }

    return '—'
}

const getDateValue = (item, ...keys) => {
    for (const key of keys) {
        if (item?.[key]) return item[key]
    }

    return null
}

const getStudioName = (item) =>
    getField(item, 'StudioName', 'studioName', 'Studio', 'studio')

const getCoachName = (item) =>
    getField(item, 'CoachName', 'coachName', 'ProfessorName', 'professorName')

const getModalityName = (item) =>
    getField(
        item,
        'ModalityName',
        'modalityName',
        'ClassName',
        'className',
        'Name',
        'name',
    )

const getStudentNames = (item) => {
    const students =
        item.StudentNames ??
        item.studentNames ??
        item.Students ??
        item.students ??
        []

    if (Array.isArray(students)) {
        return students.length > 0 ? students.join(', ') : 'Sem alunos inscritos'
    }

    if (typeof students === 'string' && students.trim()) {
        return students
    }

    return 'Sem alunos inscritos'
}

const statusOf = (item) => {
    const raw = item.Status ?? item.status
    return STATUS_MAP[raw] ?? { label: `Estado ${raw}`, css: 'approved' }
}

const overlaps = (a, b) => {
    const aStart = getDateValue(a, 'StartDatetime', 'startDatetime')
    const aEnd = getDateValue(a, 'EndDatetime', 'endDatetime')
    const bStart = getDateValue(b, 'StartDatetime', 'startDatetime')
    const bEnd = getDateValue(b, 'EndDatetime', 'endDatetime')

    if (!aStart || !aEnd || !bStart || !bEnd) return false

    const aStartMin = minutesFromDayStart(aStart)
    const aEndMin = minutesFromDayStart(aEnd)
    const bStartMin = minutesFromDayStart(bStart)
    const bEndMin = minutesFromDayStart(bEnd)

    return aStartMin < bEndMin && bStartMin < aEndMin
}

const withOverlapLayout = (items) => {
    const sorted = [...items].sort((a, b) => {
        const aStart = new Date(getDateValue(a, 'StartDatetime', 'startDatetime'))
        const bStart = new Date(getDateValue(b, 'StartDatetime', 'startDatetime'))

        return aStart - bStart
    })

    return sorted.map((item) => {
        const overlapping = sorted.filter((other) => overlaps(item, other))
        const overlapTotal = Math.max(overlapping.length, 1)
        const overlapIndex = Math.max(
            overlapping.findIndex((other) => other === item),
            0,
        )

        return {
            item,
            overlapIndex,
            overlapTotal,
        }
    })
}

function StaffAgendaPage() {
    const [mondayDate, setMondayDate] = useState(() => weekStart(new Date()))
    const [items, setItems] = useState([])
    const [studios, setStudios] = useState([])
    const [filterStudio, setFilterStudio] = useState('')
    const [filterModality, setFilterModality] = useState('')
    const [filterCoach, setFilterCoach] = useState('')
    const [loading, setLoading] = useState(true)
    const [pageError, setPageError] = useState(null)
    const [selectedClass, setSelectedClass] = useState(null)

    const weekDates = useMemo(
        () => WEEKDAYS.map((_, index) => addDays(mondayDate, index)),
        [mondayDate],
    )

    const weekLabel = useMemo(() => {
        return `${formatDate(weekDates[0])} - ${formatDate(weekDates[6])}`
    }, [weekDates])

    const hours = useMemo(
        () => Array.from({ length: TOTAL_HOURS + 1 }, (_, index) => HOUR_START + index),
        [],
    )

    useEffect(() => {
        getStudios()
            .then((data) => {
                const list = Array.isArray(data)
                    ? data
                    : data?.items ?? data?.Items ?? []

                setStudios(list)
            })
            .catch(() => {
                setStudios([])
            })
    }, [])

    useEffect(() => {
        async function loadAgenda() {
            setLoading(true)
            setPageError(null)

            try {
                const response = await getAgenda({
                    from: isoOf(weekDates[0]),
                    to: isoOf(weekDates[6]),
                })

                const list = Array.isArray(response)
                    ? response
                    : response?.items ?? response?.Items ?? []

                setItems(list)
            } catch (error) {
                setPageError(error.message)
                setItems([])
            } finally {
                setLoading(false)
            }
        }

        loadAgenda()
    }, [mondayDate])

    const studioOptions = useMemo(() => {
        const values = new Set()

        studios.forEach((studio) => {
            const name = studio.Name ?? studio.name
            if (name) values.add(name)
        })

        items.forEach((item) => {
            const name = getStudioName(item)
            if (name !== '—') values.add(name)
        })

        return [...values].sort((a, b) => a.localeCompare(b, 'pt-PT'))
    }, [studios, items])

    const modalityOptions = useMemo(() => {
        const values = new Set()

        items.forEach((item) => {
            const name = getModalityName(item)
            if (name !== '—') values.add(name)
        })

        return [...values].sort((a, b) => a.localeCompare(b, 'pt-PT'))
    }, [items])

    const coachOptions = useMemo(() => {
        const values = new Set()

        items.forEach((item) => {
            const name = getCoachName(item)
            if (name !== '—') values.add(name)
        })

        return [...values].sort((a, b) => a.localeCompare(b, 'pt-PT'))
    }, [items])

    const filteredItems = useMemo(() => {
        return items.filter((item) => {
            const studioMatches =
                !filterStudio || getStudioName(item) === filterStudio

            const modalityMatches =
                !filterModality || getModalityName(item) === filterModality

            const coachMatches =
                !filterCoach || getCoachName(item) === filterCoach

            return studioMatches && modalityMatches && coachMatches
        })
    }, [items, filterStudio, filterModality, filterCoach])

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

    const eventStyle = (item, overlapIndex, overlapTotal) => {
        const start = getDateValue(item, 'StartDatetime', 'startDatetime')
        const end = getDateValue(item, 'EndDatetime', 'endDatetime')

        const startMin = minutesFromDayStart(start)
        const endMin = minutesFromDayStart(end)

        const offsetMin = startMin - HOUR_START * 60
        const durationMin = Math.max(endMin - startMin, 30)

        const top = (offsetMin / 60) * HOUR_HEIGHT
        const height = Math.max((durationMin / 60) * HOUR_HEIGHT, 34)

        const width = 100 / overlapTotal
        const left = width * overlapIndex

        return {
            top: `${top}px`,
            height: `${height}px`,
            left: `${left}%`,
            width: `calc(${width}% - 4px)`,
        }
    }

    const clearFilters = () => {
        setFilterStudio('')
        setFilterModality('')
        setFilterCoach('')
    }

    if (pageError) {
        return (
            <section className="dashboard-page-card">
                <h2>Agenda Global</h2>
                <p className="sa-error">{pageError}</p>
            </section>
        )
    }

    return (
        <section className="dashboard-page-card">
            <div className="sa-timeline-card">
                <div className="sa-timeline-header">
                    <div>
                        <h2>Agenda Global</h2>
                        <p>Visualize todas as aulas marcadas na semana.</p>
                        <strong>{weekLabel}</strong>
                    </div>

                    <div className="sa-timeline-actions">
                        <select
                            value={filterStudio}
                            onChange={(event) => setFilterStudio(event.target.value)}
                            className="sa-select"
                        >
                            <option value="">Todos os estúdios</option>

                            {studioOptions.map((studio) => (
                                <option key={studio} value={studio}>
                                    {studio}
                                </option>
                            ))}
                        </select>

                        <select
                            value={filterModality}
                            onChange={(event) => setFilterModality(event.target.value)}
                            className="sa-select"
                        >
                            <option value="">Todas as modalidades</option>

                            {modalityOptions.map((modality) => (
                                <option key={modality} value={modality}>
                                    {modality}
                                </option>
                            ))}
                        </select>

                        <select
                            value={filterCoach}
                            onChange={(event) => setFilterCoach(event.target.value)}
                            className="sa-select"
                        >
                            <option value="">Todos os professores</option>

                            {coachOptions.map((coach) => (
                                <option key={coach} value={coach}>
                                    {coach}
                                </option>
                            ))}
                        </select>

                        {(filterStudio || filterModality || filterCoach) && (
                            <button
                                type="button"
                                className="sa-clear-btn"
                                onClick={clearFilters}
                            >
                                Limpar
                            </button>
                        )}

                        <button
                            type="button"
                            className="sa-timeline-nav-btn"
                            onClick={() => setMondayDate((date) => addDays(date, -7))}
                            title="Semana anterior"
                        >
                            ‹
                        </button>

                        <button
                            type="button"
                            className="sa-timeline-nav-btn"
                            onClick={() => setMondayDate((date) => addDays(date, 7))}
                            title="Semana seguinte"
                        >
                            ›
                        </button>
                    </div>
                </div>

                <div className="sa-timeline-scroll">
                    <div className="sa-timeline-grid">
                        <div className="sa-timeline-corner" />

                        {weekDates.map((date, index) => {
                            const isToday = isoOf(date) === isoOf(new Date())

                            return (
                                <div
                                    key={isoOf(date)}
                                    className={`sa-timeline-day-head ${isToday ? 'today' : ''}`}
                                >
                                    <span>{WEEKDAYS[index].short}</span>
                                    <strong>{date.getDate()}</strong>
                                </div>
                            )
                        })}

                        <div className="sa-timeline-hours">
                            {hours.map((hour) => (
                                <div
                                    key={hour}
                                    className="sa-timeline-hour-label"
                                    style={{ height: `${HOUR_HEIGHT}px` }}
                                >
                                    {pad(hour)}:00
                                </div>
                            ))}
                        </div>

                        {weekDates.map((date) => {
                            const iso = isoOf(date)
                            const dayItems = itemsByDate[iso] || []
                            const positionedItems = withOverlapLayout(dayItems)

                            return (
                                <div key={iso} className="sa-timeline-day-col">
                                    {hours.map((hour) => (
                                        <div
                                            key={hour}
                                            className="sa-timeline-hour-line"
                                            style={{ height: `${HOUR_HEIGHT}px` }}
                                        />
                                    ))}

                                    {!loading &&
                                        positionedItems.map(({ item, overlapIndex, overlapTotal }) => {
                                            const id = item.Id ?? item.id
                                            const status = statusOf(item)
                                            const start = getDateValue(item, 'StartDatetime', 'startDatetime')
                                            const end = getDateValue(item, 'EndDatetime', 'endDatetime')
                                            const modality = getModalityName(item)
                                            const studio = getStudioName(item)
                                            const coach = getCoachName(item)

                                            return (
                                                <button
                                                    key={id}
                                                    type="button"
                                                    className={`sa-timeline-event ${status.css}`}
                                                    style={eventStyle(item, overlapIndex, overlapTotal)}
                                                    onClick={(event) => {
                                                        event.preventDefault()
                                                        event.stopPropagation()
                                                        setSelectedClass(item)
                                                    }}
                                                >
                                                    <span>
                                                        {formatTime(start)} - {formatTime(end)}
                                                    </span>
                                                    <strong>{modality}</strong>
                                                    <small>{studio}</small>
                                                    <small>{coach}</small>
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

            {selectedClass && (
                <div className="sa-detail-backdrop" onClick={() => setSelectedClass(null)}>
                    <div className="sa-detail-popover" onClick={(event) => event.stopPropagation()}>
                        <button
                            type="button"
                            className="sa-detail-close"
                            onClick={() => setSelectedClass(null)}
                        >
                            ×
                        </button>

                        <h3>Informações da Aula</h3>

                        <div className="sa-detail-main">
                            <p>
                                <strong>Modalidade:</strong>{' '}
                                {getModalityName(selectedClass)}
                            </p>

                            <p>
                                <strong>Professor:</strong>{' '}
                                {getCoachName(selectedClass)}
                            </p>

                            <p>
                                <strong>Estúdio:</strong>{' '}
                                {getStudioName(selectedClass)}
                            </p>

                            <p>
                                <strong>Alunos:</strong>{' '}
                                {getStudentNames(selectedClass)}
                            </p>

                            <p>
                                <strong>Data:</strong>{' '}
                                {formatDate(getDateValue(selectedClass, 'StartDatetime', 'startDatetime'))}
                            </p>

                            <p>
                                <strong>Horário:</strong>{' '}
                                {formatTime(getDateValue(selectedClass, 'StartDatetime', 'startDatetime'))}
                                {' - '}
                                {formatTime(getDateValue(selectedClass, 'EndDatetime', 'endDatetime'))}
                            </p>

                            <p>
                                <strong>Vagas:</strong>{' '}
                                {getField(selectedClass, 'CurrentParticipants', 'currentParticipants')}
                                {' / '}
                                {getField(selectedClass, 'MaxParticipants', 'maxParticipants')}
                            </p>

                            <p>
                                <strong>Estado:</strong>{' '}
                                <span className={`sa-status-badge ${statusOf(selectedClass).css}`}>
                                    {statusOf(selectedClass).label}
                                </span>
                            </p>
                        </div>
                    </div>
                </div>
            )}
        </section>
    )
}

export default StaffAgendaPage