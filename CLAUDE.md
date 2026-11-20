# CLAUDE.md — Azure Dreams Reimagined

Remake em Unity do jogo de PlayStation 1 **Azure Dreams** (Konami, NTSC-U `SLUS_006.14`).
O objetivo é recriar o jogo fiel ao original (dados, regras, textos, layout da cidade e da torre),
com gráficos e mídia redesenhados.

## Regra de ouro: "original" = fonte extraída

Quando o usuário pedir algo **original** (texto, regra, tabela, stats, posição, timing, som, sprite),
**não invente**: busque nas fontes extraídas, nesta ordem:

1. `../azure-dreams-decomp-master/` — reconstrução em **C** do código do jogo (`src/`, `include/`, `docs/`, `tools/`).
   É a referência de lógica: fórmulas, tabelas, IA, eventos, máquinas de estado.
2. `../extracted_assets/` — dados e gráficos já extraídos do disco (texturas, VRAM, áudio, modelos, `azure_main/items_and_spells.json` etc.).
3. Pastas do disco na raiz: `../MAIN`, `../TOWN`, `../DUNGEON`, `../STR`, `../STRT`, `../SLUS_006.14`.
   Disco original: `../Azure Dreams.bin` (+ `.cue`).
4. `../community/` e `Documentation/Community/` — guias/planilhas da comunidade (úteis, mas **provisórios**: confirmar contra o decomp).
5. `../Azure Dreams (USA) Game Manual.pdf`, `../pro_grammar's RNG guide to Azure Dreams.txt`.

Se não houver evidência na fonte, diga isso e marque o que for suposição.

## Mapa de pastas do workspace (`Projetos Pessoais/Azure/`)

| Pasta | O que é | Usar? |
|---|---|---|
| `Azure Dreams Reimagined/` | **Este projeto Unity** (repositório git) | sim — aqui se programa |
| `_Redesigned assets/` | Arte/mídia **nova** (Genus, Portraits, item_icons, music) | sim — fonte dos assets redesenhados |
| `azure-dreams-decomp-master/` | Decompilação em C | sim — referência de lógica original |
| `extracted_assets/` | Saída dos extratores (raw) | sim — referência de dados originais |
| `extracted_assets/reimagined_pipeline/` | Intermediários dos scripts `Tools/` (antigo `Extracted/` do projeto) | só via Tools |
| `AzureDreams/` | Core C# puro (`AzureDreams.Core`) compilado para `Assets/Plugins/AzureDreams.Core.dll` | via `Tools/sync_core.py` |
| `Builds/` | Builds WebGL gerados (fora do projeto Unity) | saída |
| `Tools/` (raiz) | Ferramentas externas (ffmpeg, Noesis, NinjaRipper, timmy) | utilitários |
| `Old Files/` | Arquivos aposentados (fontes .m4a, gravações da OST) | só consulta |
| `extracted_assets/music_rendered/` | As 57 sequências de música renderizadas do disco (+ variações) | referência |
| `bkp/` | Backup antigo | **NUNCA usar** |
| `azure-decomp*`, `azure-main`, `extracted`, `extracted_re`, `*.iso` | Tentativas antigas de extração | evitar; preferir as fontes acima |

## Projeto Unity

- Editor **Unity 6000.6.7f1** (ver `ProjectSettings/ProjectVersion.txt`). Alvo principal: **WebGL**.
- Cenas principais: `Assets/AzureDreams/Scenes/ReimaginedDemo.unity` (cidade/torre), `MonsterTower.unity`,
  `Monsbaiya.unity`, interiores em `Scenes/Interiors/Interior_XX_*.unity` (um por prédio, ID do modelo original).
- Pacotes: Input System (embutido em `Packages/com.unity.inputsystem`), Timeline (embutido), Localization, uGUI, TextMesh Pro, Test Framework.

### Estrutura de `Assets/`

- `AzureDreams/` — tudo do jogo:
  - `Scripts/` — runtime (`AzureDreams.Runtime.asmdef`). `TowerGameController` é uma classe `partial`
    dividida em muitos arquivos `TowerGameController.<Feature>.cs` (lojas, NPCs, histórias, título, ações…).
    Adicione features novas como novo arquivo partial, não inchando os existentes.
  - `Scripts/Rendering/` — renderização estilo PSX (paletas indexadas, `PsxSpriteRenderer`).
  - `Shaders/` — `PsxIndexed`, `PsxVertexSnapAffine`, `PsxColorQuantization`, `TownCaptured`, etc.
  - `Resources/` — carregado por `Resources.Load` **por caminho** (ex.: `Town/Retail/...`, `Tower/...`, `Sfx/Retail`).
  - `Data/`, `Definitions/` (ScriptableObjects), `Prefabs/`, `Sprites/`, `Textures/`, `Models/`, `Audio/`, `Fonts/`, `Videos/`.
- `StreamingAssets/` — carregado por URL em runtime (`Title/`, `Media/` [Portraits, Music, Prologue, Manual], `Monsters/Community`).
  Tudo aqui vai para o build: **não colocar dados brutos/diagnóstico**.
- `Scripts/` (legado, via `AzureDreams.Runtime.asmref`) — ainda referenciado por prefabs; não remover sem migrar.
- `Textures/` (raiz) — TIMs da dungeon usados pelos materiais em `AzureDreams/Materials`.
- `Resources/FX/` — prefabs de efeitos (`Resources.Load("FX/...")`).
- `Editor/` — importadores e builders (`AzureDreams.Editor.asmdef`), `BuildConfigurator`, `CloudflareWebBuild`.
- `Tests/` (EditMode em `Tests/Editor`, PlayMode, `ArchitectTowerPlayMode`) e `CoderTests/`.

### Outras pastas do projeto

- `Tools/` — scripts Python do pipeline de extração/importação. Usam
  `PROJECT = Path(__file__).parents[1]` e `WORKSPACE = PROJECT.parent`; alguns usam `Path.cwd()` e devem
  ser rodados **a partir da raiz do workspace** (`Azure/`). Intermediários vão para
  `../extracted_assets/reimagined_pipeline/`, nunca para dentro do projeto.
- `Tools/sync_core.py` — testa e recompila o core C# (`../AzureDreams`) e copia a DLL para `Assets/Plugins`.
- `Tools/package_webgl.py <build> <destino>` — empacota um build WebGL (Cloudflare).
- `Tools/adsnd/` — renderizador da música original (driver de som + SPU em C) e scripts de exportação.
- `Documentation/` — notas técnicas por tema (torre, cidade, render, sprites, WebGL). Atualize quando mudar o sistema descrito.

## Modo Clássico × Reimagined

O jogador escolhe em **Configurações → Estilo do jogo** (salvo em `PlayerPrefs` `ad_asset_mode`; padrão Reimagined).
Código central: `Scripts/AssetMode.cs` (+ `TowerGameController.AssetMode.cs`, que limpa caches e recarrega a música ao trocar).

- **Original fica no caminho normal; o redesenhado usa o MESMO caminho relativo dentro de uma pasta `Reimagined`:**
  - `Assets/AzureDreams/Resources/Reimagined/<caminho>` → carregado por `AssetMode.Load<T>(caminho)`
    (use sempre `AssetMode.Load`, nunca `Resources.Load`, no runtime).
  - `Assets/StreamingAssets/Media/Reimagined/<caminho>` → resolvido por `AssetMode.ResolveMedia` (retratos, imagens, música).
- Reimagined usa a versão nova se existir e cai no original; Clássico nunca lê a camada `Reimagined`.
- `Resources/Reimagined/media_overlay.txt` indexa a camada de mídia (WebGL não lista StreamingAssets). É gerado pelo
  editor (`ReimaginedMediaManifest`: ao importar, antes do build e no menu *Azure Dreams > Rebuild Reimagined Media Manifest*).
  Linha com `\t*` = arquivo só-Reimagined (no Clássico não aparece nada / o código usa o fallback original).
- **Músicas:** Clássico = `Media/Music/<cue>.ogg`, **renderizadas do próprio disco** (sequências KDT + bancos VAB)
  por `Tools/adsnd` (ver o README de lá; mapeamento cue → sequência em `export_classic.py`). Cada arquivo é um
  ciclo exato do loop, sem emenda. Reimagined = `Media/Reimagined/Music/<cue>.ogg|.mp3`.
  Fanfarra de level-up: `Resources/Music/LevelUp` (+ camada Reimagined).
- **Ícones de item:** Clássico = 16px do ADStateDisplay/atlas original; Reimagined = `Resources/Reimagined/Items/<nome>.png`.
- Efeitos gráficos (pós-processamento/shaders PSX) são uma opção **separada**, não fazem parte do modo.
- Para adicionar arte nova: coloque o arquivo com o mesmo nome/caminho do original dentro de `Reimagined/`. Não sobrescreva
  o original. Fontes brutas (m4a, PNG gigante) ficam em `../_Redesigned assets`; o que sai de uso vai para `../Old Files`.

## Dias e ida à torre
- Cada retorno da torre avança um "dia" (`town.story.day`, em `FinishManagedTowerRun`). Original: `into_dn_door_jobs` (`apply_building_pending_updates`, `clear_resident_bit_patterns`).
- Obras do Tonka (e a fonte da Nico) ficam pendentes ao pagar (`town.construction.pending`, `town.construction.fountain.pending`) e só ficam prontas em `ApplyPendingConstruction`, no retorno. Só uma encomenda por vez; pendente não conta como construído.
- Passos de história que o `town_event_reference.json` coloca em dias diferentes usam `StoryNewDay(rota)`.

## Torre retail (mapas, tiles, backdrop)
- `Tools/extract_tower_retail.py` extrai de `DUNGEON/DUNGEON.BIN` + `SLUS_006.14` para `Resources/Tower/Retail/`: tilesets `geometry_29/2a/2b` (tipos 0x29 andares aleatórios, 0x2A arena do Beldo, 0x2B andares 1-2), layouts fixos `layout_NN`, `floor_variants` (variante de textura por andar), atlas `tiles_vNN`/`tiles_t1`/`tiles_t2` (um quadro 256x256 por tpage+CLUT), `water_*` (faixa animada) e `backdrop_NN`.
- `RetailTowerFloor.cs`: transliteração de `generate_floor_layout` (salas, corredores de 2 tiles, degraus, pilares, portas) e do `sample_terrain_surface_height`. Andar 1 = layouts 3/6/7/8, andar 2 = layout 2, andar 40 = layout 1; demais = gerador aleatório. Andar 31 desliga a sala-covil (registro da visão do Beldo; a correção da armadilha continua em `PlaceBeldoVision`).
- `TowerGameController.RetailFloorMesh.cs`: malha dos tiles como `render_map_cell_mesh.c` (faces dos dois ramos de visibilidade, oclusão pelo vizinho) e backdrop cilíndrico de `depth_strip_projection_g00337.c`.
- Protótipo Python (validação visual) em `extracted_assets/tower_research/`.

## Convenções

- **Nunca** mover/renomear asset dentro de `Assets/` sem o `.meta` junto (quebra GUIDs).
  Antes de remover um asset, verificar GUID em `.unity/.prefab/.mat/.asset` e strings em `Resources.Load`/StreamingAssets.
- Duplicatas dentro de `Assets/` costumam ser **intencionais** (mesmo frame em várias animações, mesma
  geometria em vários prédios, carregados por caminho). Não deduplicar sem trocar o carregamento.
- Builds, zips, logs, `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.csproj/.slnx` e `.vs/` não são versionados.
- Código novo em C# com namespace do projeto; dados editáveis via ScriptableObject; sem física Rigidbody no grid.
- Textos e IDs de personagens/monstros/itens seguem os nomes do jogo original (ex.: Koh, Kewne, Nico, Cherrl, Selfi, Wreath).
- Responder ao usuário em **português**.

## Verificação

- Rodar testes pelo Test Runner (EditMode + PlayMode) ou CLI:
  `Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayMode.xml`.
- Para mudanças visuais, abrir a cena e conferir em Play; registrar resultado em `Documentation/` quando relevante.

## Portas dos interiores, piscina e mortes de monstros

- Saída de cada interior = tapete azul com seta (página de textura 149, UV ~0.25,0.63) na parede da frente. Centros em `RetailInteriorDoors` (KohHome.cs); Koh nasce 2 casas para dentro (`InteriorDoorInward`). A piscina é a exceção: o tapete fica na borda de cima da plataforma e Koh entra para baixo (+z).
- `heights.bytes` da piscina é gerado por `Tools/build_pool_heights.py`: plataforma (y=1) andável, água (y=0.75) andável só na piscina restaurada; o gramado de fora fica bloqueado.
- Mortes de monstros: `MonsterDefeatPresenter.cs` reproduz os callbacks retail (fade aditivo de 16 ticks padrão; Cyclone estica; Mandara/Unicorn piscam; Kraken/Kid pulam; Baloon estoura, gira pela tela e cai). SFX 0x806 (Ifrit/Manoeva) e 0x807 (Baloon) extraídos com `extract_retail_sfx.py`.
- NPCs da cidade: o retail desenha moradores como sprites. Coppe/Bet/Barry/Tonka usam `OriginalTownSpriteView` (sprites em `Resources/Town/Residents/OriginalSprites`); Silver usa os sprites "Grandpa" (`RacetrackResidentSpriteView`).

## Academia (Gym)

- `TowerGameController.Gym.cs`. Overlay da cena no TOWN.BIN 0x470000 (VA 0x80010000); tabela de falas em 0x8001159C (falantes 0x34 caçador do bar, 0x29/0x2A Irmãos Kim, 0x10 Barry, 0x3E, 0x45, 0x2D); textos em 0x473000 (VA 0x80017874 = arquivo - 0x45B78C).
- Minigame = overlay ovl_79617c (VA = arquivo - 0x776000 + 0x80000000): pistas X 800/896/992 em Y 0x380, barra sai de X 0x2A0, intervalo max(1, 32 - recorde/10) ticks, recorde mínimo 20 (`gym.record`). SFX 0x506/0x512/0x525.
- Posições dos NPCs ainda não decodificadas (registros OSETDT da cena); os Irmãos Kim não têm sprite extraído e usam o prefab do caçador.

## Cassino

- `TowerGameController.Casino.cs`. Interior_2b_casino_1 (entrada pela rua) = caça-níqueis; Interior_06_casino_2 = roleta com trampolim. Passagem: 2b (9.5, 15.5) ↔ 06 (23.5, 16).
- Overlay ovl_79ded0: VA = arquivo - 0x77D800 + 0x80000000. Rolos em 0x800244B8, pagamentos em 0x800244E8 (7=200, sino=50, kiwi=20, laranja=8, rosa=4, azul=3, verde=1, x100 G x moedas).
- Os rolos são a textura capturada page_134 (linhas 128-223), redesenhada em tempo real a partir dos símbolos do topo da página (draw_scrolling_column).
- Roleta: 3 botões x6/x3/x2 (X 0x460/0x4E0/0x560, Y 0x360); roda em (13.5, 13.5), setores por ângulo & 0x7FF = 2,1,2,1,2,0. Prêmios saem como moedas de 100/1.000/10.000 G que Koh precisa pegar.
- Falas: roteiros no TOWN.BIN 0x464000 (roleta) e 0x466000 (caça-níqueis). Malhas originais da roda, alavanca e botões não foram extraídas (feitas com primitivas).

## Teatro (comédia de Fon e Dodo) e sons de cena

- Roteiro do show: TOWN.BIN 0x4B7D8A-0x4B9238. × concorda, ○ retruca; erro ao concordar = 0x8001C129, ao retrucar = 0x8001C1E5, demora = 0x8001C29B (bronca + pancada). A linha "Calm down" aceita as duas respostas; "Don't stop me!" pede concordar.
- Cada cena da cidade tem seu próprio banco VAB para os sons 0x7xx: teatro = TOWN.BIN 0x4A2800 (700 aplauso, 701 risadas, 702), cassino = 0x45A000 (700, 701 clique da roleta). `extract_retail_sfx.py --scene theater 700 701 702` gera `Sfx/Retail/Theater700.wav` etc. Os 0x7xx do banco da torre (700/701.wav) não valem para a cidade.

## Elevador da segunda torre (andar 2)

- `TowerGameController.SecondTowerElevator.cs`. O layout 2 tem quatro anéis iguais; o elevador normal fica no centro do anel nordeste (55, 9) e o extra no centro do anel noroeste (8, 9), que recebe o tile de base do elevador (kind 1) em `TowerFloorManager`.
- Lacrado (centro cinza + símbolo da Seal trap, mensagem ao pisar) até `tower.beldo_defeated`. Liberado, leva à segunda torre (ver abaixo); não conta para o recorde de andar da torre principal.

