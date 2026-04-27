import Button from '../../../components/common/Button'
import Input from '../../../components/common/Input'
import Modal from '../../../components/common/Modal'

function CreateUserModal({
    open,
    title,
    description,
    email,
    onEmailChange,
    firstName,
    lastName,
    birthDate,
    nif,
    username,
    onFirstNameChange,
    onLastNameChange,
    onBirthDateChange,
    onNifChange,
    onUsernameChange,
    onClose,
    onConfirm,
    error,
    loading,
}) {
    return (
        <Modal open={open} title={title} onClose={onClose}>
            <p>{description}</p>

            {/* Email + Data */}
            <div className="form-row">
                <div>
                    <label htmlFor="newEmail">Email *</label>
                    <Input
                        id="newEmail"
                        type="email"
                        value={email}
                        placeholder="user@entartes.pt"
                        onChange={onEmailChange}
                    />
                </div>

                <div>
                    <label htmlFor="birthDate">Data de Nascimento *</label>
                    <Input
                        id="birthDate"
                        type="date"
                        value={birthDate}
                        onChange={onBirthDateChange}
                    />
                </div>
            </div>

            {/* Nome + Apelido */}
            <div className="form-row">
                <div>
                    <label htmlFor="firstName">Nome *</label>
                    <Input
                        id="firstName"
                        type="text"
                        value={firstName}
                        placeholder="Primeiro nome"
                        onChange={onFirstNameChange}
                    />
                </div>

                <div>
                    <label htmlFor="lastName">Apelido *</label>
                    <Input
                        id="lastName"
                        type="text"
                        value={lastName}
                        placeholder="Apelido"
                        onChange={onLastNameChange}
                    />
                </div>
            </div>

            {/* NIF + Username */}
            <div className="form-row">
                <div>
                    <label htmlFor="nif">NIF *</label>
                    <Input
                        id="nif"
                        type="text"
                        value={nif}
                        placeholder="123456789"
                        onChange={onNifChange}
                    />
                </div>

                <div>
                    <label htmlFor="username">Username (opcional)</label>
                    <Input
                        id="username"
                        type="text"
                        value={username}
                        placeholder="user"
                        onChange={onUsernameChange}
                    />
                </div>
            </div>

            {error && <p className="admin-modal-error">{error}</p>}

            <div className="modal-actions">
                <Button variant="secondary" onClick={onClose}>
                    Cancelar
                </Button>

                <Button
                    variant="primary"
                    onClick={onConfirm}
                    disabled={loading}
                >
                    {loading ? 'A criar...' : 'Criar Conta'}
                </Button>
            </div>
        </Modal>
    )
}

export default CreateUserModal