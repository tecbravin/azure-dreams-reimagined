# Corridas da pista e apostas

Fale com Silver na recepção: “Vou correr” abre a modalidade controlada; “Só vou assistir” abre a corrida automática. A sequência existente carrega `Interior_37_racetrack_course`, prepara quatro Nyuels/jóqueis, faz a contagem regressiva e retorna à recepção após os resultados. Os comandos usam o mapeamento atual: correr/acelerar (manter ou apertar repetidamente), direções para mudar de faixa/recuar, pular para as barreiras. Na modalidade automática os quatro competidores usam IA e a câmera acompanha o líder, mantendo o vencedor após a chegada.

## Apostas

A pedido do usuário, os novos bilhetes escolhem **um vencedor**, substituindo a aposta antiga em dupla. Cada bilhete custa 100 G, de um a dez bilhetes. A compra guarda o número do corredor e o multiplicador exibido. O pagamento é `bilhetes × 100 × multiplicador` quando esse corredor termina em primeiro, nas duas modalidades. Todos os bilhetes são consumidos ao liquidar o resultado. O resultado só é liquidado uma vez e o ouro é limitado a int.MaxValue. Bilhetes antigos em dupla mantêm sua condição anterior até serem consumidos.

Chaves: `racetrack.ticket_winner`, `racetrack.ticket_count`, `racetrack.ticket_odds`; `racetrack.ticket_pair` permanece apenas para compatibilidade. Preço/odds e a adaptação do percurso continuam os já utilizados no projeto; não são apresentados como uma nova decodificação completa da simulação PS1.

## Validação

Compilação do runtime passou. Teste isolado em Play Mode completou quatro corridas: controlada e automática, com aposta vencedora e perdedora em cada uma. Entradas reais do Input System verificadas para acelerar, mudar de faixa e pular. Conferidos: compra/desconto, compra duplicada, ouro insuficiente, ordem de chegada, prêmio de 800 G para 2 bilhetes de 100 G a ×4, consumo do bilhete, impossibilidade de pagar novamente, câmera e compatibilidade dos bilhetes antigos. Recibo: `Validation/NpcRestaurant/race-play-passed.txt`. Carregamento completo das cenas e aparência dos modelos não foram exercitados nessa simulação isolada.