## Segunda torre (modo de dungeon 2 do retail)
- `TowerGameController.SecondTower.cs` + `SecondTowerItems.cs`. Andares internos 41–140 = 1º–100º da segunda torre (`SecondTowerFirstFloor`/`SecondTowerTopFloor`, `EffectiveTowerFloor`). No 100º não há elevador; elevador/go-up mostram "No more floors above you!" (texto do DUNGEON.BIN).
- Retail (`setup_dungeon_floor_encounters`, generate.c): sem eventos/andares especiais (12, 15, 25, 31/40, visitantes do 2º só rodam no modo 0); música = `rng & 7` a cada andar; 4 grupos de monstros, espécie aleatória DRAGON..MAXIMUM, nível = andar + (rng&1) (máx. 99); no 1º andar rola os 4, depois grupos 2,3 viram 0,1 e 2,3 são novos (`RollTowerSpecies`, `SecondTowerMonsterLevel`).
- Itens: `mark_category_items_flag_400(2)` + `shuffle_enabled_item_category_pointers` — ervas, frutas, sementes, bolas, pergaminhos, sinos, óculos e lupas ficam não identificados com nomes de aparência embaralhados (strings +0x0C dos registros em SLUS 0x80073414, ex. "Erva vermelha"). Identifica o tipo ao usar ou com Óculos da Verdade; estado salvo em `tower2.item_seed`/`tower2.known_items`; zera ao voltar à cidade.
- Loot: tabela ponderada retail (raridade flags&0x3000 → 0x80/0x55/0x20/1; flag 0x40 = só no modo 2: ervas Poison/Paralyze/Sleep/Harash/Horrey/Shomuro, Pita, Big Pita, Geropita, sementes Lar/Slow/Tovar, Flat/Alchemic, Focus Loupe, Trained Wand). Ouro: orçamento andar×200 (± metade) em pilhas ouro 160–240 / prata 80–120 / cobre 16–24 (`place_floor_gold`). Ovos: todos os ovos da torre + Picket e U-Boat (flag 0x40 na categoria 19).
- HUD: número do andar tingido de azul (draw_mode 1, cor 0x202080). Quake/tempo limite vale também na segunda torre.
- Entrada retail real: marcador tipo 4 em (8,8) no andar 2 com flag 0xA2 + item 0x0C/8 (Balloon); o port usa o elevador em (8,9) liberado por `tower.beldo_defeated` (pedido do usuário).

## Gráficos das garotas / Noise / Cyclone
- `Town_Selfi.png` (cidade e 2º andar) recomposto com o chapéu e o cajado do sheet "Selfi & Ghosh" (partes da torre, escala 42/23 do sprite antigo). Original em `~/edits/orig/Town_Selfi.png`.
- Retratos conferidos (AdResourcesDumper): Nico = cabelo verde/patins (NicoSpriteView está certo), Weedy = chapéu azul/avental amarelo, Mia = verde/óculos. **Vivian usa o próprio retrato (`Vivian_variant0_frame0.png`, 112x128) como sprite de cidade — errado, falta extrair o sprite de cidade dela dos pacotes `TownCharacterPackages` (sem paletas ainda).**
- Noise: `MonsterTurnSpriteView` mantém o atlas `NoiseActions.png` (64px, flauta em todos os quadros) em vez do dump indexado `monster_17`, que perde a flauta na maioria dos quadros de andar/ataque. Mesma regra do Cyclone (`KeepsAuthoredAtlas`).
- Cyclone (advance_backward_forward_motion): recua ~0,18, avança girando (passa pelas 8 direções), dois redemoinhos translúcidos (efeitos 0x30, 8 passos de paleta) e som 0x80E (`RetailSfx.CycloneDash`; 80E/808 extraídos).

## Móveis da casa do Koh (loja geral) e cena do banho — engenharia reversa
- Fonte: malha real capturada de cada casa em `Town/Interiors/Interior_0N_koh_house_N/geometry.json` (mesmo referencial de grade usado por `BathPoints`/`Beds`/`BedroomDoors`/`HutLinks` em `TowerGameController.KohHome.cs`). Sem .NET/emulador disponível neste ambiente para rodar PSXPrev/Ghidra interativo, então a extração foi feita analisando a altura máxima de cada célula da grade (paredes/pilares = 3, objetos fixos = 0,5–2, piso livre = 0) e os clusters de vértices por material (page/clut).
- `TowerGameController.KohHomeDecor.cs` (`HomeDecorationSlots`): os pontos antigos eram "inventados" (comentário do próprio código: "free floor display points"). Recalculados a partir da malha real — células de piso plano (altura ~0), com vizinhança também livre, fora da cama, do box do banho e das portas. Resultado: casa 1 = 8 pontos (antes 9), casa 2 = 19, casa 3 = 30. Não existe (ou não foi localizada no decomp, que ainda não tem essa função decodificada) uma tabela original de "item X vai na posição Y"; o jogo retail provavelmente distribui os móveis comprados nessas mesmas células livres de forma sequencial, que é o que o código já faz.
- Banho (`TowerGameController.HomeBath.cs`, `HomeBathLayouts`): a posição da banheira (`Water`) já estava correta — conferida de forma independente achando, nas 3 casas, o mesmo padrão de malha (plataforma pequena ~0,75×0,75 a 0,625 de altura, encostada numa parede baixa de 0,75) perto do marcador `BathPoints`; os centros batem exatamente com os valores já no código: casa1 (21.5, 10.5), casa2 (35.5, 12.5), casa3 (55.5, 13.5). `Duration`/`Distance`/`Radius`/`CameraPan` continuam sendo ajuste de sensação (não dá para extrair da malha estática; precisaria do script de animação original SSTP_PLT_DM_BATH/BATH_TURN, que também não está decodificado no decomp).
- Nada disso foi testado no Unity Editor.

## Koh esticado na torre (corrigido)
- `KohSpriteView.ConfigurePresentation`: aplicava `scale.y *= _uprightHeightScale`, onde `_uprightHeightScale = 1/cos(pitch)` (≈1,8× no pitch de 56,25° da torre, ≈1,74× no interior, ≈1,22× na cidade). A intenção era compensar o "achatamento" natural de um billboard eixo-Y visto de cima, mas o jogo retail nunca fazia essa correção — os sprites originais simplesmente não compensam o pitch da câmera. O resultado no remake era o Koh ficando visivelmente mais alto que o tamanho autoral, a ponto de atravessar vigas/decorações de parede na torre (relatado pelo usuário com print).
- Removida a aplicação ao `scale.y` do próprio sprite do Koh; `_uprightHeightScale` continua calculado e é usado só em `LiftHandPosition` (deslocamento da mão ao carregar objeto, só na cidade), que não foi alterado. Tamanho do Koh agora fica fixo na escala autoral (`_baseVisualScale`), independente do pitch da câmera.
- Não testado no Unity Editor.

## Autorun (menu de configurações)
- `TowerGameController.Settings.cs`: nova opção "Autorun" / "Correr automático" entre "Game style" e "Graphics Effects" (linha 4 do menu). Liga/desliga com a mesma tecla de confirmar/horizontal das outras opções. Salvo em `PlayerPrefs` (`ad_settings_autorun`), default ligado (`_autorun = true`).
- Descrição fixa abaixo do botão, como a de "Game style": "If on, Koh always runs in town." / "Se estiver ligada, Koh sempre corre na cidade."
- `TowerGameController.cs`: `MoveInTown(direction, Time.deltaTime, runHeld || _autorun)` — com autorun ligado, Koh corre sempre na cidade mesmo sem segurar a tecla de correr; segurar a tecla continua funcionando normalmente se autorun estiver desligado. Não mexe no comportamento de passar turnos na torre (`runHeld` continua vindo só da tecla ali).
- Não testado no Unity Editor.

## Arco de entrada da torre (1º andar) bloqueando passagem (corrigido)
- `TowerGameController.Entrance.cs`: o portal/arco de entrada do 1º andar (`CreateInnerTowerDoor`, as "Tower inner double doors" do print do usuário — a estrutura com cortina vermelha logo na entrada) ficava bloqueando aquela casa de grade pra sempre depois da cutscene de chegada.
- `CrossesClosedTowerDoor` só checava se `_innerTowerDoor` existia e estava ativo — nunca checava se a porta estava de fato aberta ou fechada. Em `PlayTowerArrival`, a porta fecha (`door.SetOpen(0f)`) no fim da cutscene, mas a referência `_innerTowerDoor` só era limpa no caminho raro (andar gerado sem parede fixa); no caminho normal (andar fixo, `_towerEntranceWidth == 1`, que é o caso do 1º andar) ela nunca era limpa — então aquela casinha específica ficava bloqueada pro resto do andar, pro Koh e pros monstros igual.
- Corrigido: depois da cutscene, `_innerTowerDoor` é zerada também nesse caminho normal, então o arco volta a ser só cenário (igual qualquer outra decoração da torre) e a passagem libera.
- Backup em `~/edits/orig/TowerGameController.Entrance.cs.bak`. Não testado no Unity Editor.

## Raio dos familiares/ovos estocados na Monster Hut (corrigido)
- `TowerGameController.Summoning.cs`, `AddStoredFamiliarLight`: usava `frames[^1]` de `FamiliarLightFrames()`, o último quadro da animação de "tirar familiar do saco" (`PresentFamiliarSummoning`) — esse quadro tem o potinho/saco marrom desenhado junto na parte de baixo do sprite, certo pra animação, mas errado aqui: ao usar como raio estático em cima de um familiar já parado (ou de um ovo), sobrava um potinho flutuando grudado nele.
- Criado `StoredFamiliarBeamSprite()`: recorta só o raio de dentro do mesmo quadro (`SummoningSheet.png`), parando exatamente no corte transparente que já existe entre o raio e o potinho na folha original — sem potinho, do jeito que aparece no jogo retail (print do usuário: só o facho de luz subindo, sem nada embaixo).
- `AddStoredFamiliarLight` agora recebe o alvo (familiar OU ovo) e compensa a escala do pai antes de anexar o raio, porque o sprite do ovo (`CreateEggView`) é escalado em runtime (os familiares já ficavam em escala 1, então isso não muda nada pra eles).
- `TowerGameController.KohHome.cs`, `RefreshHutMonsters`: ovos guardados na Monster Hut (`TryReadHutEgg`/`CreateEggView`) não tinham raio nenhum antes — chamada adicionada ali também, igual já acontecia pros familiares já eclodidos.
- Backups em `~/edits/orig/TowerGameController.Summoning.cs.bak` e `TowerGameController.KohHome.cs.bak2`. Não testado no Unity Editor.

## Ícones "blue_cape" e "trap" (Reimagined/Items)
- Faltavam em `Assets/AzureDreams/Resources/Reimagined/Items/` (usados por `ReimaginedItemSprite`, que é o único ponto do código que lê essa pasta, tanto pro item no chão quanto no inventário/loja). As artes-fonte já estavam em `_Redesigned assets/item_icons/blue_cape.png` e `trap.png` (1254×1254, RGBA, já com fundo transparente, mesmo padrão dos outros ícones dessa pasta).
- Gerados `blue_cape.png`/`trap.png` em 128×128 RGBA (redimensionamento Lanczos a partir da arte-fonte, sem recorte — o conteúdo já ocupa quase todo o canvas 1254×1254, igual aos demais ícones da pasta), batendo com o tamanho de todos os outros ícones já existentes em `Resources/Reimagined/Items/` (herb, scroll, sword, roses etc., todos 128×128). `.meta` criado espelhando as configurações de importação de `herb.png.meta` (textura padrão, não comprimida sRGB, sprite criado em runtime via `Sprite.Create`), com GUID novo para cada um.
- Não mexi no atlas clássico (`GroundItemIcons.png`, 16×8 células de 144×144 = ícones originais do PS1 em 9×): "blue_cape" já tem slot reservado lá (índice 29 em `GroundIconNames`) com a arte real extraída do jogo; "trap" não está nessa lista (só existe como ícone Reimagined).
- Não testado no Unity Editor.

## Shaders: bug no warp afim e novo toggle "PS1 vertex wobble" no menu de gráficos
- Levantamento dos shaders em `Assets/AzureDreams/Shaders/` e `Resources/Shaders/` via GUID (não pelo nome do shader, que não aparece nos `.mat`): `PsxVertexLit.shader` está em ~45 materiais (pisos/paredes da torre, prédios/NPCs da cidade gerada), `PsxIndexed.shader` em 255 materiais (texturas CLUT do retail), `FusionGlow.shader` é instanciado por script (fusão de monstros), e `PsxVertexSnapAffine.shader` **não é usado em nenhum material** — só existia solto na pasta, sem nada aplicando-o.
- **Bug corrigido em `PsxVertexSnapAffine.shader`**: a deformação "afim" (que imita a falta de correção de perspectiva da PS1 nas texturas) estava com a matemática invertida. O vértice fazia `uv * (1/w)` e o fragment desfazia com `uv / (1/w)` — mas a interpolação de hardware já divide por `w` e multiplica de volta por `w` do pixel; pré-multiplicar por `1/w` (em vez de por `w`) faz a conta sair errada (um `w²` sobrando), gerando uma deformação sem relação com o efeito pretendido. Corrigido para `uv * w` no vértice e `uv / w` no fragment (w recuperado via o truque clássico de passar `clipPos.w` como variável interpolada).
- **Novo**: como esse shader de exemplo não estava em uso, em vez de aplicá-lo a materiais eu levei a mesma lógica (corrigida) de vertex-snap + warp afim para dentro dos dois shaders que o jogo already usa de verdade (`PsxVertexLit.shader` e `PsxIndexed.shader`), controlada por **propriedades globais de shader** (`_PSXVertexSnapResolution`, `_PSXAffineWarpStrength`, setadas via `Shader.SetGlobalFloat`) em vez de propriedades por material — assim um único toggle no menu afeta todas as superfícies de uma vez, sem precisar trocar material de cada objeto.
  - `PostProcessingController.cs`: novos campos `psxVertexSnap`/`vertexSnapResolution` e `psxAffineWarp`/`affineWarpStrength`, método `ApplyPsxVertexGlobals()` (chama `Shader.SetGlobalFloat`, 0 = desligado = visual inalterado), incluído em `OnEnable`, `ApplyPrefabPreset` e no preset "Pixel Art" (que agora liga os dois por padrão).
  - `TowerGameController.Graphics.cs` (`DrawGraphicsSettings`): dois novos controles — "PS1 vertex snapping" (resolução do encaixe, 60–480) e "PS1 affine texture warp" (força, 0–1) — do lado dos outros efeitos "retro" (quantização de cor, CRT). Chama `effects.ApplyPsxVertexGlobals()` a cada frame que o menu está aberto.
  - As mesmas propriedades globais foram adicionadas também em `RetailTowerTile.shader` e `RetailTowerTranslucent.shader` (piso/parede dos andares com layout retail capturado, `TowerGameController.RetailFloorMesh.cs`) — sem isso, o andar da torre debaixo do jogador ficaria de fora do efeito, já que ele usa esses dois shaders e não `PsxVertexLit`/`PsxIndexed`. `RetailTowerBackdrop.shader` (o cilindro de fundo) ficou de fora de propósito: ele já segue a câmera, então "encaixar"/deformar seus vértices não teria efeito visual perceptível coerente com o das outras superfícies.
- Backups em `~/edits/orig/PsxVertexSnapAffine.shader.bak`, `PsxVertexLit.shader.bak`, `PsxIndexed.shader.bak`, `RetailTowerTile.shader.bak`, `RetailTowerTranslucent.shader.bak`, `PostProcessingController.cs.bak`, `TowerGameController.Graphics.cs.bak`. Não testado no Unity Editor — convém abrir o menu de gráficos e comparar visualmente os dois toggles novos, e confirmar que materiais com `PsxVertexLit`/`PsxIndexed`/`RetailTowerTile`/`RetailTowerTranslucent` continuam idênticos quando ambos ficam desligados (0 nas globais).

