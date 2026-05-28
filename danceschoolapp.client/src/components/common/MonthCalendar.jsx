// Shared month-grid calendar component used by parent and coach class creation pages.

export function isoDate(d) {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function getMonthRange(date) {
    const y = date.getFullYear(), m = date.getMonth()
    return { from: isoDate(new Date(y, m, 1)), to: isoDate(new Date(y, m + 1, 0)) }
}

export function fmtMonthLabel(date) {
    return date.toLocaleDateString('pt-PT', { month: 'long', year: 'numeric' })
}

export function fmtDateLong(iso) {
    if (!iso) return ''
    try { return new Date(iso + 'T00:00:00').toLocaleDateString('pt-PT', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }) }
    catch { return iso }
}

const DAYS_PT = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']

export default function MonthCalendar({ month, onPrev, onNext, renderDay, loading }) {
    const year = month.getFullYear()
    const mon  = month.getMonth()
    const daysInMonth = new Date(year, mon + 1, 0).getDate()
    const firstDow    = new Date(year, mon, 1).getDay()

    return (
        <div className="pc-card">
            <div className="pc-cal-header">
                <h3 className="pc-cal-title">{fmtMonthLabel(month)}</h3>
                <div className="pc-cal-nav">
                    <button className="pc-cal-nav-btn" onClick={onPrev} aria-label="Mês anterior">‹</button>
                    <button className="pc-cal-nav-btn" onClick={onNext} aria-label="Próximo mês">›</button>
                </div>
            </div>
            {loading ? (
                <div className="validate-empty"><p>Carregando...</p></div>
            ) : (
                <div className="pc-month-grid">
                    {DAYS_PT.map(d => <div key={d} className="pc-dow-label">{d}</div>)}
                    {Array.from({ length: firstDow }, (_, i) => (
                        <div key={`e${i}`} className="pc-day-cell pc-day-cell--empty" />
                    ))}
                    {Array.from({ length: daysInMonth }, (_, i) => {
                        const key = isoDate(new Date(year, mon, i + 1))
                        return renderDay(key, i + 1)
                    })}
                </div>
            )}
        </div>
    )
}
