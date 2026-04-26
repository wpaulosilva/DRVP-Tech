

# BPMN Criar/Inscrever em Aulas

mermaid
flowchart LR

    graph Encarregado Educação
    id_0((""))
    id_1{"Nova Aula?"}
    id_2["Visualizar Horários Disponíveis"]
    id_3["Criar Aula"]
    id_4["Visualizar Aulas Existentes"]
    id_5["Inscrever Aula"]
    id_6["Notificar Professor"]
    end

    graph Escola
    subgraph Professor
    id_7{"Aceita?"}
    id_8["Visualizar Pedidos"]
    id_9["Notifica Encarregado"]
    id_10["Notifica Direção"]
    end

    subgraph Direção
    id_11["Visualizar Pedidos de Aula"]
    id_12{"Aceita?"}
    id_13["Notificar Encarregado"]
    end
    end


    %% --- Links ---
    id_1 -- |Sim| --> id_2
    id_2 --> id_3
    id_3 -.-> id_11
    id_1 -- |Não| --> id_4
    id_4 --> id_5
    id_5 --> id_6
    id_6 --> id_10
    id_10 --> id_9
    id_9 --> id_7
    id_7 -- |Sim| --> id_9
    id_7 -- |Não| --> id_10
    id_12 -- |Não| --> id_13
    id_12 -- |Sim| --> id_8
    id_11 --> id_12
    id_13 --> id_0

# BPMN Validar Aulas (48h window)
mermaid
flowchart LR

    subgraph Escola Direção
    id_0((""))
    id_1("Analisar Respostas")
    id_2{"Confirmar Aula?"}
    id_3("Validar como Realizada")
    id_4(("Aula Confirmada"))
    id_5("Validar como Não Realizada")
    id_6[["Notificar Encarregado"]]
    id_7[["Notificar Professor"]]
    id_8[["Notificar Direção"]]
    end

    subgraph Professor
    id_9("Visualizar Aulas Passadas")
    id_10("Votar se Lecionou")
    id_11{"Respondeu em 48h?"}
    end

    subgraph Encarregado Educação
    id_12("Votar se Participou")
    id_13{"Respondeu em 48h?"}
    id_14[["Notificar Direção"]]
    end

    %% --- Links ---
    id_0 --> id_9
    id_9 --> id_10
    id_10 --> id_11

    id_11 -- Não --> id_8
    id_11 -- Sim --> id_1

    id_0 --> id_12
    id_12 --> id_13

    id_13 -- Não --> id_14
    id_13 -- Sim --> id_1

    id_1 --> id_2

    id_2 -- Sim --> id_3
    id_3 --> id_4

    id_2 -- Não --> id_5
    id_5 --> id_6
    id_6 --> id_7

# BPMN Criar Utilizador Novo
mermaid
flowchart LR

    subgraph Escola Direção
    id_0((""))
    id_1("Criar Credenciais")
    id_2{"Tudo certo?"}
    id_3("Validar Informação")
    id_4(("Conta Criada"))
    id_5{"Verificar Informação"}
    id_6[["Notificar Encarregado"]]
    end

    subgraph Encarregado Educação
    id_7{"Credenciais recebidas"}
    id_8("Redefinir password")
    id_9("Manter password")
    id_10{" "}
    id_11("Autenticar-se")
    id_12("Preencher Informação de Aluno")
    id_13("Alterar dados incorretos")
    end

    %% --- Links ---
    id_0 --> id_1
    id_1 -.-> id_7
    id_7 --> id_8
    id_7 --> id_9
    id_8 --> id_10
    id_9 --> id_10
    id_10 --> id_11
    id_11 --> id_12
    id_12 -.-> id_5
    id_5 --> id_2
    id_2 -- Sim --> id_3
    id_3 --> id_4
    id_2 -- Não --> id_6
    id_6 -.-> id_13
    id_13 -.-> id_5