## Camera e backdrop da torre (zoom e vazio preto embaixo — corrigidos)
- `TowerGameController.Camera.cs`: `_towerCameraDistance` (distância da câmera da torre, antes do zoom do scroll) estava em `8` — jogador relatou que ficava "muito perto"/zoom errado comparado ao original. Aumentado para `10` (tanto o valor padrão no código quanto o override serializado na cena `Assets/AzureDreams/Scenes/ReimaginedDemo.unity`, que tinha seu próprio `_towerCameraDistance: 8` gravado no componente). O zoom por scroll do mouse continua funcionando igual, de 0.55x a 1.8x em torno desse novo valor.
- `TowerGameController.RetailFloorMesh.cs` (backdrop dos andares com layout retail capturado, i.e. `UsesRetailFloorMesh`): dois problemas distintos, ambos reportados pelo usuário com uma imagem do andar atual:
  1. **Vazio preto embaixo**: o backdrop retail (`BuildRetailBackdrop`/`RebuildRetailBackdrop`, tradução de `project_depth_strips_and_quads`) só desenha 14 "tiras" horizontais de textura numa faixa ao redor da altura da câmera — eram as únicas tiras que existiam no jogo original também, mas lá a tela nunca mostrava o vazio porque a tira mais baixa já cobria a área visível na inclinação de câmera usada. Aqui, ao olhar para baixo/perto do chão, dava para ver direto a cor de fundo sólida (preto) por baixo das tiras. Diferente do backdrop dos andares *gerados* (`BuildTowerBackdrop` em `TowerGameController.FloorMesh.cs`), que já tinha uma "bacia" (bowl) fechando o fundo por esse mesmo motivo (comentário original: "the camera sees a closed environment instead of the outside void") — o backdrop retail nunca teve o equivalente. Corrigido adicionando `BuildRetailBackdropFloorCap()`: um disco preto simples, bem abaixo do chão (y = -15, raio = 1.5x o far clip plane da câmera), filho do mesmo transform do backdrop (`_towerBackdrop`, que já recentraliza em X/Z na posição da câmera todo frame via `UpdateRetailBackdrop`). Como ele usa a mesma cor preta do `clearColor`/fade das tiras, fecha o buraco sem criar uma "linha" visível.
  2. **Backdrop não girava**: no código decompilado (`process_circular_segments`/`project_depth_strips_and_quads`, `dungeon/actor/depth_strip_projection_g00337.c`), as 14 tiras só tinham *scroll vertical* de textura (efeito de profundidade/parallax, baseado no `phase`/`BackdropSpeed` de cada andar) — isso já estava implementado e correto em `RebuildRetailBackdrop`. Só que, diferente do backdrop dos andares gerados (que já girava de verdade em Y via `_towerBackdrop.Rotate(...)` em `UpdateTowerBackdropRotation`), o caminho retail (`UpdateRetailBackdrop`) nunca chamava `Rotate` — o cilindro ficava sempre na mesma orientação, só com a textura escorregando verticalmente. Adicionada a mesma rotação em sentido horário (mesma fórmula/velocidade usada no backdrop gerado: `360° × 2px × 30Hz / 32px × 0.02`), escalada pelo `BackdropSpeed` do andar (dobro na arena do Beldo, igual ao scroll vertical) — agora o backdrop retail gira junto com a textura escorrendo, igual ao visual do original.
- Backups em `~/edits/orig/TowerGameController.Camera.cs.bak`, `~/edits/orig/TowerGameController.RetailFloorMesh.cs.bak` e `~/edits/orig/ReimaginedDemo.unity.bak`. Não testado no Unity Editor — em especial, vale conferir ao vivo se o raio/altura do disco preto (`capRadius`/`capY` em `BuildRetailBackdropFloorCap`) cobrem bem o vão em todos os ângulos de câmera, e se a nova distância de câmera (10) está no ponto certo ou precisa de mais ajuste fino.

## Teleportes da casa do Koh nível 1 (corrigido)
- `TowerGameController.KohHome.cs`, array `HutLinks`: o marcador de passagem para o Monster Hut da casa 1 (`HutLinks[0]`) estava com o mesmo valor bruto `(12, 13)` do marcador da porta de saída (`InitialHouseDoor`), apesar do comentário do próprio código dizer que são dois pontos diferentes ("o tapete marrom à esquerda" vs. "o vão da porta preta capturado em (12, 13)"). Como os dois passam por transformações equivalentes (`HomeLayoutPoint`/`HomeLayoutCell`, mesma reflexão `32 - x` para a casa 1) e caem exatamente no mesmo ponto no mundo, os dois gatilhos de teleporte (voltar para a cidade vs. entrar no Monster Hut) disputavam a mesma célula perto da porta — por isso o comportamento quebrado/ambíguo relatado.
- Correção: `HutLinks[0]` passou de `(12, 13)` para `(12.5, 8.5)`. O valor novo vem da malha real capturada (`Interior_01_koh_house_1/geometry.json`): é o único quadro de piso isolado (1×1, altura 0) perto da porta que não faz parte dos pisos grandes do quarto/sala, a poucos passos da porta na mesma parede — compatível com "o tapete à esquerda" do comentário original. A textura exata desse quadrinho (`page_22_clut_29062`) aparece como um "X" de slot não utilizado na página capturada, então a aparência visual do tapete não pôde ser confirmada 100% pela textura; a escolha ficou baseada na geometria (único candidato plausível de piso isolado na região) e na distância seguro dos outros gatilhos (porta e cama).
- Depois da correção, o marcador do hut fica a ~4,5 unidades da porta (antes: 0), bem acima dos raios de toque usados (0,58–1,2), então os dois gatilhos não se sobrepõem mais.
- Backup do arquivo original em `~/edits/orig/TowerGameController.KohHome.cs.bak`. Não testado no Unity Editor.

## Erros de validação de bounds visuais na cidade (library/alley/gym/theater/casino) — falso positivo, corrigido

- Usuário reportou 5 `Debug.LogError` no console ao entrar na cidade: `'library'`, `'alley'`, `'gym'`, `'theater'`, `'casino'` "no longer covers original lot N", vindo de `TownRetailOriginalObjectLayout.ValidateVisualBounds` (chamado em `TowerGameController.CreateConstructionMesh`).
- Investigação: `CreateConstructionMesh` calcula `visualBounds` a partir de `mesh.bounds` (vértices lidos do binário da construção) e compara contra uma tabela fixa em `TownRetailOriginalObjectLayout.Lots`. Essas 5 construções são exatamente as 5 vagas intercambiáveis de 16×14 (`FlexibleConstructionLots` em `TowerGameController.Construction.cs`, slots 4/5/7/28/31) — qualquer uma das 5 pode ocupar qualquer uma das 5 vagas. O `BuildingProject.Bounds` de cada uma (calculado à mão, já inclui beiral/telhado) é **de propósito** menor que o lote genérico de 16×14 inteiro (sobra calçada/margem ao redor do prédio) — não é regressão nenhuma, a malha bate exatamente com o `Bounds` cadastrado de cada prédio.
- Ou seja: o validador estava certo para os 7 prédios de lote fixo (home_1/2, hospital, temple, fountain, circuit, arcade — cada um com vaga dedicada do próprio tamanho), mas não se aplica aos 5 prédios de vaga flexível, cujo "lote" é um terreno genérico maior que a própria construção.
- Corrigido em `TownRetailOriginalObjectLayout.cs`: adicionado `Lot.FlexibleParcel` (bool). Os 5 lotes flexíveis (library, alley, gym, theater, casino) agora marcam `flexibleParcel: true` e `IsContainedBy` pula a checagem de contenção para eles (continuam na tabela só para manter o nome "— retail lot N (x,y)" no `ObjectName`). Os outros 7 continuam validados normalmente.
- Nenhuma mudança de comportamento visual/gameplay — só parou de logar erro falso no console. Backup em `~/edits/orig/TownRetailOriginalObjectLayout.cs.bak`.

## Koh pequeno demais em interiores da cidade (corrigido)

- Usuário relatou (com print, dentro de um interior com piso de madeira/tapete redondo — provavelmente o boliche) que o Koh (personagem vermelho) ficou visivelmente menor que os NPCs ao lado.
- Causa: `ConfigurePlayerView` (`TowerGameController.Movement.cs`) aplicava um `framingScale = InteriorCameraDistance / _townCameraDistance` (7 / 9.5 ≈ 0,737) só no sprite do próprio Koh sempre que `_interior` é verdadeiro — a intenção era compensar a câmera de interior ficar mais perto (o que, por perspectiva, faria tudo parecer maior). Só que **nenhum outro personagem** (`TownCharacterSpriteView`, usado por todos os moradores comuns; `SilverSpriteView`/`RacetrackResidentSpriteView`; `NicoSpriteView`; etc.) tem esse tipo de compensação — eles são desenhados sempre no tamanho autoral, deixando a perspectiva agir livremente. Resultado: só o Koh encolhia ~26% dentro de qualquer prédio, enquanto todo o resto do elenco ficava normal (maior, por estar mais perto da câmera) — exatamente a assimetria do print.
- Mesmo achado já registrado antes na seção "Koh esticado na torre": o jogo retail nunca corrige a escala do sprite por causa de distância/ângulo de câmera, só deixa a perspectiva natural do motor agir.
- Correção: removida a compensação de interior tanto do Koh (`ConfigurePlayerView`, agora sempre passa `framingScale = 1f` pra `KohSpriteView.ConfigurePresentation`) quanto do NPC especial "wookie" (`SpawnStoryResident` em `TowerGameController.StoryActors.cs`, que tinha a mesma fórmula pra bater com a altura do Koh — removida junto, senão os dois saem de sincronia de novo). `KohSpriteView` e `TowerGameController.Camera.cs` continuam com `InteriorCameraDistance`/`_townCameraDistance` normalmente (isso só controla a posição/distância real da câmera, não mudou).
- Auditoria geral de personagens (a pedido do usuário): `pixelsPerUnit` de cada folha de sprite (Koh 48, moradores comuns 48, Nico 30, trajes de piscina 32, vaso decorativo ~28,6) são valores autorais distintos e corretos — cada folha tem resolução nativa diferente e o PPU converte pro tamanho de mundo certo; não é inconsistência, é o esperado. Não achei nenhum outro personagem com escala/framing fora do padrão.
- Backups em `~/edits/orig/TowerGameController.Movement.cs.bak`, `TowerGameController.StoryActors.cs.bak`. Não testado no Unity Editor.

## Inventário: familiares não entravam na contagem "Items X/Y" (corrigido)

- Pedido do usuário: contar os familiares guardados no total mostrado em "Items X/Y" do menu de inventário.
- Em `TowerGameController.Inventory.cs` (`DrawTowerInventory`), o cabeçalho mostrava `itemCount/Inventory.SlotCapacity`, onde `itemCount = count - FamiliarInventoryCount` — ou seja, excluía os familiares da contagem, mesmo os familiares ocupando os mesmos 20 slots compartilhados do inventário (confirmado por `TowerGameController.HomeStorage.cs`, que já soma `_townInventory.TotalItems + FamiliarInventoryCount` contra o mesmo `Inventory.SlotCapacity` ao mostrar o total no depósito). Resultado: com familiares guardados na bolsa, o "Items X/Y" mostrava um X menor que o uso real da bolsa.
- Corrigido: o cabeçalho "Items" agora mostra `count/SlotCapacity` (`count` já é o total itens+familiares, via `VisibleItemCount`). De brinde, a aba "Familiars" (que antes só mostrava o nome, sem número) agora também mostra `FamiliarInventoryCount/SlotCapacity`.
- O botão "Sort" continua usando `itemCount` (só itens) pra decidir se aparece, já que ordenação não reorganiza familiares.
- Backup em `~/edits/orig/TowerGameController.Inventory.cs.bak`. Não testado no Unity Editor.

## Fonte OpenDyslexic em todo o jogo + cores do log (dano vermelho, XP azul)

- Fonte de origem: `opendyslexic/OpenDyslexic-Regular.otf` (pasta fornecida pelo usuário). Importada em `Assets/AzureDreams/Fonts/OpenDyslexic/OpenDyslexic-Regular.otf` com `.meta` novo (`TrueTypeFontImporter`, mesmas configurações dos outros `.ttf` já no projeto: `fontSize 16`, `includeFontData 1` etc.).
- Achado importante: toda a UI do jogo (diálogo, log de combate, menus, inventário, loja, configurações, título, tutorial — tudo que usa `GUILayout`/`GUIStyle`) já passa por um único ponto central: `TowerGameController._windowFont` (campo serializado), aplicado a `_windowSkin` inteiro em `InitialiseWindowSkin`. Esse campo, na cena `ReimaginedDemo.unity`, apontava pro `LiberationSans.ttf` (fonte padrão que vem com o pacote TextMesh Pro — não tinha nenhuma fonte "PSX" de propósito, mesmo havendo duas fontes comunitárias soltas em `Fonts/Community/ADStateDisplay/` (`azure_dreams_dialogue.ttf`, `azure_dreams_menu.ttf`) que não estão referenciadas em lugar nenhum da cena/prefabs — leftovers não usados). Troquei o GUID do `_windowFont` na cena pra apontar pro novo OpenDyslexic — isso sozinho já cobre praticamente 100% do texto do jogo.
- Único lugar fora desse sistema: os rótulos de pagamento da roleta do cassino (`TowerGameController.Casino.cs`, `SetupRouletteRoom`), que usavam um `TextMesh` 3D no mundo com a fonte builtin do Unity (`LegacyRuntime.ttf`). Trocado pra usar o mesmo `_windowFont` (com fallback pra builtin se por algum motivo não estiver setado).
- Não toquei nas duas fontes comunitárias não utilizadas nem no `LiberationSans.ttf` do pacote TextMesh Pro (não é nosso, e nada do projeto usa TMP de verdade).
- **Cores do texto** (pedido do usuário: branco pra diálogo/log, azul pra ganho de XP, vermelho pra dano):
  - Diálogo e log já eram brancos por padrão (`_bodyStyle.normal.textColor = Color.white`, herdado por `_messageStyle`/`_dialogueTextStyle`/`_logLineStyle`) — mantido, nenhuma mudança necessária aí.
  - **Dano**: `TowerGameController.Movement.cs`, novo helper `DamageLogSpan(int damage) => $"<color=#FF5454>{damage}</color>"`, usado nos dois pontos que constroem texto de dano pro log (`RecordTowerHit` — dano de ataque — e `RecordTowerHazard` — dano de armadilha/perigo), tanto em inglês quanto em português. Habilitei `richText = true` explicitamente em `_bodyStyle` (e por herança em `_messageStyle`/`_dialogueTextStyle`/`_logLineStyle`) pra garantir que a tag realmente renderize como cor em vez de aparecer como texto literal. Conferido que o quebra-linha do log (`WrapTowerLogLines`) mede largura via `GUIStyle.CalcSize`, que já ignora tags de rich text corretamente, então a tag não atrapalha a quebra de linha.
  - **Ganho de XP**: o aviso flutuante "+N EXP" (`_experienceNotice`, mostrado só na torre por 2s após subir de nível/ganhar XP) ganhou seu próprio estilo `_experienceNoticeStyle` (cópia de `_bodyStyle` com a cor `TowerStatusColors.Paralysis` — o mesmo azul já usado no jogo pro status de paralisia, reaproveitado pra manter a paleta consistente, em vez de inventar um azul novo).
  - "Etc" do pedido: não inventei mais categorias (cura, level up, item, etc.) sem confirmar — dano e XP são as únicas explicitamente citadas. Se quiser mais categorias coloridas (cura em verde, por exemplo), é só pedir.
- Backups em `~/edits/orig/ReimaginedDemo.unity.bak2`, `TowerGameController.cs.bak`, `TowerGameController.Movement.cs.bak2`, `TowerGameController.Casino.cs.bak`. Não testado no Unity Editor — abrir a cena e checar se a fonte carrega (GUID novo) e se o log/diálogo/menus aparecem com OpenDyslexic, e se um dano em combate mostra o número em vermelho.

## Proporção do Koh inconsistente entre cidade/interiores/torre + respiração no idle (corrigidos)

