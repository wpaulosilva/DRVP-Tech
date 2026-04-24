import './DataTable.css'

function DataTable({
    columns = [],
    rows = [],
    loading = false,
    emptyMessage = 'Sem resultados.',
}) {
    return (
        <div className="table-wrap">
            <table className="app-table data-table">
                <thead>
                    <tr>
                        {columns.map((col) => (
                            <th key={col.key}>{col.label}</th>
                        ))}
                    </tr>
                </thead>

                <tbody>
                    {loading ? (
                        <tr>
                            <td colSpan={columns.length} className="table-empty">
                                A carregar...
                            </td>
                        </tr>
                    ) : rows.length === 0 ? (
                        <tr>
                            <td colSpan={columns.length} className="table-empty">
                                {emptyMessage}
                            </td>
                        </tr>
                    ) : (
                        rows.map((row, i) => (
                            <tr key={row.id ?? i}>
                                {columns.map((col) => (
                                    <td key={col.key}>
                                        {col.render
                                            ? col.render(row)
                                            : row[col.key] ?? '—'}
                                    </td>
                                ))}
                            </tr>
                        ))
                    )}
                </tbody>
            </table>
        </div>
    )
}

export default DataTable
