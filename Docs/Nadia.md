# Nadia na casa da Cherrl

Nadia usa o banco nativo `nerdia`: F_act_nerdia 88 corresponde ao ator/interlocutor 26 (0x1A). O banco já extraído inclui as direções e a caminhada originais. A casa cria uma única Nadia, com interação própria; as falas da mãe nos eventos da Cherrl agora selecionam a instância de Nadia em vez de apenas trocar o nome sobre o sprite da filha. O fallback antigo permanece para contextos sem a mãe carregada.

Acolhimento: TOWN.BIN 0x65F0D2. Pedido para cuidar de Cherrl: 0x65FFFF–0x66048C, usando a sequência já existente quando a etapa está disponível. Comentário após o passeio/desmaio: 0x66157E. Nadia não tem retrato original na tabela de interlocutores com retrato e não herda o retrato da Cherrl.

Posição escolhida na casa: célula (20,16), piso zero, livre dos móveis e da entrada. Essa posição não foi confirmada como coordenada do evento no PS1. O identificador `nadia` e o catálogo evitam depender da busca aleatória de posição.

Validação: compilação e teste Unity isolado passaram: banco original, posição, proteção contra duplicação, conversa própria, ausência de retrato herdado, alternância Cherrl/Nadia no evento da mãe e fala após o passeio.
