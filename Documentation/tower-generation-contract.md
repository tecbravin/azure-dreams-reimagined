# Contrato de geração e ciclo da torre — incremento Architect a71

## Escopo e integração

Unity **6000.6.7f1**, Built-in. A dependência autoritativa continua sendo `Assets/Plugins/AzureDreams.Core.dll`, sem recompilação ou substituição. Nenhum arquivo do controller, Inventory ou NPC pertence a este incremento. Nenhuma nova chamada é exigida ao Coder; assinaturas públicas existentes preservadas.

### TowerFloorManager

- `Generate(floorNumber, seed)` mantém geração determinística do Core; floor inferior a 1 vira 1, seed int é preservada sem normalização. Não implementa geração retail adicional.
- `HasReachableExit` (**nova propriedade bool**) calcula conectividade de terreno por BFS de quatro vizinhos no grid atual, limitada a 64×64. Exige tiles de entrada/saída válidos; retorna false sem geração, com escada ausente ou saída desconectada. Não considera atores/altura, não repara mapas e não é chamada automaticamente pelo controller.
- `FindOpenCell` revalida o terreno dos candidatos previamente indexados. O retorno legado `(0,0)` ainda indica esgotamento potencial: consumidores devem revalidar; células abertas externamente após Generate não entram no índice.
- `AdvanceFloor` satura em `int.MaxValue`; não implica Generate.
- `Reset` limpa seed, ambas escadas, dungeon, alturas e índice, voltando a floor 1.
- `Dungeon`, `Heights` e o índice de OpenCells não são snapshots imutáveis. A propriedade de conectividade lê o terreno vivo.

### TowerItemTrapManager

- `SpawnItems(int)` e `SpawnTraps(int)` percorrem candidatos em ordem estável y/x, escolhem sem reposição pelo RNG fornecido e encerram ao esgotar candidatos. Pedidos excessivos não produzem laços proporcionais a `int.MaxValue`. Slots limitam itens a 64.
- Apenas Floor/Corridor são candidatos. Escadas e flags **HasItem, HasTrap, HasActor, Blocked, Door** excluem a célula. O consumidor deve marcar atores/reservas no grid antes de spawn: não há descoberta de GameObjects nem inferência de reservas externas.
- Essa filtragem e a nova ordem de consumo do RNG alteram a distribuição do antigo protótipo; não são distribuição retail.
- Cada item criado pelo manager possui associação exata célula→slot; coleta em ordem inversa e ResetFloor no mesmo dungeon preservam a identidade. A associação inicial dos itens preexistentes do Core continua uma inferência por varredura; não se afirma identidade retail fiel desses registros.
- Uma armadilha criada pelo manager guarda e restaura seu terreno original (Floor ou Corridor). Armadilhas importadas sem metadados mantêm o fallback legado Floor. Nenhum efeito/dano retail foi inventado.
- Trocar a referência `floor.Dungeon` (inclusive Generate com mesmos floor/seed) invalida automaticamente contadores, associações e terreno guardado. Consultas de contadores e disparo observam essa troca. O RNG continua sendo o stream fornecido; não é resemeado automaticamente.
- `ResetFloor()` reinicia contadores, mas preserva associações e terreno de objetos vivos no mesmo dungeon. Não regenera itens/traps, não ressuscita itens consumidos.

### TowerRunManager

- `BeginRun` só aceita `NotInRun`; chamadas repetidas não descartam inventário, loot nem RNG. É necessário concluir ReturnToTown + FinishReturn antes de iniciar outra run.
- AdvanceFloor só aceita InTower e floor < int.MaxValue; repetição durante FloorTransition não avança novamente. CompleteFloorTransition continua idempotente.
- `ApplyDamage`, `AddLoot`, `SetHealth` só mutam em InTower. Dano não positivo é ignorado; dano letal satura saúde em zero. Estados terminais não aceitam essas mutações.
- ReturnToTown continua depósito único; o argumento survived e a política de recompensa existente não foram reescritos. SetHealth continua setter de sincronização e não implica automaticamente Defeat.
- **Persistência de run intermediária não suportada**. SaveRun/ClearRun permanecem no-op e LoadRun false. Town persistence permanece existente.

## Evidência e limites

Ver `Logs/ArchitectTowerGeneration/ARCHITECT_HANDOFF.md` e `verified-results.json`: XMLs enumerados, comandos preservados nos logs, hashes do snapshot e divergências com produção. Testes de manager em EditMode e PlayMode real não equivalem à aceitação independente de pickups/markers do controller, nem build WebGL, soak ou aprovação visual. Não modificar o clone congelado de QA d0313697.
