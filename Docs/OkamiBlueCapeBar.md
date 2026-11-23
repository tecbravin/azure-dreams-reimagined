# Okami: capa azul, luto e reabertura do bar

Progressão solicitada:

1. Entregar a Capa Azul a consome, marca o objetivo original e inicia `Silent`. A fala passa a ser apenas `...`, inclusive ao falar novamente na mesma visita/dia. As antigas falas da entrega sobre a morte do guerreiro não são exibidas nesta sequência solicitada.
2. Na próxima volta efetiva da torre, `Silent` passa a `Closed`. Só Okami é montada no bar: moradores serializados e o bartender ambiente são filtrados. Vivian não aparece.
3. A conversa triste usa TOWN.BIN 0x6A6BE8–0x6A74C9: chamada para levantar a cabeça, olhar de aventureiro/solidão, escolhas sobre o bar fechado ou a tristeza dela, reação e decisão de reabrir. Ao terminar, repete apenas “I've got some work to do. I've got to get this bar open for business, right?”. A conversa não altera a fase para reaberta.
4. Na volta seguinte da torre, `Closed` passa a `Reopened`, a rota Vivian vai para a etapa 3 e o elenco normal volta. Vivian pode ser apresentada já nesse dia.
5. A recepção de Okami na reabertura (0x6A7620–0x6A7760: boas-vindas, tempo de folga, aparência do bar e movimento) é mostrada uma vez. Depois volta a saudação normal “Looking cute, as always”.

Chaves persistentes: `town.story.vivian.capeBarPhase` (1 silêncio, 2 fechado, 3 reaberto), `town.story.vivian.closedBarTalk`, `town.story.vivian.reopenedThanks`. Saves antigos na etapa 2 inferem silêncio/fechado pelo dia gravado, sem reiniciar a rota; etapas posteriores não regridem. O avanço está ligado a `FinishManagedTowerRun`, antes do incremento do dia, e não à simples saída/entrada do bar.

Validação: compilação e teste Unity isolado passaram: consumo da capa, silêncio, dois retornos, filtro de moradores/bartender/Vivian, conversa triste completa e repetição final, ausência de reabertura imediata, agradecimento único, retorno da saudação e apresentação de Vivian no dia da reabertura. Recibo: `Validation/NpcRestaurant/okami-bar-passed.txt`. A arte completa da cena não foi renderizada nesse teste.
