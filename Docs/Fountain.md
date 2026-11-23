# Fonte da praça e arrecadação da Nico

A malha original já extraída de Monsbaiya desenvolvida (`Town/Retail/Construction/fountain.bytes`) fornece o piso elevado, fonte, bancos e demais obstáculos da praça. Agora recebe MeshCollider, e as mesmas faces alimentam a colisão do controlador de Koh. As faces são indexadas por célula; superfícies até 1,01 unidade definem piso, e as superiores definem obstáculos. Isso substitui o retângulo sólido que bloqueava parte da praça. Ao reconstruir a cidade ou descarregar a malha, as colisões não permanecem ativas.

A arrecadação existente da Nico mantém a meta de 3000 G contando sua doação e a de Ghosh (`2 * amount + 1`). Ao atingir a meta, `town.construction.fountain.pending` é gravada; ao retornar da torre, ApplyPendingConstruction marca `town.construction.fountain` e a cidade monta o projeto original. A celebração e os eventos posteriores continuam usando os diálogos e retratos existentes.

Nico, Ghosh e Chero continuam aparecendo nas respectivas etapas da história. Sua posição vertical agora acompanha o piso original elevado. Wookie permanece no ponto já usado junto à praça, acima da geometria capturada, sem alteração de escala nesta mudança.

Validação: compilação do runtime; teste isolado em Unity de arrecadação zero/parcial/completa, construção pendente no retorno, malha/collider original, praça caminhável, centro sólido, posição vertical dos residentes, elenco Nico/Ghosh sem duplicações e descarregamento. As 20 referências de texturas da fonte foram verificadas no disco. A aparência completa na cena principal não foi inspecionada nesta execução.

## Conversas dos moradores da praça

`FountainResidentTalk` liga os sete moradores às janelas normais de diálogo, sem herdar a fala do cliente do restaurante. Fonte primária: TOWN.BIN 0x2CE000–0x2CF218. Os comandos `57 <speaker>` identificam narder (61), kiku (60), obasan (45), oyaji (62), der (44), nouhuko (31) e ran (64); os bancos de três aliases antes provisórios foram corrigidos de acordo com esses identificadores. Os identificadores de instância existentes foram preservados.

Inclui canções das crianças, explicação dos controles com os bindings atuais no lugar dos símbolos PSX, agricultura, camisetas, comentários sobre o domador e as falas da moradora sobre os andares 10, 15 e 20. O inglês vem do disco com normalização de largura e apóstrofos; o português é tradução. O posicionamento anterior dos moradores não foi alterado. Não implementa a encenação coletiva das três fãs presente no mesmo bloco do roteiro.
