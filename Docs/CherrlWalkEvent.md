# Cherrl: passeio anterior à Erva Curativa

Fontes: TOWN/TOWN.BIN NTSC-U, falas em Shift-JIS de largura completa normalizadas para Unicode; nenhum diálogo novo em inglês.

- 0x660FBF–0x661226: convite para dar as mãos e caminhar, aceitação e recusa.
- 0x67B000–0x67B54B: passeio externo. Koh e Cherrl caminham juntos; Cherrl usa retrato 2 na escolha entre namorados e irmãos; Ghosh se aproxima antes de falar. Cherrl usa retrato 1 ao defender Koh, recebe o comando de ator 0D:16 e desmaia. Ghosh diz “I didn’t do anything!”, sai e Koh a transporta à clínica.
- Destinos do script, divididos por 32: Koh (108,42.75) → (104,42.75); Cherrl (107.5,43.5) → (104.5,43.5); Ghosh (101.75,43), saída (90,43); transporte de Koh (32,42.75) → (32,39).
- 0x5A078E: fala de avaliação de Hush após a chegada. A entrega da erva só fica disponível depois desse evento.
- 0x66122A–0x6615FD: pedido de desculpas de Cherrl e explicação de Nadia na visita posterior à casa.
- Os bancos de sprites utilizados já foram extraídos dos registros nativos de Cherrl: `cheril_window` para o passeio e `cheril_recovery` para a queda/recuperação. Imagens de retratos em alta resolução permanecem inalteradas.

O adaptador Unity conserva o trajeto, o elenco, o texto e as expressões extraídos. A duração da interpolação dos deslocamentos não é apresentada como uma medição do hardware PS1. A aproximação de Ghosh reutiliza a posição de saída registrada no roteiro; o ponto inicial da criação do ator não foi resolvido separadamente.

Progresso: `town.story.cherrl.walkHospital` só é salvo após chegar à clínica. A fase 5 de saves antigos sem o evento e sem erva entregue volta à fase 4; saves com a cura já entregue não voltam. A confirmação da erva também valida a fase e a conclusão do passeio para impedir entrega antecipada.

Validação: compilação do assembly de execução passou. Teste em Play Mode isolado passou pela escolha do passeio, aproximação de Ghosh antes de falar, retratos 2/1 de Cherrl, falas, desmaio, transporte até a aproximação da clínica, cancelamento por troca de cena, bloqueio da erva e compatibilidade de progresso antigo. As transições completas casa → cidade → clínica ainda precisam de conferência visual no projeto principal. O transporte reaproveita os quadros nativos de Koh segurando um ator; a correspondência exata desses quadros com os comandos especiais do roteiro ainda não foi confirmada.
