const features = [
    {
        title: 'Coachings para várias idades',
        description: 'Turmas adaptadas a diferentes níveis e fases de aprendizagem.',
        icon: '🩰',
    },
    {
        title: 'Professores experientes',
        description: 'Acompanhamento próximo e ensino focado na evolução de cada aluno.',
        icon: '👥',
    },
    {
        title: 'Espetáculos e projetos',
        description: 'Participação em apresentações, eventos e experiências artísticas.',
        icon: '✨',
    },
]

function Features() {
    return (
        <section className="features">
            <div className="container features-grid">
                {features.map((feature, index) => (
                    <div className="feature-card" key={index}>
                        <div className="feature-icon">{feature.icon}</div>
                        <h3>{feature.title}</h3>
                        <p>{feature.description}</p>
                    </div>
                ))}
            </div>
        </section>
    )
}

export default Features