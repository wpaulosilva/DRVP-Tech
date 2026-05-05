import Button from '../../../components/common/Button'
import StatusBadge from '../../../components/common/StatusBadge'

function UsersTable({
    users,
    loading,
    onEdit,
    onActivate,
    onDeactivate,
    onSort,
    sortBy,
    sortDir,
}) {
    const renderSortIcon = (field) => {
        if (sortBy !== field) return ''
        return sortDir === 'asc' ? ' ▲' : ' ▼'
    }

    return (
        <div className="table-wrap">
            <table className="app-table">
                <thead>
                    <tr>
                        <th onClick={() => onSort?.('name')} style={{ cursor: 'pointer' }}>
                            Nome{renderSortIcon('name')}
                        </th>

                        <th onClick={() => onSort?.('email')} style={{ cursor: 'pointer' }}>
                            Email{renderSortIcon('email')}
                        </th>

                        <th onClick={() => onSort?.('role')} style={{ cursor: 'pointer' }}>
                            Tipo{renderSortIcon('role')}
                        </th>

                        <th onClick={() => onSort?.('status')} style={{ cursor: 'pointer' }}>
                            Estado{renderSortIcon('status')}
                        </th>

                        <th>Ações</th>
                    </tr>
                </thead>

                <tbody>
                    {loading ? (
                        <tr>
                            <td colSpan={5} className="table-empty">
                                A carregar...
                            </td>
                        </tr>
                    ) : users.length === 0 ? (
                        <tr>
                            <td colSpan={5} className="table-empty">
                                Nenhum utilizador encontrado.
                            </td>
                        </tr>
                    ) : (
                        users.map((u) => (
                            <tr key={u.userId ?? u.staffId}>
                                <td>
                                    {u.name ||
                                        `${u.personInfo?.firstName ?? ''} ${u.personInfo?.lastName ?? ''}`.trim() ||
                                        '—'}
                                </td>

                                <td>{u.email || '—'}</td>

                                <td>{u.role || '—'}</td>

                                <td>
                                    <StatusBadge active={u.isActive} />
                                </td>

                                <td className="table-actions">
                                    {u.isActive ? (
                                        <>
                                            {onEdit && (
                                                <Button
                                                    variant="secondary"
                                                    onClick={() => onEdit(u)}
                                                >
                                                    Editar
                                                </Button>
                                            )}

                                            <Button
                                                variant="danger"
                                                onClick={() => onDeactivate?.(u.userId ?? u.staffId)}
                                            >
                                                Desativar
                                            </Button>
                                        </>
                                    ) : (
                                        <Button
                                            variant="secondary"
                                            onClick={() =>
                                                onActivate?.(u.userId ?? u.staffId)
                                            }
                                        >
                                            Reativar
                                        </Button>
                                    )}
                                </td>
                            </tr>
                        ))
                    )}
                </tbody>
            </table>
        </div>
    )
}

export default UsersTable