function KpiCard({ label, value, loading = false, tone = 'purple' }) {
    return (
        <div className={`kpi-card kpi-card-${tone}`}>
            <span className="kpi-label">{label}</span>
            <span className="kpi-value">
                {loading ? '—' : value ?? '—'}
            </span>
        </div>
    )
}

export default KpiCard