- **Causa da proporção diferente**: `KohSpriteView.LateUpdate` fazia o sprite do Koh girar só no eixo Y pra acompanhar a câmera (`Quaternion.Euler(0, camera.eulerAngles.y, 0)`) — um billboard "vertical fixo" que NÃO acompanha o pitch da câmera. Um billboard assim sofre achatamento genuíno quanto mais a câmera olha de cima: cidade usa pitch 35°, interior 55°, torre 56,25° — por isso achatava mais dentro de prédios/torre que na cidade. Enquanto isso, **todo NPC e monstro** usa `Rendering.CameraFacingSprite`, que copia a rotação INTEIRA da câmera (`transform.rotation = camera.transform.rotation`) — billboard completo, sem achatamento em nenhum ângulo. Só o Koh estava divergente.
- Corrigido: o billboard do Koh agora usa a rotação completa da câmera (`billboard = camera.transform.rotation`), igual ao `CameraFacingSprite`. Mesma proporção em qualquer pitch de câmera, igual ao resto do elenco. Isso também é coerente com a remoção do `_uprightHeightScale` feita antes (seção "Koh esticado na torre"): sem achatamento nenhum pra compensar, não precisa de nenhuma correção de escala por ângulo — a causa raiz dos dois problemas (esticado antes, achatado agora) sempre foi essa incoerência entre o billboard do Koh e o dos outros personagens.
- **Respiração no idle**: não existe nenhuma folha de sprites de "idle animado" pro Koh (parado ele usa um único frame estático, `cycle.idle`) — o que existia era um `idleBob` que deslocava o sprite inteiro (incluindo os pés) pra cima e para baixo, parecendo flutuar/saltitar, não respirar. Trocado por um leve "stretch" vertical (~2,5%, ritmo lento, `Mathf.Sin` em vez do `1-cos` rápido de antes) aplicado via `transform.localScale.y`, em vez de posição. Como o pivô do sprite do Koh fica perto dos pés (`pivotY` em `CreateRetailBodyFrame`), esticar a escala a partir desse pivô mantém os pés parados no chão e concentra o movimento em quem está mais longe do pivô — peito e cabeça — lendo como respiração em vez de um balanço do corpo inteiro. Só ativa quando ele está mesmo parado (não nadando, comendo, dormindo, paralisado, andando, no ar, carregando, empurrando, atacando ou subindo de nível).
- Backup em `~/edits/orig/KohSpriteView.cs.bak2`. Não testado no Unity Editor.

## Arco da torre ainda bloqueava (causa diferente: pular a conversa do Kewne) + line height da fonte nova (corrigidos)

- **Arco do 1º andar ainda bloqueando**: a correção anterior ("Arco de entrada da torre bloqueando passagem") tratou o caminho normal (cutscene de chegada completa), mas havia um segundo caminho que eu não tinha visto: segurar cancelar durante a conversa inicial do Kewne chama `SkipKewneMeeting()` (`TowerGameController.Tutorial.cs`), que seta `_kewneSkipTransition = true` e chama `EnterTower()` direto. Em `EnterTower` (`TowerGameController.cs`), `BuildTowerFloor`/`BuildManagedTowerFloor` sempre criam a porta do 1º andar fechada (`CreateInnerTowerDoor`), mas a única coisa que abre e depois zera a referência `_innerTowerDoor` é a coroutine `PlayTowerArrival` — que só roda quando `!_kewneSkipTransition`. Resultado: pulando a introdução do Kewne, a porta nascia fechada e **nunca** era aberta nem liberada — bloqueio permanente pro resto da partida, exatamente o print do usuário (arco com cortina vermelha, intransponível).
- Corrigido: em `EnterTower`, quando `_kewneSkipTransition` é verdadeiro (pulando a cutscene de chegada também), a porta é aberta e a referência `_innerTowerDoor` zerada na hora, em vez de só quando a coroutine roda.
- **Line height da fonte OpenDyslexic cortando texto**: achei o ponto concreto de corte — `DrawMessageWindow` (caixa de diálogo/log de combate) usava `const float logLineHeight = 21f`, um valor fixo calibrado pro LiberationSans (fonte antiga, mais compacta) a 15pt, usado como altura de cada linha desenhada com `_logLineStyle`, que tem `TextClipping.Clip` ligado. A OpenDyslexic é visivelmente mais alta (x-height maior, ascendentes/descendentes mais grossos) — a 15pt, a altura real da linha passava de 21px, cortando a base de letras com descendente (g, y, p, j) e deixando linhas quebradas mais coladas do que deveriam.
- Corrigido: `logLineHeight` agora é medido na hora com `_logLineStyle.CalcHeight(new GUIContent("Ag"), 1000f)` (uma string com ascendente e descendente, pra pegar a altura real que a fonte ativa precisa), com piso em 21 (nunca fica menor que o valor antigo). Assim continua funcionando mesmo se a fonte trocar de novo no futuro, sem precisar recalibrar um número mágico toda vez.
- **Observação pra depois**: a tela de salvar/carregar (`TowerGameController.SaveSummary.cs`, `DrawSaveSlotList`/`DrawSaveSlotSummary`) também usa `TextClipping.Clip` com linhas de 18-19px a 13pt (`_saveSlotLabel`) — razão parecida (18/13 ≈ 1,38, igual ao 21/15 que causou o corte no log). Não mexi ainda porque lá as linhas já ficam com um pouco de sobreposição intencional nos Rects (compensada pelo alinhamento vertical centralizado do texto), e mudar a altura sem também reajustar os deslocamentos Y de cada linha podia trocar "corte" por "sobreposição" sem eu conseguir ver o resultado no Editor. Se depois de testar o log/diálogo ainda aparecer corte na tela de save, aviso que é o próximo lugar a olhar.
- Backups em `~/edits/orig/TowerGameController.cs.bak2` (porta) e `~/edits/orig/TowerGameController.cs.bak3` (line height — aplicado por cima do `.bak2`, ambos preservam o estado anterior a cada edição). Não testado no Unity Editor.

## Texto do Inventario ainda cortado apos a correcao da fonte (corrigido)

Problema: mesmo depois do ajuste de `logLineHeight` no log de combate/dialogo,
a tela de Inventario continuava mostrando o topo das letras cortado (ex.:
"Serapim", "Antidoto") nas linhas da lista de itens. Print do usuario:
"AINDA ESTA CORTADO".

Causa raiz: a lista de Inventario (`TowerGameController.Inventory.cs`) desenha
cada linha em um `Rect` de altura FIXA (23f, espacamento 24f), calibrado para
a fonte antiga. A OpenDyslexic tem metricas de ascent/descent maiores que a
fonte antiga para o mesmo `fontSize`, entao o texto de 17pt (`_bodyStyle`)
nao cabia mais dentro da linha de 23px - o topo das letras ficava cortado.
Esse mesmo padrao (fontSize ocupando quase 100% da altura da linha) existia
em varios outros lugares do codigo (loja, tela de saves, titulo, config, HUD
de HP em portugues).

Correcao: em vez de reescrever a geometria de cada tela (o que arriscava
quebrar o numero de linhas visiveis por pagina, paginacao, etc.), reduzi o
`fontSize` de todos os estilos de texto legados que usam largura/altura fixa,
dando margem de respiro para a OpenDyslexic:

- `_bodyStyle` (texto padrao, usado no Inventario): 17 -> 15
- `_messageStyle` (dialogo/log): 15 -> 13
- `_shopDialogueStyle` (loja do monstro): 15 -> 13
- `_saveSlotLabel`/`_saveSlotHeading` (tela de save): 13 -> 12
- `_settingsHeadingStyle` (titulo das configuracoes): 17 -> 15
- `_dialogueSkipStyle` (aviso de pular dialogo): 14 -> 12
- `_portugueseHpStyle` (HUD de HP em portugues): 14 -> 12
- `_titleOptionStyle` (menu do titulo, `Title.cs`): antes usava
  `fontSize = rowHeight` (ocupando 100% da altura da linha, sem margem
  nenhuma); agora usa `fontSize = rowHeight * 0.78f`, deixando ~22% de
  espaco livre para o ascent/descent da fonte.

Como `InventoryNameStyle()` deriva de `_bodyStyle` sem sobrescrever o
`fontSize`, a correcao cobre diretamente a lista de itens e familiares do
Inventario, alem de proteger preventivamente as outras telas que usam o
mesmo padrao de Rect de altura fixa.

Arquivos alterados: `TowerGameController.cs`, `TowerGameController.MonsterShop.cs`,
`TowerGameController.SaveSummary.cs`, `TowerGameController.Settings.cs`,
`TowerGameController.Tutorial.cs`, `TowerGameController.RetailArt.cs`,
`TowerGameController.Title.cs`.

## Botao "Continue" trocado por icone de canto de pagina (no estilo do original)

Pedido: nas caixas de dialogo onde a unica opcao e "Continue"/"Continuar" (fala
de NPC/historia sendo apenas confirmada, sem escolha real), trocar a barra
inteira de botao por um simbolo pequeno no canto inferior direito, como uma
pagina de livro sendo levantada - igual existe no jogo original.

Causa do visual antigo: toda fala de historia de uma linha so (`StoryTalk` em
`TowerGameController.TownStories.cs`) sempre passa por `StoryAsk` com uma
unica escolha rotulada "Continue"/"Continuar". Essa escolha unica caia no mesmo
caminho de `DrawStoryChoices`, que desenha uma barra de botao full-width
(`DrawInventoryButton`) reservando uma linha inteira de altura no painel -
dai o retangulo azul "Continue" do print.

Correcao (`TowerGameController.cs`, metodo `DrawMessageWindow`): adicionada
a flag `singleContinueChoice` (verdadeira quando ha exatamente 1 escolha de
historia com o rotulo "Continue"/"Continuar"). Quando verdadeira:
- `choicesHeight` fica 0 (nao reserva mais a linha do botao no painel);
- o indicador de texto "  ›" (ja usado para dialogo simples sem escolha) fica
  suprimido para nao duplicar com o novo icone;
- em vez de `DrawStoryChoices`, chama o novo `DrawContinuePrompt(panel)`.

`DrawContinuePrompt` desenha um icone pequeno (20x20) ancorado no canto
inferior direito do proprio painel de dialogo, com uma leve animacao de
"respiracao" vertical (seno, mesma tecnica ja usada na animacao idle do Koh)
simulando a pagina sendo levantada. O icone e gerado em runtime por
`BuildPageCornerIcon()` (textura 28x28 procedural, sem precisar de asset
importado): um triangulo no canto representando a dobra da pagina, com tom
pergaminho no meio, uma borda clara na diagonal (brilho da aba levantada) e
uma sombra no bico bem no canto (vinco da dobra). O painel inteiro continua
clicavel (`GUI.Button` invisivel sobre o `panel`) chamando
`PickStoryChoice(0)`, entao clicar em qualquer lugar da caixa tambem avanca -
alem do botao de interagir do teclado/controle, que ja funcionava antes sem
precisar do botao desenhado (`HandleTownStory` ja despachava
`PickStoryChoice(_storyChoice)` direto no `_interact.WasPressedThisFrame()`).

Esse caminho so se aplica ao caso de escolha UNICA chamada "Continue"; dialogos
com 2+ opcoes reais (sim/nao, multiplas respostas de historia, escolhas de
visitante etc.) continuam desenhando os botoes normalmente.

Arquivo alterado: `TowerGameController.cs` (metodo `DrawMessageWindow` +
novos metodos `DrawContinuePrompt` e `BuildPageCornerIcon`).

## Arcos do primeiro andar ainda bloqueando Koh (causa raiz real: coroutine abortada deixava a porta travada)

Pedido: "ainda tem arcos no primeiro andar que nao deixam koh passar. revise
todos os arcos" - apos a correcao anterior (caminho de pular a conversa do
Kewne), o problema continuava aparecendo.

Investigacao: no jogo so existe UM objeto de porta jogavel no andar (campo
unico `_innerTowerDoor` em `TowerGameController.Entrance.cs`) - todos os
outros arcos visiveis no andar sao so decoracao do gerador de dungeon
(`RetailTowerFloor`/`Doorway()`, fiel ao original), sem logica de
bloqueio propria. Entao "varios arcos travando" na pratica sempre volta a
esse mesmo mecanismo de bloqueio da porta de entrada do andar 1
(`CrossesClosedTowerDoor`), so que podendo travar em ocasioes diferentes.

Causa raiz encontrada: `CrossesClosedTowerDoor` bloqueia o andar inteiro
enquanto o campo `_innerTowerDoor` apontar pra qualquer porta, nao importa
se ela esta visualmente aberta ou fechada - o `TownGateDoor` nao guardava
nenhum estado de "o quanto esta aberta", so a rotacao visual das folhas. A
limpeza desse campo (`_innerTowerDoor = null`) so acontecia no FINAL da
corrotina `PlayTowerArrival()` (a caminhada de chegada). Mas o loop dessa
corrotina tinha uma guarda `if (!_towerArrivalPlaying || !actor || !door)
yield break;` logo apos o `for`: se a sequencia fosse interrompida no meio
(por exemplo via `StopTowerEntrance()`, ou o ator/porta sendo destruidos no
meio da animacao) esse `yield break` pulava DIRETO por cima da limpeza -
a porta ficava fisicamente meio aberta/fechada e o `_innerTowerDoor`
continuava apontando pra ela, bloqueando aquele par de tiles para sempre
pelo resto do andar (tanto para Koh quanto para monstros).

Correcao (duas camadas, pra nao depender so de lembrar de limpar a
referencia em cada caminho novo que aparecer no futuro):

1. `TowerGameController.Entrance.cs`, `PlayTowerArrival()`: trocada a guarda
   `yield break` por limpeza incondicional apos o loop - `if (door) {
   door.SetOpen(0f); ... }`, `if (actor) { ... }`, `_innerTowerDoor = null`
   sempre executam, nao importa como o loop terminou.
2. `TownGateDoor.cs`: adicionada a propriedade `public float OpenAmount`,
   atualizada dentro de `SetOpen`. `CrossesClosedTowerDoor` agora tambem
   checa `_innerTowerDoor.OpenAmount >= 0.98f` alem da referencia existir -
   assim, mesmo que uma referencia fique esquecida em algum caminho futuro,
   uma porta realmente aberta nunca mais bloqueia o andar. As duas camadas
   sao independentes: so uma delas falhar nao trava mais o jogo.

Arquivos alterados: `TowerGameController.Entrance.cs`, `TownGateDoor.cs`.

## Habilidades especiais e traits passivos dos monstros (parcial)

Pedido: implementar habilidades especiais (ex: Dragon ingerir metal, Manoeva
copiar um alvo) e traits passivos (ex: narcolepsia do Naplass, resistencia a
sono do Dreamin repassada a Koh) dos monstros, usando o decomp do jogo
original como referencia.

O que foi implementado:

1. **Narcolepsia do Naplass**: a cada turno em que o Naplass esta ciente de
   Koh, ha 1 chance em 32 de ele cochilar sozinho por 4 turnos (igual ao
   original, ver `start_naplass_status_action.c` no decomp). Isso usa o
   mesmo gate generico de imunidade a status (`Status()`), entao se o
   Naplass algum dia ganhar um talento de Sleep-proof (por fusao, por
   exemplo), a narcolepsia deixa de valer automaticamente. Essa habilidade
   e involuntaria, por isso foi deliberadamente mantida FORA do sistema
   `HasSpeciesCommand`/menu de comando da coleira (que e para habilidades
   que o jogador pode mandar um familiar domesticado usar, como Mergulhar
   ou Criar Pedra) - um Naplass domesticado nao ganha um comando manual
   "dormir" no menu, ele continua cochilando por conta propria.
   Novo: `NaplassNarcolepsy()`, `HasInvoluntarySpeciesAction()` em
   `TowerGameController.SpeciesActions.cs`; ponto de disparo ajustado em
   `TowerGameController.Movement.cs`.

2. **Resistencia a sono do Dreamin repassada a Koh**: enquanto o Dreamin
   estiver fora da bolsa e acompanhando Koh ativamente (nao guardado), o
   talento de Sleep-proof do Dreamin tambem protege Koh contra ser
   adormecido - imitando o traco "resistencia emprestada pelo familiar
   ativo" do jogo original. A implementacao e generica: qualquer talento de
   imunidade a status que um familiar ativo tenha (Sleep-proof,
   Confusion-proof, Blind-proof, Paralysis-proof, Poison-proof,
   Magic-Bind-proof, Brainwash-proof) passa a proteger Koh tambem, nao so
   o sono do Dreamin especificamente. Essa uniao foi restrita de proposito
   ao unico ponto onde o jogo decide "esse status pega ou nao" (dentro de
   `Status()`), e nao ao `HasTalent()` em geral, para nao vazar talentos de
   atributo do familiar (Quick, HP Increased, Growth Promoted etc.) para
   Koh - so resistencias a status sao compartilhadas.
   Novo: `HasActiveFamiliarImmunity()` em
   `TowerGameController.ItemEffects.cs`.

