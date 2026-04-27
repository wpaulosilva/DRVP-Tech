import { useCallback, useEffect, useState } from 'react'
import PageCard from '../../components/common/PageCard'
import Button from '../../components/common/Button'
import Input from '../../components/common/Input'
import UsersTable from '../../features/users/components/UsersTable'
import CreateUserModal from '../../features/users/components/CreateUserModal'
import {
    getAdminUsers,
    createUser,
    activateUser,
    deactivateUser,
} from '../../services/usersService'
import '../../styles/AdminPage.css'

const PAGE_SIZE = 7

function AdminUsersPage() {
    const [users, setUsers] = useState([])
    const [total, setTotal] = useState(0)
    const [page, setPage] = useState(1)
    const [search, setSearch] = useState('')
    const [debouncedSearch, setDebouncedSearch] = useState('')
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')
    const [sortBy, setSortBy] = useState('')
    const [sortDir, setSortDir] = useState('asc')

    const [showModal, setShowModal] = useState(false)
    const [newEmail, setNewEmail] = useState('')
    const [newFirstName, setNewFirstName] = useState('')
    const [newLastName, setNewLastName] = useState('')
    const [newBirthDate, setNewBirthDate] = useState('')
    const [newNif, setNewNif] = useState('')
    const [newUsername, setNewUsername] = useState('')
    const [modalError, setModalError] = useState('')
    const [creating, setCreating] = useState(false)

    const fetchUsersData = useCallback(async (currentPage, currentSearch) => {
        setLoading(true)
        setError('')

        try {
            const data = await getAdminUsers({
                page: currentPage,
                pageSize: PAGE_SIZE,
                search: currentSearch,
                sortBy,
                sortDir,
            })

            setUsers(data.items ?? [])
            setTotal(data.totalCount ?? 0)
        } catch (err) {
            setError(err.message)
        } finally {
            setLoading(false)
        }
    }, [sortBy, sortDir])

    useEffect(() => {
        const timer = setTimeout(() => {
            setDebouncedSearch(search)
            setPage(1)
        }, 350)

        return () => clearTimeout(timer)
    }, [search])

    useEffect(() => {
        fetchUsersData(page, debouncedSearch)
    }, [page, debouncedSearch, fetchUsersData])

    const handleCreate = async () => {
        if (!newEmail.trim()) {
            setModalError('O email é obrigatório.')
            return
        }
        if (!newFirstName.trim() || !newLastName.trim()) {
            setModalError('Nome e apelido são obrigatórios.')
            return
        }
        if (!newBirthDate) {
            setModalError('Data de nascimento é obrigatória.')
            return
        }
        if (!newNif.trim()) {
            setModalError('NIF é obrigatório.')
            return
        }

        setCreating(true)
        setModalError('')

        try {
            await createUser({
                email: newEmail.trim(),
                username: newUsername.trim() || undefined,
                firstRole: 1,
                personInfo: {
                    firstName: newFirstName.trim(),
                    lastName: newLastName.trim(),
                    birthDate: newBirthDate,
                    nif: newNif.trim(),
                }
            })

            setShowModal(false)
            setNewEmail('')
            setNewFirstName('')
            setNewLastName('')
            setNewBirthDate('')
            setNewNif('')
            setNewUsername('')
            setPage(1)
            fetchUsersData(1, debouncedSearch)
        } catch (err) {
            setModalError(err.message)
        } finally {
            setCreating(false)
        }
    }

    const handleActivate = async (userId) => {
        if (!window.confirm('Reativar esta conta?')) return

        try {
            await activateUser(userId)
            fetchUsersData(page, debouncedSearch)
        } catch (err) {
            alert(err.message)
        }
    }

    const handleDeactivate = async (userId) => {
        if (!window.confirm('Desativar esta conta?')) return

        try {
            await deactivateUser(userId)
            fetchUsersData(page, debouncedSearch)
        } catch (err) {
            alert(err.message)
        }
    }

    const openModal = () => {
        setNewEmail('')
        setModalError('')
        setShowModal(true)
    }

    const from = total === 0 ? 0 : (page - 1) * PAGE_SIZE + 1
    const to = Math.min(page * PAGE_SIZE, total)

    const handleSort = (field) => {
        setPage(1)

        if (sortBy === field) {
            setSortDir((current) => current === 'asc' ? 'desc' : 'asc')
        } else {
            setSortBy(field)
            setSortDir('asc')
        }
    }

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Contas de Direção</h2>
                    <p>Criar e gerir contas de utilizadores da Direção.</p>
                </div>

                <Button variant="primary" onClick={openModal}>
                    + Nova Conta
                </Button>
            </div>

            <div className="search-bar">
                <Input
                    type="text"
                    value={search}
                    placeholder="Pesquisar por nome ou email..."
                    onChange={setSearch}
                />
            </div>

            {error && <p className="admin-error">{error}</p>}

            <UsersTable
                users={users}
                loading={loading}
                onEdit={(user) => console.log('editar', user.userId)}
                onActivate={handleActivate}
                onDeactivate={handleDeactivate}
                onSort={handleSort}
                sortBy={sortBy}
                sortDir={sortDir}
            />

            <div className="pagination">
                <span className="pag-info">
                    {total > 0 ? `${from}–${to} de ${total}` : ''}
                </span>

                <div className="pag-btns">
                    <Button
                        variant="secondary"
                        onClick={() => setPage((p) => p - 1)}
                        disabled={page <= 1}
                    >
                        ‹ Anterior
                    </Button>

                    <Button
                        variant="secondary"
                        onClick={() => setPage((p) => p + 1)}
                        disabled={page * PAGE_SIZE >= total}
                    >
                        Próxima ›
                    </Button>
                </div>
            </div>

            <CreateUserModal
                open={showModal}
                title="Nova Conta de Direção"
                description="Criar uma nova conta com acesso de Direção."
                email={newEmail}
                onEmailChange={(value) => {
                    setNewEmail(value)
                    setNewUsername(value.split('@')[0])
                }}                firstName={newFirstName}
                lastName={newLastName}
                birthDate={newBirthDate}
                nif={newNif}
                username={newUsername}
                onFirstNameChange={setNewFirstName}
                onLastNameChange={setNewLastName}
                onBirthDateChange={setNewBirthDate}
                onNifChange={setNewNif}
                onUsernameChange={setNewUsername}
                onClose={() => setShowModal(false)}
                onConfirm={handleCreate}
                error={modalError}
                loading={creating}
            />
        </PageCard>
    )
}

export default AdminUsersPage