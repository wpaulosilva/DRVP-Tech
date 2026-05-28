import { useState } from 'react'
import Button from '../../../components/common/Button'
import Input from '../../../components/common/Input'
import Modal from '../../../components/common/Modal'
import Icon from '../../../components/ui/Icon'
import '../../users/styles/CreateUserModal.css'

function AddStudentModal({
    open,
    title = 'Adicionar Novo Estudante',
    description = 'Preencha as informações do estudante.',
    confirmLabel = 'Adicionar Estudante',
    loadingLabel = 'A adicionar...',
    firstName,
    lastName,
    birthDate,
    phone,
    address,
    nif,
    modalities = [],
    selectedModalityIds = [],
    onModalityToggle,
    onFirstNameChange,
    onLastNameChange,
    onBirthDateChange,
    onPhoneChange,
    onAddressChange,
    onNifChange,
    onClose,
    onConfirm,
    error,
    loading,
}) {
    const [modalitySearch, setModalitySearch] = useState('')

    const filteredModalities = modalities.filter((m) => {
        const name = m.name ?? m.Name ?? ''
        return name.toLowerCase().includes(modalitySearch.toLowerCase())
    })

    return (
        <Modal open={open} title={title} onClose={onClose}>
            <p>{description}</p>

            <div className="form-row">
                <div>
                    <label htmlFor="studentFirstName">Primeiro Nome *</label>
                    <Input
                        id="studentFirstName"
                        type="text"
                        value={firstName}
                        placeholder="Primeiro nome do estudante"
                        onChange={onFirstNameChange}
                    />
                </div>

                <div>
                    <label htmlFor="studentLastName">Último Nome *</label>
                    <Input
                        id="studentLastName"
                        type="text"
                        value={lastName}
                        placeholder="Último nome do estudante"
                        onChange={onLastNameChange}
                    />
                </div>
            </div>

            <div className="form-row">
                <div>
                    <label htmlFor="studentBirthDate">Data de Nascimento *</label>
                    <Input
                        id="studentBirthDate"
                        type="date"
                        value={birthDate}
                        onChange={onBirthDateChange}
                    />
                </div>

                <div>
                    <label htmlFor="studentPhone">Telemóvel</label>
                    <Input
                        id="studentPhone"
                        type="text"
                        value={phone}
                        placeholder="912 345 678"
                        onChange={onPhoneChange}
                    />
                </div>
            </div>

            <div className="form-row">
                <div>
                    <label htmlFor="studentNif">NIF</label>
                    <Input
                        id="studentNif"
                        type="text"
                        value={nif}
                        placeholder="123456789"
                        onChange={onNifChange}
                    />
                </div>

                <div>
                    <label htmlFor="studentAddress">Morada</label>
                    <Input
                        id="studentAddress"
                        type="text"
                        value={address}
                        placeholder="Rua, número, localidade"
                        onChange={onAddressChange}
                    />
                </div>
            </div>

            {modalities.length > 0 && (
                <div className="student-modalities-section">
                    <div className="student-modalities-header">
                        <h4 className="student-modalities-title">
                            Modalidades
                        </h4>

                        <div className="student-modalities-search-wrap">
                            <Icon name="search" size={15} />
                            <input
                                type="text"
                                className="student-modalities-search"
                                placeholder="Pesquisar..."
                                value={modalitySearch}
                                onChange={(e) => setModalitySearch(e.target.value)}
                            />
                        </div>
                    </div>

                    <div className="student-modalities-list">
                        {filteredModalities.map((m) => {
                            const id = m.modalityId ?? m.ModalityId
                            const name = m.name ?? m.Name ?? ''
                            const checked = selectedModalityIds.includes(id)

                            return (
                                <label
                                    key={id}
                                    className={`student-modality-chip ${checked ? 'student-modality-chip--checked' : ''}`}
                                >
                                    <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => onModalityToggle && onModalityToggle(id)}
                                    />
                                    {name}
                                </label>
                            )
                        })}

                        {filteredModalities.length === 0 && (
                            <p className="student-modalities-empty">
                                Nenhuma modalidade encontrada.
                            </p>
                        )}
                    </div>
                </div>
            )}

            {error && (
                <div className="form-error">
                    {error}
                </div>
            )}

            <div className="modal-actions">
                <Button variant="secondary" onClick={onClose}>
                    Cancelar
                </Button>

                <Button variant="primary" onClick={onConfirm} disabled={loading}>
                    {loading ? loadingLabel : confirmLabel}
                </Button>
            </div>
        </Modal>
    )
}

export default AddStudentModal