O que **nao** foi implementado nesta passada, e por que:

- **Dragon ingerir metal**: o JSON `unit_progression.json` ja tem o campo
  `likesMetal` por unidade, mas o struct `UnitGrowth` em
  `TowerGameController.Experience.cs` ainda nao deserializa esse campo (so
  le `likesPitas`/`likesBigPitas`), e nao existe nenhum caso de
  Metal/Ovo/Vegetal/Carne em `ApplyInventoryEffect`
  (`TowerGameController.ItemEffects.cs`) - so Herb/Fruit/Seed/Scroll/
  Bell/Glasses/Loupe/Ball/Crystal. Alem disso, nao foi possivel confirmar
  se `ItemSystem.Kind` (dentro da DLL fechada `AzureDreams.Core.dll`) tem
  de fato um membro `Metal` - essa DLL e pre-compilada e fechada, e este
  ambiente nao tem nenhuma ferramenta de reflexao/decompilacao .NET para
  verificar com certeza (so o utilitario `strings`, que e inconclusivo
  para confirmar membros de enum). Arriscar uma referencia a um membro de
  enum que pode nao existir quebraria a compilacao sem nenhuma forma de
  detectar isso aqui. Fica como proximo passo, idealmente confirmando no
  Unity Editor se `ItemSystem.Kind.Metal` existe antes de implementar.

- **Manoeva "copiar um alvo"**: ja esta implementado em
  `TowerGameController.SpeciesActions.cs`
  (`QueueManoevaSplit`/`ResolveManoevaClones`), mas o mecanismo real (confirmado
  no decomp, `copy_linked_position_and_check_status.c`) e o Manoeva se
  clonar/dividir quando leva dano - nao copiar atributos de um alvo. Nenhuma
  mudanca foi necessaria.

- **Demais ~19 especies com IA unica no jogo original** (conforme o
  dispatcher `select_creature_action_and_target.c` do decomp): Baloon,
  Troll, Golem, Tyrant, Weadog, Garuda, Killer, Stealth, Zu, Mandara,
  Picket, Viper, Blume, Saber/Snowman, Noise, Cyclone, Pulunpa, e a
  habilidade ATIVA do Dreamin de adormecer um alvo adjacente (separada do
  seu trait passivo de Sleep-proof, que ja existia no JSON e agora tambem e
  repassado a Koh). Nenhuma delas foi portada ainda - ficam como escopo de
  uma proxima passada, caso o usuario queira.

Arquivos alterados: `TowerGameController.SpeciesActions.cs`,
`TowerGameController.Movement.cs`, `TowerGameController.ItemEffects.cs`.

---

## Sessao: IA de monstros - Garuda, Nyuel, Dragao, ordem de turno, Manoeva e ~6 especies novas

**Correcao importante sobre o Manoeva**: a nota da passada anterior dizia
que o Manoeva se clona/divide ao receber dano
(`QueueManoevaSplit`/`ResolveManoevaClones`), baseada no arquivo do decomp
`copy_linked_position_and_check_status.c`. Ao reler esse arquivo com mais
calma nesta passada, ficou claro que ele e so um helper de baixo nivel de
copia de posicao/sprite para uma cadeia de entidades ligadas (renderizacao),
nao evidencia de um mecanismo de "clonar ao levar dano". A habilidade real
do Manoeva, confirmada em `select_creature_action_and_target.c` (label
`check_forward_target`), e **disfarce**: ele mira a celula na sua frente e,
se houver um monstro elegivel ali, copia-o (nunca copia outro Manoeva); se
nao houver monstro mas houver item no chao, copia o item. O codigo antigo de
clonagem foi removido e substituido pelo disfarce de verdade (detalhes
abaixo). Isso tambem corrige a ressalva anterior "Nenhuma mudanca foi
necessaria" sobre o Manoeva - mudou tudo.

1. **Ordem de turno dos monstros**: os monstros decidiam seu alvo/direcao
   usando `_playerCell` (posicao de Koh ANTES de ele se mover neste turno),
   mesmo que `ResolveTurn` (fechado, da DLL) aplique o movimento de Koh com
   prioridade mais alta. Isso fazia a IA reagir a posicao antiga de Koh, um
   turno "atrasada". Agora o alvo/linha de visao dos monstros usa
   `kohActedCell` (= `destination`, a celula onde Koh vai efetivamente
   ficar neste turno), como se os turnos fossem sequenciais. O check de
   ocupacao fisica usado para validar o ataque aleatorio de um monstro
   confuso continua usando `_playerCell` de verdade (isso nao e decisao de
   IA, e validacao de ocupacao).
   Alterado: `TowerGameController.Movement.cs` (`CommitTowerTurn`).

2. **Abduct do Garuda**: 1 chance em 16 por turno de levantar um familiar
   adjacente (vermelho ou azul - nunca Koh, por construcao: so varre os
   dois slots de familiar, igual ao decomp) e teleporta-lo para uma celula
   aberta aleatoria do andar, reaproveitando o mesmo teletransporte das
   armadilhas de warp (`WarpTrapActor`). Garuda entra em
   `HasInvoluntarySpeciesAction` (junto com o Naplass) para nunca aparecer
   como comando manual no menu da coleira.
   Novo: `TryGarudaAbduct()` em `TowerGameController.SpeciesActions.cs`.

3. **Nyuel recupera MP com qualquer erva**: antes so curava MP com Pita
   Fruit (como qualquer outra especie); agora, ao comer QUALQUER erva
   (`ApplyHerb`), o Nyuel recupera MP total, independente do tipo.
   Alterado: `ApplyHerb()` em `TowerGameController.ItemEffects.cs`.

4. **Dragao come metal**: o campo `likesMetal` ja existia no JSON
   `unit_progression.json` mas nao era lido (`UnitGrowth` nao tinha o
   campo), e nao existia nenhuma logica de "item e metal". Corrigido:
   `UnitGrowth` ganhou o campo `likesMetal`; `ApplyInventoryEffect` ganhou
   um caso para itens de metal (`IsMetalItem`: todas as espadas e todos os
   escudos exceto Wood/Leather/Mirror/Diamond, ids confirmados no catalogo
   `Resources/Town/Retail/inventory_text.json`) que recupera MP do monstro
   de acordo com `likesMetal`, igual ao padrao ja usado para
   likesPitas/likesBigPitas. Confirmado via a ferramenta de reflexao .NET
   (item 6) que `ItemSystem.Kind` NAO tem um membro `Metal` - por isso a
   deteccao e por `Kind == Sword || (Kind == Shield && subtipo nao excluido)`
   em vez de um enum dedicado.
   Alterado: `TowerGameController.Experience.cs` (`UnitGrowth`),
   `TowerGameController.ItemEffects.cs` (`IsMetalItem`, `ApplyMetal`).

5. **Ferramenta de reflexao .NET**: como pedido, foi instalado um leitor de
   metadados .NET (ECMA-335) do zero em Python puro (sem rede, sem sudo,
   sem dependencias - nenhuma das tres formas usuais de instalar uma
   ferramenta de reflexao funcionou neste ambiente). Fica em
   `~/tools/dotnet_reflect.py` na maquina onde o Unity roda (fora do
   repositorio do jogo). Uso: `python3 dotnet_reflect.py <dll> list-types`,
   `... type NomeDoTipo` (lista campos, incluindo valores de enum) ou
   `... find trecho` (busca por nome). Foi usado para confirmar os valores
   de `ItemSystem.Kind` (item 4) e de `MonsterSpecies` (itens 6-7).

6. **Disfarce do Manoeva** (ver correcao no topo desta secao): a partir do
   andar 16 (ou na segunda torre - `ManoevaCanDisguise`, limiar escolhido
   por nao haver esse dado exato extraido; andares 5-19 do Manoeva ficam
   sem a habilidade, 27-30 sempre tem por estarem na segunda torre e acima
   do andar 16) o Manoeva mira a celula na frente dele: se houver outro
   monstro (nao outro Manoeva), copia-o; senao, se houver item no chao,
   copia o item. Enquanto disfarçado ele fica parado (nao se move), mas
   continua atacando se um alvo chegar adjacente. Atacar ou receber
   qualquer dano desfaz o disfarce (ganchado tanto no dano calculado contra
   monstros quanto contra o familiar/aliado azul, em
   `TowerGameController.ItemEffects.cs`). Enquanto disfarçado, nenhum outro
   monstro vai escolher esse Manoeva como alvo (exclusao em
   `targetFamiliar`/`targetBlue`). Familiares (vermelho e azul) tambem
   podem se disfarcar automaticamente pela mesma regra; um familiar
   disfarcado de ITEM nao pode se mover (familiar disfarcado de monstro
   continua se movendo normalmente, conforme pedido), e o jogador pode
   mandar o familiar voltar a forma original a qualquer momento pelo novo
   comando "Forma Original" no menu da coleira (`ManualOrder.Species`).
   Novo: `_manoevaDisguised`, `ManoevaCanDisguise`, `TryManoevaDisguise()`,
   `RevertManoevaDisguise()` em `TowerGameController.SpeciesActions.cs`;
   pontos de disparo/revert/bloqueio de movimento em
   `TowerGameController.Movement.cs`; comando manual em
   `TowerGameController.InventoryMenu.cs`. Removido:
   `QueueManoevaSplit`/`ResolveManoevaClones`/`_manoevaCopies`/
   `_manoevaCloneCandidates` (mecanismo antigo, baseado no entendimento
   errado do decomp).

7. **~6 das ~19 especies com IA propria que faltavam**: ao investigar o
   dispatcher `select_creature_action_and_target.c` do decomp, descobri que
   quase toda especie ja tem um `hiddenSpellId` configurado em
   `unit_progression.json`, e o sistema generico de magia de monstro
   (`PrepareMonsterSpell`) ja usa esse spell automaticamente a distancia -
   ou seja, Golem (paralisia a distancia), Stealth (cegueira a distancia),
   Tyrant (dano a distancia), Killer (dano a distancia), Noise e Dreamin
   (sono a distancia - a habilidade ATIVA do Dreamin, separada do trait
   passivo de Sleep-proof) e Baloon (dano a distancia) **ja funcionavam**
   sem nenhuma mudanca, so com dados que ja estavam no JSON. O que de fato
   faltava eram habilidades de contato que o sistema generico de magia nao
   cobre (ele so cobre ataque a distancia):
   - **Troll**: ataca a distancia (até 10 celulas) arremessando uma arma
     quando alinhado em linha/coluna/diagonal e com linha de visao livre -
     mesma resolucao de dano do ataque normal, só que a mais longe (Troll é
     excluído de propósito do sistema generico de magia, então precisava de
     caminho próprio).
   - **Zu e Cyclone**: paralisam no ataque corpo a corpo.
   - **Blume**: lavagem cerebral (Brainwash) no ataque corpo a corpo.
   - **Viper**: envenena no ataque corpo a corpo (o decomp mostra o Viper
     perseguindo itens no chao antes de atacar; simplifiquei para só o
     veneno no contato, que é o comportamento mais conhecido/documentado
     do monstro no jogo original, e deixei a perseguição de itens de fora).
   - **Weadog**: dreno de MP (status MP_DRAIN) no ataque corpo a corpo, em
     vez do dano normal (no decomp ele mira o jogador quando um aliado está
     com pouco MP; simplifiquei para aplicar sempre que ataca, sem checar o
     MP do aliado).
   Todas essas cinco (Zu/Cyclone/Blume/Viper/Weadog) substituem o dano do
   ataque por um status, no mesmo padrão já usado para a confusão do
   Mandara (`TryMandaraSpin`) - ataque corpo a corpo "vira" a habilidade em
   vez de causar dano normal naquele turno.
   Novo: `trollRangedAttack` (extensao do bool `attack`) e
   `TryStatusOnAttack()` em `TowerGameController.Movement.cs`.

O que **ainda nao** foi implementado, e por que:

- **Saber/Snowman**: no decomp a habilidade deles e atacar/destruir
  construcoes adjacentes (parede de fogo/gelo), nao uma acao contra Koh ou
  familiares - não tem um equivalente direto nos sistemas já existentes
  (dano, status, teleporte) e exigiria uma logica nova de interagir com os
  obstaculos do Volcano/Glacier. Fica pendente.
- **Pulunpa**: o `hiddenSpellId` configurado (26) nao gera nem dano nem
  status pelo sistema generico (`DirectSpellPower`/`DirectSpellStatus`
  retornam 0 para essa familia de magia), e o decomp mostra uma condicao
  bem especifica (um contador interno == 0 e o vizinho tocado tem que ser
  exatamente o aliado principal) que nao da pra mapear com confianca para
  os sistemas atuais sem arriscar inventar um efeito que nao existe no
  jogo original. Fica pendente.
- **Picket**: ja persegue/ataca normalmente pela IA generica de
  movimento, mas a habilidade especial dele no decomp e uma maquina de
  estados mais complexa (persegue vs. foge vs. "enrosca" perto de um
  aliado especifico) que nao mapeei com seguranca para um efeito concreto.
  Fica pendente.
- **Killer**: o ataque a distancia generico (via `hiddenSpellId`) ja
  funciona, mas o decomp mostra que ele liga/desliga uma flag interna de
  "emboscada" que provavelmente da bonus de critico quando ataca sem ter
  sido notado - não achei um gancho claro e seguro no sistema de combate
  atual para essa nuance sem arriscar alterar as chances de critico de
  todo mundo por engano. Fica pendente.

Como em toda esta sessao, nada foi compilado/testado no Editor (sem Unity
disponivel aqui) - as mudancas foram verificadas por leitura cuidadosa e
checagem de balanceamento de chaves, nao por compilacao real.

Arquivos alterados nesta secao: `TowerGameController.Movement.cs`,
`TowerGameController.SpeciesActions.cs`, `TowerGameController.ItemEffects.cs`,
`TowerGameController.Experience.cs`, `TowerGameController.InventoryMenu.cs`.

## Verificacao dos calculos de combate contra o jogo original (2026-11-18)

Pedido do usuario: confirmar se os calculos de combate (dano fisico, dano de
magia, defesa, critico, afinidade elemental) estao iguais ao jogo original,
com a excecao explicita do "evade" (esquiva), que estava quebrado no
original e foi corrigido de proposito - essa correcao deve ser mantida.

### Onde o calculo real mora

A formula de combate nao esta no projeto Unity (`Scripts/`) - ela vive no
projeto de biblioteca `AzureDreams/AzureDreams.Core/Combat.cs` (compilado
para `AzureDreams.Core.dll`, referenciado como plugin pela Unity). Esse
arquivo ja e, ele mesmo, uma traducao comentada linha a linha da funcao do
decomp `resolve_attack_damage_and_effects`
(`azure-dreams-decomp-master/src/dungeon/effect/screen/change.c`, definicao
na linha 271) e de `prepare_directional_attack` (mesmo arquivo, linha 626).

### O que foi conferido e bate exatamente com o original

- **Afinidade elemental fisica** (`Elements.AffinityPhysical` /
  `ProtectingStatusId`): o ciclo Fogo -> Vento (+1) -> Agua (+1) -> Fogo
  (+1), com -2 para o elemento desfavoravel, e os status que anulam 1/4 do
  ataque (`WATER_PROTECT` contra fogo, `WIND_PROTECT` contra agua,
  `FIRE_PROTECT` contra vento) batem bit a bit com os tres blocos
  `if (attack_elements & 1/2/4)` do decomp.
- **Modificador de altura** (`Combat.HeightModifier`): -1 se o atacante
  esta 32+ unidades acima do alvo, +1 se esta 32+ unidades abaixo, 0 caso
  contrario - igual ao decomp (`height_diff >= 0x20` / `< -0x1F`).
- **Escala do poder de ataque**: `a += (a * modifier) >> 3;` seguido do
  bonus de "slots ocupados" `a += (a >> 3) * occupiedSlots;` reproduz
  exatamente as duas linhas equivalentes do decomp
  (`element_product >> 3` somado ao valor original, depois o bonus por
  `count_occupied_entity_slots`).
- **Defesa efetiva**: defesa + bonus de equipamento, reduzida a metade com
  status de Sapo (`FROG`), minimo 1 - mesma logica do decomp (so difere no
  detalhe de implementacao de como o "zero" e tratado, sem efeito de
  jogabilidade).
- **Dano final e critico**: `delta = (defesa - poder) / 2`, e se for
  critico `delta = delta + delta/2` (ou seja, 1.5x), com o dano minimo
  forcado para 1 se o resultado nao for negativo - bate exatamente com o
  bloco `half_delta`/`quarter_delta` do decomp, incluindo a logica de
  "critico multiplica por 1.5".
- **Rolagem de critico**: limiar de `Luck/2` (se com status de raiva) ou
  `Luck/6` (os bits exatos `>>1` / `>>6`), comparado contra um numero
  aleatorio de 0-127 - identico ao decomp.
- **Rolagem de acerto (accuracy)**: valor base do atacante (`unk_84`) mais
  um bonus proporcional a sorte (`Luck`), reduzido a metade se o alvo for o
  jogador com um escudo do subtipo 2 - identico ao decomp, incluindo o caso
  especifico do escudo.

### O "evade" (esquiva): confirmado que o bug documentado e a correcao batem com o pedido

Rastreei o fluxo completo: `prepare_directional_attack` liga uma flag
(`0x4000000` em `flags1C` do atacante) assim que encontra o alvo, e so
chega a limpar essa flag se o ataque for considerado "conectado". A funcao
de dano (`resolve_attack_damage_and_effects`) checa essa mesma flag no
inicio e, se ela ainda estiver ligada, mostra o popup de erro (-1) e sai
sem aplicar dano nenhum - ou seja, e essa flag que decide "acertou" vs
"errou", nao o valor de retorno das funcoes (os dois lugares que chamam
`prepare_directional_attack` - `begin_actor_attack.c` e
`begin_front_tile_attack.c` - ignoram completamente o retorno).

O decomp tem, dentro da propria funcao, um comentario dos autores do
projeto de decompilacao citando um bug documentado pela comunidade
(adrando.com/guide/bugs.html): a rolagem de esquiva por sorte/luck do alvo
so e executada se o alvo estiver incapacitado (dormindo, paralisado, cego
ou sem MP) - um alvo consciente nunca tem chance de esquivar, so o
"accuracy" do atacante conta. E pior: mesmo para alvos incapacitados, essa
rolagem usa um campo (`unk_70`) que fica zerado, ou seja, na pratica nunca
funciona de verdade nem ali.

A correcao ja presente em `Combat.PhysicalDodgeRoll` resolve exatamente
isso: todo monstro consciente (nao incapacitado, especie valida) agora
ganha uma chance real de esquiva proporcional a sua Sorte (1% para cada 8
pontos de Luck, minimo 1% se Luck > 0), aplicada DEPOIS da rolagem de
acerto original do atacante - ou seja, a esquiva original (quebrada,
praticamente inexistente) foi substituida por uma mecanica que de fato
funciona, sem alterar nada da rolagem de acerto do atacante. Confirmei que
essa correcao esta intacta e nao foi revertida.

### Nao verificado nesta passada

O dano de magia (`Combat.ResolveSpellDamage`) ja tem comentarios
detalhados no codigo citando a formula (afinidade -1 por elemento
desfavoravel, bonus padrao de `min(4*nivel, 99)` quando vem `0xFF`), mas eu
nao localizei e nao li a funcao equivalente no decomp nesta sessao para
confirmar linha a linha (o dano fisico tomou a maior parte do tempo
disponivel). Se quiser, posso fazer essa segunda verificacao numa proxima
leva.

**Conclusao**: os calculos de combate fisico (dano, defesa, critico,
afinidade elemental, altura, rolagem de acerto) batem exatamente com o
jogo original. A esquiva (evade) continua corrigida como pedido - nao foi,
e nao deve ser, revertida para o comportamento quebrado original.

## Habilidades especiais de monstros - Viper, Weadog, Manoeva, Picket, Atrocious e dieta do Dragao (2026-11-18)

Implementadas seis mecanicas pedidas na mesma leva, todas em
`TowerGameController.ItemEffects.cs`, `TowerGameController.Movement.cs` e
`TowerGameController.SpeciesActions.cs`:

### 1. Atrocious (talento do Killer): +10% de critico fixo

Antes, o talento Atrocious (`AtrociousTalent`) setava a flag `ActorFlag.ANGRY`
no atacante, o que alterava indiretamente (e de forma imprevisivel) a formula
base de chance de critico, com efeitos colaterais da flag ANGRY em outros
lugares do jogo. Troquei por um bonus direto e previsivel, reaproveitando o
mesmo mecanismo aditivo usado pelas espadas (`DarkSword=+30`,
`SeraphimSword`/`Keepsake=+20`, etc.):

```csharp
// Atrocious talent (Killer): +10% de critico fixo, independente da arma.
if (HasTalent(actor, AtrociousTalent)) critAdjustment += 10;
```

Em `TowerGameController.ItemEffects.cs`, dentro do bloco de calculo de
`critAdjustment`.

### 2. Picket: habilidade Quick (Haste) - duas acoes por turno

`MovementSpeedTurns` ja controlava quantos "passos extra" um ator ganha por
turno via status `StatusId.SPEED` (usado por armadilhas de velocidade etc.).
Dei ao Picket esse bonus permanente e inato, mas sem sobrepor um status
explicito (por exemplo, uma armadilha de Slow) que porventura o afete:

```csharp
private static int MovementSpeedTurns(CoreActor actor)
{
    if (actor == null) return 0;
    for (int i = 0; i < 4; i++)
        if (actor.StatusId(i) == StatusId.SPEED) return actor.StatusValue(i);
    // Habilidade inata do Picket ('Quick'/Haste - duas acoes por turno): sempre
    // hasteado quando nao ha um status de Speed explicito (ex: armadilha de Slow)
    // sobrepondo o valor acima.
    if (actor.Species == (byte)MonsterSpecies.PICKET) return 2;
    return 0;
}
```

Em `TowerGameController.Movement.cs`.

### 3. Dragao (e qualquer especie com `likesMetal > 0`) come moedas e sinos

A mecanica de "comer metal" (`IsMetalItem`/`ApplyMetal`) ja existia para
espadas, escudos etc. Estendi para moedas de cobre/prata/ouro (`ItemKind.Coin`)
e todos os sinos (`ItemKind.Bell`: Sagrado, Malicioso, Familiar).

Problema encontrado: o branch de `ItemKind.Bell` em `ApplyInventoryEffect` ja
capturava incondicionalmente qualquer sino ANTES de chegar no catch-all do
`IsMetalItem`, tornando a extensao morta para sinos. Corrigido adicionando uma
excecao a esse branch, para que sinos dados a uma criatura que come metal
caiam no branch de comer metal, preservando o uso normal do sino pelo jogador
ou por aliados que nao comem metal:

```csharp
else if (item.Kind == ItemKind.Bell &&
    !(target != _playerActor && (UnitProfile(target.Species)?.likesMetal ?? 0) > 0))
{
    PlayRetailSound(RetailSfx.Spell);
    ApplyBell(item.Subtype);
}
```

Em `TowerGameController.ItemEffects.cs` (dentro de `ApplyInventoryEffect`).

### 4. Viper: "come" (carrega) ovos do chao, devolve ao ser derrotada

Confirmado no decomp (`update_viper_item_grab.c`): a Viper detecta um ovo
(`ItemKind.Egg`) numa celula adjacente, "engole" (remove do chao) e gasta 1 MP.
O decomp nao mostrava logica de carregar persistentemente nem de devolver o
ovo ao morrer - isso foi implementado por instrucao explicita do usuario.

- Novo dicionario `_viperCarriedEgg` (`ActorRecord -> DungeonItemEntry`) em
  `TowerGameController.SpeciesActions.cs`, limpo em `ResetSpeciesActions()`.
- Novo metodo `TryViperGrabEgg(actor, cell)`: procura um ovo numa celula
  vizinha (via `BellSteps`), remove do chao com `_towerItemTraps.TakeItem`,
  guarda no dicionario.
- Novo case na IA de especies (`TryEnemySpeciesAction`): Viper tenta pegar um
  ovo proximo (1/8 de chance por turno) se ainda nao estiver carregando um.
- Em `TowerGameController.Movement.cs`, no loop de derrota de criaturas: se a
  Viper derrotada estava carregando um ovo, ele e devolvido ao chao via
  `DropTowerReward` (mesma funcao usada para o Medalhao da Agua).

### 5. Weadog: dano fisico normal E dreno de MP ao mesmo tempo

Correcao de uma mecanica errada: o Weadog estava no mesmo padrao de
Zu/Blume/Viper em `TryStatusOnAttack`, onde o status SUBSTITUI o dano normal
(1 em 4 rolagens). O usuario confirmou que o Weadog deve causar dano normal E
aplicar o dreno de MP no MESMO golpe, nao um ou outro.

- Removido o `case MonsterSpecies.WEADOG` de `TryStatusOnAttack`.
- Adicionada, dentro do bloco de ataque corpo-a-corpo normal (apos
  `RecordTowerHit`), uma chance separada (1 em 4, mesma frequencia do padrao
  antigo) de enfileirar `StatusId.MP_DRAIN` em `_pendingMonsterStatuses`,
  SEM pular o dano normal:

```csharp
// Weadog drena MP no mesmo golpe que causa dano normal (nao substitui o
// dano, diferente de Zu/Blume/Viper - ver TryStatusOnAttack).
if (cs.Actor.Species == (byte)MonsterSpecies.WEADOG && RollSpawnRange(4) == 0)
    _pendingMonsterStatuses.Add((cs.Actor, target, targetCell, StatusId.MP_DRAIN, false));
```

Em `TowerGameController.Movement.cs`.

### 6. Manoeva: restaurada a habilidade de se multiplicar (alem de copiar forma)

**Correcao importante**: numa sessao anterior eu havia removido a habilidade
do Manoeva de se multiplicar ao ser atingido, por entender (erroneamente) que
era uma mecanica incorreta, substituindo-a pela habilidade de copiar forma
(disguise). O usuario confirmou que SAO DUAS habilidades distintas e que a
remocao foi um erro - as duas devem coexistir.

Investiguei o decomp inteiro da pasta `manoeva/` (`spawn_manoeva.c`,
`spawn_manoeva_from_record.c`, `update_actor_target_effect_action.c`,
`copy_linked_position_and_check_status.c`) e nao encontrei a funcao exata
responsavel pela multiplicacao - so a logica de disguise/copiar forma estava
visivel. Tambem nao havia backup recuperavel da versao anterior (sem
historico de git disponivel na sessao, sem arquivos `.bak` remanescentes).

**IMPORTANTE - mecanica reconstruida, nao extraida do original**: implementei
a partir de julgamento de design, sem fonte exata. Se o usuario lembrar os
numeros originais (chance exata, proporcao da divisao de HP), eles devem
substituir os valores abaixo.

Novo metodo `TryManoevaSplit(ActorRecord target, int damage)` em
`TowerGameController.SpeciesActions.cs`:

- So se aplica a Manoevas SELVAGENS (presentes em `_creatures`); uma Manoeva
  domesticada (familiar/aliado) nao se multiplica nesta implementacao.
- Chance reconstruida: 50% (`RollSpawnRange(2) == 0`) por acerto que nao
  seria fatal.
- So ocorre se houver uma celula livre adjacente (mesma logica de busca usada
  em `TryViperGrabEgg`) e se o HP restante apos o dano for > 1.
- Divide o HP restante pela metade: o clone nasce com `half` HP (via
  `SpawnManagedCreature(..., levelOverride: target.Level, allowSpawnSleep: false)`
  seguido de override de `Actor.Hp`/`Health`), e o Manoeva original fica com
  `half` HP tambem.
- Detalhe tecnico importante: como o dano retornado pela funcao de combate
  ainda NAO foi aplicado ao HP do alvo no momento em que `TryManoevaSplit` e
  chamado (a aplicacao real acontece depois, no pipeline de dano
  diferido), o metodo retorna um dano AJUSTADO (`target.Hp - half`) em vez do
  dano original, para que o pipeline de dano diferido deixe o Manoeva
  original com exatamente `half` HP, sem contar o dano duas vezes.
- Chamado nos dois pontos onde `RevertManoevaDisguise(target)` ja era
  chamado em `TowerGameController.ItemEffects.cs` (dano fisico e
  `SpellOrImpactDamage`), logo apos o calculo de `damage`:

```csharp
if (damage > 0) damage = TryManoevaSplit(target, damage);
```

A habilidade de copiar forma (disguise, `TryManoevaDisguise`/
`RevertManoevaDisguise`) foi mantida intacta e sem alteracoes - as duas
habilidades agora coexistem, como pedido.

## Efeitos graficos das magias - auditoria e pergaminhos sem feedback visual (2026-11-18)

O usuario pediu para "implementar os efeitos graficos das magias". Investiguei
antes de escrever qualquer coisa, porque ja existe um sistema bem extenso e
funcional para isso: `TowerGameController.SpellPresentation.cs` (~560 linhas),
com orbe/projetil que viaja do conjurador ao alvo, explosao de impacto,
parede de fogo (Fire Wall), sprites de mistura de espada do Koh e reflexo de
magia (quando o alvo tem reflexao ativa). Esse sistema ja estava conectado em:

- Bola de cristal (`CastCrystalBall`, `ItemEffects.cs`)
- Varinhas/misturas manuais do Koh (`TryManualMixMagic`, `SpellPresentation.cs`)
- Ordens do familiar (`FamiliarOrders.cs`)
- Menu de inventario / uso direto de itens magicos (`InventoryMenu.cs`)
- Magia nativa de monstros em combate, incluindo habilidades ocultas
  (`hiddenSpellId`) (`Movement.cs`)

Perguntei ao usuario o que exatamente faltava, e ele escolheu duas coisas:

### 1. Pergaminhos (Scroll) sem nenhum feedback visual - CORRIGIDO

Encontrei uma lacuna real: `ApplyScroll` (usado quando um pergaminho e
dado/usado em um alvo - pedra sagrada, confusao, revelar armadilhas, remover
maldicao, passar de andar, converter itens em ouro) so tocava o som
`RetailSfx.Spell`, sem NENHUM efeito visual. E o pergaminho ARREMESSADO
(`ResolveThrownItem`, branch `ItemKind.Scroll`) nao tinha nem som nem efeito
visual algum.

Corrigido:

```csharp
private void ApplyScroll(DungeonItemEntry item, ActorRecord actor)
{
    // Efeito visual generico de magia no local de quem usou o pergaminho - antes
    // nao havia nenhum feedback visual aqui (so o som RetailSfx.Spell).
    Vector2Int scrollCell = actor == _playerActor ? _playerCell : actor == _familiarActor ? _familiarCell :
        actor == _blueCollarAlly?.Actor ? _blueCollarAlly.Cell :
        _creatures.Find(creature => creature.Actor == actor).Cell;
    SpawnFx(_fxMagic, CellPosition(scrollCell));
    switch (item.Subtype) { ... }
}
```

E no branch de pergaminho arremessado em `ResolveThrownItem`:

```csharp
else if (target != null && item.Kind == ItemKind.Scroll)
{
    // Pergaminho arremessado nao tinha nenhum feedback visual/sonoro de magia.
    PlayRetailSound(RetailSfx.Spell);
    SpawnFx(_fxMagic, CellPosition(impact));
    if (item.Subtype == 1) CreateSacredTile(impact);
    else if (item.Subtype == 2) Status(target, StatusId.CONFUSION, 10);
}
```

Ambos em `TowerGameController.ItemEffects.cs`.

### 2. Revisao dos efeitos ja existentes - nenhum bug encontrado

Fiz uma leitura cuidadosa de todo `SpellPresentation.cs` e dos pontos onde ele
e chamado (`TickTurnPresentation` em `Movement.cs`), procurando bugs de logica
(nao de fidelidade de cor/paleta ao jogo original, que eu nao tenho como
verificar sem rodar no editor ou achar a fonte exata no decomp):

- Confirmei que `TickSpellPresentation` NAO e chamada duas vezes por frame
  (os dois pontos de chamada em `Movement.cs` sao ramos mutuamente exclusivos
  da mesma funcao `TickTurnPresentation`, chamada uma vez por frame).
- O ciclo de vida do orbe (`SpellCue.Orb`) e criado/destruido corretamente,
  incluindo no caminho de mistura de espada (`PresentSwordMixture`) e ao
  limpar tudo (`ClearSpellPresentation`).
- A logica de reflexo de magia (`ReflectFrom`) interpola corretamente
  conjurador -> ponto de reflexao -> alvo, com o burst de impacto disparando
  no momento certo (t >= 0.5).

Nao encontrei nenhum bug funcional. Se o usuario notar algo especifico
errado ao rodar no editor (cor errada, timing estranho, FX faltando em
alguma magia em particular), me avisar com o caso especifico para eu
corrigir - sem saber o alvo exato, reescrever paletas/timings seria
chute, nao correcao.

## Correcao: magias elementais (principalmente as de "mix") sem grafico de impacto (2026-11-18)

O usuario testou no editor e confirmou que algumas magias - principalmente
as "mix" (familias elementais compartilhadas entre conjuracao nativa de
monstro e a mistura manual do Koh) - nao mostravam efeito grafico. Pedi para
ele investigar antes de mudar algo, mas como ele pediu para eu mesmo
comparar com o original, fiz o seguinte:

1. Tentei achar a funcao exata de renderizacao de efeito de magia no decomp
   (`src/dungeon/actor/spell_resource_ids_g01008.c`, `begin_actor_spell_action.c`).
   Confirmado: no jogo original, o efeito visual de cada magia e codigo de
   overlay carregado do CD por ID (`load_spell_effect_code` -> `Control_CD`),
   nao algo que sobrevive como C legivel no decomp - mesma situacao do Weadog
   (so visivel como jump table binario). Ou seja, nao da pra confirmar cor
   exata por magia direto no decomp; a logica abaixo vem de consistencia
   interna do proprio codigo do remake, nao do original.

2. Simulei em Python o gerador procedural de sprite da "Mix Magic" de espada
   (`BuildSwordMixtureSprites`) pixel a pixel para confirmar que ele realmente
   desenha algo visivel em todos os 8 frames x 3 paletas (~100-116 pixels
   preenchidos de 1024, por frame/paleta) - NAO esta gerando uma textura vazia/
   invisivel. Esse sistema (usado por `TryManualMixMagic` via
   `QueueSwordMixtureCue`/`PresentSwordMixture`) esta ok.

3. Encontrei o bug real lendo `QueueSpellCue` (usada quando QUALQUER magia e
   conjurada nativamente por um monstro - incluindo as mesmas familias
   elementais usadas pelas misturas manuais do Koh, ver `MixtureFor`):

   - Familias 0,1,2,3 (Breath/Sled/Brid/Rise - grupo "Sword" de mistura):
     o campo `impact` so tinha valor para elemento Fogo
     (`<= 3 when element == Element.Fire => "FX_Fire"`); para Agua ou Vento
     caia no `_ => null`, ou seja, **nenhum efeito de impacto era mostrado**.
   - Familias 7 (Summon), 8 (Sword) e 9 (Sword): cor e/ou impacto fixos em
     azul/agua (`FX_Water`) **independente do elemento do conjurador** -
     um monstro de fogo usando uma magia da familia 8 mostrava respingo de
     agua em vez de fogo.
   - Familia 13 (Wave) e 15 (variante elemental de LoGrave, conforme o
     comentario em `DirectSpellPower`: "LoGrave and its elemental variants"
     para as familias 14/15/16): ambas caiam no grupo visual de status
     (roxo/fumaca, junto com cegueira/sono), em vez de serem elementais como
     a familia 14 (que fica no mesmo grupo `Mixture.Wave`). Isso fazia a
     MESMA familia ter visual diferente dependendo de ser conjurada
     nativamente (roxo fixo) ou misturada manualmente pelo Koh (sempre
     elemental via `MixtureArt`).
   - Familia 14: igual ao caso das familias 0-3, o `impact` caia em `null`
     sempre (nao esta no `<=3` nem em nenhum outro case).
   - Familia 16 (unica magia de familia propria, spell 49): cor fixa verde
     mas impacto fixo "FX_Water" - combinacao incoerente entre si.
   - Familia 5 (parede elemental - fogo/agua/vento, `WallProtectionStatus`):
     a variante de vento (spell 18) caia no mesmo `_ => azul/agua` das
     demais, em vez de verde/vento.

   **Corrigido** em `TowerGameController.SpellPresentation.cs`,
   `QueueSpellCue`: as familias elementais (0,1,2,3,6,7,8,9,13,14,15,16) agora
   caem no caso `_` do switch (cor/impacto baseados no elemento REAL do
   conjurador, mesma formula ja usada e comprovada em `MixtureArt`), em vez de
   valores fixos. So continuam fixas as familias genuinamente nao-elementais:
   veneno (4, verde/fumaca), parede (5, agora com fogo/agua/vento corretos
   por spell id), cegueira (10), paralisia (11), sono (12) e o placeholder de
   giro sem magia (17, spell==0).

**Resultado esperado**: qualquer monstro conjurando nativamente uma magia das
familias "mix" (sword/blade/summon/wave) agora sempre mostra um efeito de
impacto, com a cor certa para o elemento de quem conjurou - antes, varias
combinacoes de familia+elemento nao mostravam nada ou mostravam a cor errada.
Como nao tenho como abrir o editor, pedi para o usuario confirmar se isso
resolve o que ele viu, e me apontar qualquer caso especifico que ainda esteja
errado.

## 2026-11-18 - Armadilhas: assets redesenhados, mola do Go Up, pentagramas brilhando e nova armadilha Go Down

Pedido do usuario em 4 partes, usando os PNGs de
`_Redesigned assets/traps` para a versao Reimagined das armadilhas.

### 1) Substituicao dos sprites da versao Reimagined

Os 16 PNGs de `_Redesigned assets/traps` (blind, bomb, chaos, crack, frog,
go_up, go_up2, poison_trap, prison_trap, reversal_trap, rust_trap, seal_trap,
slam_trap, sleep_trap, slow_trap, upheaval, warp_trap) foram copiados para
`Resources/Reimagined/Tower/Traps/`, renomeados para o nome de recurso exato
que `TrapSprite()` (`TowerGameController.Traps.cs`) espera (ex.: `blind.png`
-> `Blind_trap.png`), com `.meta` novo para cada um (GUID proprio, baseado no
template ja usado em `Reimagined/Items/trap.png.meta` - `textureType: 0`,
`filterMode: 1`, `alphaIsTransparency: 1`). Como `AssetMode.Load` ja procura
primeiro em `Reimagined/...` antes de cair para o classico, nenhum codigo
precisou mudar - so a presenca dos arquivos na pasta certa ja faz a troca.

Nao havia asset redesenhado para a armadilha `Bump`: ela continua usando o
sprite classico (fallback automatico do `AssetMode`), ja que o usuario nao
forneceu esse arquivo.

### 2) `go_up2.png` - mola aberta depois de ativada

Confirmei visualmente que `go_up.png` e a plataforma fechada e `go_up2.png` e
a mesma plataforma aberta com uma mola visivel no meio (o "mola balancando"
mencionado pelo usuario). Copiei `go_up2.png` como
`Go_up_trap_sprung.png` e implementei, em `TowerGameController.Traps.cs`:
- `GoUpSprungSprite()`: carrega e cacheia esse sprite (so existe no
  Reimagined; no Classic fica `null` e o comportamento antigo e mantido).
- `AnimateTrapMarkerEffects()` (chamado todo frame a partir de
  `LateUpdate()` em `TowerGameController.Camera.cs`, logo apos
  `OrientVisibleTrapMarkers()`): quando uma armadilha Go Up ja foi ativada
  (`!trap.IsActive`) e o sprite da mola existe, troca o sprite do marcador
  para `Go_up_trap_sprung` e aplica uma pequena oscilacao continua de
  rotacao e escala (seno no tempo, com uma semente por celula para nao
  ficarem todas sincronizadas) para dar a sensacao de balanco.

### 3) Pentagramas de Seal e Frog brilhando

`seal_trap.png`/`frog.png` sao cada um uma unica imagem de pentagrama
brilhante (sem camada separada de "base" e "brilho"), entao o efeito foi
feito como um pulso continuo de alpha sobre o sprite inteiro do marcador -
mesmo padrao ja usado para o selo do segundo elevador da torre
(`AnimateSecondTowerElevator` em `TowerGameController.SecondTowerElevator.cs`).
Isso tambem foi colocado dentro de `AnimateTrapMarkerEffects()`: para
armadilhas `Seal` ou `Frog`, a opacidade oscila entre ~56% e 100% (multiplicado
pela opacidade base que ja indica se a armadilha esta ativa ou nao).

### 4) Nova armadilha: Go Down

Implementada como espelho direto da Go Up, mas descendo um andar em vez de
subir:

- **`TowerItemTrapManager.cs`**: novo valor `GoDown` no enum
  `TowerTrapEffect`, adicionado tambem em `GeneratedTrapEffects` (entra no
  ciclo normal de sorteio por andar). Em `GeneratedTrapEffect(floor, index)`,
  se o resultado sorteado for `GoDown` e o andar for 3 ou
  `TowerFloorRules.BeldoFloor` (40), ela e trocada por `Slam` - ou seja, Go
  Down nunca aparece nos andares 1, 2 (ja cobertos pela regra generica
  `floor <= 2 => Slam`), 3 ou 40, exatamente como pedido.
- **`TowerGameController.Traps.cs`**:
  - `TrapSprite()`: `GoDown => "Go_down_trap"` (segue o mesmo padrao de nome
    da Go Up, que tambem foge do sufixo generico `_trap` duplicado).
  - `TriggerTrapFor`: novo `case TowerTrapEffect.GoDown`, que liga
    `_trapGoDown = true` quando quem pisou foi o jogador e o jogo esta no
    estado `InTower`. Nao precisei de nenhuma restricao extra de andar (tipo
    a `CanAscendFromFloor` usada na Go Up): como a armadilha nunca e gerada
    abaixo do andar 4, o andar de destino (`_floor - 1`) nunca fica abaixo
    de 3, e `JumpToFloor` nao tem limite inferior - o mesmo mecanismo usado
    pelo elevador para subir funciona igual para descer.
  - `TrapDisplayName`: nova entrada `GoDown => "Go down"/"Descida"`.
  - Novos campos `_trapGoDown` e `_goDownTransition` (espelhando
    `_trapGoUp`/`_goUpTransition`).
  - `PlayGoDownTransition()`: corrotina identica em estrutura a
    `PlayGoUpTransition()` (fade para preto em 0.3s, espera 2s, chama
    `CompleteTrapDescent()`).
  - `CompleteTrapDescent()`: `_towerRun.JumpToFloor(_floor - 1)` (com
    `Mathf.Max(1, ...)` como protecao extra), reconstroi o andar e mostra a
    mensagem "Você cai para o Andar X".
- **`TowerGameController.Movement.cs`**: logo apos o bloco que consome
  `_trapGoUp`, novo bloco igual para `_trapGoDown`, chamando
  `PlayGoDownTransition()`.
- **`TowerGameController.cs`**: `_goDownTransition` adicionado ao guard de
  input (ao lado de `_goUpTransition`) e resetado para `false` junto com
  `_trapGoUp` ao trocar de andar/area.

**Assets gerados para a Go Down** (nao existia nenhuma referencia no decomp
nem nos assets redesenhados para essa armadilha nova - ela nao existe no jogo
original, entao os dois sprites abaixo sao criacoes minhas, geradas
proceduralmente com Python/Pillow, e nao arte feita a mao):
- `Resources/Reimagined/Tower/Traps/Go_down_trap.png` (1254x1254, com
  `.meta` seguindo o mesmo template das outras texturas Reimagined): um
  alcapao com aro de metal escuro, duas portas de madeira com tiras de
  ferro em X e uma fresta escura central, no mesmo estilo "botao 3D" das
  demais armadilhas redesenhadas, mas em tons terrosos/metal escuro (em vez
  de dourado/azul da Go Up) para diferenciar visualmente "descer" de
  "subir".
  mesma posicao/escala que as demais (usa o mesmo `TrapMarkerWidth` e
  `CreateTrapMarker` de sempre).
- `Resources/Tower/Traps/Go_down_trap.png` (80x60, `.meta` igual ao
  template classico): um disco simples marrom com centro mais escuro,
  seguindo o mesmo estilo "blob" borrado e de baixa resolucao dos outros
  sprites classicos (que tambem sao discos coloridos simples, nao pixel art
  detalhado).

Como nao tenho acesso ao Editor, nao pude conferir visualmente dentro do
jogo (escala, enquadramento no marcador, contraste no chao da torre) - pedi
para o usuario revisar esses dois sprites e a animacao da mola/pentagramas
in-game e avisar se algo precisar de ajuste.

## 2026-11-18 - Imagens grandes movidas para StreamingAssets (carregamento assincrono)

Pedido do usuario: mover para `Assets/StreamingAssets` os graficos que podem ser
streamados, para facilitar a edicao e melhorar o carregamento na versao Web.

### O que foi movido (Resources -> StreamingAssets/Media)

Destino escolhido: `Assets/StreamingAssets/Media/<mesmo caminho relativo>`, seguindo
a convencao que ja existe no projeto (`Media/Manual`, `Media/Portraits`,
`Media/Prologue`, overlay `Media/Reimagined`). `StreamedMediaUrl()` monta a URL
sempre a partir de `Media/`, entao usar `StreamingAssets/Title` direto exigiria
um segundo caminho de carregamento - preferi nao criar essa excecao.

- `Title/AngelOpeningFrames.png` (sprite sheet 1700x260, 10 quadros do anjo)
- `Title/JordaImpactStar.png`
- `Title/Newborn.png`, `Title/WreathResting.png` (cena do nascimento)
- `Title/RainCast/{Aunt,Grandpa}{Back,Front,Side}.png` (6 poses do flashback da chuva)
- `Tower/Entrance/KewneDialogueSkip.png` (icone de pular dialogo, 2.3 MB)

Ficaram em Resources de proposito (pequenos, ou nao sao imagens que podem
streamar): `Title/RetailEffects/*` (Crystals, Flare0-9, shader TitleLens,
data.json - total poucos KB e o shader nao pode ser streamado),
`Title/AuntRain`, `Title/GrandpaRain` (40x48 px), `Title/Manual/` e
`Title/Wreath*` remanescentes. Os `.meta` antigos desses arquivos foram
removidos e os novos foram gerados com GUID novo.

Os PNGs redesenhados de traps e a Go Down continuam em Resources: sao usados
em gameplay a todo momento e pequenos o bastante para o custo de Resources
nao ser relevante.

### Novo loader: `TowerGameController.StreamedMedia.cs`

- `StreamMediaTexture(relativePath, filterMode, wrapMode)`: cache por caminho
  (`_streamedTextures`), baixa via `UnityWebRequestTexture` (funciona em WebGL,
  onde `Resources` ja e empacotado mas StreamingAssets precisa de request),
  resolve overlay/classic com `AssetMode.ResolveMedia` e respeita o caso
  "redesigned-only" (Classic nao mostra nada). Enquanto nao carrega, retorna
  `null` e dispara a requisicao; quando chega, a proxima chamada devolve a textura.
- `StreamMediaSprite(relativePath, pixelsPerUnit)`: monta `Sprite` com pivot
  0.5/0.5. `pixelsPerUnit` foi conferido em cada `.meta` original antes de mover:
  48 para o elenco da chuva, 96 para Newborn/WreathResting. Errar esse valor
  deixa o sprite na escala errada na cena.
- `ReleaseStreamedTextures()`: libera tudo quando o estilo Classic/Reimagined
  muda (`OnAssetModeChanged`) e no `OnDestroy`. Cache sem LRU de proposito:
  varias dessas imagens ficam visiveis ao mesmo tempo (tia + avo no flashback),
  entao descartar uma no meio da cena a faria sumir.

### Call sites atualizados

- `TowerGameController.Prologue.cs`: `StartPrologue` e `DrawPrologueAngel`
  (Angel usa `Bilinear` + `Repeat`, como o original; o timer do voo so e
  reiniciado quando a textura chega, para nao perder os primeiros quadros);
  `PlaceBirthSprite` (ppu 96); `SetRainPose` (ppu 48); `DrawJordaImpactStars`
  (Point + Clamp, como o original).
- `TowerGameController.Tutorial.cs`: `DrawDialogueSkipPrompt` (Bilinear + Clamp).
- `TowerGameController.AssetMode.cs` (`OnAssetModeChanged`) e
  `TowerGameController.FloorMesh.cs` (`OnDestroy`): chamam `ReleaseStreamedTextures()`.

Balanco de chaves/parenteses conferido em todos os arquivos tocados.

### Pendente

- A "tela de titulo" em si nao esta em Resources como uma imagem unica. O
  que existe grande ali e o `AngelOpeningFrames` (ja movido); o resto da
  tela de titulo e RetailEffects, que ficou em Resources por ser pequeno.
- Bump trap: o usuario pediu para usar uma imagem de rocha como sprite da
  Bump. O arquivo nao estava acessivel no ambiente, entao NAO foi gerado
  `Resources/Reimagined/Tower/Traps/Bump_trap.png`. Quando o arquivo chegar:
  remover o fundo branco (para ficar transparente, como os outros traps), salvar
  com esse nome e criar o `.meta` com o template Reimagined.

Nao foi possivel testar em Unity (sem Editor). Pedi para o usuario verificar,
principalmente na build Web, se as pocas de Title/Prologue/chuva aparecem
com a escala certa.

## 2026-11-18 - Silver: retrato Reimagined e correcao de regressao do movimento para StreamingAssets

### Retrato da Silver (Reimagined)

`SilverPortrait` (`TowerGameController.StoryActors.cs`) agora e uma propriedade:
Reimagined usa `Dialogue/Grandpa_variant1_frame0` (arquivo so-Reimagined, ja
listado como `*` em `Resources/Reimagined/media_overlay.txt`); Classic mantem
`Dialogue/Grandpa_variant0_frame0`, porque o Classic nao tem a variante 1.

### Regressao corrigida: sprites da Silver (RainCast)

`RacetrackResidentSpriteView` (sprites Front/Back/Side do Grandpa, usados pela
Silver) ainda carregava `Title/RainCast/*` com `AssetMode.Load<Sprite>`. Como
esses PNGs foram movidos para `StreamingAssets/Media/Title/RainCast` na
mudanca anterior, o carregamento retornava `null`, e o componente caia de volta
para o visual do prefab `NPC_Dar` (modelo errado). Agora ele baixa os cels com
`UnityWebRequestTexture` via `AssetMode.ResolveMedia`, com `filterMode` Point,
`pixelsPerUnit` 48 e pivot 0.5/0.5 (mesmos valores do `.meta` original). Os
renderers do prefab ficam ocultos ate a carga terminar e sao restaurados se ela
falhar.

### Pendente: modelos de Hush e Dr. Hal

Nao consegui confirmar o modelo original destes dois. Achados:
- Hush usa o prefab `NPC_Hush` com `Town_Hush.png` (20x48, recorte de 20x48).
- Dr. Hal usa o prefab `NPC_Dar` (`Town_Dar.png`, 30x29). O comentario em
  `TowerGameController.StoryActors.cs` diz que `Town_Dar` e "o sprite retail do
  velho", mas o codigo deixa o retrato do Hal como `null`, embora o comentario
  diga que ele recebe o retrato do Grandpa. O comentario e o codigo divergem.
- Silver: a documentacao (`Documentation/Racetrack.md`) confirma que o modelo
  original e o Grandpa (cels Aunt/Grandpa), coerente com a correcao acima.

Para corrigir Hush e Dr. Hal de acordo com o original, falta identificar o
registro de residente de cada um no `TOWN.BIN` (metodo usado para a Vivian em
`Documentation/VivianTownSprites.md`: enderecos de residente menos
`0x8007D760`, slot de pessoa, tabelas de apresentacao) ou ter uma referencia
visual do original.

## 2026-11-18 - Sprite Reimagined da armadilha Bump

Imagem de rocha enviada pelo usuario instalada como
`Resources/Reimagined/Tower/Traps/Bump_trap.png` (1024x1024, fundo ja
transparente; alpha quase opaco 250-254 normalizado para 255 para a rocha nao
ficar levemente translucida). `.meta` copiado do template de `Go_up_trap.png.meta`
com GUID novo. `TrapSprite()` ja resolve `Bump` para `Bump_trap` pelo nome padrao,
entao nenhum codigo mudou; o Classic continua com o sprite original.

## 2026-11-18 - RE dos NPCs da cidade (Dr. Hal, Hush, Silver e menores)

Detalhes completos em `Documentation/RetailTownResidents.md`.

- Cada pessoa do retail e registrada por `register_person` (`jal 0x800C3050`,
  115 chamadas no TOWN.BIN): slot = `F_act_<nome>` - 62. Varios NPCs dividem o
  mesmo corpo e mudam so a paleta pelo `SpriteFrameState.clut` gravado no init.
- Resultado: **Hush** = corpo do Tonka com paleta +576 (o `Town_Hush.png`
  anterior estava errado); **Silver** = corpo do Romi/Dar com paleta +576 (os
  cels Grandpa usados antes eram do avo); **Dr. Hal** = corpo do avo (gigi) com
  paleta +640 (antes usava o prefab NPC_Dar).
- `Tools/extract_retail_town_residents.py` gera 63 bancos animados (atlas +
  json com ticks originais) em `Resources/Town/Residents/Retail/`.
- `RetailResidentBanks.cs` aplica o banco por npcId em `Spawn`,
  `SpawnStoryResident` e nos residentes de interior; remove views antigas
  (OriginalTownSpriteView, RacetrackResidentSpriteView, SilverSpriteView,
  TownCharacterSpriteView, TownHumanoidView, WeedyIdleAnimation).
  `MatchHushVisualToKoh` e `SpawnRetailResident` respeitam o banco.
- Excluidos: as 7 garotas, Beldo, Koh, Kewne. Mapeados so os NPCs com
  identidade comprovada (tabela no documento).
- Nao compilado ainda: o shell do PC e uma VM Linux; a compilacao no Unity
  depende de computer use (pendente de ativacao).

### Continuacao - tabelas de falas e casino

- Nas cenas, a tabela de falas `(u16 falante, u16 indice, u32 script)` usa o
  falante = slot de pessoa. Com isso foram mapeados: academia (gym_woman,
  gym_developer, gym_legs, gym_hunter), mestre da loja de monstros (mamonoya),
  cacadores do bar (hunter1-3, ordem assumida), recepcionista e marido do
  racetrack (kicca/dai, inferido) e Jorda (yoda).
- Os atlas nativos `RouletteAttendant` e `CasinoLoser` estavam com nomes
  trocados (pixels batem com os slots 44 e 43); `Casino.cs` agora aplica cada
  um ao residente certo.
- `RetailTownResidentSpriteView.Configure` agora pode ser chamado de novo
  (descarta quadros/visual anteriores); antes, um banco aplicado no Spawn
  impedia o atlas especifico da cena (casino/academia) de entrar.
- Faltam no Unity: o tio (ozisan) na casa da familia e a mae da Cherrl
  (nerdia). Sem identidade comprovada: isaac, chero, shiela,
  monster_shop_hunter, restaurant_hunter, arcade_attendant.

## 2026-11-18 - Familiar aprende magia de bola (10+ cargas)

Pedido do usuario (regra de design, nao veio do decomp):
- "Dar para o familiar" uma bola com 10 ou mais cargas faz o familiar
  aprender a magia em vez de lança-la. Nivel aprendido = cargas / 10
  (mesma formula de `BallSpellLevel`). A bola e consumida inteira.
- Bola de Acid Rain (subtipo 17, magia 49) nao pode ser dada ao familiar
  (`IsGiveableToFamiliar` a exclui; a opcao nem aparece).
- Maximo de 3 magias: usa os 3 slots nativos do registro do ator
  (offsets 8/11/14: id, nivel guardado, nivel), os mesmos que fusao,
  sementes e level-up ja mantem. Se o familiar so tinha a magia nativa
  implicita, ela e gravada no slot 0 antes, para nao ser perdida.
- Se ja conhece a familia da magia: sobe o nivel se o da bola for maior;
  senao recusa sem consumir. Se ja tem 3 e e magia nova: recusa sem consumir.
- A magia aprendida e convertida para o elemento do familiar (como na fusao).
- Funciona na torre e na cidade (na cidade so bolas de 10+ podem ser dadas).
- Bolas com menos de 10 cargas continuam como antes (o familiar lanca).
- IA de suporte (`TryFamiliarSupportMagic`) agora tenta todas as magias
  conhecidas em ordem; a ordem manual e o Mix continuam usando o slot 0.

Arquivos: novo `TowerGameController.FamiliarSpellLearning.cs`;
`TowerGameController.ItemCommands.cs` (IsGiveableToFamiliar, CanGiveToFamiliar,
desvio em ExecuteItemCommand); `TowerGameController.FamiliarOrders.cs`
(TryFamiliarSupportMagic -> TryFamiliarSupportSpell). Nao compilado.

### Isaac, Shiela e piscina

- Isaac = slot 30 (`F_act_ethrk`): o seletor de falas em TOWN.BIN 0x2ADC20
  testa as flags `Ftt_fkoya_isaku_scene00_*` (5214/5215) so para o falante 30.
- Shiela (cartomante) = slot 79 (sem simbolo F_act): unica falante da cena da
  casa da vidente (tabela 0x62A408; texto 0x62C028). Ambos mapeados em
  `RetailResidentBanks`.
- Piscina (overlay 0x51E000): Barry e os irmaos Kim aparecem juntos (mesmo
  script 0x8001AEF0), alem de varios moradores menores. As versoes de roupa
  de banho precisam do pacote da cena da piscina (nao decodificado). O Unity
  hoje nao coloca nenhum deles na piscina.

## 2026-11-18 - Moradores da piscina e Chero

- Chero (violoncelista viajante) = slot 37 `F_act_serohiki`; mapeado em `RetailResidentBanks`.
- Piscina restaurada: pacote de roupas de banho em TOWN.BIN 0x8B8000; extrator gera
  bancos `pool_*`. Novo `TowerGameController.PoolResidents.cs` cria 13 moradores
  (Barry + Irmaos Kim, 2 banhistas, menina, garoto, avo, mulher, cacador, tia,
  homem, avo) com as falas originais (Shift-JIS do overlay 0x51E000) e traducao.
- O antigo `pool_visitor` (texto reconstruido/inventado) foi removido; o Ghosh da
  piscina usa o sprite de roupa de banho original; a `ApplyPoolPresentation`
  so roda quando o NPC nao tem banco retail.
- Posicoes: celulas livres da piscina (posicoes nativas nao decodificadas).
- Nao compilado.

### Evento da Vivian na piscina: os dois irmaos

- Script 0x8001C9B4 (texto TOWN.BIN 0x536312-0x536687), compartilhado pelos falantes
  0x39 (aniki) e 0x3A (otouto). Implementado em `VivianPoolHecklers`
  (`TowerGameController.PoolResidents.cs`), com sprites `pool_aniki`/`pool_otouto`.
- Aparecem so durante o encontro (VivianPoolDateKey 1 ou 2), perto da Vivian. A cena
  completa toca uma vez (`vivian.poolHecklers`); depois so "Bye!".
- "He's been to level N": o jogo imprime um numero; usamos o andar mais alto atingido.
- A condicao exata de aparicao no original nao foi decodificada; o encontro existente
  (convite no teatro, visitas 1-3) ja estava implementado e nao mudou.

## 2026-11-19 - Concerto do Chero e Infusao Magica

### Chero toca musicas na fonte (`TowerGameController.CheroConcert.cs`)
- Depois que a rota da Nico termina (`StoryStage("nico") == StoryComplete`), o Chero volta a aparecer na fonte (`SpawnOutdoorStoryResidents`, celula `chero` 58,72, banco `serohiki`).
- Ao conversar: "Quer ouvir uma musica?" Sim -> lista rolavel com as 35 faixas de `StreamingAssets/Media/Music` (+ "Deixa pra la"); Nao -> fala de despedida. Roteado em `NicoStory` (`npc.npcId == "chero" && stage == StoryComplete`).
- A faixa escolhida toca por `PlayAreaMusic(cue)`; `MusicFileName` escolhe a versao Classica (`Media/Music/<cue>.ogg`) ou a Reimagined (overlay `Media/Reimagined/Music`), e a troca de modo reinicia a faixa via `RestartAreaMusicForAssetMode`.
- `UpdateCheroConcertMusic` (chamado no inicio de `UpdateOutdoorSelfiMusic`) volta ao tema `Town` quando Koh se afasta mais de 9 unidades do Chero; ao entrar num interior a musica da area substitui a do concerto normalmente.
- As falas do Chero neste pos-evento sao novas (nao existem no original).

### Infusao Magica na Loja de Monstros (`TowerGameController.MagicInfusion.cs`)
- Nova opcao "Magic Infusion / Infusao Magica" no menu de servicos do Master (vender / comprar / infusao / so olhando).
- Paginas novas `MonsterShopPage.InfusionFamiliar` (familiar ativo + todos os da cabana, `_bagFamiliars` nao temporarios) e `InfusionSpell` (magias do familiar via `FamiliarSpellList`).
- Cada compra sobe uma magia em +1 nivel por 500G (`MagicInfusionCost`), so se o nivel da magia for menor que o nivel do familiar; senao mostra "(max.)". Grava nos slots nativos 8/11/14 (`WriteSpellSlot`); uma magia nativa implicita e gravada no slot 0 antes. Salva com `PersistTownProgress`.
- Esc/Voltar: lista de magias -> lista de familiares -> servicos.
- Unity compilou sem erros (so o aviso antigo de Theater.cs).

### Fruta do Weedy (`TowerGameController.HomeResidents.cs`)
- Com a casa nivel 3 (`CurrentHomeRank >= 2`), a primeira conversa com o Weedy em casa depois de cada ida a torre da uma fruta aleatoria (ids 1-10 do tipo Fruit: Pita, Pita grande, Tumna, Leva, Leolam, Laev, Roche, Limit, Oleem, Geropita) e depois segue para a oferta do Livro dos Monstros.
- Chave `home.weedy_fruit_given` (zerada em `EnterTower`, igual a Pita da Wreath). Com o inventario cheio ele avisa e guarda a fruta para a proxima conversa. Falas novas (nao originais). Entrada no Debug de quests.

### Bencao do Jorda e Pergaminho de purificacao (`TowerGameController.PriestBlessing.cs`)
- Em qualquer templo, se Koh carrega equipamento amaldicoado, o Jorda inspeciona, da um "*TOC!*" (som `RetailSfx.Punch`) e remove a maldicao de todos de graca (tambem identifica). Gancho no inicio de `TryConstructionStory` (antes das historias do templo).
- Na 3a visita: "Again? Boy, you need a blessing." / "Mumble, mumble..." / *BONK* / "Done. You should feel more fortunate now." -> `temple.priest_blessing` = true (contador em `temple.decurse_visits`).
- Bencao: moedas pegas valem +10% (minimo +1) em `CreditCoinPickup` via `BlessedCoinValue`; em `TowerItemTrapManager.CreateTowerEquipment` o corte do -1 amaldicoado cai 51/1024 (~5 pontos percentuais: espadas/escudos 184->133, varinhas 128->77). A Dark Sword continua sempre amaldicoada.
- Templo nivel 2: depois das falas de historia, o Jorda abre um menu que vende o De-curse Scroll / Pergaminho de purificacao (tipo 5, id 5) por 1000G (`JordaTempleShop`).

### Correcao: texturas de midia 404 (`TowerGameController.StreamedMedia.cs`)
- `StreamMediaTexture`/`StreamMediaSprite` recebiam o caminho sem extensao ("Title/Newborn", "Title/AngelOpeningFrames", "Title/WreathResting", "Tower/Entrance/KewneDialogueSkip", "Title/RainCast/...", "Title/JordaImpactStar"), mas os arquivos em StreamingAssets/Media sao .png -> HTTP 404. Agora o caminho sem extensao recebe ".png" antes de `AssetMode.ResolveMedia` (o overlay Reimagined tambem lista com extensao).

### Correcao: tamanho dos NPCs com sprite retail (`RetailTownResidentSpriteView.cs`)
- Causa: `SpawnActor` aumenta o sprite do Koh em 1.8x (agora a constante `RetailTownResidentSpriteView.KohVisualScale`), mas os moradores vem de `Spawn` sem esse fator, entao com 48 px/unidade apareciam com 1/1.8 do tamanho do Koh (as celulas retail tem a mesma escala de pixel: Koh 42 px, Barry 42 px, Okami 43 px...).
- Correcao: os quadros sao criados com `TownPixelsPerUnit = 48 / 1.8` (~26.67) em vez de escalar o transform; textura sem reamostragem, pixels quadrados. Vale para todos os bancos (cidade, piscina, minigames via NativeMinigameArt) e para o `_pixelsPerUnit` serializado do NPC_Vivian.prefab (48 -> 26.666666).
- Nico (ppu 30) e Selfi (escala 1.54) usam outros caminhos e nao foram mexidos.
